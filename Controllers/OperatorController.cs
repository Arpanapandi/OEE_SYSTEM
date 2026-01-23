using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OeeSystem.Data;
using OeeSystem.Models;
using OeeSystem.Models.ViewModels;
using OeeSystem.Hubs;

namespace OeeSystem.Controllers;

public class OperatorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<OeeHub> _hubContext;
    private readonly Services.IOeeService _oeeService;
    private readonly Services.ProductionReporterService _reporterService;

    public OperatorController(ApplicationDbContext context, IHubContext<OeeHub> hubContext, Services.IOeeService oeeService, Services.ProductionReporterService reporterService)
    {
        _context = context;
        _hubContext = hubContext;
        _oeeService = oeeService;
        _reporterService = reporterService;
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
            // .Where(j => j.StartTime <= now)
            .OrderByDescending(j => j.StartTime)
            .ThenByDescending(j => j.Id)
            .FirstOrDefault(j => j.EndTime == null);

        var allCounts = activeJob?.ProductionCounts ?? Array.Empty<ProductionCount>();

        var openDowntime = activeJob?.DowntimeEvents
            .OrderByDescending(d => d.StartTime)
            .FirstOrDefault(d => d.EndTime == null);

        // ✅ PERBAIKAN: Gunakan logic terpusat dari OeeService dengan WINDOW SHIFT
        // Ini memastikan tampilan awal operator view sinkron dengan dashboard dan behavior tombol Start/Resume
        var shiftWindow = await _oeeService.GetCurrentShiftWindowAsync();
        
        TimeSpan sinceLastChange = TimeSpan.Zero;
        if (activeJob != null)
        {
            var durationMetrics = _oeeService.CalculateJobDuration(activeJob, now, shiftWindow.Start, shiftWindow.End);
            if (durationMetrics.IsRunning)
            {
                sinceLastChange = durationMetrics.OperatingTime;
            }
            else
            {
                sinceLastChange = durationMetrics.CurrentDowntime;
            }
        }
        else if (openDowntime != null)
        {
             // Fallback logic if activeJob is null but somehow we have openDowntime (unlikely given logic above)
             sinceLastChange = now - openDowntime.StartTime;
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
            SinceLastChange = sinceLastChange,
            MachineStatus = machine.Status, // Status dari Admin (Aktif/Tidak Aktif)
            
            
            // ✅ STRICT STATE LOGIC:
            // 1. If Job == null -> STOPPED
            // 2. If Open Downtime exists -> Check Reason (Rest/NoLoading/LineStop)
            // 3. If No Open Downtime -> RUNNING
            CurrentState = activeJob == null ? "STOPPED" : 
                           (openDowntime != null ? 
                                (openDowntime.IsRestBreak ? "REST_BREAK" : 
                                 openDowntime.IsNoLoading ? "NO_LOADING" : "LINE_STOP") 
                           : "RUNNING"),
                           
            LastStatusChangeTime = activeJob?.LastStatusChangeTime, // ✅ NEW
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
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ✅ STRICT POLICY: Ensure ONLY one active JobRun exists for this machine
        await _oeeService.EnsureOnlyOneActiveJobAsync(machineId);

        // ✅ CHECK IDEMPOTENCY: Is machine already RUNNING?
        // Reuse the same logic to determine current state
        var activeJob = await _context.JobRuns
             .Include(j => j.DowntimeEvents)
             .ThenInclude(d => d.Reason)
             .OrderByDescending(j => j.StartTime)
             .FirstOrDefaultAsync(j => j.MachineId == machineId && j.EndTime == null);

        if (activeJob != null)
        {
            var openDowntime = activeJob.DowntimeEvents.FirstOrDefault(d => d.EndTime == null);
            if (openDowntime == null)
            {
                // Machine is ALREADY in RUNNING state (Active Job + No Open Downtime)
                // Return success immediately to prevent Timer Reset
                 if (isAjax)
                 {
                     return Json(new
                     {
                         success = true,
                         message = "Machine is already running",
                         lastStatusChangeTime = activeJob.LastStatusChangeTime?.ToString("O"), // Use existing time
                         machineStatus = "Aktif",
                         currentState = "RUNNING"
                     });
                 }
                 return RedirectToAction(nameof(Index), new { machineId });
            }
        }

        // ✅ CORE LOGIC: Use centralized state management
        var result = await ChangeMachineState(machineId, "RUNNING", null, manPowerId);

        if (!result.Success)
        {
            if (isAjax)
            {
                return Json(new { success = false, message = result.Message });
            }
            TempData["OperationError"] = result.Message;
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        // ✅ SUCCESS RESPONSE
        if (isAjax)
        {
            return Json(new
            {
                success = true,
                message = "Machine running",
                lastStatusChangeTime = result.LastChangeTime?.ToString("O"),
                machineStatus = "Aktif",
                currentState = "RUNNING"
            });
        }

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index), new { machineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rest(string machineId, int reasonId, string? returnUrl = null)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ✅ CORE LOGIC: Use centralized state management
        var result = await ChangeMachineState(machineId, "REST_BREAK", reasonId);

        if (!result.Success)
        {
            if (isAjax)
                return Json(new { success = false, message = result.Message });
            TempData["OperationError"] = result.Message;
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        if (isAjax)
        {
            return Json(new
            {
                success = true,
                message = "Rest Break started",
                lastStatusChangeTime = result.LastChangeTime?.ToString("O"),
                currentState = "REST_BREAK"
            });
        }

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index), new { machineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LineStop(string machineId, int reasonId, string? returnUrl = null)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ✅ CORE LOGIC: Use centralized state management
        var result = await ChangeMachineState(machineId, "LINE_STOP", reasonId);

        if (!result.Success)
        {
            if (isAjax)
                return Json(new { success = false, message = result.Message });
            TempData["OperationError"] = result.Message;
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        if (isAjax)
        {
            return Json(new
            {
                success = true,
                message = "Line Stop started",
                lastStatusChangeTime = result.LastChangeTime?.ToString("O"),
                currentState = "LINE_STOP"
            });
        }

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index), new { machineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoLoading(string machineId, string? returnUrl = null)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ✅ CORE LOGIC: Use centralized state management
        var result = await ChangeMachineState(machineId, "NO_LOADING");

        if (!result.Success)
        {
            if (isAjax)
                return Json(new { success = false, message = result.Message });
            TempData["OperationError"] = result.Message;
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        if (isAjax)
        {
            return Json(new
            {
                success = true,
                message = "No Loading started",
                lastStatusChangeTime = result.LastChangeTime?.ToString("O"),
                currentState = "NO_LOADING"
            });
        }

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index), new { machineId });
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
                InjectionGroup = string.IsNullOrWhiteSpace(injection) ? null : injection.Trim().ToUpper(),
                ManPowerId = manPowerId
            };

            // ✅ PERSIST to JobRun metadata as well
            if (manPowerId.HasValue && manPowerId.Value > 0) job.ManPowerId = manPowerId;
            if (!string.IsNullOrEmpty(injection)) job.InjectionGroup = injection.Trim().ToUpper();

            _context.ProductionCounts.Add(count);
            await _context.SaveChangesAsync();

            // ✅ EVENT-DRIVEN: Lapor ke Laptop Server Monitoring
            if (goodQty > 0)
            {
                await _reporterService.LaporServer(goodQty);
            }

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
        string? lotBo, // ✅ RENAMED from partNumber
        string? namaCompound,
        double? beratAct,
        string? penipisan,
        string? keterangan,
        int? manPowerId,
        string? injection,
        int? komponenId,
        int? durasiProduksiSeconds,
        int goodQty = 0,
        int rejectQty = 0,
        string? rejectReason = null)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Query["ajax"] == "1";
        var now = DateTime.Now;

        try
        {
            // Validasi Job Aktif
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
                return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
            }

            // Create ProductionCount with details
            var count = new ProductionCount
            {
                JobRunId = job.Id,
                Timestamp = now,
                GoodCount = goodQty, 
                RejectCount = rejectQty,
                RejectReason = rejectReason,
                NomorLot = nomorLot,
                LotBo = lotBo, // ✅ MAP to lotBo parameter
                NamaCompound = namaCompound,
                BeratAct = beratAct,
                Penipisan = penipisan,
                Keterangan = keterangan,
                ManPowerId = manPowerId,
                InjectionGroup = string.IsNullOrWhiteSpace(injection) ? null : injection.Trim().ToUpper(),
                ComponentId = komponenId,
                DurasiProduksiSeconds = durasiProduksiSeconds
            };

            // Persist metadata to JobRun as well
            if (manPowerId.HasValue) job.ManPowerId = manPowerId.Value;
            if (!string.IsNullOrWhiteSpace(injection)) job.InjectionGroup = injection.Trim().ToUpper();

            _context.ProductionCounts.Add(count);
            await _context.SaveChangesAsync();

            // ✅ EVENT-DRIVEN: Lapor ke Laptop Server Monitoring
            if (goodQty > 0)
            {
                await _reporterService.LaporServer(goodQty);
            }

            // Broadcast SignalR update
            await _hubContext.Clients.All.SendAsync("OeeUpdated", new
            {
                Type = "ProductionDataSubmitted",
                MachineId = machineId,
                MachineName = job.Machine?.Name,
                ProductName = job.WorkOrder?.Product?.Name,
                GoodCount = goodQty,
                RejectCount = rejectQty,
                Message = $"Data Produksi: {nomorLot} ({goodQty} Good, {rejectQty} NG)",
                Timestamp = now
            });

            if (isAjax)
            {
                return Json(new { success = true, message = "Data produksi berhasil disimpan" });
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Error submitting production data: {ex.Message}");
             if (isAjax) return Json(new { success = false, message = "Terjadi kesalahan saat menyimpan data: " + ex.Message });
        }

        return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
    }





    [HttpGet]
    public async Task<IActionResult> GetOperatorData(string machineId)
    {
        if (string.IsNullOrEmpty(machineId))
        {
            return Json(new { error = "machineId is required" });
        }
        
        var now = DateTime.Now;
        var metrics = await _oeeService.GetTimeMetricsAsync(machineId);
        
        if (metrics == null || !metrics.HasActiveJob)
        {
            // Jika tidak ada job aktif, coba ambil info dasar mesin dan shift
            var machineOnly = await _context.Machines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == machineId);
            var shiftWindow = await _oeeService.GetCurrentShiftWindowAsync();
            
            return Json(new {
                MachineId = machineId,
                MachineName = machineOnly?.Name,
                HasActiveJob = false,
                TotalGood = 0,
                TotalReject = 0,
                TargetQuantity = 0,
                OeeValue = 0, 
                AvailabilityValue = 0,
                PerformanceValue = 0,
                QualityValue = 0,
                Oee = 0,
                Availability = 0,
                Performance = 0,
                Quality = 0,
                PlannedProductionTime = (shiftWindow.End - shiftWindow.Start).ToString(@"hh\:mm\:ss"),
                PlannedProductionTimeSeconds = (shiftWindow.End - shiftWindow.Start).TotalSeconds,
                OperatingTime = "00:00:00",
                OperatingTimeSeconds = 0,
                Downtime = "00:00:00",
                DowntimeSeconds = 0,
                ShiftEnd = shiftWindow.End.ToString("O"),
                Status = machineOnly?.Status.ToString() == "Aktif" ? "AKTIF" : "TIDAK AKTIF",
                MachineImageUrl = machineOnly?.ImageUrl,
                WorkOrderNumber = "-",
                ProductName = "-",
                IsRunning = false
            });
        }

        // Calculate Progress (UI specific calculation not in result class if needed, but let's just return result)
        // JS now handles data.OEE, data.Availability, data.TotalGood, data.Downtime, etc.
        // All these now map 1:1 with TimeMetricsResult properties.
        
        return Json(metrics);
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

    // ✅ CORE FUNCTION: Centralized State Management
    // Semua action (Start, Rest, LineStop, NoLoading) WAJIB memanggil fungsi ini
    private async Task<(bool Success, string Message, DateTime? LastChangeTime)> ChangeMachineState(
        string machineId, 
        string newState, 
        int? reasonId = null,
        int? manPowerId = null)
    {
        var now = DateTime.Now;

        try
        {
            // 1. Validasi State
            var validStates = new[] { "RUNNING", "REST_BREAK", "LINE_STOP", "NO_LOADING", "STOPPED" };
            if (!validStates.Contains(newState))
            {
                return (false, $"Invalid state: {newState}", null);
            }

            // 2. Cari JobRun Aktif
            var activeJob = await _context.JobRuns
                .Include(j => j.DowntimeEvents)
                .Include(j => j.Machine)
                .Include(j => j.WorkOrder)
                    .ThenInclude(w => w!.Product)
                .Where(j => j.MachineId == machineId && j.EndTime == null && j.StartTime <= now.AddHours(12)) // Filter future jobs
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefaultAsync();

            if (activeJob == null)
            {
                return (false, "Tidak ada job aktif. Silakan start job terlebih dahulu.", null);
            }

            // 3. TUTUP EVENT SEBELUMNYA (Jika ada)
            var openEvent = activeJob.DowntimeEvents
                .OrderByDescending(d => d.StartTime)
                .FirstOrDefault(d => d.EndTime == null);

            if (openEvent != null)
            {
                openEvent.EndTime = now;
                openEvent.DurationSeconds = (now - openEvent.StartTime).TotalSeconds;
                Console.WriteLine($"✅ Closed previous event: {openEvent.Reason?.Description ?? "Unknown"} (Duration: {openEvent.DurationSeconds}s)");
            }

            // 4. BUKA EVENT BARU (Jika bukan RUNNING)
            if (newState != "RUNNING")
            {
                // Validasi reasonId untuk state yang memerlukan reason
                if ((newState == "REST_BREAK" || newState == "LINE_STOP") && !reasonId.HasValue)
                {
                    return (false, $"ReasonId required for {newState}", null);
                }

                // Untuk NO_LOADING, gunakan reason default jika tidak ada
                int effectiveReasonId = reasonId ?? 0;
                if (newState == "NO_LOADING" && !reasonId.HasValue)
                {
                    var noLoadingReason = await _context.DowntimeReasons
                        .FirstOrDefaultAsync(r => r.Description == "No Loading");
                    effectiveReasonId = noLoadingReason?.Id ?? 0;
                }

                var newEvent = new DowntimeEvent
                {
                    JobRunId = activeJob.Id,
                    ReasonId = effectiveReasonId,
                    StartTime = now,
                    EndTime = null,
                    DurationSeconds = 0,
                    IsRestBreak = (newState == "REST_BREAK"),
                    IsNoLoading = (newState == "NO_LOADING"),
                    IsLineStop = (newState == "LINE_STOP")
                };

                _context.DowntimeEvents.Add(newEvent);
                Console.WriteLine($"✅ Created new event: {newState}");
            }

            // 5. UPDATE JOBRUN CORE (CRITICAL!)
            activeJob.CurrentState = newState;
            activeJob.LastStatusChangeTime = now;

            // Update ManPower jika diberikan
            if (manPowerId.HasValue && manPowerId.Value > 0)
            {
                activeJob.ManPowerId = manPowerId.Value;
            }

            // 6. SAVE CHANGES
            await _context.SaveChangesAsync();

            // 7. UPDATE MACHINE STATUS
            var machine = await _context.Machines.FindAsync(machineId);
            if (machine != null && newState == "RUNNING" && machine.Status != MachineStatus.Aktif)
            {
                machine.Status = MachineStatus.Aktif;
                await _context.SaveChangesAsync();
            }

            // 8. BROADCAST SIGNALR
            var reason = openEvent?.Reason?.Description ?? "";
            if (newState != "RUNNING" && reasonId.HasValue)
            {
                var newReason = await _context.DowntimeReasons.FindAsync(reasonId.Value);
                reason = newReason?.Description ?? "";
            }

            await _hubContext.Clients.All.SendAsync("OeeUpdated", new
            {
                Type = $"StateChanged_{newState}",
                MachineId = machineId,
                MachineName = activeJob.Machine?.Name,
                CurrentState = newState,
                Reason = reason,
                Message = $"Machine {activeJob.Machine?.Name}: {newState}",
                Timestamp = now,
                LastStatusChangeTime = now.ToString("O"),
                RefreshTimeMetrics = true,
                RefreshOperatorData = true,
                RefreshRecentDowntime = true,
                RefreshOeeMetrics = true,
                MachineStatus = newState == "RUNNING" ? "Aktif" : "Aktif",
                HasActiveDowntime = newState == "LINE_STOP",
                DowntimeDescription = reason
            });

            Console.WriteLine($"✅ State changed: {machineId} -> {newState} at {now:HH:mm:ss}");
            return (true, $"State changed to {newState}", now);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR in ChangeMachineState: {ex.Message}");
            return (false, $"Error: {ex.Message}", null);
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

