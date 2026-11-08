using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class GradingService : IGradingService
{
    private const int MaxCodeFileBytes = 200_000;

    private const int MaxCodeChars = 400_000;

    private const long MaxFilesBytes = 20L * 1024 * 1024;
    private const long MaxImageBytes = 5L * 1024 * 1024;

    private const int MaxSubmissionImages = 20;

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
          When the instructions give an expected output, trace the code to check whether it would produce the same
          values and behaviour, and say "likely" when you cannot be sure. Small differences in the printed text itself
          do not count (see the grading policy below).
        - Deduct points only for real problems measured against the instructions: missing or wrong functionality, rules
          the instructions state (for example that fields must be private), and clear design mistakes for this course
          level. Do not deduct for style choices the instructions do not mention, and do not invent requirements.
        - A requirement met in a different but valid way (other variable names, extra helper methods) earns full points,
          unless the instructions fix the exact form.
        - Award a bonus only when its task is actually done, and show it separately from the base total.
        - Some submitted files may be listed as not included; do not deduct points for content you could not see, but
          mention it.

        Grading policy (the teacher's own rules; they override the general rules above):
        - Trivial issues never cost points. The teacher wants students judged on whether their program works and is
          designed well, not punished for polish. Trivial means: the wording, spacing, punctuation, capitalization or
          labels of printed text (as long as the printed values are right); comments (missing, too few, or with typos);
          unused imports; and unused or unnecessary variables. Still list each one in "mistakes" with severity Style, so
          the student sees it as a suggestion, and say in the message that no points were deducted.
        - Names fixed by the instructions: when the instructions specify the exact name of a variable or method and the
          student used a different one, deduct 3 points for each such name (a misspelling or different capitalization
          counts as different). The teacher relies on these names, for example to test the code. List each one with
          severity Minor and say in the message that it cost 3 points and in which task. Names the instructions do not
          fix are the student's choice: a poor name there is a Style suggestion that costs nothing.

        Your answer is in English and has these parts.

        "review" is Markdown with exactly these sections:
        1. **Score**: a table with one row per task (task, points awarded / maximum, a one-line reason), then the total,
           and the bonus separately if there is one.
        2. **Feedback**: two or three sentences the teacher can adapt for the student: what was done well and the most
           important thing to improve.
        Do not list the mistakes in the review; they go in "mistakes".

        "score", "maxScore" and "bonus" repeat the Score section as numbers, so the teacher's lists can show and compare
        them: the total awarded (without the bonus), the maximum it is out of, and the bonus awarded (null when there
        is none). They must match the review exactly.

        "mistakes" lists every mistake, one entry each, so the teacher sees it as a mark on the exact lines in a code
        viewer:
        - file: the path exactly as written in <file path="...">. startLine and endLine: the lines the mistake is on,
          using the line numbers printed in front of each line; mark the few lines that show the problem (a field
          declaration, the wrong condition), not a whole class or method.
        - For a mistake that is not on specific lines (a missing class, a task not attempted, a problem in a drawing
          or document), set file, startLine and endLine to null.
        - severity: Major for wrong or missing functionality and broken rules from the instructions; Minor for small real
          problems that cost points (including a name fixed by the instructions); Style for suggestions that cost no
          points (readability, and every trivial issue from the grading policy).
        - message: what is wrong and how to fix it in one or two sentences, and which task it costs points in, or that
          it cost no points (always the case for Style).
        - An empty list when there are no mistakes.

        Keep it concise; the teacher reviews a whole class.
        """;

    private readonly ISubmissionStore _store;
    private readonly IInstructionsStore _instructionsStore;
    private readonly ISnapshotStorage _storage;
    private readonly IDocumentReader _documentReader;
    private readonly ILanguageModel _languageModel;
    private readonly IGradingStore _gradings;
    private readonly TimeProvider _timeProvider;

    public GradingService(
        ISubmissionStore store,
        IInstructionsStore instructionsStore,
        ISnapshotStorage storage,
        IDocumentReader documentReader,
        ILanguageModel languageModel,
        IGradingStore gradings,
        TimeProvider timeProvider)
    {
        _store = store;
        _instructionsStore = instructionsStore;
        _storage = storage;
        _documentReader = documentReader;
        _languageModel = languageModel;
        _gradings = gradings;
        _timeProvider = timeProvider;
    }

    public async Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var submission = await _store.FindByVersionAsync(versionId, cancellationToken);
        if (submission is null)
        {
            return null;
        }

        var version = submission.Versions.Single(v => v.Id == versionId);

        var stored = await _instructionsStore.GetAsync(submission.Source, submission.AssignmentId, cancellationToken);
        if (stored?.LatestVersion is not { } instructions)
        {
            throw new InvalidRequestException(
                "The assignment's instructions are not stored yet. Use \"Fetch from Classroom\" first; it stores them " +
                "together with the submissions.");
        }

        var prompt = new Prompt();

        AddInstructions(prompt, instructions);
        if (!prompt.HasInstructions)
        {
            throw new InvalidRequestException(
                "The assignment has no instructions the model can read. Attach them in Classroom as a PDF or Word " +
                "document, or write a task statement, then fetch again.");
        }

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

        var reply = await _languageModel.GradeAsync(SystemPrompt, prompt.Parts, GradingOutput.Schema, cancellationToken);
        var read = GradingOutput.Read(reply.Text, prompt.CodeLines);

        var grading = new AiGrading(versionId, reply.Model, read.Review, read.Score, read.MaxScore, read.Bonus,
            instructions.Number, prompt.Sent, prompt.Skipped, reply.Text,
            read.Mistakes.Select(m => new GradingMistake(m.File, m.StartLine, m.EndLine, m.Severity, m.Category, m.Message)),
            _timeProvider.GetUtcNow());
        _gradings.Add(grading);
        await _gradings.SaveChangesAsync(cancellationToken);
        return GradingDraftService.ToResponse(grading);
    }

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
            prompt.CodeLines[file.Path] = lines.Length;
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

    private static bool IsBinary(byte[] bytes) => Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0;

    private sealed class Prompt
    {
        private long _fileBytes;

        public List<PromptPart> Parts { get; } = [];
        public List<string> Sent { get; } = [];
        public List<string> Skipped { get; } = [];

        public Dictionary<string, int> CodeLines { get; } = [];

        public bool HasInstructions { get; set; }
        public bool HasStudentWork { get; set; }

        public void Text(string text) => Parts.Add(new TextPart(text));

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
