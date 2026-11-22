namespace TeachingAssistant.Application.Contracts;

/// <param name="Compared">Reports whose submission has a grade returned in Classroom.</param>
/// <param name="AiWithinTolerance">How many AI totals are within the tolerance of that grade.</param>
/// <param name="AiMeanAbsoluteDifference">Average size of the AI's difference from it, in percent of the maximum.</param>
/// <param name="TeacherWithinTolerance">How many of the teacher's report totals are within the tolerance of it.</param>
public record EvaluationClassroom(
    int Compared,
    int AiWithinTolerance,
    double? AiMeanAbsoluteDifference,
    int TeacherWithinTolerance);
