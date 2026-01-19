using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;
using OeeSystem.Models;

namespace OeeSystem.Services;

public class OeeService : IOeeService
{
    private readonly ApplicationDbContext _context;

    public OeeService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Menghitung OEE sesuai formula standar industri:
    /// - Availability = Operating Time / Planned Production Time
    /// - Performance = (CT Standar × Product Output) / Operating Time
    /// - Quality = Good Count / Total Count
    /// - OEE = Availability × Performance × Quality
    /// </summary>
    public OeeResult CalculateOee(
        TimeSpan plannedProductionTime,
        TimeSpan unplannedDowntime,
        int totalCount,
        int goodCount,
        double standarCycleTime)
    {
        double plannedProductionSeconds = plannedProductionTime.TotalSeconds;
        double unplannedDownSeconds = unplannedDowntime.TotalSeconds;
        
        // Operating Time = Planned Production Time - Unplanned Downtime
        double operatingSeconds = Math.Max(0, plannedProductionSeconds - unplannedDownSeconds);

        // 1. Availability = Operating Time / Planned Production Time
        double availability = plannedProductionSeconds <= 0
            ? 0
            : Math.Min(100.0, (operatingSeconds / plannedProductionSeconds) * 100.0);

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
        // Status real-time untuk dashboard utama
        if (hasOpenDowntime) return MachineStatus.TidakAktif;
        return (activeJobRun != null && activeJobRun.EndTime == null) ? MachineStatus.Aktif : MachineStatus.TidakAktif;
    }

