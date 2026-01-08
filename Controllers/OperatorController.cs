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

    public OperatorController(ApplicationDbContext context, IHubContext<OeeHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index(string machineId)
    {
        var now = DateTime.Now;

        // ✅ PERBAIKAN: Gunakan AsNoTracking() untuk memastikan data fresh dari database
        var machine = await _context.Machines
            .AsNoTracking()
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.WorkOrder)
                    .ThenInclude(w => w.Product)
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
    public async Task<IActionResult> Start(string machineId, string? returnUrl = null)
    {
        // ✅ EVENT-DRIVEN: Gunakan UTC untuk konsistensi
        var nowUtc = DateTime.UtcNow;
        var nowLocal = DateTime.Now; // Untuk display/logging

        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // Cek apakah sudah ada job aktif
        var existingJob = await _context.JobRuns
            .Include(j => j.DowntimeEvents)
            .Include(j => j.Machine)
            .Where(j => j.MachineId == machineId)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync(j => j.EndTime == null);

        if (existingJob != null)
        {
            // ✅ PERBAIKAN: Jika ada downtime aktif, end downtime terlebih dahulu
            var openDowntime = existingJob.DowntimeEvents
                .OrderByDescending(d => d.StartTime)
                .FirstOrDefault(d => d.EndTime == null);
                
            if (openDowntime != null)
            {
                // End downtime yang aktif
                openDowntime.EndTime = nowLocal;
                openDowntime.DurationSeconds = (int)(nowLocal - openDowntime.StartTime).TotalSeconds;
                
                // ✅ PERBAIKAN: Update LastStatusChangeTime untuk kalkulasi OEE operating time
                existingJob.LastStatusChangeTime = nowLocal;
                
                await _context.SaveChangesAsync();
                
                // ✅ EVENT-DRIVEN: Broadcast RunningStarted event (downtime ended = running started)
                // ✅ PERBAIKAN: Gunakan waktu downtime end sebagai start time untuk running
                // Ini memastikan durasi running melanjutkan dari waktu rest break selesai, bukan dari job start time
                var machineIdInt = int.TryParse(machineId, out var id) ? id : 0;
                if (machineIdInt > 0)
                {
                    // Convert to ISO 8601 string untuk konsistensi dengan JavaScript Date parsing
                    // Gunakan waktu downtime end (nowUtc) sebagai start time untuk running
                    var startTimeUtcString = nowUtc.ToString("O"); // ISO 8601 format
                    await _hubContext.Clients.Group($"machine_{machineIdInt}")
                        .SendAsync("RunningStarted", machineIdInt, startTimeUtcString);
                    Console.WriteLine($"📡 Broadcasted RunningStarted (after downtime end): machine_{machineIdInt}, startTimeUtc: {startTimeUtcString}");
                }
                
                // ✅ PERBAIKAN FINAL: Update machine status ke Aktif saat downtime end (running start)
                var machineForDowntime = await _context.Machines.FindAsync(machineId);
                if (machineForDowntime != null && machineForDowntime.Status != MachineStatus.Aktif)
                {
                    machineForDowntime.Status = MachineStatus.Aktif;
                    await _context.SaveChangesAsync();
                }
                
                await _hubContext.Clients.All.SendAsync("OeeUpdated", new
                {
                    Type = "DowntimeEnded",
                    MachineId = machineId,
                    MachineName = existingJob.Machine?.Name,
                    Message = $"Downtime berakhir pada mesin {existingJob.Machine?.Name}",
                    Timestamp = nowLocal,
                    RefreshOperatorData = true,
                    MachineStatus = "Aktif" // ✅ PERBAIKAN FINAL: Kirim status update
                });
                
                if (isAjax)
                {
                    return Json(new { success = true, message = "Downtime berakhir, machine running" });
                }
                
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(nameof(Index), new { machineId });
            }
            else
            {
                // ✅ PERBAIKAN: Jika job sudah aktif dan tidak ada downtime, update LastStatusChangeTime
                existingJob.LastStatusChangeTime = nowLocal;
                await _context.SaveChangesAsync();
                
                if (isAjax)
                {
                    return Json(new { success = true, message = "Machine sudah running" });
                }
                TempData["OperationError"] = "Sudah ada job aktif. Tidak bisa start job baru.";
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(nameof(Index), new { machineId });
            }
        }

        // Tidak ada job aktif, START job baru
        var activeWorkOrder = await _context.WorkOrders
            .Include(w => w.Product)
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
            return RedirectToAction(nameof(Index), new { machineId });
        }

        var operatorUser = await _context.Users
            .Where(u => u.Role == UserRole.Operator)
            .FirstOrDefaultAsync();

        var machine = await _context.Machines.FindAsync(machineId);
        
        // ✅ PERBAIKAN FINAL: Update machine status ke Aktif saat running start
        if (machine != null && machine.Status != MachineStatus.Aktif)
        {
            machine.Status = MachineStatus.Aktif;
        }
        
        var newJob = new JobRun
        {
            MachineId = machineId,
            WorkOrderId = activeWorkOrder.Id,
            OperatorId = operatorUser?.Id ?? 0,
            StartTime = nowLocal, // Simpan local time untuk display
            EndTime = null,
            // ✅ PERBAIKAN: Set LastStatusChangeTime untuk kalkulasi OEE operating time
            LastStatusChangeTime = nowLocal
        };

        // ✅ TAMBAHKAN: Stop Dandori jika sedang berjalan di job aktif sebelum membuat job baru
        var activeJobWithDandori = await _context.JobRuns
            .Where(j => j.MachineId == machineId && j.EndTime == null)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync();
        
        if (activeJobWithDandori != null)
        {
            try
            {
                if (activeJobWithDandori.DandoriStartTime.HasValue && !activeJobWithDandori.DandoriEndTime.HasValue)
                {
                    activeJobWithDandori.DandoriEndTime = nowLocal;
                    var dandoriDuration = (int)(nowLocal - activeJobWithDandori.DandoriStartTime.Value).TotalSeconds;
                    activeJobWithDandori.DandoriDurationSeconds = (activeJobWithDandori.DandoriDurationSeconds ?? 0) + dandoriDuration;
                    await _context.SaveChangesAsync();
                    
                    // ✅ EVENT-DRIVEN: Broadcast DandoriStopped event
                    var machineIdInt2 = int.TryParse(machineId, out var id2) ? id2 : 0;
                    if (machineIdInt2 > 0)
                    {
                        await _hubContext.Clients.Group($"machine_{machineIdInt2}")
                            .SendAsync("DandoriStopped", machineIdInt2, nowUtc, activeJobWithDandori.DandoriDurationSeconds ?? 0);
                    }
                    
                    Console.WriteLine($"INFO: Dandori di-stop otomatis saat Start Job (durasi: {dandoriDuration} detik)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Error saat stop Dandori otomatis: {ex.Message}");
            }
        }

        _context.JobRuns.Add(newJob);
        await _context.SaveChangesAsync(); // ✅ PERBAIKAN FINAL: Save machine status update

        // ✅ EVENT-DRIVEN: Broadcast RunningStarted event
        var machineIdInt3 = int.TryParse(machineId, out var id3) ? id3 : 0;
        if (machineIdInt3 > 0)
        {
            // Convert to ISO 8601 string untuk konsistensi dengan JavaScript Date parsing
            var startTimeUtcString = nowUtc.ToString("O"); // ISO 8601 format
            await _hubContext.Clients.Group($"machine_{machineIdInt3}")
                .SendAsync("RunningStarted", machineIdInt3, startTimeUtcString);
            Console.WriteLine($"📡 Broadcasted RunningStarted: machine_{machineIdInt3}, startTimeUtc: {startTimeUtcString}");
        }
        
        await _hubContext.Clients.All.SendAsync("OeeUpdated", new
        {
            Type = "MachineStarted",
            MachineId = machineId,
            MachineName = machine?.Name,
            Message = $"Mesin {machine?.Name} telah dimulai",
            Timestamp = nowLocal,
            RefreshTimeMetrics = true,
            RefreshOperatorData = true
        });

        if (isAjax)
        {
            return Json(new { success = true, message = "Machine started successfully" });
        }

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index), new { machineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rest(string machineId, int reasonId, string? returnUrl = null)
    {
        return await StartDowntime(machineId, reasonId, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LineStop(string machineId, int reasonId, string? returnUrl = null)
    {
        return await StartDowntime(machineId, reasonId, returnUrl);
    }

    private async Task<IActionResult> StartDowntime(string machineId, int reasonId, string? returnUrl = null)
    {
        var now = DateTime.Now;

        var job = await _context.JobRuns
            .Include(j => j.DowntimeEvents)
            .Include(j => j.Machine)
            .Where(j => j.MachineId == machineId)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync(j => j.EndTime == null);

        // Check if this is an AJAX request
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (job == null)
        {
            if (isAjax)
            {
                return Json(new { success = false, message = "Tidak ada job aktif. Silakan start RUNNING PROCESS terlebih dahulu." });
            }
            TempData["OperationError"] = "Tidak ada job aktif. Silakan start RUNNING PROCESS terlebih dahulu.";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        // Cek apakah sudah ada downtime aktif
        var openDowntime = job.DowntimeEvents.FirstOrDefault(d => d.EndTime == null);
        if (openDowntime != null)
        {
            if (isAjax)
            {
                return Json(new { success = false, message = "Sudah ada downtime aktif. Tidak bisa start downtime baru." });
            }
            TempData["OperationError"] = "Sudah ada downtime aktif. Tidak bisa start downtime baru.";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        // Tidak ada downtime aktif, START downtime baru
        var reason = await _context.DowntimeReasons.FindAsync(reasonId);
        if (reason == null)
        {
            if (isAjax)
            {
                return Json(new { success = false, message = "Alasan downtime tidak ditemukan." });
            }
            TempData["OperationError"] = "Alasan downtime tidak ditemukan.";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        var newDowntime = new DowntimeEvent
        {
            JobRunId = job.Id,
            ReasonId = reasonId,
            StartTime = now,
            EndTime = null,
            DurationSeconds = 0
        };

        _context.DowntimeEvents.Add(newDowntime);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("OeeUpdated", new
        {
            Type = "DowntimeStarted",
            MachineId = machineId,
            MachineName = job.Machine?.Name,
            Reason = reason.Description,
            Category = reason.Category,
            Message = $"Downtime: {reason.Description} pada mesin {job.Machine?.Name}",
            Timestamp = now,
            RefreshTimeMetrics = true, // Flag untuk trigger refresh Time Metrics di OEE View
            RefreshOperatorData = true // ✅ TAMBAHKAN untuk trigger refresh Operator Data
        });

        // Return JSON for AJAX requests
        if (isAjax)
        {
            return Json(new { success = true, message = $"Downtime: {reason.Description} dimulai" });
        }

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index), new { machineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoLoading(string machineId, string? returnUrl = null)
    {
        var now = DateTime.Now;

        // Check if this is an AJAX request
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // Cari job aktif untuk mesin ini
        var activeJob = await _context.JobRuns
            .Include(j => j.Machine)
            .Include(j => j.DowntimeEvents)
            .Where(j => j.MachineId == machineId)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync(j => j.EndTime == null);

        if (activeJob == null)
        {
            if (isAjax)
            {
                return Json(new { success = false, message = "Tidak ada job aktif. Tidak bisa set NO LOADING." });
            }
            TempData["OperationError"] = "Tidak ada job aktif. Tidak bisa set NO LOADING.";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        // Cek apakah ada downtime aktif, jika ada end terlebih dahulu
        var openDowntime = activeJob.DowntimeEvents
            .OrderByDescending(d => d.StartTime)
            .FirstOrDefault(d => d.EndTime == null);

        if (openDowntime != null)
        {
            // End downtime yang aktif
            openDowntime.EndTime = now;
            openDowntime.DurationSeconds = (int)(now - openDowntime.StartTime).TotalSeconds;
        }

        // ✅ PERBAIKAN: JANGAN end job aktif saat NO LOADING
        // NO LOADING hanya set flag/status, tidak menghentikan job
        // Ini memungkinkan mesin bisa di-running kembali setelah NO LOADING
        // activeJob.EndTime tetap null agar job tetap aktif dan bisa di-running kembali
        
        // ✅ PERBAIKAN: Update LastStatusChangeTime untuk kalkulasi OEE
        activeJob.LastStatusChangeTime = now;

        await _context.SaveChangesAsync();

        // Broadcast SignalR update
        await _hubContext.Clients.All.SendAsync("OeeUpdated", new
        {
            Type = "NoLoadingStarted",
            MachineId = machineId,
            MachineName = activeJob.Machine?.Name,
            Message = $"NO LOADING: Mesin {activeJob.Machine?.Name} dihentikan (tidak masuk perhitungan OEE)",
            Timestamp = now,
            RefreshTimeMetrics = true, // Flag untuk trigger refresh Time Metrics di OEE View
            RefreshOperatorData = true // Flag untuk trigger refresh Operator Data
        });

        // Return JSON for AJAX requests
        if (isAjax)
        {
            return Json(new { success = true, message = "NO LOADING dimulai" });
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
        var now = DateTime.Now;

        // Validasi input
        if (goodQty < 0 || rejectQty < 0)
        {
            TempData["OperationError"] = "Quantity tidak boleh negatif";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }

        if (goodQty == 0 && rejectQty == 0)
        {
            TempData["OperationError"] = "Minimal harus ada input quantity (good atau reject)";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }

        var job = await _context.JobRuns
            .Include(j => j.Machine)
            .Include(j => j.WorkOrder)
                .ThenInclude(w => w.Product)
            .Where(j => j.MachineId == machineId)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync(j => j.EndTime == null);

        if (job == null)
        {
            TempData["OperationError"] = "Tidak ada job run aktif untuk mesin ini";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);

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
        string? lotNumber,
        string? partNumber,
        string? namaCompound,
        double? beratAct,
        string? penipisan,
        string? keterangan,
        int? manPowerId,
        string? injection,
        int? komponenId,
        int? durationSeconds,
        int qty = 1)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var now = DateTime.Now;

        try
        {
            // Validasi Job Aktif
            var job = await _context.JobRuns
                .Include(j => j.Machine)
                .Include(j => j.WorkOrder)
                    .ThenInclude(w => w.Product)
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
                GoodCount = qty, // Default 1 for item submission
                RejectCount = 0,
                LotNumber = lotNumber,
                LotBo = partNumber,
                CompoundName = namaCompound,
                ActualWeight = beratAct,
                Thinning = penipisan,
                Remarks = keterangan,
                ManPowerId = manPowerId,
                InjectionGroup = string.IsNullOrWhiteSpace(injection) ? null : injection.Trim().ToLower(),
                ComponentId = komponenId,
                DurationSeconds = durationSeconds
            };

            _context.ProductionCounts.Add(count);
            await _context.SaveChangesAsync();

            // Broadcast SignalR update
            await _hubContext.Clients.All.SendAsync("OeeUpdated", new
            {
                Type = "ProductionDataSubmitted",
                MachineId = machineId,
                MachineName = job.Machine?.Name,
                ProductName = job.WorkOrder?.Product?.Name,
                GoodCount = qty,
                Message = $"Data Produksi tersimpan: {lotNumber} ({partNumber})",
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

    private static TimeSpan GetOverlap(DateTime start, DateTime end, DateTime windowStart, DateTime windowEnd)
    {
        var overlapStart = start < windowStart ? windowStart : start;
        var overlapEnd = end > windowEnd ? windowEnd : end;
        return overlapEnd > overlapStart ? overlapEnd - overlapStart : TimeSpan.Zero;
    }

    private static (DateTime Start, DateTime End, DateTime ShiftDate, string Code, string Key) ResolveShiftWindow(DateTime now, DateTime? shiftDate, string? shiftCode)
    {
        var shiftTemplates = new List<(string Code, TimeSpan Start, TimeSpan End)>
        {
            ("A", new TimeSpan(6, 0, 0), new TimeSpan(14, 0, 0)),
            ("B", new TimeSpan(14, 0, 0), new TimeSpan(22, 0, 0)),
            ("C", new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0))
        };

        var code = string.IsNullOrWhiteSpace(shiftCode)
            ? null
            : shiftCode!.Trim().ToUpperInvariant();

        // Tentukan kode shift jika tidak dikirim
        if (code == null)
        {
            var tod = now.TimeOfDay;
            if (tod >= shiftTemplates[0].Start && tod < shiftTemplates[0].End)
            {
                code = "A";
            }
            else if (tod >= shiftTemplates[1].Start && tod < shiftTemplates[1].End)
            {
                code = "B";
            }
            else
            {
                code = "C";
            }
        }

        var template = shiftTemplates.FirstOrDefault(s => s.Code == code);
        if (template == default)
        {
            template = shiftTemplates[0];
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

    [HttpGet]
    public async Task<IActionResult> GetOperatorData(string machineId)
    {
        // ✅ PERBAIKAN: Validasi machineId
        if (string.IsNullOrEmpty(machineId))
        {
            return Json(new { error = "machineId is required" });
        }
        
        // ✅ PERBAIKAN: Log untuk debugging
        System.Diagnostics.Debug.WriteLine($"GetOperatorData called with machineId: '{machineId}'");
        
        var now = DateTime.Now;

        // ✅ PERBAIKAN: Gunakan AsNoTracking() untuk memastikan data fresh dari database
        var machine = await _context.Machines
            .AsNoTracking()
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.WorkOrder)
                    .ThenInclude(w => w.Product)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.ProductionCounts)
            .Include(m => m.JobRuns)
                .ThenInclude(j => j.DowntimeEvents)
                    .ThenInclude(d => d.Reason)
            .FirstOrDefaultAsync(m => m.Id == machineId);

        if (machine == null)
        {
            // ✅ PERBAIKAN: Return JSON error instead of NotFound untuk AJAX call
            return Json(new { 
                error = $"Machine with ID '{machineId}' not found",
                machineId = machineId 
            });
        }

        var activeJob = machine.JobRuns
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefault(j => j.EndTime == null);

        var allCounts = activeJob?.ProductionCounts ?? Array.Empty<ProductionCount>();

        var openDowntime = activeJob?.DowntimeEvents
            .OrderByDescending(d => d.StartTime)
            .FirstOrDefault(d => d.EndTime == null);

        // ✅ PERBAIKAN: Hitung LastStatusChangeTime yang sinkron dengan Operating Time untuk OEE
        // - Jika ada downtime aktif (REST/LINE STOP) -> pakai StartTime downtime (RESET ke 00:00:00)
        // - Jika sedang RUNNING (tidak ada downtime aktif) -> hitung dari StartTime job dikurangi total downtime
        DateTime lastStatusChangeTime;
        int sinceLastChangeSeconds;

        if (openDowntime != null)
        {
            // Sedang REST / LINE STOP: RESET durasi ke 00:00:00 (mulai hitung dari awal downtime)
            lastStatusChangeTime = openDowntime.StartTime;
            sinceLastChangeSeconds = (int)(now - openDowntime.StartTime).TotalSeconds;
        }
        else if (activeJob != null)
        {
            // Sedang RUNNING: Hitung durasi yang sinkron dengan Operating Time (untuk OEE)
            // ✅ PERBAIKAN: Gunakan LastStatusChangeTime jika ada (lebih akurat)
            // Jika tidak ada, hitung dari StartTime dikurangi downtime
            
            if (activeJob.LastStatusChangeTime.HasValue)
            {
                // ✅ PERBAIKAN: Gunakan LastStatusChangeTime sebagai start time untuk durasi running
                // Ini memastikan durasi bisa start kapan saja, tidak terpaku dengan shift start
                lastStatusChangeTime = activeJob.LastStatusChangeTime.Value;
                sinceLastChangeSeconds = Math.Max(0, (int)(now - lastStatusChangeTime).TotalSeconds);
            }
            else
            {
                // Fallback: Hitung dari StartTime dikurangi downtime (untuk backward compatibility)
            var jobDuration = (now - activeJob.StartTime).TotalSeconds;
            
            // Hitung total Unplanned downtime yang sudah selesai di job ini
            var totalUnplannedDowntimeSeconds = activeJob.DowntimeEvents
                .Where(d => d.EndTime.HasValue && d.Reason?.Category == "Unplanned")
                .Sum(d => d.DurationSeconds > 0 
                    ? d.DurationSeconds 
                    : (d.EndTime!.Value - d.StartTime).TotalSeconds);
            
            // Operating Time murni = JobRun duration - Unplanned Downtime duration (sinkron dengan perhitungan OEE)
            var operatingTimeSeconds = jobDuration - totalUnplannedDowntimeSeconds;
            sinceLastChangeSeconds = Math.Max(0, (int)operatingTimeSeconds);
            
            // LastStatusChangeTime untuk display (backward compatibility)
            lastStatusChangeTime = now.AddSeconds(-sinceLastChangeSeconds);
            }
        }
        else
        {
            // Tidak ada job aktif: fallback
            lastStatusChangeTime = machine.JobRuns
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefault()?.StartTime ?? now;
            sinceLastChangeSeconds = 0;
        }

        // Keep lastChangeTime untuk backward compatibility
        DateTime lastChangeTime = lastStatusChangeTime;

        var totalGood = allCounts.Sum(c => c.GoodCount);
        var totalReject = allCounts.Sum(c => c.RejectCount);
        var targetQuantity = activeJob?.WorkOrder?.TargetQuantity ?? 0;
        var progressPercent = targetQuantity > 0
            ? Math.Min(100, (double)totalGood / targetQuantity * 100)
            : 0;

        // Calculate estimated completion time
        string? estimatedCompletion = null;
        if (targetQuantity > 0 && totalGood > 0 && activeJob != null && activeJob.WorkOrder?.Product != null)
        {
            var remaining = targetQuantity - totalGood;
            if (remaining > 0)
            {
                var standarCycleTime = activeJob.WorkOrder.Product.StandarCycleTime;
                var elapsedTime = (now - activeJob.StartTime).TotalSeconds;
                var currentRate = elapsedTime > 0 ? totalGood / elapsedTime : 0; // units per second
                
                if (currentRate > 0)
                {
                    var estimatedSeconds = remaining / currentRate;
                    var estimatedCompletionTime = now.AddSeconds(estimatedSeconds);
                    estimatedCompletion = estimatedCompletionTime.ToString("HH:mm");
                }
            }
        }

        // sinceLastChangeSeconds sudah dihitung di atas

        // ✅ PERBAIKAN: Tampilkan status sesuai dengan Machine.Status dari Admin (AKTIF atau TIDAK AKTIF)
        // Machine.Status adalah sumber kebenaran dari Admin Machines panel
        string displayStatus = machine.Status == MachineStatus.Aktif ? "AKTIF" : "TIDAK AKTIF";
        
        // ✅ PERBAIKAN: Log untuk debugging - pastikan MachineStatus dikembalikan dengan benar
        System.Diagnostics.Debug.WriteLine($"GetOperatorData - MachineId: '{machineId}', MachineStatus: '{machine.Status}', MachineStatus.ToString(): '{machine.Status.ToString()}'");

        // ========== CALCULATE TIME METRICS ==========
        var shiftWindow = ResolveShiftWindow(now, null, null);
        var effectiveNow = now < shiftWindow.End ? now : shiftWindow.End;
        
        var shiftJobRuns = machine.JobRuns
            .Where(j => j.StartTime < shiftWindow.End && (j.EndTime ?? effectiveNow) > shiftWindow.Start)
            .ToList();

        // 1. Total Shift Time
        TimeSpan totalShiftTime = shiftWindow.End - shiftWindow.Start;

        // 2. Pisahkan Planned dan Unplanned Downtime
        TimeSpan plannedDowntime = TimeSpan.Zero;
        TimeSpan unplannedDowntime = TimeSpan.Zero;

        foreach (var jr in shiftJobRuns)
        {
            foreach (var d in jr.DowntimeEvents)
            {
                var dEnd = d.EndTime ?? effectiveNow;
                var overlap = GetOverlap(d.StartTime, dEnd, shiftWindow.Start, shiftWindow.End);
                
                // Pisahkan berdasarkan kategori
                if (d.Reason?.Category == "Unplanned")
                {
                    unplannedDowntime += overlap;
                }
                else
                {
                    // Planned: Rest Break, Setup, dll
                    plannedDowntime += overlap;
                }
            }
        }

        // 3. Planned Production Time = Shift Time - Planned Downtime
        TimeSpan plannedProductionTime = totalShiftTime - plannedDowntime;
        if (plannedProductionTime.TotalSeconds < 0)
        {
            plannedProductionTime = TimeSpan.Zero;
        }

        // Jika belum ada data, gunakan durasi shift
        if (plannedProductionTime.TotalSeconds == 0 && shiftJobRuns.Count == 0)
        {
            plannedProductionTime = totalShiftTime;
        }

        // 4. Operating Time = Planned Production Time - Unplanned Downtime
        TimeSpan operatingTime = plannedProductionTime - unplannedDowntime;
        if (operatingTime.TotalSeconds < 0)
        {
            operatingTime = TimeSpan.Zero;
        }

        // 5. Total Downtime untuk display = Planned + Unplanned
        TimeSpan downtimeTotal = plannedDowntime + unplannedDowntime;

        // ✅ PERBAIKAN: Pastikan MachineStatus selalu di-return dengan explicit variable
        var machineStatusString = machine.Status.ToString(); // 'Aktif' atau 'TidakAktif'
        
        // ✅ PERBAIKAN: Log untuk debugging
        System.Diagnostics.Debug.WriteLine($"GetOperatorData RETURN - MachineId: '{machineId}', MachineStatus: '{machineStatusString}'");

        // ✅ TAMBAHKAN: Get Dandori status untuk GetOperatorData
        DateTime? dandoriStartTime = null;
        DateTime? dandoriEndTime = null;
        int? dandoriDurationSeconds = null;
        bool isDandoriRunning = false;
        
        if (activeJob != null)
        {
            try
            {
                dandoriStartTime = activeJob.DandoriStartTime;
                dandoriEndTime = activeJob.DandoriEndTime;
                dandoriDurationSeconds = activeJob.DandoriDurationSeconds;
                isDandoriRunning = dandoriStartTime.HasValue && !dandoriEndTime.HasValue;
            }
            catch
            {
                // Jika kolom belum ada, gunakan default
            }
        }

        return Json(new
        {
            // Production Data
            TotalGood = totalGood,
            TotalReject = totalReject,
            TotalCount = totalGood + totalReject,
            TargetQuantity = targetQuantity,
            ProgressPercent = Math.Round(progressPercent, 1),
            
            // Status & Timing
            SinceLastChangeSeconds = sinceLastChangeSeconds,
            SinceLastChange = (now - lastChangeTime).ToString(@"hh\:mm\:ss"),
            LastStatusChangeTime = lastStatusChangeTime.ToString("O"), // ✅ ISO 8601 format untuk sinkronisasi timer
            
            // ✅ TAMBAHKAN: Dandori Status
            DandoriStartTime = dandoriStartTime?.ToString("O"),
            DandoriEndTime = dandoriEndTime?.ToString("O"),
            DandoriDurationSeconds = dandoriDurationSeconds,
            IsDandoriRunning = isDandoriRunning,
            
            // Job & Downtime Status
            HasActiveJob = activeJob != null,
            HasActiveDowntime = openDowntime != null,
            ActiveDowntimeDescription = openDowntime?.Reason?.Description,
            
            // ✅ PERBAIKAN: MachineStatus SELALU di-return (sumber kebenaran dari Admin)
            MachineStatus = machineStatusString, // 'Aktif' atau 'TidakAktif'
            Status = displayStatus, // 'AKTIF' atau 'TIDAK AKTIF' untuk display
            
            // Product Info
            EstimatedCompletion = estimatedCompletion,
            ProductName = activeJob?.WorkOrder?.Product?.Name,
            WorkOrderNumber = activeJob?.WorkOrder?.OrderNumber,
            ProductImageUrl = activeJob?.WorkOrder?.Product?.ImageUrl,
            
            // Time Metrics
            PlannedProductionTimeSeconds = plannedProductionTime.TotalSeconds,
            OperatingTimeSeconds = operatingTime.TotalSeconds,
            DowntimeTotalSeconds = downtimeTotal.TotalSeconds
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
            
            return Json(new { success = true, remarks = remarks });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: Start SCW
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scw(string machineId, int scw4MTypeId, int scwRemarkId, string? additionalNotes = null, string? returnUrl = null)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var now = DateTime.Now;
        
        try
        {
            // Validasi
            var scw4MType = await _context.Scw4MTypes.FindAsync(scw4MTypeId);
            var scwRemark = await _context.ScwRemarks.FindAsync(scwRemarkId);
            
            if (scw4MType == null || scwRemark == null)
            {
                if (isAjax)
                    return Json(new { success = false, message = "Jenis 4M atau Remark tidak ditemukan" });
                TempData["OperationError"] = "Jenis 4M atau Remark tidak ditemukan";
                return RedirectToAction(nameof(Index), new { machineId });
            }
            
            // Validasi: Remark harus sesuai dengan 4M Type
            if (scwRemark.Scw4MTypeId != scw4MTypeId)
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
                Scw4MTypeId = scw4MTypeId,
                ScwRemarkId = scwRemarkId,
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

