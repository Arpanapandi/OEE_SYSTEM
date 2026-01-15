using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;
using OeeSystem.Models;

namespace OeeSystem.Services;

public class OeeService : IOeeService
{
    private readonly ApplicationDbContext _context;
    private static readonly List<(string Code, TimeSpan Start, TimeSpan End)> ShiftTemplates = new()
    {
        ("A", new TimeSpan(6, 0, 0), new TimeSpan(14, 0, 0)),
        ("B", new TimeSpan(14, 0, 0), new TimeSpan(22, 0, 0)),
        ("C", new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0))
    };

    public OeeService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Menghitung OEE sesuai formula standar dari gambar:
    /// - Availability = (Loading Time - Down Time) / Loading Time × 100
    /// - Performance = (CT Standar × Product Output) / Operating Time × 100
    /// - Quality = (Product Unit Processed - Defect Unit) / Product Unit Processed × 100
    /// - OEE = Availability × Performance × Quality
    /// </summary>
    public OeeResult CalculateOee(
        TimeSpan loadingTime,
        TimeSpan downTime,
        int totalCount,
        int goodCount,
        double standarCycleTime)
    {
        double loadingSeconds = loadingTime.TotalSeconds;
        double downSeconds = downTime.TotalSeconds;
        
        // Industrial Standard:
        // Operating Time = Loading Time - Unplanned Downtime
        double operatingSeconds = Math.Max(0, loadingSeconds - downSeconds);

        // 1. Availability = Operating Time / Loading Time
        double availability = loadingSeconds <= 0
            ? 0
            : Math.Min(100.0, operatingSeconds / loadingSeconds * 100.0);

        // 2. Performance = (Standar Cycle Time * Total Output) / Operating Time
        double performance = (operatingSeconds <= 0 || standarCycleTime <= 0 || totalCount <= 0)
            ? 0
            : Math.Min(100.0, (standarCycleTime * totalCount) / operatingSeconds * 100.0);

        // 3. Quality = Good Count / Total Count
        double quality = totalCount <= 0
            ? 100.0
            : Math.Min(100.0, (double)goodCount / totalCount * 100.0);

        // OEE = A * P * Q
        double oee = (availability / 100.0) * (performance / 100.0) * (quality / 100.0) * 100.0;

        return new OeeResult(
            Math.Round(availability, 2),
            Math.Round(performance, 2),
            Math.Round(quality, 2),
            Math.Round(oee, 2));
    }

    public MachineStatus GetRealTimeStatus(Machine machine, JobRun? activeJobRun, bool hasOpenDowntime)
    {
        if (hasOpenDowntime)
        {
            return MachineStatus.TidakAktif;
        }

        if (activeJobRun != null && activeJobRun.EndTime == null)
        {
            return MachineStatus.Aktif;
        }

        return MachineStatus.TidakAktif;
    }

    public async Task<TimeMetricsResult> GetTimeMetricsAsync(string machineId, int? shiftId = null, DateTime? shiftDate = null, string? shiftCode = null)
    {
        var now = DateTime.Now;
        var today = now.Date;
        
        // Ambil semua shifts dari database
        var shifts = await _context.Shifts.AsNoTracking().ToListAsync();
        
        // Tentukan shift yang dipilih atau shift saat ini
        Shift? selectedShift = null;
        if (shiftId.HasValue)
        {
            selectedShift = shifts.FirstOrDefault(s => s.Id == shiftId.Value);
        }
        
        if (selectedShift == null)
        {
            foreach (var s in shifts)
            {
                var start = today + s.StartTime;
                var end = today + s.EndTime;
                if (s.EndTime < s.StartTime) // Shift malam
                {
                    if (now >= start || now <= end) { selectedShift = s; break; }
                }
                else
                {
                    if (now >= start && now <= end) { selectedShift = s; break; }
                }
            }
        }
        
        if (selectedShift == null) selectedShift = shifts.FirstOrDefault();
        
        DateTime shiftStart, shiftEnd, shiftDateForWindow;
        if (selectedShift != null)
        {
            if (selectedShift.EndTime < selectedShift.StartTime)
            {
                var shiftStartToday = today + selectedShift.StartTime;
                var shiftEndToday = today.AddDays(1) + selectedShift.EndTime;
                
                if (shiftDate.HasValue)
                {
                    shiftDateForWindow = shiftDate.Value.Date;
                    shiftStart = shiftDateForWindow + selectedShift.StartTime;
                    shiftEnd = shiftDateForWindow.AddDays(1) + selectedShift.EndTime;
                }
                else
                {
                    if (now.TimeOfDay < selectedShift.EndTime)
                    {
                        shiftStart = today.AddDays(-1) + selectedShift.StartTime;
                        shiftEnd = shiftEndToday;
                        shiftDateForWindow = today.AddDays(-1);
                    }
                    else
                    {
                        shiftStart = shiftStartToday;
                        shiftEnd = shiftEndToday;
                        shiftDateForWindow = today;
                    }
                }
            }
            else
            {
                shiftStart = today + selectedShift.StartTime;
                shiftEnd = today + selectedShift.EndTime;
                shiftDateForWindow = today;
            }
        }
        else
        {
            shiftStart = today + new TimeSpan(6, 0, 0);
            shiftEnd = today + new TimeSpan(14, 0, 0);
            shiftDateForWindow = today;
        }
        
        var shiftKey = $"{shiftDateForWindow:yyyy-MM-dd}|{selectedShift?.Id ?? 0}";
        var effectiveNow = now < shiftEnd ? now : shiftEnd;

        var machine = await _context.Machines
            .AsNoTracking()
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.WorkOrder!)
                    .ThenInclude(w => w.Product)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.DowntimeEvents)
                    .ThenInclude(d => d.Reason)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.ProductionCounts)
            .FirstOrDefaultAsync(m => m.Id == machineId);

        if (machine == null) return new TimeMetricsResult();

        var activeJob = machine.JobRuns
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefault(j => j.EndTime == null);

        var activeDowntime = activeJob?.DowntimeEvents
            .OrderByDescending(d => d.StartTime)
            .FirstOrDefault(d => d.EndTime == null);

        var shiftJobRuns = machine.JobRuns
            .Where(j => j.StartTime < shiftEnd && (j.EndTime ?? effectiveNow) > shiftStart)
            .ToList();

        TimeSpan totalShiftTime = shiftEnd - shiftStart;
        
        // ✅ CUSTOM OEE CALCULATION:
        // Rest Break dan No Loading TIDAK masuk Downtime Total
        // HANYA Line Stop yang masuk Downtime Total
        TimeSpan restBreakTime = TimeSpan.Zero;
        TimeSpan noLoadingTime = TimeSpan.Zero;
        TimeSpan lineStopTime = TimeSpan.Zero;  // HANYA ini yang masuk Downtime
        
        // Legacy tracking (untuk backward compatibility)
        TimeSpan plannedDowntime = TimeSpan.Zero;
        TimeSpan unplannedDowntime = TimeSpan.Zero;
        bool hasActiveRestBreak = false;

        foreach (var jr in shiftJobRuns)
        {
            foreach (var d in jr.DowntimeEvents)
            {
                var dEnd = d.EndTime ?? effectiveNow;
                var overlap = GetOverlap(d.StartTime, dEnd, shiftStart, shiftEnd);
                
                // ✅ CUSTOM: Categorize berdasarkan flags
                if (d.IsRestBreak || 
                    d.Reason?.Description?.Contains("Rest", StringComparison.OrdinalIgnoreCase) == true)
                {
                    restBreakTime += overlap;
                    plannedDowntime += overlap;  // Legacy
                    if (d.EndTime == null) hasActiveRestBreak = true;
                }
                else if (d.IsNoLoading || 
                         d.Reason?.Description?.Contains("No Loading", StringComparison.OrdinalIgnoreCase) == true)
                {
                    noLoadingTime += overlap;
                    plannedDowntime += overlap;  // Legacy
                }
                else if (d.IsLineStop || 
                         d.Reason?.Category == "Unplanned")
                {
                    lineStopTime += overlap;  // HANYA ini yang masuk Downtime Total
                    unplannedDowntime += overlap;  // Legacy
                }
                else
                {
                    // Fallback: jika tidak ada flag, gunakan Category
                    if (d.Reason?.Category == "Unplanned")
                    {
                        lineStopTime += overlap;
                        unplannedDowntime += overlap;
                    }
                    else
                    {
                        plannedDowntime += overlap;
                    }
                }
            }
        }

        // ✅ CUSTOM FORMULA:
        // Planned Production Time = Total Shift - Rest Break - No Loading
        TimeSpan plannedProductionTime = totalShiftTime - restBreakTime - noLoadingTime;
        if (plannedProductionTime.TotalSeconds < 0) plannedProductionTime = TimeSpan.Zero;
        if (plannedProductionTime.TotalSeconds == 0 && shiftJobRuns.Count == 0) 
            plannedProductionTime = totalShiftTime;
        
        // Operating Time = Planned Production - Line Stop (HANYA Line Stop)
        TimeSpan operatingTime = plannedProductionTime - lineStopTime;
        if (operatingTime.TotalSeconds < 0) operatingTime = TimeSpan.Zero;
        
        // Downtime Total = HANYA Line Stop
        TimeSpan downtimeTotal = lineStopTime;

        var allCounts = shiftJobRuns
            .SelectMany(j => j.ProductionCounts
                .Where(p => p.Timestamp >= shiftStart && p.Timestamp <= shiftEnd))
            .ToList();

        int totalCount = allCounts.Sum(c => c.GoodCount + c.RejectCount);
        int goodCount = allCounts.Sum(c => c.GoodCount);
        int rejectCount = allCounts.Sum(c => c.RejectCount);

        double standarCycleTime = activeJob?.WorkOrder?.Product?.StandarCycleTime ?? 0;
        TimeSpan nettOperatingTime = (standarCycleTime > 0 && totalCount > 0) 
            ? TimeSpan.FromSeconds(standarCycleTime * totalCount) 
            : TimeSpan.Zero;

        var oeeResult = CalculateOee(plannedProductionTime, unplannedDowntime, totalCount, goodCount, standarCycleTime > 0 ? standarCycleTime : 1);

        DateTime? lastStatusChangeTime = null;
        int? sinceLastChangeSeconds = null;

        if (activeJob != null)
        {
            if (activeDowntime != null)
            {
                // Ada downtime aktif: timer hitung dari downtime start
                lastStatusChangeTime = activeDowntime.StartTime;
                sinceLastChangeSeconds = (int)(now - activeDowntime.StartTime).TotalSeconds;
            }
            else
            {
                // Tidak ada downtime aktif (Running):
                // Timer hitung dari LastStatusChangeTime (bisa dari job start atau downtime end terakhir)
                if (activeJob.LastStatusChangeTime.HasValue)
                {
                    lastStatusChangeTime = activeJob.LastStatusChangeTime.Value;
                    sinceLastChangeSeconds = Math.Max(0, (int)(now - activeJob.LastStatusChangeTime.Value).TotalSeconds);
                }
                else
                {
                    // Fallback: gunakan job start time
                    lastStatusChangeTime = activeJob.StartTime;
                    sinceLastChangeSeconds = Math.Max(0, (int)(now - activeJob.StartTime).TotalSeconds);
                }
            }
        }

        int? dandoriDurationSeconds = null;
        DateTime? dandoriStart = null, dandoriEnd = null;
        if (activeJob != null)
        {
            try {
                dandoriStart = activeJob.DandoriStartTime;
                dandoriEnd = activeJob.DandoriEndTime;
                var storedDandori = activeJob.DandoriDurationSeconds;
                if (dandoriStart.HasValue && dandoriEnd.HasValue) dandoriDurationSeconds = (int)(dandoriEnd.Value - dandoriStart.Value).TotalSeconds;
                else if (dandoriStart.HasValue && !dandoriEnd.HasValue) dandoriDurationSeconds = (storedDandori ?? 0) + (int)(now - dandoriStart.Value).TotalSeconds;
                else if (storedDandori.HasValue) dandoriDurationSeconds = storedDandori.Value;
            } catch { }
        }

        return new TimeMetricsResult
        {
            ShiftKey = shiftKey,
            ShiftCode = selectedShift?.Name ?? "A",
            ShiftDate = shiftDateForWindow,
            ShiftStart = shiftStart,
            ShiftEnd = shiftEnd,
            PlannedProductionTimeSeconds = Math.Floor(plannedProductionTime.TotalSeconds),
            PlannedProductionTime = plannedProductionTime.ToString(@"hh\:mm\:ss"),
            OperatingTimeSeconds = Math.Floor(operatingTime.TotalSeconds),
            OperatingTime = operatingTime.ToString(@"hh\:mm\:ss"),
            DowntimeTotalSeconds = Math.Floor(downtimeTotal.TotalSeconds),
            DowntimeTotal = downtimeTotal.ToString(@"hh\:mm\:ss"),
            RestBreakTimeSeconds = Math.Floor(restBreakTime.TotalSeconds),
            RestBreakTime = restBreakTime.ToString(@"hh\:mm\:ss"),
            HasActiveRestBreak = hasActiveRestBreak,
            NoLoadingTimeSeconds = Math.Floor(noLoadingTime.TotalSeconds),
            NoLoadingTime = noLoadingTime.ToString(@"hh\:mm\:ss"),
            NettOperatingTimeSeconds = Math.Floor(nettOperatingTime.TotalSeconds),
            NettOperatingTime = nettOperatingTime.ToString(@"hh\:mm\:ss"),
            OperatingPercent = plannedProductionTime.TotalSeconds > 0 ? Math.Round(operatingTime.TotalSeconds / plannedProductionTime.TotalSeconds * 100, 1) : 0,
            DowntimePercent = plannedProductionTime.TotalSeconds > 0 ? Math.Round(downtimeTotal.TotalSeconds / plannedProductionTime.TotalSeconds * 100, 1) : 0,
            NoLoadingPercent = totalShiftTime.TotalSeconds > 0 ? Math.Round(noLoadingTime.TotalSeconds / totalShiftTime.TotalSeconds * 100, 1) : 0,
            HasActiveJob = activeJob != null,
            ActiveJobStartTime = activeJob?.StartTime.ToString("O"),
            HasActiveDowntime = activeDowntime != null,
            LastStatusChangeTime = lastStatusChangeTime?.ToString("O"),
            SinceLastChangeSeconds = sinceLastChangeSeconds,
            ActiveDowntimeStartTime = activeDowntime?.StartTime.ToString("O"),
            Oee = oeeResult.Oee,
            Availability = oeeResult.Availability,
            Performance = oeeResult.Performance,
            Quality = oeeResult.Quality,
            GoodCount = goodCount,
            RejectCount = rejectCount,
            TotalCount = totalCount,
            MachineStatus = machine.Status.ToString(),
            DandoriDurationSeconds = dandoriDurationSeconds,
            DandoriStartTime = dandoriStart?.ToString("O"),
            DandoriEndTime = dandoriEnd?.ToString("O")
        };
    }

    private static TimeSpan GetOverlap(DateTime start, DateTime end, DateTime windowStart, DateTime windowEnd)
    {
        var overlapStart = start < windowStart ? windowStart : start;
        var overlapEnd = end > windowEnd ? windowEnd : end;
        return overlapEnd > overlapStart ? overlapEnd - overlapStart : TimeSpan.Zero;
    }
}


