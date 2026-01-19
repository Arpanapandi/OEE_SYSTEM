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
        // 0. Auto-close expired shift jobs first
        await AutoCloseShiftJobsAsync();

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
                        shiftEnd = today + selectedShift.EndTime;
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

        // Filter job runs that overlap with the shift window
        var shiftJobRuns = machine.JobRuns
            .Where(j => j.StartTime < shiftEnd && (j.EndTime ?? effectiveNow) > shiftStart)
            .ToList();

        // Total shift duration
        TimeSpan totalShiftTime = shiftEnd - shiftStart;
        
        // CUSTOM OEE CALCULATION RULES:
        // 1. Planned Production = Shift Time - (Rest Break + NoLoading)
        // 2. Operating Time = Planned Production - Unplanned Downtime (Line Stop)
        // 3. Availability = Operating Time / Planned Production
        
        TimeSpan restBreakTime = TimeSpan.Zero;
        TimeSpan noLoadingTime = TimeSpan.Zero;
        TimeSpan lineStopTime = TimeSpan.Zero; // This is the only one that counts as "Down Time" for OEE
        
        bool hasActiveRestBreak = false;

        foreach (var jr in shiftJobRuns)
        {
            // Calculate overlap of JobRun with Shift
            var jrStart = jr.StartTime < shiftStart ? shiftStart : jr.StartTime;
            var jrEnd = (jr.EndTime ?? effectiveNow) > shiftEnd ? shiftEnd : (jr.EndTime ?? effectiveNow);
            
            foreach (var d in jr.DowntimeEvents)
            {
                var dStart = d.StartTime < jrStart ? jrStart : d.StartTime;
                var dEnd = (d.EndTime ?? effectiveNow) > jrEnd ? jrEnd : (d.EndTime ?? effectiveNow);
                
                if (dStart >= dEnd) continue;
                
                TimeSpan overlap = dEnd - dStart;
                
                if (d.IsRestBreak)
                {
                    restBreakTime += overlap;
                    if (d.EndTime == null) hasActiveRestBreak = true;
                }
                else if (d.IsNoLoading)
                {
                    noLoadingTime += overlap;
                }
                else if (d.IsLineStop || d.Reason?.Category == "Unplanned")
                {
                    lineStopTime += overlap;
                }
            }
        }

        // FORMULA IMPLEMENTATION
        TimeSpan plannedProductionTime = totalShiftTime - restBreakTime - noLoadingTime;
        if (plannedProductionTime.TotalSeconds < 0) plannedProductionTime = TimeSpan.Zero;
        
        // Operating Time = Planned Production - Line Stop
        TimeSpan operatingTime = plannedProductionTime - lineStopTime;
        if (operatingTime.TotalSeconds < 0) operatingTime = TimeSpan.Zero;

        // OEE Calculations
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

        // Use standard industri: Loading Time = Planned Production, Down Time = Line Stop
        var oeeResult = CalculateOee(plannedProductionTime, lineStopTime, totalCount, goodCount, standarCycleTime > 0 ? standarCycleTime : 1);

        // Timer Sync Info
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
                // Running: display current operating duration
                lastStatusChangeTime = activeJob.LastStatusChangeTime ?? activeJob.StartTime;
                sinceLastChangeSeconds = Math.Max(0, (int)(now - lastStatusChangeTime.Value).TotalSeconds);
            }
        }

        // Dandori Info
        int? dandoriDurationSeconds = null;
        DateTime? dandoriStart = null, dandoriEnd = null;
        if (activeJob != null)
        {
            try {
                dandoriStart = activeJob.DandoriStartTime;
                dandoriEnd = activeJob.DandoriEndTime;
                var storedDandori = activeJob.DandoriDurationSeconds;
                if (dandoriStart.HasValue && dandoriEnd.HasValue) 
                    dandoriDurationSeconds = (int)(dandoriEnd.Value - dandoriStart.Value).TotalSeconds;
                else if (dandoriStart.HasValue && !dandoriEnd.HasValue) 
                    dandoriDurationSeconds = (storedDandori ?? 0) + (int)(now - dandoriStart.Value).TotalSeconds;
                else if (storedDandori.HasValue) 
                    dandoriDurationSeconds = storedDandori.Value;
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
            DowntimeTotalSeconds = Math.Floor(lineStopTime.TotalSeconds), // Down Time = Line Stop
            DowntimeTotal = lineStopTime.ToString(@"hh\:mm\:ss"),
            RestBreakTimeSeconds = Math.Floor(restBreakTime.TotalSeconds),
            RestBreakTime = restBreakTime.ToString(@"hh\:mm\:ss"),
            HasActiveRestBreak = hasActiveRestBreak,
            NoLoadingTimeSeconds = Math.Floor(noLoadingTime.TotalSeconds),
            NoLoadingTime = noLoadingTime.ToString(@"hh\:mm\:ss"),
            NettOperatingTimeSeconds = Math.Floor(nettOperatingTime.TotalSeconds),
            NettOperatingTime = nettOperatingTime.ToString(@"hh\:mm\:ss"),
            OperatingPercent = plannedProductionTime.TotalSeconds > 0 ? Math.Round(operatingTime.TotalSeconds / plannedProductionTime.TotalSeconds * 100, 1) : 0,
            DowntimePercent = plannedProductionTime.TotalSeconds > 0 ? Math.Round(lineStopTime.TotalSeconds / plannedProductionTime.TotalSeconds * 100, 1) : 0,
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
    public async Task AutoCloseShiftJobsAsync()
    {
        var now = DateTime.Now;
        var today = now.Date;
        var shifts = await _context.Shifts.AsNoTracking().ToListAsync();
        
        // Cari status shift saat ini
        Shift? currentShift = null;
        DateTime currentShiftEnd = DateTime.MinValue;

        foreach (var s in shifts)
        {
            DateTime sStart, sEnd;
            if (s.EndTime < s.StartTime) // Malam
            {
                if (now.TimeOfDay < s.EndTime)
                {
                    sStart = today.AddDays(-1) + s.StartTime;
                    sEnd = today + s.EndTime;
                }
                else
                {
                    sStart = today + s.StartTime;
                    sEnd = today.AddDays(1) + s.EndTime;
                }
            }
            else
            {
                sStart = today + s.StartTime;
                sEnd = today + s.EndTime;
            }

            if (now >= sStart && now < sEnd)
            {
                currentShift = s;
                currentShiftEnd = sEnd;
                break;
            }
        }

        if (currentShift == null) return;

        // Cari JobRun aktif yang StartTime-nya di luar window shift saat ini (artinya dari shift sebelumnya)
        // ATAU JobRun yang StartTime-nya di shift ini tapi 'now' sudah melewati shift end (meskipun ini jarang jika dipanggil setiap request)
        
        var activeJobs = await _context.JobRuns
            .Include(j => j.DowntimeEvents)
            .Where(j => j.EndTime == null)
            .ToListAsync();

        bool changed = false;
        foreach (var job in activeJobs)
        {
            // Tentukan shift window untuk job ini berdasarkan StartTime-nya
            Shift? jobShift = null;
            DateTime jobShiftEnd = DateTime.MinValue;
            var jobStartToday = job.StartTime.Date;

            foreach (var s in shifts)
            {
                DateTime sStart, sEnd;
                if (s.EndTime < s.StartTime)
                {
                    // Ini agak tricky, kita asumsikan jobStart adalah awal shift
                    sStart = jobStartToday + s.StartTime;
                    sEnd = jobStartToday.AddDays(1) + s.EndTime;
                    
                    // Jika job started sebelum sStart tapi masih masuk window malam (misal jam 5 pagi)
                    if (job.StartTime < sStart && job.StartTime.TimeOfDay < s.EndTime)
                    {
                        sStart = jobStartToday.AddDays(-1) + s.StartTime;
                        sEnd = jobStartToday + s.EndTime;
                    }
                }
                else
                {
                    sStart = jobStartToday + s.StartTime;
                    sEnd = jobStartToday + s.EndTime;
                }

                if (job.StartTime >= sStart && job.StartTime < sEnd)
                {
                    jobShift = s;
                    jobShiftEnd = sEnd;
                    break;
                }
            }

            // Jika shift job ini sudah berakhir (now > jobShiftEnd), tutup paksa
            if (jobShift != null && now > jobShiftEnd)
            {
                job.EndTime = jobShiftEnd;
                
                // Tutup juga downtime yang masih open
                foreach (var d in job.DowntimeEvents.Where(de => de.EndTime == null))
                {
                    d.EndTime = jobShiftEnd;
                    d.DurationSeconds = (jobShiftEnd - d.StartTime).TotalSeconds;
                }
                
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
            // Optional: Broadcast SignalR ShiftEnded
        }
    }
}


