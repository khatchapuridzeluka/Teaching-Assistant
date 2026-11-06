using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Grades one stored version by reading: the assignment's instructions (the files the teacher attached in Classroom and
/// the task statement) and the student's work go to the model in one message, and its Markdown review comes back.
/// PDFs and images are sent as files the model reads itself; Word documents are sent as their text.
/// Instructions come first and the student's work last, so the shared part can be cached across a class later.
/// The student's name is never sent.
/// </summary>
public class GradingService : IGradingService
{
    // Source files larger than this are not code a student wrote by hand (generated or data files).
    private const int MaxCodeFileBytes = 200_000;

    // All code of one submission together: roughly 100k tokens, far above any homework.
    private const int MaxCodeChars = 400_000;

    // Files the model reads itself (PDFs, images). The API takes 32 MB per request and files grow by a third as
    // base64, so all files together stay well below that. Images have their own 5 MB limit at the API.
    private const long MaxFilesBytes = 20L * 1024 * 1024;
    private const long MaxImageBytes = 5L * 1024 * 1024;

    private const string SystemPrompt =
        """
        You grade programming homework for a university course (Advanced Java, and C++ basics) as the teacher's
        assistant. Your review is a draft: the teacher reads it, corrects it and decides the final grade. So be precise,
        give short reasons, and say when you are unsure instead of sounding certain.

        How to grade:
        - The assignment's instructions (the attached instruction files and the task statement) define the tasks and how
          many points each is worth. Follow that breakdown exactly. If the instructions give no points, split the
          assignment's maximum points across the tasks sensibly and say that you did.
        - You only see the source code; it has not been compiled or run. Judge behaviour by reading the code carefully.
          When the instructions give an expected output, trace the code to check whether it would print exactly that,
          and say "likely" when you cannot be sure.
        - Deduct points only for real problems measured against the instructions: missing or wrong functionality, rules
          the instructions state (for example that fields must be private), and clear design mistakes for this course
          level. Do not deduct for style choices the instructions do not mention, and do not invent requirements.
        - A requirement met in a different but valid way (other variable names, extra helper methods) earns full points,
          unless the instructions fix the exact form.
        - Award a bonus only when its task is actually done, and show it separately from the base total.
        - Some submitted files may be listed as not included; do not deduct points for content you could not see, but
          mention it.

        Write the review in English, in Markdown, with exactly these sections:
        1. **Score**: a table with one row per task (task, points awarded / maximum, a one-line reason), then the total,
           and the bonus separately if there is one.
        2. **Mistakes**: one bullet per mistake: file and line (for example `Zoo.java:14`), what is wrong, and which
           task it costs points in. Write "None" if there are none.
        3. **Feedback**: two or three sentences the teacher can adapt for the student: what was done well and the most
           important thing to improve.

        Keep it concise; the teacher reviews a whole class.
        """;

    private readonly ISubmissionStore _store;
    private readonly ISnapshotStorage _storage;
    private readonly ISubmissionSource _submissionSource;
    private readonly IRemoteFileSource _fileSource;
    private readonly IDocumentReader _documentReader;
    private readonly ILanguageModel _languageModel;

    public GradingService(
        ISubmissionStore store,
        ISnapshotStorage storage,
        ISubmissionSource submissionSource,
        IRemoteFileSource fileSource,
        IDocumentReader documentReader,
        ILanguageModel languageModel)
    {
        _store = store;
        _storage = storage;
        _submissionSource = submissionSource;
        _fileSource = fileSource;
        _documentReader = documentReader;
        _languageModel = languageModel;
    }

    public async Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var submission = await _store.FindByVersionAsync(versionId, cancellationToken);
        if (submission is null)
        {
            return null;
        }

        var version = submission.Versions.Single(v => v.Id == versionId);
        var assignment = await _submissionSource.GetAssignmentAsync(submission.CourseId, submission.AssignmentId, cancellationToken);

        var prompt = new Prompt();

