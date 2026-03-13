namespace IMSharp.Api.Configuration;

public class MessageCleanupOptions
{
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 7;
    public string ScheduleTime { get; set; } = "00:00:00";
}
