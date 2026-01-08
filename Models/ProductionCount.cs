namespace OeeSystem.Models;

public class ProductionCount
{
    public int Id { get; set; }

    public int JobRunId { get; set; }
    public JobRun? JobRun { get; set; }

    public DateTime Timestamp { get; set; }
    public int GoodCount { get; set; }
    public int RejectCount { get; set; }
    public string? RejectReason { get; set; }
    
    public int? NgTypeId { get; set; }
    public NgType? NgType { get; set; }
    
    // ✅ Group Injection: "merah" atau "biru"
    public string? InjectionGroup { get; set; }

    // ✅ Traceability Fields
    public string? LotNumber { get; set; }
    public string? LotBo { get; set; }
    public string? CompoundName { get; set; }
    public double? ActualWeight { get; set; }
    public string? Thinning { get; set; }
    public string? Remarks { get; set; }
    
    // ✅ Man Power & Components
    public int? ManPowerId { get; set; }
    public ManPower? ManPower { get; set; }
    
    public int? ComponentId { get; set; }
    public Komponen? Component { get; set; }

    // ✅ Duration per item (seconds)
    public int? DurationSeconds { get; set; }
}


