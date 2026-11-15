using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
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

    internal static string SystemPromptFor(
        IReadOnlyList<GradingPrinciple> principles, string? courseGuidance, IReadOnlyList<PolicyRule> rules,
        IReadOnlyList<AssignmentNote>? notes = null)
    {
        return PromptLibrary.Grading(GradingPromptSections.Build(principles, courseGuidance, rules, notes));
    }

    private readonly ISubmissionStore _store;
    private readonly IInstructionsStore _instructionsStore;
    private readonly ISnapshotStorage _storage;
    private readonly IDocumentReader _documentReader;
    private readonly ILanguageModel _languageModel;
    private readonly IGradingStore _gradings;
    private readonly IGradingPrincipleStore _principles;
    private readonly ICourseGuidanceStore _courseGuidance;
    private readonly IPolicyRuleStore _rules;
    private readonly IAssignmentNoteStore _notes;
    private readonly ModelCallContext _callContext;
    private readonly TimeProvider _timeProvider;

    public GradingService(
        ISubmissionStore store,
        IInstructionsStore instructionsStore,
        ISnapshotStorage storage,
        IDocumentReader documentReader,
        ILanguageModel languageModel,
        IGradingStore gradings,
        IGradingPrincipleStore principles,
        ICourseGuidanceStore courseGuidance,
        IPolicyRuleStore rules,
        IAssignmentNoteStore notes,
        ModelCallContext callContext,
        TimeProvider timeProvider)
    {
        _store = store;
        _instructionsStore = instructionsStore;
        _storage = storage;
        _documentReader = documentReader;
        _languageModel = languageModel;
        _gradings = gradings;
        _principles = principles;
        _courseGuidance = courseGuidance;
        _rules = rules;
        _notes = notes;
        _callContext = callContext;
        _timeProvider = timeProvider;
    }

    public async Task<GradableVersion?> CheckAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(versionId, cancellationToken);
        return prepared is null ? null : new GradableVersion(prepared.Submission.CourseId, prepared.Submission.AssignmentId);
    }

    public async Task<GradeAllPlan> PlanGradeAllAsync(
        string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default)
    {
        var submissions = (await _store.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
            .Where(s => s.CourseId == courseId)
            .ToList();
        var latest = submissions
            .Select(s => (Submission: s, Version: s.LatestVersion))
            .Where(x => x.Version is not null && x.Version.Status != VersionStatus.Failed)
            .ToList();

        var drafted = (await _gradings.GetByVersionsAsync(latest.Select(x => x.Version!.Id).ToList(), cancellationToken))
            .Where(g => g.Status == GradingStatus.Draft)
            .Select(g => g.VersionId)
            .ToHashSet();

        var toGrade = new List<Guid>();
        var problems = new List<string>();
        foreach (var (submission, version) in latest.Where(x => regrade || !drafted.Contains(x.Version!.Id)))
        {
            try
            {
                if (await CheckAsync(version!.Id, cancellationToken) is not null)
                {
                    toGrade.Add(version.Id);
                }
            }
            catch (InvalidRequestException ex)
            {
                problems.Add($"{submission.StudentName}: {ex.Message}");
            }
        }

        var (noWork, notFetched) = Unstored(submissions);
        return new GradeAllPlan(toGrade, submissions.Count, latest.Count(x => drafted.Contains(x.Version!.Id)), noWork, problems,
            NotFetched: notFetched);
    }

    public async Task<GradeAllPlan> PlanGradeStudentAsync(string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default)
    {
        var studentAssignments = (await _store.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .Where(s => s.StudentId == studentId)
            .Select(s => s.AssignmentId)
            .Distinct()
            .ToList();

        var submissions = new List<Submission>();
        foreach (var assignmentId in studentAssignments)
        {
            submissions.AddRange((await _store.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
                .Where(s => s.CourseId == courseId && s.StudentId == studentId));
        }

        var latest = submissions
            .Select(s => (Submission: s, Version: s.LatestVersion))
            .Where(x => x.Version is not null && x.Version.Status != VersionStatus.Failed)
            .ToList();

        var drafted = (await _gradings.GetByVersionsAsync(latest.Select(x => x.Version!.Id).ToList(), cancellationToken))
            .Where(g => g.Status == GradingStatus.Draft)
            .Select(g => g.VersionId)
            .ToHashSet();

        var toGrade = new List<Guid>();
        var assignmentIds = new Dictionary<Guid, string>();
        var problems = new List<string>();
        foreach (var (submission, version) in latest.Where(x => regrade || !drafted.Contains(x.Version!.Id)))
        {
            try
            {
                if (await CheckAsync(version!.Id, cancellationToken) is not null)
                {
                    toGrade.Add(version.Id);
                    assignmentIds[version.Id] = submission.AssignmentId;
                }
            }
            catch (InvalidRequestException ex)
            {
                var title = (await _instructionsStore.GetAsync(submission.Source, submission.AssignmentId, cancellationToken))
                    ?.LatestVersion?.Title ?? "An assignment";
                problems.Add($"{title}: {ex.Message}");
            }
        }

        var (noWork, notFetched) = Unstored(submissions);
        return new GradeAllPlan(toGrade, submissions.Count, latest.Count(x => drafted.Contains(x.Version!.Id)), noWork, problems,
            assignmentIds, notFetched);
    }

    private static (int NoWork, int NotFetched) Unstored(IReadOnlyList<Submission> submissions)
    {
        var unstored = submissions.Where(s => s.LatestVersion is null || s.LatestVersion.Status == VersionStatus.Failed).ToList();
        var notFetched = unstored.Count(s => s.IsTurnedIn && s.LatestVersion is null);
        return (unstored.Count - notFetched, notFetched);
    }

    public async Task<string?> FindStudentNameAsync(string courseId, string studentId, CancellationToken cancellationToken = default)
    {
        return (await _store.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .FirstOrDefault(s => s.StudentId == studentId)?.StudentName;
    }

    public async Task<IReadOnlyList<PromptPart>?> BuildInstructionsAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        var stored = await _instructionsStore.GetAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken);
        if (stored?.LatestVersion is not { } instructions)
        {
            return null;
        }

        var prompt = new Prompt();
        AddInstructions(prompt, instructions);
        return prompt.HasInstructions ? prompt.Parts : null;
    }

    public async Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(versionId, cancellationToken);
        if (prepared is null)
        {
            return null;
        }

        var (submission, instructions, prompt) = prepared;
        _callContext.Describe(submission.CourseId, submission.AssignmentId, versionId);
        var principles = await _principles.GetActiveAsync(cancellationToken);
        var guidance = await _courseGuidance.FindAsync(submission.CourseId, cancellationToken);
        var rules = await _rules.GetActiveAsync(cancellationToken);
        var notes = await _notes.GetActiveAsync(submission.AssignmentId, cancellationToken);
        var reply = await _languageModel.GradeAsync(
            SystemPromptFor(principles, guidance?.Text, rules, notes), prompt.Parts, GradingOutput.Schema, cancellationToken);
        var read = GradingOutput.Read(reply.Text, prompt.CodeLines);

        var grading = new AiGrading(versionId, reply.Model, read.Review, read.Score, read.MaxScore, read.Bonus,
            instructions.Number, prompt.Sent, prompt.Skipped, reply.Text,
            read.Mistakes.Select(m => new GradingMistake(m.File, m.StartLine, m.EndLine, m.Severity, m.Category, m.Message, m.Points)),
            _timeProvider.GetUtcNow(),
            read.Strengths.Select(s => new GradingStrength(s.File, s.StartLine, s.EndLine, s.Category, s.Message)));
        _gradings.Add(grading);
        await _gradings.SaveChangesAsync(cancellationToken);
        return GradingDraftService.ToResponse(grading);
    }

    private async Task<Prepared?> PrepareAsync(Guid versionId, CancellationToken cancellationToken)
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

        return new Prepared(submission, instructions, prompt);
    }

    private sealed record Prepared(Submission Submission, InstructionsVersion Instructions, Prompt Prompt);

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
            if (ContentClassifier.IsCode(content.Kind))
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
