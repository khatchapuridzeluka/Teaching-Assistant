namespace TeachingAssistant.WebAPI.Jobs;

public class JobOptions
{
    public const string SectionName = "Jobs";

    public int MaxConcurrent { get; set; } = 3;
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan Heartbeat { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan Poll { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RecoveryCheck { get; set; } = TimeSpan.FromMinutes(1);
}