        // Shared by every student of the assignment: the task statement and the instruction files.
        await AddInstructionsAsync(prompt, assignment, cancellationToken);
        if (!prompt.HasInstructions)
        {
            throw new InvalidRequestException(
                "The assignment has no instructions the model can read. Attach the instructions as a PDF in Classroom " +
                "or write a task statement.");
        }

        // This student's work.
        AddCode(prompt, version);
        AddStudentFiles(prompt, version);
        if (!prompt.HasStudentWork)
        {
            throw new InvalidRequestException(
                "This version has nothing the model can read: no extracted code, PDFs or images.");
        }

        prompt.Text(prompt.Skipped.Count == 0
            ? "Grade this submission against the instructions."
            : "These submitted files could not be included:\n" +
              string.Join("\n", prompt.Skipped.Select(s => $"- {s}")) +
              "\n\nGrade this submission against the instructions.");

        var reply = await _languageModel.GradeAsync(SystemPrompt, prompt.Parts, cancellationToken);
        return new GradingResponse(versionId, reply.Model, reply.Text, prompt.Sent, prompt.Skipped);
    }

    private async Task AddInstructionsAsync(Prompt prompt, AssignmentResponse assignment, CancellationToken cancellationToken)
    {
        var task = new StringBuilder("<task>\n")
            .Append("Title: ").AppendLine(assignment.Title);
        if (assignment.MaxPoints is { } maxPoints)
        {
            task.Append("Maximum points: ").AppendLine(maxPoints.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(assignment.Description))
        {
            task.AppendLine().AppendLine(assignment.Description.Trim());
            prompt.HasInstructions = true;
        }

        prompt.Text(task.Append("</task>").ToString());

        foreach (var material in assignment.Materials)
        {
            if (material is not { Kind: AttachmentKind.DriveFile, FileId: { } fileId })
            {
                prompt.Skip($"Instructions: '{material.Title}' (only files can be sent, not links or forms)");
                continue;
            }

            // PDFs and images go as files; Word documents (how most instructions are written) as their text.
            var file = await _fileSource.GetFileAsync(fileId, cancellationToken);
            var asFile = file.IsDownloadable && FilePart.IsSupported(file.DownloadMimeType);
            var asText = file.IsDownloadable && !asFile && _documentReader.CanRead(file.DownloadName, file.DownloadMimeType);
            if (!asFile && !asText)
            {
                prompt.Skip($"Instructions: '{file.Name}' (the model cannot read {file.MimeType} files)");
                continue;
            }

            if (!prompt.FitsFiles(file.SizeBytes ?? 0))
            {
                prompt.Skip($"Instructions: '{file.Name}' (too large)");
                continue;
            }

            using var download = new MemoryStream();
            await _fileSource.DownloadAsync(file, download, cancellationToken);
            if (asFile)
            {
                prompt.File($"Instructions file: {file.DownloadName}",
                    new FilePart(file.DownloadName, file.DownloadMimeType, download.ToArray()));
                prompt.Sent.Add($"Instructions: {file.DownloadName}");
            }
            else if (_documentReader.ReadText(download.ToArray()) is { } text)
            {
                prompt.Text($"<instructions_document name=\"{file.DownloadName}\">\n{text}\n</instructions_document>");
                prompt.Sent.Add($"Instructions: {file.DownloadName} (as text)");
            }
            else
            {
                prompt.Skip($"Instructions: '{file.Name}' (the document could not be read)");
                continue;
            }

            prompt.HasInstructions = true;
        }
    }

    // The extracted code as text, every file with its path and line numbers so the review can point at lines.
    private void AddCode(Prompt prompt, SubmissionVersion version)
    {
        var code = new StringBuilder();
        foreach (var file in _storage.ListCode(version.StoragePath))
        {
            if (file.SizeBytes > MaxCodeFileBytes)
            {
                prompt.Skip($"{file.Path} (larger than {MaxCodeFileBytes / 1000} KB, not hand-written code)");
                continue;
            }

            var bytes = ReadAll(_storage.OpenCode(version.StoragePath, file.Path));
            if (bytes is null || IsBinary(bytes))
            {
                // Binary files in an archive (images, compiled leftovers) are not code; images inside archives are
                // not sent yet.
                prompt.Skip($"{file.Path} (not a text file)");
                continue;
            }

            var text = new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
            if (code.Length + text.Length > MaxCodeChars)
            {
                prompt.Skip($"{file.Path} (the submission's code is larger than the limit)");
                continue;
            }

            code.Append("<file path=\"").Append(file.Path).AppendLine("\">");
            var lines = text.ReplaceLineEndings("\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                code.Append((i + 1).ToString().PadLeft(4)).Append(" | ").AppendLine(lines[i]);
            }

            code.AppendLine("</file>");
            prompt.Sent.Add($"Code: {file.Path}");
        }

        if (code.Length > 0)
        {
            prompt.Text("<submission>\nThe student's source code. Each line starts with its line number.\n\n" + code + "</submission>");
            prompt.HasStudentWork = true;
        }
    }

    // Files the student attached besides code (reports, screenshots of output). Archives and source files are already
    // in the code above; links and repositories have nothing stored yet and are reported as skipped.
    private void AddStudentFiles(Prompt prompt, SubmissionVersion version)
    {
        foreach (var content in version.Contents)
        {
            if (content.Kind is ContentKind.CodeArchive or ContentKind.SourceFile)
            {
                continue;
            }

            if (content.StoredName is null)
            {
                prompt.Skip($"'{content.Name}' (not stored)");
                continue;
            }

            var asFile = FilePart.IsSupported(content.MimeType);
            var asText = !asFile && _documentReader.CanRead(content.Name, content.MimeType);
            if (!asFile && !asText)
            {
                prompt.Skip($"'{content.Name}' (the model cannot read {content.MimeType ?? "this type of"} files yet)");
                continue;
            }

            var size = content.SizeBytes ?? 0;
            var isImage = asFile && content.MimeType!.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            if ((isImage && size > MaxImageBytes) || !prompt.FitsFiles(size))
            {
                prompt.Skip($"'{content.Name}' (too large)");
                continue;
            }

            var bytes = ReadAll(_storage.OpenOriginal(version.StoragePath, content.StoredName));
            if (bytes is null)
            {
                prompt.Skip($"'{content.Name}' (missing from storage)");
                continue;
            }

            if (asFile)
            {
                prompt.File($"The student's attached file: {content.Name}", new FilePart(content.Name, content.MimeType!, bytes));
                prompt.Sent.Add($"Student file: {content.Name}");
            }
            else if (_documentReader.ReadText(bytes) is { } text)
            {
                prompt.Text($"<student_document name=\"{content.Name}\">\n{text}\n</student_document>");
                prompt.Sent.Add($"Student file: {content.Name} (as text)");
            }
            else
            {
                prompt.Skip($"'{content.Name}' (the document could not be read)");
                continue;
            }

            prompt.HasStudentWork = true;
        }
    }

    private static byte[]? ReadAll(Stream? stream)
    {
        if (stream is null)
        {
            return null;
        }

        using (stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }

    // A zero byte in the first few KB means the file is not text.
    private static bool IsBinary(byte[] bytes) => Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0;

    // The message being built: parts in order, plus what was sent and skipped for the teacher.
    private sealed class Prompt
    {
        private long _fileBytes;

        public List<PromptPart> Parts { get; } = [];
        public List<string> Sent { get; } = [];
        public List<string> Skipped { get; } = [];
        public bool HasInstructions { get; set; }
        public bool HasStudentWork { get; set; }

        public void Text(string text) => Parts.Add(new TextPart(text));

        // A short label first, so the model knows what each file is.
        public void File(string label, FilePart file)
        {
            Parts.Add(new TextPart(label));
            Parts.Add(file);
            _fileBytes += file.Content.Length;
        }

        public bool FitsFiles(long size) => _fileBytes + size <= MaxFilesBytes;

        public void Skip(string reason) => Skipped.Add(reason);
    }
}
