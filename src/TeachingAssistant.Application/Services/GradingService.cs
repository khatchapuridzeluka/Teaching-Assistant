using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Grades one stored version by reading: the assignment's instructions (as stored by the last fetch: the task statement
/// and the files the teacher attached in Classroom) and the student's work go to the model in one message, and its
/// Markdown review comes back. Nothing is read from the source here.
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

    // Images taken from one submission's archive. The API accepts many more; homework has a handful of drawings, and the
    // cap keeps a folder of screenshots from crowding out the code.
    private const int MaxSubmissionImages = 20;

    // Image files in the extracted code (drawings, diagrams, screenshots of output) the model can read, by extension.
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

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
    private readonly IInstructionsStore _instructionsStore;
    private readonly ISnapshotStorage _storage;
    private readonly IDocumentReader _documentReader;
    private readonly ILanguageModel _languageModel;

    public GradingService(
        ISubmissionStore store,
        IInstructionsStore instructionsStore,
        ISnapshotStorage storage,
        IDocumentReader documentReader,
        ILanguageModel languageModel)
    {
        _store = store;
        _instructionsStore = instructionsStore;
        _storage = storage;
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

        // The instructions as stored by the last fetch: no calls to the source, and the same bytes for every student.
        var stored = await _instructionsStore.GetAsync(submission.Source, submission.AssignmentId, cancellationToken);
        if (stored?.LatestVersion is not { } instructions)
        {
            throw new InvalidRequestException(
                "The assignment's instructions are not stored yet. Use \"Fetch from Classroom\" first; it stores them " +
                "together with the submissions.");
        }

        var prompt = new Prompt();

        // Shared by every student of the assignment: the task statement and the instruction files.
        AddInstructions(prompt, instructions);
        if (!prompt.HasInstructions)
        {
            throw new InvalidRequestException(
                "The assignment has no instructions the model can read. Attach them in Classroom as a PDF or Word " +
                "document, or write a task statement, then fetch again.");
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
        return new GradingResponse(versionId, reply.Model, reply.Text, instructions.Number, prompt.Sent, prompt.Skipped);
    }

    // The stored task statement and instruction files. PDFs and images go as files; Word documents (how most
    // instructions are written) as their text, read from the stored file.
    private void AddInstructions(Prompt prompt, InstructionsVersion instructions)
    {
        var task = new StringBuilder("<task>\n")
            .Append("Title: ").AppendLine(instructions.Title);
        if (instructions.MaxPoints is { } maxPoints)
        {
            task.Append("Maximum points: ").AppendLine(maxPoints.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(instructions.Description))
        {
            task.AppendLine().AppendLine(instructions.Description.Trim());
            prompt.HasInstructions = true;
        }

        prompt.Text(task.Append("</task>").ToString());

        foreach (var content in instructions.Contents)
        {
            if (content.StoredName is null)
            {
                // Links and videos are recorded at fetch but never stored; too large files neither.
                prompt.Skip(content.Status == ContentStatus.TooLarge
                    ? $"Instructions: '{content.Name}' (too large)"
                    : $"Instructions: '{content.Name}' (not a file the model can read)");
                continue;
            }

            var bytes = ReadAll(_storage.OpenOriginal(instructions.StoragePath, content.StoredName));
            if (bytes is null)
            {
                prompt.Skip($"Instructions: '{content.Name}' (missing from storage)");
                continue;
            }

            if (FilePart.IsSupported(content.MimeType) && prompt.FitsFiles(bytes.Length))
            {
                prompt.File($"Instructions file: {content.StoredName}", new FilePart(content.StoredName, content.MimeType!, bytes));
                prompt.Sent.Add($"Instructions: {content.StoredName}");
            }
            else if (_documentReader.CanRead(content.StoredName, content.MimeType) && _documentReader.Read(bytes) is { } document)
            {
                AddDocument(prompt, "instructions_document", "Instructions", content.StoredName, document);
            }
            else
            {
                prompt.Skip($"Instructions: '{content.Name}' (could not be read)");
                continue;
            }

            prompt.HasInstructions = true;
        }
    }

    // The extracted code as text, every file with its path and line numbers so the review can point at lines.
    // Images in the extracted code (homework often asks for drawings, e.g. stack and heap diagrams) go as images right
    // after the code, labelled with their path. Other binary files are skipped.
    private void AddCode(Prompt prompt, SubmissionVersion version)
    {
        var code = new StringBuilder();
        var images = new List<(string Path, FilePart Image)>();
        long imageBytes = 0;

        foreach (var file in _storage.ListCode(version.StoragePath))
        {
            if (ImageTypes.TryGetValue(Path.GetExtension(file.Path), out var imageType))
            {
                if (images.Count >= MaxSubmissionImages)
                {
                    prompt.Skip($"{file.Path} (more than {MaxSubmissionImages} images in the submission)");
                }
                else if (file.SizeBytes > MaxImageBytes || !prompt.FitsFiles(imageBytes + file.SizeBytes))
                {
                    prompt.Skip($"{file.Path} (image too large)");
                }
                else if (ReadAll(_storage.OpenCode(version.StoragePath, file.Path)) is { } image)
                {
                    images.Add((file.Path, new FilePart(file.Path, imageType, image)));
                    imageBytes += image.Length;
                }

                continue;
            }

            if (file.SizeBytes > MaxCodeFileBytes)
            {
                prompt.Skip($"{file.Path} (larger than {MaxCodeFileBytes / 1000} KB, not hand-written code)");
                continue;
            }

            var bytes = ReadAll(_storage.OpenCode(version.StoragePath, file.Path));
            if (bytes is null || IsBinary(bytes))
            {
                // Other binary files in an archive (compiled leftovers, other formats) are not code.
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

        foreach (var (path, image) in images)
        {
            prompt.File($"Image in the student's submission: {path}", image);
            prompt.Sent.Add($"Image: {path}");
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
            else if (_documentReader.Read(bytes) is { } document)
            {
                AddDocument(prompt, "student_document", "Student file", content.Name, document);
            }
            else
            {
                prompt.Skip($"'{content.Name}' (the document could not be read)");
                continue;
            }

            prompt.HasStudentWork = true;
        }
    }

    // A Word document as its text, then the pictures embedded in it (often code screenshots), each labelled with the
    // "[image N]" marker that shows where it sits in the text, so the model knows which picture belongs to which task.
    private static void AddDocument(Prompt prompt, string tag, string sentLabel, string name, ReadDocument document)
    {
        prompt.Text($"<{tag} name=\"{name}\">\n{document.Text}\n</{tag}>");

        var images = 0;
        for (var i = 0; i < document.Images.Count; i++)
        {
            var image = document.Images[i];
            if (!prompt.FitsFiles(image.Content.Length))
            {
                prompt.Skip($"{sentLabel}: [image {i + 1}] in '{name}' (too large)");
                continue;
            }

            prompt.File($"[image {i + 1}] in {name}:", new FilePart($"{name} [image {i + 1}]", image.MediaType, image.Content));
            images++;
        }

        prompt.Sent.Add(images == 0 ? $"{sentLabel}: {name} (as text)" : $"{sentLabel}: {name} (as text, {images} images)");
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
