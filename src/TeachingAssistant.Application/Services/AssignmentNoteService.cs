using System.Text.RegularExpressions;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

/// <summary>The teacher's notes for one assignment, read by the grader only when it grades that assignment.</summary>
public partial class AssignmentNoteService : IAssignmentNoteService
{
    private readonly IAssignmentNoteStore _notes;
    private readonly TimeProvider _timeProvider;

    public AssignmentNoteService(IAssignmentNoteStore notes, TimeProvider timeProvider)
    {
        _notes = notes;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AssignmentNoteResponse>> GetAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        return (await _notes.GetActiveAsync(assignmentId, cancellationToken)).Select(ToResponse).ToList();
    }

    public async Task<AssignmentNoteResponse> AddAsync(
        string courseId, string assignmentId, AssignmentNoteRequest request, CancellationToken cancellationToken = default)
    {
        var (title, detail) = Validate(request);
        var position = await _notes.GetLastPositionAsync(assignmentId, cancellationToken) + 1;
        var note = new AssignmentNote(Guid.CreateVersion7(), courseId, assignmentId, position, title, detail, _timeProvider.GetUtcNow());
        _notes.Add(note);
        await _notes.SaveChangesAsync(cancellationToken);
        return ToResponse(note);
    }

    public async Task<AssignmentNoteResponse?> UpdateAsync(Guid id, AssignmentNoteRequest request, CancellationToken cancellationToken = default)
    {
        var (title, detail) = Validate(request);
        var note = await _notes.FindAsync(id, cancellationToken);
        if (note is null || note.IsDeleted)
        {
            return null;
        }

        if (title != note.Title || OneLine(detail) != OneLine(note.Detail))
        {
            note.Edit(title, detail, _timeProvider.GetUtcNow());
            await _notes.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(note);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await _notes.FindAsync(id, cancellationToken);
        if (note is null || note.IsDeleted)
        {
            return false;
        }

        note.Delete(_timeProvider.GetUtcNow());
        await _notes.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static (string Title, string Detail) Validate(AssignmentNoteRequest request)
    {
        var title = (request.Title ?? "").Trim();
        var detail = (request.Detail ?? "").Trim();
        if (title.Length == 0 || detail.Length == 0)
        {
            throw new InvalidRequestException("A note needs a title and a text.");
        }

        if (title.Length > AssignmentNote.MaxTitleLength || detail.Length > AssignmentNote.MaxDetailLength)
        {
            throw new InvalidRequestException(
                $"The title can have at most {AssignmentNote.MaxTitleLength} characters and the text at most {AssignmentNote.MaxDetailLength}.");
        }

        return (title, detail);
    }

    private static AssignmentNoteResponse ToResponse(AssignmentNote note)
    {
        return new AssignmentNoteResponse(note.Id, note.AssignmentId, note.Title, OneLine(note.Detail), note.UpdatedAt);
    }

    private static string OneLine(string text)
    {
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
