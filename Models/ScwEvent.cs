namespace OeeSystem.Models;

public class ScwEvent
{
    public int Id { get; set; }
    public int JobRunId { get; set; }
    public JobRun? JobRun { get; set; }
    
    public int Scw4MTypeId { get; set; }
    public Scw4MType? Scw4MType { get; set; }
    public int ScwRemarkId { get; set; }
    public ScwRemark? ScwRemark { get; set; }
    
    public string MachineId { get; set; } = string.Empty;
    public Machine? Machine { get; set; }
    
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double DurationSeconds { get; set; }
    
    public string? AdditionalNotes { get; set; }
}

