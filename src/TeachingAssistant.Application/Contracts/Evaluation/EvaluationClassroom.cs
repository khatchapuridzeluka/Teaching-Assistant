namespace TeachingAssistant.Application.Contracts;

public record EvaluationClassroom
{
    /// <summary>Reports whose submission has a grade returned in Classroom.</summary>
    public required int Compared { get; init; }

    /// <summary>How many AI totals are within the tolerance of that grade.</summary>
    public required int AiWithinTolerance { get; init; }

    /// <summary>Average size of the AI's difference from it, in percent of the maximum.</summary>
    public double? AiMeanAbsoluteDifference { get; init; }

    /// <summary>How many of the teacher's report totals are within the tolerance of it.</summary>
    public required int TeacherWithinTolerance { get; init; }
}
