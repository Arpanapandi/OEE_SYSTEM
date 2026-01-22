namespace OeeSystem.Models;

public class JobRun
{
    public int Id { get; set; }

    public string MachineId { get; set; } = string.Empty;
    public Machine? Machine { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    // ✅ PERBAIKAN: Waktu terakhir status mesin berubah (untuk kalkulasi OEE duration)
    public DateTime? LastStatusChangeTime { get; set; }

    // ✅ NEW: SINGLE SOURCE OF TRUTH - State mesin saat ini
    // Value: "RUNNING", "REST_BREAK", "LINE_STOP", "NO_LOADING", "STOPPED"
    public string CurrentState { get; set; } = "STOPPED";

    public int OperatorId { get; set; }
    public User? Operator { get; set; }

    public int? ManPowerId { get; set; }
    public ManPower? ManPower { get; set; }

    // ✅ TAMBAHKAN: Dandori Duration (kolom baru di database)
    public DateTime? DandoriStartTime { get; set; } // Waktu mulai dandori
    public DateTime? DandoriEndTime { get; set; } // Waktu selesai dandori
    public int? DandoriDurationSeconds { get; set; } // Durasi dandori dalam detik

    // ✅ HASIL SCAN PRODUKSI (DB_HOSS)
    public string? ScannedPartNumber { get; set; }      // Part Number dari scan
    public string? ScannedLotNumber { get; set; }       // No Lot Produksi dari scan
    public int? ScannedKomponenId { get; set; }         // Id di DB_HOSS.dbo_komponen
    public int? ScannedJmlKomponen { get; set; }        // jml_komponen dari DB_HOSS
    
    // ✅ NEW: Injection Group (merah/biru)
    public string? InjectionGroup { get; set; }

    public ICollection<DowntimeEvent> DowntimeEvents { get; set; } = new List<DowntimeEvent>();
    public ICollection<ProductionCount> ProductionCounts { get; set; } = new List<ProductionCount>();
}


