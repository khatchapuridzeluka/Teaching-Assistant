namespace TeachingAssistant.Domain.Jobs;

/// <summary>What a background job does; each type has one handler.</summary>
public enum JobType
{
    /// <summary>Grade one stored version with the AI (payload: the version ID).</summary>
    GradeVersion,

    /// <summary>Grade every student of an assignment: a parent job whose children are <see cref="GradeVersion"/> jobs.</summary>
    GradeAssignment,

    /// <summary>Fetch an assignment's submissions and instructions from Classroom.</summary>
    FetchAssignment,

    /// <summary>Copy a course's roster, assignments, grades and photos from Classroom.</summary>
    SyncCourse,

    /// <summary>Extract the code of an assignment's stored versions again.</summary>
    ReExtract,
}
