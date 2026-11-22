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
    private const double MaxRulePoints = 100;

    private readonly ICourseGuidanceStore _courseGuidance;
    private readonly IPolicyRuleStore _rules;
    private readonly IAssignmentNoteStore _notes;
    private readonly TimeProvider _timeProvider;

    public GradingGuidanceService(
        IGradingPrincipleStore principles, ICourseGuidanceStore courseGuidance, IPolicyRuleStore rules, IAssignmentNoteStore notes,
        TimeProvider timeProvider)
    {
        _principles = principles;
        _courseGuidance = courseGuidance;
        _rules = rules;
        _notes = notes;
        _timeProvider = timeProvider;
    }

    public async Task<GradingGuidanceResponse> GetAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var approach = (await _principles.GetActiveAsync(cancellationToken)).Select(ToResponse).ToList();
        var rules = (await _rules.GetActiveAsync(cancellationToken)).Select(ToResponse).ToList();
        var guidance = await _courseGuidance.FindAsync(courseId, cancellationToken);
        var notes = (await _notes.CountByAssignmentAsync(courseId, cancellationToken))
            .Select(n => new AssignmentNoteCount { AssignmentId = n.Key, Count = n.Value })
            .ToList();
        return new GradingGuidanceResponse
        {
            CourseGuidance = guidance?.Text,
            CourseGuidanceUpdatedAt = guidance?.UpdatedAt,
            Approach = approach,
            Rules = rules,
            Notes = notes,
        };
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

    public async Task<PolicyRuleResponse> AddRuleAsync(PolicyRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = Validate(request);
        var position = await _rules.GetLastPositionAsync(cancellationToken) + 1;
        var added = new PolicyRule(Guid.CreateVersion7(), position, rule.Title, rule.Effect, rule.Points, rule.PointsPer, rule.Covers,
            rule.Detail, _timeProvider.GetUtcNow());
        _rules.Add(added);
        await _rules.SaveChangesAsync(cancellationToken);
        return ToResponse(added);
    }

    public async Task<PolicyRuleResponse?> UpdateRuleAsync(Guid id, PolicyRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = Validate(request);
        var existing = await _rules.FindAsync(id, cancellationToken);
        if (existing is null || existing.IsDeleted)
        {
            return null;
        }

        // The stored text keeps its line breaks for the grader; saving the same rule again changes nothing.
        var changed = rule.Title != existing.Title || rule.Effect != existing.Effect || rule.Points != existing.Points
            || rule.PointsPer != existing.PointsPer || rule.Covers != existing.Covers || OneLine(rule.Detail) != OneLine(existing.Detail);
        if (changed)
        {
            existing.Edit(rule.Title, rule.Effect, rule.Points, rule.PointsPer, rule.Covers, rule.Detail, _timeProvider.GetUtcNow());
            await _rules.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(existing);
    }

    public async Task<bool> DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _rules.FindAsync(id, cancellationToken);
        if (rule is null || rule.IsDeleted)
        {
            return false;
        }

        rule.Delete(_timeProvider.GetUtcNow());
        await _rules.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static PolicyRuleRequest Validate(PolicyRuleRequest request)
    {
        var title = (request.Title ?? "").Trim();
        var covers = (request.Covers ?? "").Trim();
        var detail = (request.Detail ?? "").Trim();
        if (title.Length == 0 || covers.Length == 0 || detail.Length == 0)
        {
            throw new InvalidRequestException("A rule needs a title, what it covers and the text the grader gets.");
        }

        if (title.Length > PolicyRule.MaxTitleLength || covers.Length > PolicyRule.MaxCoversLength
            || detail.Length > PolicyRule.MaxDetailLength || (request.PointsPer?.Trim().Length ?? 0) > PolicyRule.MaxPointsPerLength)
        {
            throw new InvalidRequestException(
                $"Too long: the title can have at most {PolicyRule.MaxTitleLength} characters, what it covers {PolicyRule.MaxCoversLength}, "
                + $"\"per\" {PolicyRule.MaxPointsPerLength} and the text {PolicyRule.MaxDetailLength}.");
        }

        if (!Enum.IsDefined(request.Effect))
        {
            throw new InvalidRequestException("Unknown effect.");
        }

        if (request.Effect == PolicyEffect.Deduction && request.Points is not (> 0 and <= MaxRulePoints))
        {
            throw new InvalidRequestException($"A deduction needs points between 0 and {MaxRulePoints}.");
        }

        var deduction = request.Effect == PolicyEffect.Deduction;
        var per = deduction && !string.IsNullOrWhiteSpace(request.PointsPer) ? request.PointsPer.Trim() : null;
        return new PolicyRuleRequest
        {
            Title = title,
            Effect = request.Effect,
            Points = deduction ? request.Points : null,
            PointsPer = per,
            Covers = covers,
            Detail = detail,
        };
    }

    private static PolicyRuleResponse ToResponse(PolicyRule rule)
    {
        return new PolicyRuleResponse
        {
            Id = rule.Id,
            Title = rule.Title,
            Effect = rule.Effect,
            Points = rule.Points,
            PointsPer = rule.PointsPer,
            Covers = rule.Covers,
            Detail = OneLine(rule.Detail),
            UpdatedAt = rule.UpdatedAt,
        };
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
        return new GradingPrincipleResponse
        {
            Id = principle.Id,
            Title = principle.Title,
            Detail = OneLine(principle.Detail),
            UpdatedAt = principle.UpdatedAt,
        };
    }

    // The prompt text is wrapped for the model; the page shows it as one paragraph.
    private static string OneLine(string text)
    {
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