    public async Task<TimeMetricsResult> GetTimeMetricsAsync(string machineId, int? shiftId = null, DateTime? shiftDate = null, string? shiftCode = null)
    {
        var now = DateTime.Now;
        var today = shiftDate ?? now.Date;

        // 1. Resolve Shift (Gunakan DB jika Id ada, atau tetap pakai Rolling Shift untuk fallback/testing)
        Shift? selectedShift = null;
        if (shiftId.HasValue)
        {
            selectedShift = await _context.Shifts.FindAsync(shiftId.Value);
        }

        DateTime shiftStartTime, shiftEndTime;
        string finalShiftCode;

        if (selectedShift != null)
        {
            shiftStartTime = today.Add(selectedShift.StartTime);
            shiftEndTime = today.Add(selectedShift.EndTime);
            
            // Handle cross-day shift (e.g. 22:00 - 06:00)
            if (shiftEndTime <= shiftStartTime)
            {
                if (now.TimeOfDay < selectedShift.EndTime)
                {
                    shiftStartTime = shiftStartTime.AddDays(-1);
                }
                else
                {
                    shiftEndTime = shiftEndTime.AddDays(1);
                }
            }
            finalShiftCode = selectedShift.Name;
        }
        else
        {
            // Fallback Rolling Shift (10 menit per blok)
            int blockIndex = now.Minute / 10;
            shiftStartTime = now.Date.AddHours(now.Hour).AddMinutes(blockIndex * 10);
            shiftEndTime = shiftStartTime.AddMinutes(10);
            finalShiftCode = (blockIndex % 2 == 0) ? "A" : "B";
        }

        var effectiveEnd = now < shiftEndTime ? now : shiftEndTime;
        var shiftKey = $"{today:yyyy-MM-dd}|{(selectedShift?.Id.ToString() ?? finalShiftCode)}";

        // 2. Fetch Machine Data with Related Shift Records
        var machine = await _context.Machines
            .AsNoTracking()
            .Include(m => m.JobRuns.Where(j => j.StartTime < shiftEndTime && (j.EndTime == null || j.EndTime > shiftStartTime)))
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
            .FirstOrDefault(j => j.EndTime == null || j.EndTime > shiftStartTime);

        // 3. Calculate Durations based on Timestamps within Shift Window
        TimeSpan totalShiftElapsed = effectiveEnd - shiftStartTime;
        if (totalShiftElapsed.TotalSeconds < 0) totalShiftElapsed = TimeSpan.Zero;

        TimeSpan restBreakTime = TimeSpan.Zero;
        TimeSpan noLoadingTime = TimeSpan.Zero;
        TimeSpan lineStopTime = TimeSpan.Zero;
        
        bool hasActiveDowntime = false;
        bool hasActiveRestBreak = false;
        bool isNoLoading = false;
        DowntimeEvent? currentOpenDowntime = null;

        foreach (var job in machine.JobRuns)
        {
            foreach (var d in job.DowntimeEvents)
            {
                var dStart = d.StartTime;
                var dEnd = d.EndTime ?? now;
                
                // Clip to Shift Window
                var overlap = GetOverlap(dStart, dEnd, shiftStartTime, effectiveEnd);
                if (overlap.TotalSeconds <= 0) continue;

                if (d.IsRestBreak)
                {
                    restBreakTime += overlap;
                    if (d.EndTime == null) hasActiveRestBreak = true;
                }
                else if (d.IsNoLoading)
                {
                    noLoadingTime += overlap;
                    if (d.EndTime == null) isNoLoading = true;
                }
                else if (d.IsLineStop || d.Reason?.Category == "Unplanned")
                {
                    lineStopTime += overlap;
                }

                if (d.EndTime == null)
                {
                    hasActiveDowntime = true;
                    currentOpenDowntime = d;
                }
            }
        }

        // 4. Industrial OEE Metrics
        // Planned Production = Shift Time - (Rest Break + NoLoading)
        TimeSpan plannedProductionTime = totalShiftElapsed - restBreakTime - noLoadingTime;
        if (plannedProductionTime.TotalSeconds < 0) plannedProductionTime = TimeSpan.Zero;

        // Operating Time = Planned Production - Line Stop
        TimeSpan operatingTime = plannedProductionTime - lineStopTime;
        if (operatingTime.TotalSeconds < 0) operatingTime = TimeSpan.Zero;

        // 5. Production Metrics
        var productionInShift = machine.JobRuns
            .SelectMany(j => j.ProductionCounts)
            .Where(p => p.Timestamp >= shiftStartTime && p.Timestamp <= effectiveEnd)
            .ToList();

        int goodCount = productionInShift.Sum(p => p.GoodCount);
        int rejectCount = productionInShift.Sum(p => p.RejectCount);
        int totalCount = goodCount + rejectCount;

        double standarCycleTime = activeJob?.WorkOrder?.Product?.StandarCycleTime ?? 0;
        TimeSpan nettOperatingTime = (standarCycleTime > 0 && totalCount > 0)
            ? TimeSpan.FromSeconds(standarCycleTime * totalCount)
            : TimeSpan.Zero;

        var oeeResult = CalculateOee(plannedProductionTime, lineStopTime, totalCount, goodCount, standarCycleTime > 0 ? standarCycleTime : 1);

        // 6. UI Specific Metrics
        DateTime? lastStatusChangeTime = null;
        int? sinceLastChangeSeconds = null;

        if (activeJob != null)
        {
            if (currentOpenDowntime != null)
            {
                lastStatusChangeTime = currentOpenDowntime.StartTime;
                sinceLastChangeSeconds = (int)(now - currentOpenDowntime.StartTime).TotalSeconds;
            }
            else
            {
                // LastStatusChangeTime is either job start or end of last downtime
                var lastClosedDowntime = activeJob.DowntimeEvents
                    .Where(d => d.EndTime != null)
                    .OrderByDescending(d => d.EndTime)
                    .FirstOrDefault();

                lastStatusChangeTime = lastClosedDowntime?.EndTime ?? activeJob.StartTime;
                sinceLastChangeSeconds = (int)(now - lastStatusChangeTime.Value).TotalSeconds;
            }
        }

        return new TimeMetricsResult
        {
            ShiftKey = shiftKey,
            ShiftCode = finalShiftCode,
            ShiftDate = today,
            ShiftStart = shiftStartTime,
            ShiftEnd = shiftEndTime,
            
            PlannedProductionTimeSeconds = Math.Floor(plannedProductionTime.TotalSeconds),
            PlannedProductionTime = FormatTimeSpan(plannedProductionTime),
            OperatingTimeSeconds = Math.Floor(operatingTime.TotalSeconds),
            OperatingTime = FormatTimeSpan(operatingTime),
            DowntimeTotalSeconds = Math.Floor(lineStopTime.TotalSeconds),
            DowntimeTotal = FormatTimeSpan(lineStopTime),
            
            RestBreakTimeSeconds = Math.Floor(restBreakTime.TotalSeconds),
            RestBreakTime = FormatTimeSpan(restBreakTime),
            NoLoadingTimeSeconds = Math.Floor(noLoadingTime.TotalSeconds),
            NoLoadingTime = FormatTimeSpan(noLoadingTime),
            NettOperatingTimeSeconds = Math.Floor(nettOperatingTime.TotalSeconds),
            NettOperatingTime = FormatTimeSpan(nettOperatingTime),
            
            OperatingPercent = plannedProductionTime.TotalSeconds > 0 ? Math.Round(operatingTime.TotalSeconds / plannedProductionTime.TotalSeconds * 100, 1) : 0,
            DowntimePercent = plannedProductionTime.TotalSeconds > 0 ? Math.Round(lineStopTime.TotalSeconds / plannedProductionTime.TotalSeconds * 100, 1) : 0,
            
            HasActiveJob = activeJob != null && (activeJob.EndTime == null || activeJob.EndTime > shiftStartTime),
            ActiveJobStartTime = activeJob?.StartTime.ToString("O"),
            HasActiveDowntime = hasActiveDowntime,
            HasActiveRestBreak = hasActiveRestBreak,
            IsNoLoading = isNoLoading,
            IsIdle = activeJob == null || (activeJob.EndTime != null && activeJob.EndTime < now),
            
            LastStatusChangeTime = lastStatusChangeTime?.ToString("O"),
            SinceLastChangeSeconds = sinceLastChangeSeconds,
            ActiveDowntimeStartTime = currentOpenDowntime?.StartTime.ToString("O"),
            
            Oee = oeeResult.Oee,
            Availability = oeeResult.Availability,
            Performance = oeeResult.Performance,
            Quality = oeeResult.Quality,
            
            GoodCount = goodCount,
            RejectCount = rejectCount,
            TotalCount = totalCount,
            MachineStatus = machine.Status.ToString()
        };
    }

    private static TimeSpan GetOverlap(DateTime start, DateTime end, DateTime windowStart, DateTime windowEnd)
    {
        var overlapStart = start < windowStart ? windowStart : start;
        var overlapEnd = end > windowEnd ? windowEnd : end;
        return overlapEnd > overlapStart ? overlapEnd - overlapStart : TimeSpan.Zero;
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        int hours = (int)ts.TotalHours;
        return $"{hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}


