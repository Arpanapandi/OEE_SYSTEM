using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OeeSystem.Data;
using OeeSystem.Models;
using OeeSystem.Models.ViewModels;
using OeeSystem.Hubs;
using OeeSystem.Services;

namespace OeeSystem.Controllers;

public class OperatorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<OeeHub> _hubContext;
    private readonly IOeeService _oeeService;

    public OperatorController(ApplicationDbContext context, IHubContext<OeeHub> hubContext, IOeeService oeeService)
    {
        _context = context;
        _hubContext = hubContext;
        _oeeService = oeeService;
    }

    public async Task<IActionResult> Index(string machineId)
    {
        var now = DateTime.Now;

        // ✅ PERBAIKAN: Gunakan AsNoTracking() untuk memastikan data fresh dari database
        var machine = await _context.Machines
            .AsNoTracking()
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.WorkOrder)
                    .ThenInclude(w => w!.Product)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.Operator)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.DowntimeEvents)
                    .ThenInclude(d => d.Reason)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.ProductionCounts)
            .FirstOrDefaultAsync(m => m.Id == machineId);

        if (machine == null)
        {
            return NotFound();
        }

        var activeJob = machine.JobRuns
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefault(j => j.EndTime == null);

        var allCounts = activeJob?.ProductionCounts ?? Array.Empty<ProductionCount>();

        var openDowntime = activeJob?.DowntimeEvents
            .OrderByDescending(d => d.StartTime)
            .FirstOrDefault(d => d.EndTime == null);

        DateTime lastChangeTime = activeJob?.StartTime ?? machine.JobRuns
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefault()?.StartTime ?? now;

        if (openDowntime != null)
        {
            lastChangeTime = openDowntime.StartTime;
        }

        var plannedRests = await _context.DowntimeReasons
            .Where(r => r.Category == "Planned")
            .ToListAsync();

        var restReason = plannedRests
            .FirstOrDefault(r => r.Description.Contains("Rest", StringComparison.OrdinalIgnoreCase))
                         ?? plannedRests.FirstOrDefault();

        var lineStops = await _context.DowntimeReasons
            .Where(r => r.Category == "Unplanned")
            .OrderBy(r => r.Description)
            .ToListAsync();

        var ngTypes = await _context.NgTypes
            .OrderBy(n => n.Code)
            .ToListAsync();

        var vm = new OperatorViewModel
        {
            MachineId = machine.Id,
            MachineName = machine.Name,
            OperatorName = activeJob?.Operator?.Username,
            ProductName = activeJob?.WorkOrder?.Product?.Name,
            ProductImageUrl = activeJob?.WorkOrder?.Product?.ImageUrl,
            WorkOrderNumber = activeJob?.WorkOrder?.OrderNumber,
            TargetQuantity = activeJob?.WorkOrder?.TargetQuantity ?? 0,
            TotalGood = allCounts.Sum(c => c.GoodCount),
            TotalReject = allCounts.Sum(c => c.RejectCount),
            HasActiveJob = activeJob != null,
            HasActiveDowntime = openDowntime != null,
            ActiveDowntimeDescription = openDowntime?.Reason?.Description,
            SinceLastChange = now - lastChangeTime,
            MachineStatus = machine.Status, // Status dari Admin (Aktif/Tidak Aktif)
            LineStopReasons = lineStops,
            RestReason = restReason,
            NgTypes = ngTypes
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string machineId, int? manPowerId = null, string? returnUrl = null)
    {
        var now = DateTime.Now;
        var nowUtc = DateTime.UtcNow;
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // 1. Check for Active Job
        var activeJob = await _context.JobRuns
            .Include(j => j.DowntimeEvents)
            .Include(j => j.Machine)
            .Where(j => j.MachineId == machineId && j.EndTime == null)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync();

        // 2. If Job exists, ensure it's within the current shift, else auto-close
        var metrics = await _oeeService.GetTimeMetricsAsync(machineId);
        if (activeJob != null && activeJob.StartTime < metrics.ShiftStart)
        {
            activeJob.EndTime = metrics.ShiftStart.AddSeconds(-1); // Close at previous shift end
            // Close any open downtime too
            var openDt = activeJob.DowntimeEvents.FirstOrDefault(d => d.EndTime == null);
            if (openDt != null) {
                openDt.EndTime = activeJob.EndTime;
                openDt.DurationSeconds = (int)(openDt.EndTime.Value - openDt.StartTime).TotalSeconds;
            }
            await _context.SaveChangesAsync();
            activeJob = null; // Forces new job start
        }

        if (activeJob != null)
        {
            // Resume/Switch to Running: Close any open downtime
            var openDowntime = activeJob.DowntimeEvents.FirstOrDefault(d => d.EndTime == null);
            if (openDowntime != null)
            {
                openDowntime.EndTime = now;
                openDowntime.DurationSeconds = (int)(now - openDowntime.StartTime).TotalSeconds;
                activeJob.LastStatusChangeTime = now;
            }

            if (manPowerId.HasValue && manPowerId.Value > 0)
                activeJob.ManPowerId = manPowerId.Value;

            await _context.SaveChangesAsync();
        }
        else
        {
            // Start NEW Job
            var activeWorkOrder = await _context.WorkOrders
                .Where(w => w.Status == WorkOrderStatus.InProgress)
                .FirstOrDefaultAsync();

            if (activeWorkOrder == null)
            {
                return isAjax ? Json(new { success = false, message = "No active Work Order found." }) : (IActionResult)BadRequest("No active Work Order.");
            }

            var machine = await _context.Machines.FindAsync(machineId);
            if (machine != null) machine.Status = MachineStatus.Aktif;

            activeJob = new JobRun
            {
                MachineId = machineId,
                WorkOrderId = activeWorkOrder.Id,
                StartTime = now,
                LastStatusChangeTime = now
            };
            _context.JobRuns.Add(activeJob);
            await _context.SaveChangesAsync();
        }

        // Broadcast
        await _hubContext.Clients.Group($"machine_{machineId}").SendAsync("RunningStarted", machineId, nowUtc.ToString("O"));
        await _hubContext.Clients.All.SendAsync("OeeUpdated", new { Type = "RunningStarted", MachineId = machineId, Timestamp = now });

        return isAjax ? Json(new { success = true, lastStatusChangeTime = now.ToString("O") }) : (IActionResult)RedirectToAction(nameof(Index), new { machineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rest(string machineId, int reasonId, string? returnUrl = null)
    {
        return await StartDowntime(machineId, reasonId, returnUrl, isRestBreak: true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LineStop(string machineId, int reasonId, string? returnUrl = null)
    {
        return await StartDowntime(machineId, reasonId, returnUrl, isLineStop: true);
    }

    private async Task<IActionResult> StartDowntime(string machineId, int reasonId, string? returnUrl = null, bool isRestBreak = false, bool isLineStop = false, bool isNoLoading = false)
    {
        var now = DateTime.Now;
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // 1. Check for Active Job
        var metrics = await _oeeService.GetTimeMetricsAsync(machineId);
        var activeJob = await _context.JobRuns
            .Include(j => j.DowntimeEvents)
            .Where(j => j.MachineId == machineId && j.EndTime == null)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync();

        if (activeJob == null || activeJob.StartTime < metrics.ShiftStart)
        {
            // If job at previous shift exists, it should have been closed.
            // But if it hasn't, we should start a new one or error out.
             return isAjax ? Json(new { success = false, message = "Please start RUNNING first." }) : (IActionResult)BadRequest("No active job.");
        }

        // 2. Transisi: Tutup Downtime yang sedang terbuka jika ada
        var openDt = activeJob.DowntimeEvents.FirstOrDefault(d => d.EndTime == null);
        if (openDt != null)
        {
            openDt.EndTime = now;
            openDt.DurationSeconds = (int)(now - openDt.StartTime).TotalSeconds;
        }

        // 3. Create NEW Downtime Event
        var reason = await _context.DowntimeReasons.FindAsync(reasonId);
        var newDowntime = new DowntimeEvent
        {
            JobRunId = activeJob.Id,
            ReasonId = reasonId,
            StartTime = now,
            IsRestBreak = isRestBreak,
            IsLineStop = isLineStop,
            IsNoLoading = isNoLoading
        };
        _context.DowntimeEvents.Add(newDowntime);
        activeJob.LastStatusChangeTime = now;
        await _context.SaveChangesAsync();

        // Broadcast
        await _hubContext.Clients.All.SendAsync("OeeUpdated", new { 
            Type = "DowntimeStarted", 
            MachineId = machineId, 
            Description = reason?.Description ?? (isNoLoading ? "No Loading" : "Downtime") 
        });

        return isAjax ? Json(new { success = true, lastStatusChangeTime = now.ToString("O"), downtimeDescription = reason?.Description }) : (IActionResult)RedirectToAction(nameof(Index), new { machineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoLoading(string machineId, string? returnUrl = null)
    {
        var reason = await _context.DowntimeReasons.FirstOrDefaultAsync(r => r.Description == "No Loading")
                     ?? await _context.DowntimeReasons.FirstOrDefaultAsync(r => r.Category == "Planned" && r.Description.Contains("Loading"))
                     ?? await _context.DowntimeReasons.FirstOrDefaultAsync();

        return await StartDowntime(machineId, reason?.Id ?? 0, returnUrl, isNoLoading: true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuantity(
        string machineId,
        int goodQty,
        int rejectQty,
        string? rejectReason,
        int? ngTypeId = null,
        string? injection = null,
        int? manPowerId = null,
        string? returnUrl = null)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Query["ajax"] == "1";
        var now = DateTime.Now;

        if (goodQty < 0 || rejectQty < 0)
        {
            if (isAjax) return Json(new { success = false, message = "Quantity tidak boleh negatif" });
            TempData["OperationError"] = "Quantity tidak boleh negatif";
            if (!string.IsNullOrEmpty(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }

        if (goodQty == 0 && rejectQty == 0)
        {
            if (isAjax) return Json(new { success = false, message = "Minimal harus ada input quantity" });
            TempData["OperationError"] = "Minimal harus ada input quantity (good atau reject)";
            if (!string.IsNullOrEmpty(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }

        var job = await _context.JobRuns
            .Include(j => j.Machine)
            .Include(j => j.WorkOrder)
                .ThenInclude(w => w!.Product)
            .Where(j => j.MachineId == machineId)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync(j => j.EndTime == null);

        if (job == null)
        {
            if (isAjax) return Json(new { success = false, message = "Tidak ada job run aktif untuk mesin ini" });
            TempData["OperationError"] = "Tidak ada job run aktif untuk mesin ini";
            if (!string.IsNullOrEmpty(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }

        if (goodQty > 0 || rejectQty > 0)
        {
            var count = new ProductionCount
            {
                JobRunId = job.Id,
                Timestamp = now,
                GoodCount = goodQty,
                RejectCount = rejectQty,
                RejectReason = string.IsNullOrWhiteSpace(rejectReason) ? null : rejectReason,
                NgTypeId = ngTypeId,
                InjectionGroup = string.IsNullOrWhiteSpace(injection) ? null : injection.Trim().ToLower(),
                ManPowerId = manPowerId
            };

            _context.ProductionCounts.Add(count);
            await _context.SaveChangesAsync();

            // Broadcast SignalR update
            await _hubContext.Clients.All.SendAsync("OeeUpdated", new
            {
                Type = "ProductionCountAdded",
                MachineId = machineId,
                MachineName = job.Machine?.Name,
                ProductName = job.WorkOrder?.Product?.Name,
                GoodCount = goodQty,
                RejectCount = rejectQty,
                Message = $"Produksi: +{goodQty} Good, +{rejectQty} Reject pada mesin {job.Machine?.Name}",
                Timestamp = now
            });
        }

        if (isAjax) return Json(new { success = true, message = "Quantity saved successfully" });

        // Jika ada returnUrl (dari OEE Detail), utamakan redirect ke sana
        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);

        // Fallback: kembali ke OEE Detail untuk mesin ini
        return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitProductionData(
        string machineId,
        string? nomorLot,
        string? lotBo,
        string? namaCompound,
        double? beratAct,
        string? penipisan,
        string? keterangan,
        int? manPowerId,
        string? injection,
        int? komponenId,
        int? durasiProduksiSeconds,
        int qty = 1)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var now = DateTime.Now;

        // 1. Validasi Material Code (Lot BO)
        var product = await _context.Products.FirstOrDefaultAsync(p => p.MaterialCode == lotBo || p.Name == lotBo);
        if (product == null && !string.IsNullOrEmpty(lotBo))
        {
            return isAjax ? Json(new { success = false, message = "Material Code (Lot BO) tidak terdaftar di sistem." }) : (IActionResult)BadRequest("Invalid Material.");
        }

        var activeJob = await _context.JobRuns
            .Include(j => j.Machine)
            .Where(j => j.MachineId == machineId && j.EndTime == null)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync();

        if (activeJob == null)
        {
             return isAjax ? Json(new { success = false, message = "No active job." }) : (IActionResult)BadRequest("No job.");
        }

        var count = new ProductionCount
        {
            JobRunId = activeJob.Id,
            Timestamp = now,
            GoodCount = qty,
            NomorLot = nomorLot,
            LotBo = lotBo,
            NamaCompound = namaCompound,
            BeratAct = beratAct,
            Penipisan = penipisan,
            Keterangan = keterangan,
            ManPowerId = manPowerId,
            InjectionGroup = injection?.Trim().ToLower(),
            ComponentId = komponenId,
            DurasiProduksiSeconds = durasiProduksiSeconds
        };

        _context.ProductionCounts.Add(count);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("OeeUpdated", new { Type = "ProductionDataSubmitted", MachineId = machineId, Timestamp = now });

        return isAjax ? Json(new { success = true }) : (IActionResult)RedirectToAction("OeeDetail", "Machine", new { id = machineId });
    }

    private static TimeSpan GetOverlap(DateTime start, DateTime end, DateTime windowStart, DateTime windowEnd)
    {
        var overlapStart = start < windowStart ? windowStart : start;
        var overlapEnd = end > windowEnd ? windowEnd : end;
        return overlapEnd > overlapStart ? overlapEnd - overlapStart : TimeSpan.Zero;
    }

    private static (DateTime Start, DateTime End, DateTime ShiftDate, string Code, string Key) ResolveShiftWindow(DateTime now, DateTime? shiftDate, string? shiftCode)
    {
        // ✅ LOGIKA ROLLING SHIFT (UNTUK TESTING KAPAN SAJA):
        // Setiap 1 jam dibagi 6 blok (10 menit per blok)
        int blockIndex = now.Minute / 10;
        bool isEvenBlock = blockIndex % 2 == 0;
        
        string rollingShiftCode = isEvenBlock ? "A" : "B";
        DateTime shiftStart = now.Date.AddHours(now.Hour).AddMinutes(blockIndex * 10);
        DateTime shiftEnd = shiftStart.AddMinutes(10);
        DateTime baseDate = now.Date;
        
        var key = $"{baseDate:yyyy-MM-dd}|{rollingShiftCode}";
        return (shiftStart, shiftEnd, baseDate, rollingShiftCode, key);
    }

    [HttpGet]
    public async Task<IActionResult> GetOperatorData(string machineId)
    {
        if (string.IsNullOrEmpty(machineId)) return Json(new { error = "machineId is required" });

        var metrics = await _oeeService.GetTimeMetricsAsync(machineId);

        // Fetch additional data not covered by metrics service if needed
        var machine = await _context.Machines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == machineId);
        var activeJob = await _context.JobRuns.AsNoTracking()
            .Include(j => j.WorkOrder).ThenInclude(w => w!.Product)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync(j => j.MachineId == machineId && (j.EndTime == null || j.EndTime > metrics.ShiftStart));

        return Json(new
        {
            // Production data from metrics
            TotalGood = metrics.GoodCount,
            TotalReject = metrics.RejectCount,
            TotalCount = metrics.TotalCount,
            TargetQuantity = activeJob?.WorkOrder?.TargetQuantity ?? 0,
            ProgressPercent = activeJob?.WorkOrder?.TargetQuantity > 0 ? Math.Round((double)metrics.GoodCount / activeJob.WorkOrder.TargetQuantity * 100, 1) : 0,

            // Status & Timing from metrics
            SinceLastChangeSeconds = metrics.SinceLastChangeSeconds,
            SinceLastChange = TimeSpan.FromSeconds(metrics.SinceLastChangeSeconds ?? 0).ToString(@"hh\:mm\:ss"),
            LastStatusChangeTime = metrics.LastStatusChangeTime,

            // Job & Machine Info
            HasActiveJob = metrics.HasActiveJob,
            MachineStatus = metrics.MachineStatus,
            Status = metrics.MachineStatus == "Aktif" ? "AKTIF" : "TIDAK AKTIF",
            ProductName = activeJob?.WorkOrder?.Product?.Name,
            WorkOrderNumber = activeJob?.WorkOrder?.OrderNumber,
            
            // Availability metrics
            PlannedProductionTimeSeconds = metrics.PlannedProductionTimeSeconds,
            OperatingTimeSeconds = metrics.OperatingTimeSeconds,
            DowntimeTotalSeconds = metrics.DowntimeTotalSeconds,
            
            // Flags
            HasActiveDowntime = metrics.HasActiveDowntime,
            IsNoLoading = metrics.IsNoLoading,
            IsIdle = metrics.IsIdle
        });
    }

    // ✅ EVENT-DRIVEN: Start Dandori (saat operator mulai persiapan)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartDandori(string machineId, string? returnUrl = null)
    {
        // ✅ EVENT-DRIVEN: Gunakan UTC untuk konsistensi
        var nowUtc = DateTime.UtcNow;
        var nowLocal = DateTime.Now; // Untuk display/logging

        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        try
        {
            var activeJob = await _context.JobRuns
                .Where(j => j.MachineId == machineId)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefaultAsync(j => j.EndTime == null);

            if (activeJob == null)
            {
                var activeWorkOrder = await _context.WorkOrders
                    .Where(w => w.Status == WorkOrderStatus.InProgress)
                    .FirstOrDefaultAsync();

                if (activeWorkOrder == null)
                {
                    if (isAjax)
                    {
                        return Json(new { success = false, message = "Tidak ada Work Order aktif. Silakan buat Work Order terlebih dahulu." });
                    }
                    TempData["OperationError"] = "Tidak ada Work Order aktif. Silakan buat Work Order terlebih dahulu.";
                    if (!string.IsNullOrEmpty(returnUrl))
                        return Redirect(returnUrl);
                    return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
                }

                var operatorUser = await _context.Users
                    .Where(u => u.Role == UserRole.Operator)
                    .FirstOrDefaultAsync();

                activeJob = new JobRun
                {
                    MachineId = machineId,
                    WorkOrderId = activeWorkOrder.Id,
                    OperatorId = operatorUser?.Id ?? 0,
                    StartTime = nowLocal,
                    EndTime = null,
                    DandoriStartTime = nowLocal, // Simpan local time untuk display
                    DandoriEndTime = null
                };

                _context.JobRuns.Add(activeJob);
            }
            else
            {
                if (activeJob.DandoriStartTime.HasValue && !activeJob.DandoriEndTime.HasValue)
                {
                    if (isAjax)
                    {
                        return Json(new { success = false, message = "Dandori sudah berjalan. Silakan stop Dandori terlebih dahulu." });
                    }
                    TempData["OperationError"] = "Dandori sudah berjalan. Silakan stop Dandori terlebih dahulu.";
                    if (!string.IsNullOrEmpty(returnUrl))
                        return Redirect(returnUrl);
                    return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
                }

                activeJob.DandoriStartTime = nowLocal;
                activeJob.DandoriEndTime = null;
            }

            await _context.SaveChangesAsync();

            // ✅ EVENT-DRIVEN: Broadcast DandoriStarted event dengan UTC timestamp (ISO 8601 string)
            var machineIdInt = int.TryParse(machineId, out var id) ? id : 0;
            if (machineIdInt > 0)
            {
                // Convert to ISO 8601 string untuk konsistensi dengan JavaScript Date parsing
                var startTimeUtcString = nowUtc.ToString("O"); // ISO 8601 format
                await _hubContext.Clients.Group($"machine_{machineIdInt}")
                    .SendAsync("DandoriStarted", machineIdInt, startTimeUtcString);
                Console.WriteLine($"📡 Broadcasted DandoriStarted: machine_{machineIdInt}, startTimeUtc: {startTimeUtcString}");
            }
            
            await _hubContext.Clients.All.SendAsync("OeeUpdated", new
            {
                Type = "DandoriStarted",
                MachineId = machineId,
                Message = "Dandori telah dimulai",
                Timestamp = nowLocal,
                RefreshTimeMetrics = true
            });

            if (isAjax)
            {
                // ✅ PERBAIKAN: Return startTimeUtc dalam response untuk client langsung start timer
                var startTimeUtcString = nowUtc.ToString("O"); // ISO 8601 format
                return Json(new { 
                    success = true, 
                    message = "Dandori started successfully",
                    startTimeUtc = startTimeUtcString
                });
            }

            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error saat Start Dandori: {ex.Message}");
            Console.WriteLine($"ERROR: Stack trace: {ex.StackTrace}");
            
            if (isAjax)
            {
                return Json(new { success = false, message = $"Error saat start Dandori: {ex.Message}" });
            }
            
            TempData["OperationError"] = $"Error saat start Dandori: {ex.Message}";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }
    }

    // ✅ EVENT-DRIVEN: Stop Dandori (saat operator mulai produksi)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StopDandori(string machineId, string? returnUrl = null)
    {
        // ✅ EVENT-DRIVEN: Gunakan UTC untuk konsistensi
        var nowUtc = DateTime.UtcNow;
        var nowLocal = DateTime.Now; // Untuk display/logging

        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        try
        {
            var activeJob = await _context.JobRuns
                .Where(j => j.MachineId == machineId)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefaultAsync(j => j.EndTime == null);

            if (activeJob == null)
            {
                if (isAjax)
                {
                    return Json(new { success = false, message = "Tidak ada job aktif. Tidak bisa stop Dandori." });
                }
                TempData["OperationError"] = "Tidak ada job aktif. Tidak bisa stop Dandori.";
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
            }

            if (!activeJob.DandoriStartTime.HasValue || activeJob.DandoriEndTime.HasValue)
            {
                if (isAjax)
                {
                    return Json(new { success = false, message = "Dandori tidak sedang berjalan." });
                }
                TempData["OperationError"] = "Dandori tidak sedang berjalan.";
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
            }

            // ✅ EVENT-DRIVEN: Hitung durasi 1x saja saat STOP
            activeJob.DandoriEndTime = nowLocal;
            var dandoriDuration = (int)(nowLocal - activeJob.DandoriStartTime.Value).TotalSeconds;
            
            if (dandoriDuration < 0)
            {
                Console.WriteLine($"WARNING: Negative dandori duration detected: {dandoriDuration} seconds. Setting to 0.");
                dandoriDuration = 0;
            }
            
            activeJob.DandoriDurationSeconds = (activeJob.DandoriDurationSeconds ?? 0) + dandoriDuration;

            await _context.SaveChangesAsync();

            // ✅ EVENT-DRIVEN: Broadcast DandoriStopped event dengan UTC timestamp dan total seconds
            var machineIdInt = int.TryParse(machineId, out var id) ? id : 0;
            if (machineIdInt > 0)
            {
                // Convert to ISO 8601 string untuk konsistensi dengan JavaScript Date parsing
                var endTimeUtcString = nowUtc.ToString("O"); // ISO 8601 format
                await _hubContext.Clients.Group($"machine_{machineIdInt}")
                    .SendAsync("DandoriStopped", machineIdInt, endTimeUtcString, activeJob.DandoriDurationSeconds ?? 0);
                Console.WriteLine($"📡 Broadcasted DandoriStopped: machine_{machineIdInt}, endTimeUtc: {endTimeUtcString}, totalSeconds: {activeJob.DandoriDurationSeconds ?? 0}");
            }
            
            await _hubContext.Clients.All.SendAsync("OeeUpdated", new
            {
                Type = "DandoriStopped",
                MachineId = machineId,
                Message = $"Dandori telah di-stop (durasi: {TimeSpan.FromSeconds(dandoriDuration):hh\\:mm\\:ss})",
                Timestamp = nowLocal,
                RefreshTimeMetrics = true
            });

            if (isAjax)
            {
                return Json(new { success = true, message = "Dandori stopped successfully", duration = activeJob.DandoriDurationSeconds ?? 0 });
            }

            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error saat Stop Dandori: {ex.Message}");
            Console.WriteLine($"ERROR: Stack trace: {ex.StackTrace}");
            
            if (isAjax)
            {
                return Json(new { success = false, message = $"Error saat stop Dandori: {ex.Message}" });
            }
            
            TempData["OperationError"] = $"Error saat stop Dandori: {ex.Message}";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }
    }

    // ========== SCW (Stop Call Waiting) ENDPOINTS ==========
    
    // GET: Get SCW Remarks berdasarkan 4M Type
    [HttpGet]
    [Route("/api/Operator/GetScwRemarks")]
    public async Task<IActionResult> GetScwRemarks(int scw4MTypeId)
    {
        Console.WriteLine($"[API] GetScwRemarks called for TypeId: {scw4MTypeId}");
        try
        {
            var remarks = await _context.ScwRemarks
                .Where(r => r.Scw4MTypeId == scw4MTypeId)
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.Description)
                .Select(r => new
                {
                    id = r.Id,
                    description = r.Description
                })
                .ToListAsync();
            
            Console.WriteLine($"[API] Found {remarks.Count} remarks for TypeId: {scw4MTypeId}");
            return Json(new { success = true, remarks = remarks });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] ERROR in GetScwRemarks: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: Start SCW
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scw(string machineId, int jenis4MId, int jenisRemarkId, string? additionalNotes = null, string? returnUrl = null)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var now = DateTime.Now;
        
        try
        {
            // Validasi
            var scw4MType = await _context.Scw4MTypes.FindAsync(jenis4MId);
            var scwRemark = await _context.ScwRemarks.FindAsync(jenisRemarkId);
            
            if (scw4MType == null || scwRemark == null)
            {
                if (isAjax)
                    return Json(new { success = false, message = "Jenis 4M atau Remark tidak ditemukan" });
                TempData["OperationError"] = "Jenis 4M atau Remark tidak ditemukan";
                return RedirectToAction(nameof(Index), new { machineId });
            }
            
            // Validasi: Remark harus sesuai dengan 4M Type
            if (scwRemark.Scw4MTypeId != jenis4MId)
            {
                if (isAjax)
                    return Json(new { success = false, message = "Remark tidak sesuai dengan kategori 4M yang dipilih" });
                TempData["OperationError"] = "Remark tidak sesuai dengan kategori 4M yang dipilih";
                return RedirectToAction(nameof(Index), new { machineId });
            }
            
            // Cari active job
            var activeJob = await _context.JobRuns
                .Where(j => j.MachineId == machineId && j.EndTime == null)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefaultAsync();
            
            if (activeJob == null)
            {
                if (isAjax)
                    return Json(new { success = false, message = "Tidak ada job aktif" });
                TempData["OperationError"] = "Tidak ada job aktif";
                return RedirectToAction(nameof(Index), new { machineId });
            }
            
            // Buat SCW Event
            var scwEvent = new ScwEvent
            {
                JobRunId = activeJob.Id,
                Jenis4MId = jenis4MId,
                JenisRemarkId = jenisRemarkId,
                MachineId = machineId,
                StartTime = now,
                EndTime = null,
                DurationSeconds = 0,
                AdditionalNotes = additionalNotes
            };
            
            _context.ScwEvents.Add(scwEvent);
            await _context.SaveChangesAsync();
            
            // Broadcast event
            await _hubContext.Clients.All.SendAsync("OeeUpdated", new
            {
                Type = "ScwStarted",
                MachineId = machineId,
                Scw4MType = scw4MType.Name,
                ScwRemark = scwRemark.Description,
                Message = $"SCW: {scw4MType.Name} - {scwRemark.Description}",
                Timestamp = now
            });
            
            if (isAjax)
                return Json(new { success = true, message = $"SCW: {scw4MType.Name} - {scwRemark.Description} dimulai" });
            
            TempData["OperationSuccess"] = $"SCW: {scw4MType.Name} - {scwRemark.Description} dimulai";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }
        catch (Exception ex)
        {
            if (isAjax)
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            TempData["OperationError"] = $"Error: {ex.Message}";
            return RedirectToAction(nameof(Index), new { machineId });
        }
    }
}

