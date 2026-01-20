using OeeSystem.Models;

namespace OeeSystem.Services;

public record OeeResult(
    double Availability,
    double Performance,
    double Quality,
    double Oee);

public interface IOeeService
{
    /// <summary>
    /// Menghitung OEE sesuai formula standar:
    /// - Availability = (Loading Time - Down Time) / Loading Time × 100
    /// - Performance = (CT Standar × Product Output) / Operating Time × 100
    /// - Quality = (Product Unit Processed - Defect Unit) / Product Unit Processed × 100
    /// - OEE = Availability × Performance × Quality
    /// </summary>
    /// <param name="loadingTime">Total Shift Time (Loading Time)</param>
    /// <param name="downTime">Total Downtime (Planned + Unplanned)</param>
    /// <param name="totalCount">Total Product Output (Good + Reject)</param>
    /// <param name="goodCount">Good Product Output</param>
    /// <param name="standarCycleTime">Standar Cycle Time dalam detik</param>
    OeeResult CalculateOee(
        TimeSpan loadingTime,
        TimeSpan downTime,
        int totalCount,
        int goodCount,
        double standarCycleTime);

    MachineStatus GetRealTimeStatus(Machine machine, JobRun? activeJobRun, bool hasOpenDowntime);

    /// <summary>
    /// Mengambil metrik waktu real-time (Operating, Downtime, OEE) untuk mesin tertentu pada shift saat ini.
    /// Memperhitungkan event yang sedang berjalan (ongoing) dengan menggunakan DateTime.Now.
    /// </summary>
    Task<TimeMetricsResult> GetTimeMetricsAsync(string machineId, int? shiftId = null, DateTime? shiftDate = null, string? shiftCode = null);

    /// <summary>
    /// Menutup otomatis JobRun dan DowntimeEvent yang melewati batas shift.
    /// </summary>
    Task AutoCloseShiftJobsAsync();

    /// <summary>
    /// Menghitung durasi detail sebuah JobRun (Running, Downtime, NetOperating) secara real-time.
    /// Opsional: Batasi perhitungan dalam window waktu tertentu (misal: Shift saat ini).
    /// </summary>
    JobDurationMetrics CalculateJobDuration(JobRun job, DateTime now, DateTime? windowStart = null, DateTime? windowEnd = null);

    /// <summary>
    /// Helper untuk mendapatkan window shift saat ini berdasarkan waktu server.
    /// </summary>
    Task<(DateTime Start, DateTime End, Shift Shift)> GetCurrentShiftWindowAsync();
}

public record JobDurationMetrics(
    TimeSpan TotalDuration,       // Sejak job start sampai now (clipped by window)
    TimeSpan TotalNonRunningTime, // Akumulasi semua stops (Rest + LineStop + NoLoading)
    TimeSpan TotalDowntime,       // HANYA Line Stop (Unplanned)
    TimeSpan OperatingTime,       // TotalDuration - TotalNonRunningTime
    TimeSpan CurrentDowntime,     // Durasi downtime yang sedang aktif (jika ada)
    bool IsRunning                // True jika tidak ada downtime aktif
);

public class TimeMetricsResult
{
    public string ShiftKey { get; set; } = string.Empty;
    public string ShiftCode { get; set; } = string.Empty;
    public DateTime ShiftDate { get; set; }
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    
    public double PlannedProductionTimeSeconds { get; set; }
    public string PlannedProductionTime { get; set; } = "00:00:00";
    public double OperatingTimeSeconds { get; set; }
    public string OperatingTime { get; set; } = "00:00:00";
    public double DowntimeTotalSeconds { get; set; }
    public string DowntimeTotal { get; set; } = "00:00:00";
    public double RestBreakTimeSeconds { get; set; }
    public string RestBreakTime { get; set; } = "00:00:00";
    public double NoLoadingTimeSeconds { get; set; }
    public string NoLoadingTime { get; set; } = "00:00:00";
    public double NettOperatingTimeSeconds { get; set; }
    public string NettOperatingTime { get; set; } = "00:00:00";
    
    public double OperatingPercent { get; set; }
    public double DowntimePercent { get; set; }
    public double NoLoadingPercent { get; set; }
    
    public bool HasActiveJob { get; set; }
    public string? ActiveJobStartTime { get; set; }
    public bool HasActiveDowntime { get; set; }
    public bool HasActiveRestBreak { get; set; }
    public string? LastStatusChangeTime { get; set; }
    public int? SinceLastChangeSeconds { get; set; }
    public string? ActiveDowntimeStartTime { get; set; }
    
    public double Oee { get; set; }
    public double Availability { get; set; }
    public double Performance { get; set; }
    public double Quality { get; set; }
    
    public int GoodCount { get; set; }
    public int RejectCount { get; set; }
    public int TotalCount { get; set; }
    
    public string MachineStatus { get; set; } = string.Empty;
    
    public int? DandoriDurationSeconds { get; set; }
    public string? DandoriStartTime { get; set; }
    public string? DandoriEndTime { get; set; }
}


