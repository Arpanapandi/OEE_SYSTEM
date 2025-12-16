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
        var now = DateTime.Now;

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
                openDowntime.EndTime = now;
                openDowntime.DurationSeconds = (int)(now - openDowntime.StartTime).TotalSeconds;
                
                await _context.SaveChangesAsync();
                
                // Broadcast SignalR update untuk downtime ended
                await _hubContext.Clients.All.SendAsync("OeeUpdated", new
                {
                    Type = "DowntimeEnded",
                    MachineId = machineId,
                    MachineName = existingJob.Machine?.Name,
                    Message = $"Downtime berakhir pada mesin {existingJob.Machine?.Name}",
                    Timestamp = now,
                    RefreshOperatorData = true
                });
                
                // Setelah end downtime, machine akan running (job sudah aktif, downtime sudah di-end)
                // Redirect kembali ke operator view
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(nameof(Index), new { machineId });
            }
            else
            {
                // Sudah ada job aktif TANPA downtime, tidak bisa start lagi
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
            TempData["OperationError"] = "Tidak ada Work Order aktif. Silakan buat Work Order terlebih dahulu.";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        var operatorUser = await _context.Users
            .Where(u => u.Role == UserRole.Operator)
            .FirstOrDefaultAsync();

        var machine = await _context.Machines.FindAsync(machineId);
        var newJob = new JobRun
        {
            MachineId = machineId,
            WorkOrderId = activeWorkOrder.Id,
            OperatorId = operatorUser?.Id ?? 0,
            StartTime = now,
            EndTime = null
        };

        _context.JobRuns.Add(newJob);
        await _context.SaveChangesAsync();

        // Reload job dengan Include untuk memastikan data lengkap
        var reloadedJob = await _context.JobRuns
            .Include(j => j.WorkOrder)
                .ThenInclude(w => w.Product)
            .Include(j => j.Operator)
            .Include(j => j.DowntimeEvents)
            .Include(j => j.ProductionCounts)
            .FirstOrDefaultAsync(j => j.Id == newJob.Id);

        // Broadcast SignalR update
        await _hubContext.Clients.All.SendAsync("OeeUpdated", new
        {
            Type = "MachineStarted",
            MachineId = machineId,
            MachineName = machine?.Name,
            Message = $"Mesin {machine?.Name} telah dimulai",
            Timestamp = now,
            RefreshTimeMetrics = true, // Flag untuk trigger refresh Time Metrics di OEE View
            RefreshOperatorData = true // Flag untuk trigger refresh Operator Data
        });

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

        if (job == null)
        {
            TempData["OperationError"] = "Tidak ada job aktif. Silakan start RUNNING PROCESS terlebih dahulu.";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        // Cek apakah sudah ada downtime aktif
        var openDowntime = job.DowntimeEvents.FirstOrDefault(d => d.EndTime == null);
        if (openDowntime != null)
        {
            TempData["OperationError"] = "Sudah ada downtime aktif. Tidak bisa start downtime baru.";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { machineId });
        }

        // Tidak ada downtime aktif, START downtime baru
        var reason = await _context.DowntimeReasons.FindAsync(reasonId);
        if (reason == null)
        {
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
            RefreshTimeMetrics = true // Flag untuk trigger refresh Time Metrics di OEE View
        });

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index), new { machineId });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuantity(string machineId, int goodQty, int rejectQty, string? rejectReason, int? ngTypeId = null)
    {
        var now = DateTime.Now;

        // Validasi input
        if (goodQty < 0 || rejectQty < 0)
        {
            TempData["OperationError"] = "Quantity tidak boleh negatif";
            return RedirectToAction(nameof(Index), new { machineId });
        }

        if (goodQty == 0 && rejectQty == 0)
        {
            TempData["OperationError"] = "Minimal harus ada input quantity (good atau reject)";
            return RedirectToAction(nameof(Index), new { machineId });
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
            return RedirectToAction(nameof(Index), new { machineId });
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
                NgTypeId = ngTypeId
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

        return RedirectToAction(nameof(Index), new { machineId });
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

        // ✅ PERBAIKAN: Hitung LastStatusChangeTime untuk sinkronisasi timer
        DateTime lastStatusChangeTime = activeJob?.StartTime ?? machine.JobRuns
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefault()?.StartTime ?? now;

        if (openDowntime != null)
        {
            lastStatusChangeTime = openDowntime.StartTime;
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

        var sinceLastChangeSeconds = (now - lastChangeTime).TotalSeconds;

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

        // 1. Operating Time = Total durasi waktu running (JobRun) yang overlap dengan window shift
        TimeSpan operatingTime = TimeSpan.Zero;
        foreach (var jr in shiftJobRuns)
        {
            var jrEnd = jr.EndTime ?? effectiveNow;
            operatingTime += GetOverlap(jr.StartTime, jrEnd, shiftWindow.Start, shiftWindow.End);
        }

        // 2. Downtime = Total durasi line stop (DowntimeEvents) yang overlap shift
        TimeSpan downtimeTotal = TimeSpan.Zero;
        foreach (var jr in shiftJobRuns)
        {
            foreach (var d in jr.DowntimeEvents)
            {
                var dEnd = d.EndTime ?? effectiveNow;
                downtimeTotal += GetOverlap(d.StartTime, dEnd, shiftWindow.Start, shiftWindow.End);
            }
        }

        // 3. Planned Production Time = Operating Time + Downtime
        TimeSpan plannedProductionTime = operatingTime + downtimeTotal;
        if (plannedProductionTime.TotalSeconds == 0)
        {
            plannedProductionTime = shiftWindow.End - shiftWindow.Start;
        }

        // ✅ PERBAIKAN: Pastikan MachineStatus selalu di-return dengan explicit variable
        var machineStatusString = machine.Status.ToString(); // 'Aktif' atau 'TidakAktif'
        
        // ✅ PERBAIKAN: Log untuk debugging
        System.Diagnostics.Debug.WriteLine($"GetOperatorData RETURN - MachineId: '{machineId}', MachineStatus: '{machineStatusString}'");

        return Json(new
        {
            // Production Data
            TotalGood = totalGood,
            TotalReject = totalReject,
            TotalCount = totalGood + totalReject,
            TargetQuantity = targetQuantity,
            ProgressPercent = Math.Round(progressPercent, 1),
            
            // Status & Timing
            SinceLastChangeSeconds = Math.Floor(sinceLastChangeSeconds),
            SinceLastChange = (now - lastChangeTime).ToString(@"hh\:mm\:ss"),
            LastStatusChangeTime = lastStatusChangeTime.ToString("O"), // ✅ ISO 8601 format untuk sinkronisasi timer
            
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
}

