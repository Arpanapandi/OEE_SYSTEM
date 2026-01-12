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
        TimeSpan plannedDowntime = TimeSpan.Zero;
        TimeSpan unplannedDowntime = TimeSpan.Zero;
        TimeSpan restBreakTime = TimeSpan.Zero;
        bool hasActiveRestBreak = false;

        foreach (var jr in shiftJobRuns)
        {
            foreach (var d in jr.DowntimeEvents)
            {
                var dEnd = d.EndTime ?? effectiveNow;
                var overlap = GetOverlap(d.StartTime, dEnd, shiftStart, shiftEnd);
                
                if (d.Reason?.Category == "Unplanned")
                    unplannedDowntime += overlap;
                else
                    plannedDowntime += overlap;

                if (d.Reason?.Description == "Rest Break" || (d.Reason?.Category == "Planned" && d.Reason?.Description?.Contains("Rest", StringComparison.OrdinalIgnoreCase) == true))
                {
                    restBreakTime += overlap;
                    if (d.EndTime == null) hasActiveRestBreak = true;
                }
            }
        }

        TimeSpan downtimeTotal = plannedDowntime + unplannedDowntime;
        TimeSpan operatingTime = totalShiftTime - downtimeTotal;
        if (operatingTime.TotalSeconds < 0) operatingTime = TimeSpan.Zero;

        TimeSpan plannedProductionTime = totalShiftTime - plannedDowntime;
        if (plannedProductionTime.TotalSeconds < 0) plannedProductionTime = TimeSpan.Zero;
        if (plannedProductionTime.TotalSeconds == 0 && shiftJobRuns.Count == 0) plannedProductionTime = totalShiftTime;

        TimeSpan noLoadingTime = totalShiftTime - (operatingTime + downtimeTotal);
        if (noLoadingTime.TotalSeconds < 0) noLoadingTime = TimeSpan.Zero;

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
                lastStatusChangeTime = activeDowntime.StartTime;
                sinceLastChangeSeconds = (int)(now - activeDowntime.StartTime).TotalSeconds;
            }
            else
            {
                var lastDowntime = activeJob.DowntimeEvents.Where(d => d.EndTime.HasValue).OrderByDescending(d => d.EndTime).FirstOrDefault();
                DateTime runningStartTime = lastDowntime?.EndTime ?? (activeJob.StartTime < shiftStart ? shiftStart : activeJob.StartTime);
                
                var runningEndInShift = now > shiftEnd ? shiftEnd : now;
                var runningDuration = (runningEndInShift - runningStartTime).TotalSeconds;
                if (runningDuration < 0) runningDuration = 0;
                
                var totalUnplannedDowntimeSeconds = activeJob.DowntimeEvents
                    .Where(d => d.EndTime.HasValue && d.Reason?.Category == "Unplanned")
                    .Sum(d => GetOverlap(d.StartTime < shiftStart ? shiftStart : d.StartTime, d.EndTime!.Value > shiftEnd ? shiftEnd : d.EndTime.Value, shiftStart, shiftEnd).TotalSeconds);
                
                sinceLastChangeSeconds = Math.Max(0, (int)(runningDuration - totalUnplannedDowntimeSeconds));
                lastStatusChangeTime = now.AddSeconds(-(double)sinceLastChangeSeconds);
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


