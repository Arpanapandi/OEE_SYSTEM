using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;
using OeeSystem.Models;
using OeeSystem.Services;
using OeeSystem.Models.ViewModels;

namespace OeeSystem.Controllers;

public class MachineController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IOeeService _oeeService;
    private static readonly List<(string Code, TimeSpan Start, TimeSpan End)> ShiftTemplates = new()
    {
        ("A", new TimeSpan(6, 0, 0), new TimeSpan(14, 0, 0)),
        ("B", new TimeSpan(14, 0, 0), new TimeSpan(22, 0, 0)),
        ("C", new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0))
    };

    public MachineController(ApplicationDbContext context, IOeeService oeeService)
    {
        _context = context;
        _oeeService = oeeService;
    }

    private static TimeSpan GetOverlap(DateTime start, DateTime end, DateTime windowStart, DateTime windowEnd)
    {
        var overlapStart = start < windowStart ? windowStart : start;
        var overlapEnd = end > windowEnd ? windowEnd : end;
        return overlapEnd > overlapStart ? overlapEnd - overlapStart : TimeSpan.Zero;
    }

    private static (DateTime Start, DateTime End, DateTime ShiftDate, string Code, string Key) ResolveShiftWindow(DateTime now, DateTime? shiftDate, string? shiftCode)
    {
        var code = string.IsNullOrWhiteSpace(shiftCode)
            ? null
            : shiftCode!.Trim().ToUpperInvariant();

        // Tentukan kode shift jika tidak dikirim
        if (code == null)
        {
            var tod = now.TimeOfDay;
            if (tod >= ShiftTemplates[0].Start && tod < ShiftTemplates[0].End)
            {
                code = "A";
            }
            else if (tod >= ShiftTemplates[1].Start && tod < ShiftTemplates[1].End)
            {
                code = "B";
            }
            else
            {
                code = "C";
            }
        }

        var template = ShiftTemplates.FirstOrDefault(s => s.Code == code);
        if (template == default)
        {
            template = ShiftTemplates[0];
            code = template.Code;
        }

        var baseDate = shiftDate?.Date ?? now.Date;

        // Jika tidak ada shiftDate dan shift C berjalan lewat tengah malam, tarik ke hari sebelumnya
        if (!shiftDate.HasValue && code == "C" && now.TimeOfDay < template.End)
        {
            baseDate = baseDate.AddDays(-1);
        }

        var start = baseDate.Add(template.Start);
        var end = template.End > template.Start
            ? baseDate.Add(template.End)
            : baseDate.AddDays(1).Add(template.End);

        var key = $"{baseDate:yyyy-MM-dd}|{code}";
        return (start, end, baseDate, code!, key);
    }

    public async Task<IActionResult> OeeDetail(string id, int? shiftId = null, DateTime? shiftDate = null, string? shiftCode = null, string? filterDate = null, string? filterTime = null)
    {
        var now = DateTime.Now;
        var today = now.Date;
        
        // Ambil semua shifts dari database
        var shifts = await _context.Shifts.ToListAsync();
        
        // Tentukan shift yang dipilih atau shift saat ini
        Shift? selectedShift = null;
        if (shiftId.HasValue)
        {
            selectedShift = shifts.FirstOrDefault(s => s.Id == shiftId.Value);
        }
        
        // Jika tidak ada shift yang dipilih, gunakan shift saat ini
        if (selectedShift == null)
        {
            foreach (var s in shifts)
            {
                var start = today + s.StartTime;
                var end = today + s.EndTime;
                
                // Handle shift malam (end < start)
                if (s.EndTime < s.StartTime)
                {
                    if (now >= start || now <= end)
                    {
                        selectedShift = s;
                        break;
                    }
                }
                else
                {
                    if (now >= start && now <= end)
                    {
                        selectedShift = s;
                        break;
                    }
                }
            }
        }
        
        // Jika masih tidak ada shift, gunakan shift pertama sebagai default
        if (selectedShift == null)
        {
            selectedShift = shifts.FirstOrDefault();
        }
        
        // Tentukan periode shift yang dipilih
        DateTime shiftStart;
        DateTime shiftEnd;
        DateTime shiftDateForWindow;
        
        if (selectedShift != null)
        {
            // Handle shift malam (end < start, contoh: 22:00 - 06:00)
            if (selectedShift.EndTime < selectedShift.StartTime)
            {
                // Shift malam: mulai hari ini, selesai besok
                var shiftStartToday = today + selectedShift.StartTime;
                var shiftEndToday = today.AddDays(1) + selectedShift.EndTime;
                
                // ✅ PERBAIKAN: Jika ada shiftDate dari parameter (sudah di-resolve oleh JavaScript), gunakan itu
                if (shiftDate.HasValue)
                {
                    // shiftDate sudah di-resolve dengan logika shift melewati tengah malam
                    shiftDateForWindow = shiftDate.Value.Date;
                    shiftStart = shiftDateForWindow + selectedShift.StartTime;
                    shiftEnd = shiftDateForWindow.AddDays(1) + selectedShift.EndTime;
                }
                else if (shiftId.HasValue)
                {
                    // Jika shift dipilih secara eksplisit, tentukan shift hari ini atau kemarin
                    // Jika sekarang masih dalam periode shift hari ini
                    if (now >= shiftStartToday && now < shiftEndToday)
                    {
                        shiftStart = shiftStartToday;
                        shiftEnd = shiftEndToday;
                        shiftDateForWindow = today;
                    }
                    else
                    {
                        // Shift kemarin (mulai kemarin, selesai hari ini)
                        shiftStart = today.AddDays(-1) + selectedShift.StartTime;
                        shiftEnd = shiftEndToday;
                        shiftDateForWindow = today.AddDays(-1);
                    }
                }
                else
                {
                    // Auto-detect: jika sekarang sebelum end time, berarti shift kemarin
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
                // Shift normal (start < end)
                shiftStart = today + selectedShift.StartTime;
                shiftEnd = today + selectedShift.EndTime;
                shiftDateForWindow = today;
            }
        }
        else
        {
            // Fallback: gunakan shift template A
            shiftStart = today + new TimeSpan(6, 0, 0);
            shiftEnd = today + new TimeSpan(14, 0, 0);
            shiftDateForWindow = today;
        }
        
        var shiftWindow = (Start: shiftStart, End: shiftEnd, ShiftDate: shiftDateForWindow, Code: selectedShift?.Name ?? "A", Key: $"{shiftDateForWindow:yyyy-MM-dd}|{selectedShift?.Id ?? 0}");
        var effectiveNow = now < shiftWindow.End ? now : shiftWindow.End;

        // ✅ PERBAIKAN: Gunakan AsNoTracking() untuk menghindari error saat mapping property Dandori yang belum ada
        var machine = await _context.Machines
            // .AsNoTracking() // Dihapus agar bisa Load ManPower
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.WorkOrder!)
                    .ThenInclude(w => w.Product)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.Operator)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.DowntimeEvents)
                    .ThenInclude(d => d.Reason)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.ProductionCounts)
            .FirstOrDefaultAsync(m => m.Id == id);
        
        // Load ManPower secara terpisah untuk menghindari error jika tabel belum ada
        if (machine != null)
        {
            try
            {
                foreach (var jobRun in machine.JobRuns.Where(j => j.ManPowerId.HasValue))
                {
                    await _context.Entry(jobRun)
                        .Reference(j => j.ManPower)
                        .LoadAsync();
                }
            }
            catch
            {
                // Jika tabel ManPower belum ada, abaikan (ManPower akan null)
            }
        }

        if (machine == null)
        {
            return NotFound();
        }

        // DEBUG LOGGING
        Console.WriteLine($"[DEBUG] Machine: {machine.Name} (ID: {id})");
        Console.WriteLine($"[DEBUG] Now: {now}");
        Console.WriteLine($"[DEBUG] Total JobRuns: {machine.JobRuns?.Count ?? 0}");
        foreach(var j in machine.JobRuns ?? Enumerable.Empty<JobRun>()) {
            Console.WriteLine($"[DEBUG] Job: ID={j.Id}, WO={j.WorkOrderId}, Start={j.StartTime}, End={j.EndTime ?? (object)"NULL"}");
        }

        var activeJob = (machine.JobRuns ?? Enumerable.Empty<JobRun>())
            // .Where(j => j.StartTime <= now)
            .Where(j => j.StartTime <= now.AddHours(12)) // Filter future jobs (prevent masking by next month's plan)
            .OrderByDescending(j => j.StartTime)
            .ThenByDescending(j => j.Id)
            .FirstOrDefault(j => j.EndTime == null);
        
        Console.WriteLine($"[DEBUG] ActiveJob Found: {activeJob?.Id ?? (object)"NONE"}");

        bool hasOpenDowntime = activeJob != null &&
                               activeJob.DowntimeEvents.Any(d => d.EndTime == null);

        // ✅ MOVED UP: Required for CurrentState calculation in VM
        var openDowntimeForStatus = activeJob?.DowntimeEvents
            .OrderByDescending(d => d.StartTime)
            .FirstOrDefault(d => d.EndTime == null);

        var status = _oeeService.GetRealTimeStatus(machine, activeJob, hasOpenDowntime);

        // ========== DATA GATHERING FOR DETAILED LISTS ==========
        var shiftJobRuns = (machine.JobRuns ?? Enumerable.Empty<JobRun>())
            .Where(j => j.StartTime < shiftWindow.End && (j.EndTime ?? effectiveNow) > shiftWindow.Start)
            .ToList();

        var metrics = await _oeeService.GetTimeMetricsAsync(id, selectedShift?.Id, shiftDateForWindow);
        int goodCount = metrics.TotalGood;

        // Standar cycle time (needed for some UI logic below)
        double standarCycleTime = activeJob?.WorkOrder?.Product?.StandarCycleTime ?? 0;


        // 6. Build ViewModel
        var vm = new MachineOeeViewModel
        {
            MachineId = machine.Id,
            MachineName = machine.Name,
            LineId = machine.LineId,
            ShiftCode = metrics.ShiftCode,
            ShiftName = metrics.ShiftCode, // Menggunakan kode yang sama jika tidak ada mapping nama
            ShiftId = selectedShift?.Id,
            ShiftDate = metrics.ShiftDate,
            ShiftKey = metrics.ShiftKey,
            ShiftStart = metrics.ShiftStart,
            ShiftEnd = metrics.ShiftEnd,
            Status = metrics.IsRunning ? MachineStatus.Aktif : MachineStatus.TidakAktif,
            StandarCycleTime = standarCycleTime,
            ImageUrl = machine.ImageUrl,
            Oee = metrics.Oee,
            Availability = metrics.Availability,
            Performance = metrics.Performance,
            Quality = metrics.Quality,
            PlannedProductionTime = TimeSpan.FromSeconds(metrics.PlannedProductionTimeSeconds),
            OperatingTime = TimeSpan.FromSeconds(metrics.OperatingTimeSeconds),
            DowntimeTotal = TimeSpan.FromSeconds(metrics.DowntimeSeconds),
            RestBreakTime = TimeSpan.FromSeconds(metrics.RestBreakTimeSeconds),
            NoLoadingTime = TimeSpan.FromSeconds(metrics.NoLoadingTimeSeconds),
            NettOperatingTime = TimeSpan.FromSeconds(metrics.NettOperatingTimeSeconds),
            TotalCount = metrics.TotalCount,
            GoodCount = metrics.TotalGood,
            RejectCount = metrics.TotalReject,
            HasActiveJob = metrics.HasActiveJob,
            HasActiveDowntime = metrics.HasActiveDowntime,
            HasActiveRestBreak = metrics.HasActiveRestBreak,
            IsNoLoading = metrics.HasActiveNoLoading,
            MachineStatus = machine.Status,
            // ✅ ALIGNMENT: Calculate CurrentState dynamically (consistent with OeeService & OperatorController)
            CurrentState = activeJob == null ? "STOPPED" : 
                           (hasOpenDowntime && openDowntimeForStatus != null ? 
                                (openDowntimeForStatus.IsRestBreak ? "REST_BREAK" : 
                                 openDowntimeForStatus.IsNoLoading ? "NO_LOADING" : "LINE_STOP") 
                           : "RUNNING")
        };

        // Active Job Info
        if (activeJob != null)
        {
            var currentQty = activeJob.ProductionCounts.Sum(p => p.GoodCount + p.RejectCount);
            
            // ✅ PERUBAHAN: Gunakan logic terpusat dari OeeService dengan WINDOW SHIFT
            // Ini memastikan timer sinkron dengan OEE metrics (reset per shift)
            var durationMetrics = _oeeService.CalculateJobDuration(activeJob, DateTime.Now, shiftWindow.Start, shiftWindow.End);
            DateTime lastChangeTime;
            int sinceLastChangeSeconds;

            if (!durationMetrics.IsRunning)
            {
                // Downtime Mode: Timer menghitung Current Downtime Duration
                // Note: CurrentDowntime biasanya durasi real event, tidak perlu diclip ke shift window untuk display timer
                // Kecuali user mau timer reset per shift juga untuk downtime.
                // Tapi biasanya downtime duration itu absolut event.
                // Namun, durationMetrics.CurrentDowntime dihitung via CalculateJobDuration yang sudah kita clip.
                // Jika event mulai sebelum shift, calculated CurrentDowntime akan terpotong.
                // Untuk timer visual "Lama Kerusakan", mungkin user ingin total durasi kerusakan (bukan shift-clipped).
                // Tapi untuk konsistensi "OEE Downtime", harus clipped.
                // Kita ikuti logic CalculateJobDuration (Clipped) demi konsistensi.
                lastChangeTime = DateTime.Now.Subtract(durationMetrics.CurrentDowntime);
                sinceLastChangeSeconds = (int)durationMetrics.CurrentDowntime.TotalSeconds;
            }
            else
            {
                // Running Mode: Timer menghitung Operating Time Bersih (Clipped Shift)
                lastChangeTime = DateTime.Now.Subtract(durationMetrics.OperatingTime);
                sinceLastChangeSeconds = (int)durationMetrics.OperatingTime.TotalSeconds;
            }
            
            // Started time: Jika job dimulai sebelum shift start, gunakan shift start
            DateTime displayStartTime = activeJob.StartTime < shiftWindow.Start 
                ? shiftWindow.Start 
                : activeJob.StartTime;
            
            // Calculate estimated completion time (sama seperti Operator View)
            string? estimatedCompletion = null;
            var targetQuantity = activeJob.WorkOrder?.TargetQuantity ?? 0;
            if (targetQuantity > 0 && goodCount > 0 && activeJob.WorkOrder?.Product != null)
            {
                var remaining = targetQuantity - goodCount;
                if (remaining > 0)
                {
                    var productCycleTime = activeJob.WorkOrder.Product.StandarCycleTime;
                    var currentTime = DateTime.Now;
                    var elapsedTime = (currentTime - activeJob.StartTime).TotalSeconds;
                    var currentRate = elapsedTime > 0 ? goodCount / elapsedTime : 0; // units per second
                    
                    if (currentRate > 0)
                    {
                        var estimatedSeconds = remaining / currentRate;
                        var estimatedCompletionTime = currentTime.AddSeconds(estimatedSeconds);
                        estimatedCompletion = estimatedCompletionTime.ToString("HH:mm");
                    }
                }
            }
            
            // ✅ TAMBAHKAN: Hitung Dandori Duration (dengan error handling)
            int? dandoriDurationSeconds = null;
            DateTime? dandoriStartTime = null;
            DateTime? dandoriEndTime = null;
            int? dandoriDurationSecondsStored = null;
            
            try
            {
                // Coba akses property Dandori (jika kolom belum ada, akan return null)
                dandoriStartTime = activeJob.DandoriStartTime;
                dandoriEndTime = activeJob.DandoriEndTime;
                dandoriDurationSecondsStored = activeJob.DandoriDurationSeconds;
                
                if (dandoriStartTime.HasValue && dandoriEndTime.HasValue)
                {
                    // Dandori sudah selesai
                    dandoriDurationSeconds = (int)(dandoriEndTime.Value - dandoriStartTime.Value).TotalSeconds;
                }
                else if (dandoriStartTime.HasValue && !dandoriEndTime.HasValue)
                {
                    // Dandori sedang berjalan
                    var currentTime = DateTime.Now;
                    var baseDuration = dandoriDurationSecondsStored ?? 0;
                    dandoriDurationSeconds = baseDuration + (int)(currentTime - dandoriStartTime.Value).TotalSeconds;
                }
                else if (dandoriDurationSecondsStored.HasValue)
                {
                    // Hanya ada duration (backward compatibility)
                    dandoriDurationSeconds = dandoriDurationSecondsStored.Value;
                }
            }
            catch (Exception ex)
            {
                // Jika kolom Dandori belum ada di database, gunakan nilai default (null)
                Console.WriteLine($"WARNING: Error saat mengakses property Dandori: {ex.Message}");
                dandoriDurationSeconds = null;
                dandoriStartTime = null;
                dandoriEndTime = null;
            }
            
            // ✅ PERBAIKAN: Tambahkan fallback ke machine image jika Product.ImageUrl null
            string? productImageUrl = activeJob.WorkOrder?.Product?.ImageUrl;
            // Fallback Removed: Strictly use Product Image per user request
            // if (string.IsNullOrEmpty(productImageUrl)) { productImageUrl = machine.ImageUrl; }
            
            vm.ActiveJob = new JobRunViewModel
            {
                Id = activeJob.Id,
                WorkOrderNumber = activeJob.WorkOrder?.OrderNumber ?? "",
                ProductName = activeJob.WorkOrder?.Product?.Name ?? "",
                ProductImageUrl = productImageUrl, // ✅ PERBAIKAN: Gunakan fallback logic
                OperatorName = activeJob.Operator?.Username ?? "",
                ManPowerId = activeJob.ManPowerId,
                ManPowerName = activeJob.ManPower?.Name,
                StartTime = displayStartTime, // Sinkron dengan shift
                EndTime = activeJob.EndTime,
                TargetQuantity = targetQuantity,
                CurrentQuantity = currentQty,
                LastStatusChangeTime = lastChangeTime, // Tetap ada untuk backward compatibility
                SinceLastChangeSeconds = sinceLastChangeSeconds, // ✅ TAMBAHKAN untuk sinkronisasi dengan Operator View
                // ✅ TAMBAHKAN: Dandori Duration
                DandoriStartTime = dandoriStartTime,
                DandoriEndTime = dandoriEndTime,
                DandoriDurationSeconds = dandoriDurationSeconds
            };
            
            // Store estimated completion untuk JavaScript
            ViewData["EstimatedCompletion"] = estimatedCompletion;
        }

        // Recent Downtimes (10 terakhir) dalam window shift
        vm.RecentDowntimes = shiftJobRuns
            .SelectMany(j => j.DowntimeEvents)
            .Where(d => d.StartTime < shiftWindow.End && (d.EndTime ?? effectiveNow) > shiftWindow.Start)
            .OrderByDescending(d => d.StartTime)
            .Take(10)
            .Select(d => new DowntimeEventViewModel
            {
                Id = d.Id,
                ReasonCategory = d.Reason?.Category ?? "",
                ReasonDescription = d.Reason?.Description ?? "",
                StartTime = d.StartTime,
                EndTime = d.EndTime,
                DurationSeconds = d.DurationSeconds > 0
                    ? d.DurationSeconds
                    : (d.EndTime ?? effectiveNow).Subtract(d.StartTime).TotalSeconds
            })
            .ToList();

        // Recent Production Counts (20 terakhir) dalam window shift
        // ✅ PERBAIKAN: Gunakan shiftJobRuns untuk akses relasi yang sudah ter-load
        vm.RecentProductionCounts = shiftJobRuns
            .SelectMany(j => j.ProductionCounts
                .Where(p => p.Timestamp >= shiftWindow.Start && p.Timestamp <= shiftWindow.End)
                .Select(p => new { ProductionCount = p, JobRun = j }))
            .OrderByDescending(x => x.ProductionCount.Timestamp)
            .Take(20)
            .Select(x => new ProductionCountViewModel
            {
                Id = x.ProductionCount.Id,
                Timestamp = x.ProductionCount.Timestamp,
                GoodCount = x.ProductionCount.GoodCount,
                RejectCount = x.ProductionCount.RejectCount,
                RejectReason = x.ProductionCount.RejectReason,
                // ✅ TAMBAHKAN: Data baru (formula akan diimplementasikan nanti)
                PartCode = x.JobRun.WorkOrder != null && x.JobRun.WorkOrder.Product != null
                    ? x.JobRun.WorkOrder.Product.MaterialCode
                    : "-",
                PlanningQty = x.JobRun.WorkOrder != null
                    ? x.JobRun.WorkOrder.TargetQuantity
                    : 0,
                AchieveRate = 0, // ✅ Formula akan diimplementasikan nanti
                RejectionRate = 0, // ✅ Formula akan diimplementasikan nanti
                LoadingTime = (x.ProductionCount.Timestamp - x.JobRun.StartTime)
            })
            .ToList();

        // Populate Form Data
        vm.ManPowerList = await _context.ManPowers.Where(m => m.IsActive).OrderBy(m => m.Value).ToListAsync();
        vm.ActiveManPowerId = activeJob?.ManPowerId;
        vm.ActiveInjection = activeJob?.InjectionGroup ?? "MERAH";

        // Chart Data
        vm.ChartData = new ChartDataViewModel
        {
            RunTimeMinutes = metrics.OperatingTimeSeconds / 60.0,
            IdleTimeMinutes = (metrics.DowntimeSeconds + metrics.RestBreakTimeSeconds) / 60.0,
            OffTimeMinutes = (metrics.PlannedProductionTimeSeconds - metrics.OperatingTimeSeconds - metrics.DowntimeSeconds) / 60.0,
            
            Oee = metrics.Oee,
            Availability = metrics.Availability,
            Performance = metrics.Performance,
            Quality = metrics.Quality,
            
            WeeklyTrend = new List<WeeklyTrendData>() 
        };

        // Action Buttons Data
        var plannedRests = await _context.DowntimeReasons
            .Where(r => r.Category == "Planned")
            .ToListAsync();
        vm.RestReason = plannedRests
            .FirstOrDefault(r => r.Description.Contains("Rest", StringComparison.OrdinalIgnoreCase))
            ?? plannedRests.FirstOrDefault();
        
        vm.LineStopReasons = await _context.DowntimeReasons
            .Where(r => r.Category == "Unplanned")
            .OrderBy(r => r.Description)
            .ToListAsync();
        
        // NgTypes untuk modal Add Quantity
        vm.NgTypes = await _context.NgTypes
            .OrderBy(n => n.Code)
            .ToListAsync();
        
        // ManPowers untuk form
        try
        {
            ViewBag.ManPowers = await _context.ManPowers
                .Where(m => m.IsActive)
                .OrderBy(m => m.Value)
                .ToListAsync();
        }
        catch
        {
            // Jika tabel ManPowers belum ada (migration belum dijalankan), gunakan empty list
            ViewBag.ManPowers = new List<ManPower>();
        }
        
        // ✅ TAMBAHKAN: DowntimeReasons untuk modal Line Stop
        try
        {
            ViewBag.DowntimeReasons = await _context.DowntimeReasons
                .OrderBy(r => r.Category)
                .ThenBy(r => r.Description)
                .ToListAsync();
        }
        catch
        {
            ViewBag.DowntimeReasons = new List<DowntimeReason>();
        }
        
        // ✅ TAMBAHKAN: Kirim shifts untuk filter dropdown
        ViewBag.Shifts = await _context.Shifts
            .OrderBy(s => s.Name)
            .ToListAsync();
        
        // ✅ TAMBAHKAN: Kirim SCW 4M Types & Remarks untuk dropdown
        // ✅ PERBAIKAN: Load data SCW SEBELUM return View() untuk memastikan data tersedia
        Console.WriteLine($"🔍 DEBUG: Loading SCW data for machine {id}...");
        try
        {
            // ✅ PERBAIKAN: Selalu load data SCW, jangan check CanConnectAsync karena sudah connect di awal
            var scw4MTypes = await _context.Scw4MTypes
                .OrderBy(t => t.DisplayOrder)
                .ToListAsync();

            var scwRemarks = await _context.ScwRemarks
                .OrderBy(r => r.Scw4MTypeId)
                .ThenBy(r => r.DisplayOrder)
                .ToListAsync();
            
            ViewBag.Scw4MTypes = scw4MTypes;
            ViewBag.ScwRemarks = scwRemarks;
            
            // ✅ TAMBAHKAN: Log untuk debugging
            Console.WriteLine($"✅ SCW Data Loaded Successfully:");
            Console.WriteLine($"   - Scw4MTypes: {scw4MTypes.Count} items");
            Console.WriteLine($"   - ScwRemarks: {scwRemarks.Count} items");
            
            if (scw4MTypes.Count > 0)
            {
                Console.WriteLine($"   - First 4M Type: {scw4MTypes[0].Name} (ID: {scw4MTypes[0].Id})");
            }
            if (scwRemarks.Count > 0)
            {
                Console.WriteLine($"   - First Remark: {scwRemarks[0].Description} (Parent ID: {scwRemarks[0].Scw4MTypeId})");
            }
        }
        catch (Exception ex)
        {
            // Jika tabel Scw4MTypes/ScwRemarks belum ada atau error, gunakan empty list
            Console.WriteLine($"❌ ERROR loading SCW Data: {ex.Message}");
            Console.WriteLine($"   Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"   StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner Exception: {ex.InnerException.Message}");
            }
            ViewBag.Scw4MTypes = new List<Scw4MType>();
            ViewBag.ScwRemarks = new List<ScwRemark>();
        }
        
        // Status untuk action buttons
        vm.HasActiveJob = activeJob != null;
        vm.HasActiveDowntime = hasOpenDowntime;
        // openDowntimeForStatus declared above
        vm.ActiveDowntimeDescription = openDowntimeForStatus?.Reason?.Description;
        vm.MachineStatus = machine.Status; // Status dari Admin (Aktif/TidakAktif)



        // Weekly Trend Data (7 hari terakhir) tetap disajikan per hari untuk konteks historis
        var startOfWeek = now.Date.AddDays(-6);
        var weeklyTrend = new List<WeeklyTrendData>();

        for (int i = 0; i < 7; i++)
        {
            var date = startOfWeek.AddDays(i);
            var dayStart = date;
            var dayEnd = date.AddDays(1);

            var dayJobRuns = (machine.JobRuns ?? Enumerable.Empty<JobRun>())
                .Where(j => j.StartTime < dayEnd && (j.EndTime ?? effectiveNow) > dayStart)
                .ToList();

            double dayRunTime = 0;
            double dayDowntime = 0;

            foreach (var jr in dayJobRuns)
            {
                var jrEnd = jr.EndTime ?? (jr.StartTime < dayEnd ? effectiveNow : dayEnd);
                dayRunTime += GetOverlap(jr.StartTime, jrEnd, dayStart, dayEnd).TotalMinutes;

                var jrDowntimeMinutes = jr.DowntimeEvents.Sum(d =>
                {
                    var dEnd = d.EndTime ?? (d.StartTime < dayEnd ? effectiveNow : dayEnd);
                    return GetOverlap(d.StartTime, dEnd, dayStart, dayEnd).TotalMinutes;
                });

                dayDowntime += jrDowntimeMinutes;
            }

            var dayPlannedMinutes = dayRunTime + dayDowntime;
            var dayIdleTime = 0;
            var dayOffTime = dayDowntime;

            weeklyTrend.Add(new WeeklyTrendData
            {
                DateLabel = date.ToString("MMM dd"),
                RunTimeMinutes = dayRunTime,
                IdleTimeMinutes = dayIdleTime,
                OffTimeMinutes = dayOffTime
            });
        }

        vm.ChartData.WeeklyTrend = weeklyTrend;

        return View(vm);
    }

    // API endpoint untuk mendapatkan data real-time time metrics
    [HttpGet]
    public async Task<IActionResult> GetTimeMetrics(string id, int? shiftId = null, DateTime? shiftDate = null, string? shiftCode = null)
    {
        try
        {
            var result = await _oeeService.GetTimeMetricsAsync(id, shiftId, shiftDate, shiftCode);
            return Json(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetTimeMetrics: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
    }
}

