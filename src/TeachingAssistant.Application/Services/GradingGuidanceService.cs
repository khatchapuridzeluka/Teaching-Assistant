using System.Text.RegularExpressions;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// What a course is graded with: the stored principles (shared by every course, editable), the rules, and later the
/// course's own guidance and assignment notes.
/// </summary>
public partial class GradingGuidanceService : IGradingGuidanceService
{
    private readonly IGradingPrincipleStore _principles;
    private readonly ICourseGuidanceStore _courseGuidance;
    private readonly TimeProvider _timeProvider;

    public GradingGuidanceService(IGradingPrincipleStore principles, ICourseGuidanceStore courseGuidance, TimeProvider timeProvider)
    {
        _principles = principles;
        _courseGuidance = courseGuidance;
        _timeProvider = timeProvider;
    }

    public async Task<GradingGuidanceResponse> GetAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var approach = (await _principles.GetActiveAsync(cancellationToken)).Select(ToResponse).ToList();
        var rules = BuiltInGradingGuidance.Rules
            .Select(r => new PolicyRuleResponse(r.Title, r.Effect, r.Points, r.PointsPer, r.Covers, OneLine(r.Detail)))
            .ToList();
        var guidance = await _courseGuidance.FindAsync(courseId, cancellationToken);
        return new GradingGuidanceResponse(guidance?.Text, guidance?.UpdatedAt, approach, rules);
    }

    public async Task<GradingGuidanceResponse> SetCourseGuidanceAsync(
        string courseId, CourseGuidanceRequest request, CancellationToken cancellationToken = default)
    {
        var text = (request.Text ?? "").Trim();
        if (text.Length > CourseGuidance.MaxTextLength)
        {
            throw new InvalidRequestException($"Course guidance can have at most {CourseGuidance.MaxTextLength} characters.");
        }

        var existing = await _courseGuidance.FindAsync(courseId, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        if (text.Length == 0)
        {
            if (existing is not null)
            {
                _courseGuidance.Remove(existing);
            }
        }
        else if (existing is null)
        {
            _courseGuidance.Add(new CourseGuidance(courseId, text, now));
        }
        else if (existing.Text != text)
        {
            existing.Change(text, now);
        }

        await _courseGuidance.SaveChangesAsync(cancellationToken);
        return await GetAsync(courseId, cancellationToken);
    }

    public async Task<GradingPrincipleResponse> AddPrincipleAsync(
        GradingPrincipleRequest request, CancellationToken cancellationToken = default)
    {
        var (title, detail) = Validate(request);
        var position = await _principles.GetLastPositionAsync(cancellationToken) + 1;
        var principle = new GradingPrinciple(Guid.CreateVersion7(), position, title, detail, _timeProvider.GetUtcNow());
        _principles.Add(principle);
        await _principles.SaveChangesAsync(cancellationToken);
        return ToResponse(principle);
    }

    public async Task<GradingPrincipleResponse?> UpdatePrincipleAsync(
        Guid id, GradingPrincipleRequest request, CancellationToken cancellationToken = default)
    {
        var (title, detail) = Validate(request);
        var principle = await _principles.FindAsync(id, cancellationToken);
        if (principle is null || principle.IsDeleted)
        {
            return null;
        }

        // The stored text keeps its line breaks for the grader; saving the same words again changes nothing.
        if (title != principle.Title || OneLine(detail) != OneLine(principle.Detail))
        {
            principle.Edit(title, detail, _timeProvider.GetUtcNow());
            await _principles.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(principle);
    }

    public async Task<bool> DeletePrincipleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var principle = await _principles.FindAsync(id, cancellationToken);
        if (principle is null || principle.IsDeleted)
        {
            return false;
        }

        principle.Delete(_timeProvider.GetUtcNow());
        await _principles.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static (string Title, string Detail) Validate(GradingPrincipleRequest request)
    {
        var title = (request.Title ?? "").Trim();
        var detail = (request.Detail ?? "").Trim();
        if (title.Length == 0 || detail.Length == 0)
        {
            throw new InvalidRequestException("A principle needs a title and a text.");
        }

        if (title.Length > GradingPrinciple.MaxTitleLength || detail.Length > GradingPrinciple.MaxDetailLength)
        {
            throw new InvalidRequestException(
                $"The title can have at most {GradingPrinciple.MaxTitleLength} characters and the text at most {GradingPrinciple.MaxDetailLength}.");
        }

        return (title, detail);
    }

    private static GradingPrincipleResponse ToResponse(GradingPrinciple principle)
    {
        return new GradingPrincipleResponse(principle.Id, principle.Title, OneLine(principle.Detail), principle.UpdatedAt);
    }

    // The prompt text is wrapped for the model; the page shows it as one paragraph.
    private static string OneLine(string text)
    {
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
