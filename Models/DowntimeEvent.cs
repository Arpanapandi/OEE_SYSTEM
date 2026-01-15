namespace OeeSystem.Models;

public class DowntimeEvent
{
    public int Id { get; set; }

    public int JobRunId { get; set; }
    public JobRun? JobRun { get; set; }

    public int ReasonId { get; set; }
    public DowntimeReason? Reason { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    // Calculated, but stored for reporting convenience
    public double DurationSeconds { get; set; }
    
    // ✅ CUSTOM OEE: Flags untuk kategorisasi downtime
    // Rest Break dan No Loading TIDAK masuk Downtime Total
    // HANYA Line Stop yang masuk Downtime Total
    public bool IsRestBreak { get; set; }
    public bool IsNoLoading { get; set; }
    public bool IsLineStop { get; set; }
}


