using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;

namespace OeeSystem.Controllers.Api;

/// <summary>
/// API Controller untuk mendapatkan status OEE saat ini (untuk page reload)
/// 
/// PRINSIP:
/// - Return timestamp UTC untuk client menghitung durasi
/// - Tidak return durasi yang sudah dihitung (client akan hitung sendiri)
/// </summary>
[ApiController]
[Route("api/oee")]
public class OeeStatusController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OeeStatusController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get current status untuk machine tertentu
    /// Digunakan saat page reload untuk resume timer
    /// </summary>
    [HttpGet("current-status")]
    public async Task<IActionResult> GetCurrentStatus(
        [FromQuery] string machineId,
        [FromQuery] int? shiftId = null)
    {
        if (string.IsNullOrEmpty(machineId))
        {
            return BadRequest(new { error = "machineId is required" });
        }

        try
        {
            // Cari job aktif untuk mesin ini
            var activeJob = await _context.JobRuns
                .Where(j => j.MachineId == machineId && j.EndTime == null)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefaultAsync();

            if (activeJob == null)
            {
                return Ok(new
                {
                    status = "Idle",
                    running = (object?)null,
                    dandori = (object?)null
                });
            }

            // Check Running status (job aktif tanpa downtime aktif)
            var hasActiveDowntime = await _context.DowntimeEvents
                .AnyAsync(d => d.JobRunId == activeJob.Id && d.EndTime == null);

            // ✅ PERBAIKAN: Hitung start time untuk running
            // Jika ada downtime yang baru saja selesai (rest break), gunakan waktu downtime end sebagai start time
            // Jika tidak ada downtime, gunakan job start time
            DateTime? runningStartTime = null;
            if (!hasActiveDowntime)
            {
                // Cek apakah ada downtime yang baru saja selesai (rest break)
                var lastDowntime = await _context.DowntimeEvents
                    .Where(d => d.JobRunId == activeJob.Id && d.EndTime.HasValue)
                    .OrderByDescending(d => d.EndTime)
                    .FirstOrDefaultAsync();
                
                if (lastDowntime != null && lastDowntime.EndTime.HasValue)
                {
                    // ✅ PERBAIKAN: Gunakan waktu downtime end sebagai start time untuk running
                    // Ini memastikan durasi running melanjutkan dari waktu rest break selesai
                    runningStartTime = lastDowntime.EndTime.Value;
                }
                else
                {
                    // Jika tidak ada downtime sebelumnya, gunakan job start time
                    runningStartTime = activeJob.StartTime;
                }
            }

            var runningStatus = !hasActiveDowntime && runningStartTime.HasValue ? new
            {
                isRunning = true,
                startTimeUtc = runningStartTime.Value.ToUniversalTime().ToString("O") // ISO 8601 format
            } : null;

            // Check Dandori status
            var dandoriStatus = activeJob.DandoriStartTime.HasValue && !activeJob.DandoriEndTime.HasValue
                ? new
                {
                    isRunning = true,
                    startTimeUtc = activeJob.DandoriStartTime.Value.ToUniversalTime().ToString("O") // ISO 8601 format
                }
                : null;

            // ✅ PERBAIKAN: Ambil detail downtime jika ada
            var activeDowntime = hasActiveDowntime 
                ? await _context.DowntimeEvents
                    .Include(d => d.Reason)
                    .Where(d => d.JobRunId == activeJob.Id && d.EndTime == null)
                    .OrderByDescending(d => d.StartTime)
                    .FirstOrDefaultAsync()
                : null;

            object? downtimeStatus = null;
            string statusText = "Idle";

            if (activeDowntime != null)
            {
                // Tentukan status text berdasarkan tipe downtime
                if (activeDowntime.IsRestBreak || (activeDowntime.Reason?.Description?.Contains("Rest", StringComparison.OrdinalIgnoreCase) == true))
                    statusText = "Rest Break";
                else if (activeDowntime.IsNoLoading || (activeDowntime.Reason?.Description?.Contains("No Loading", StringComparison.OrdinalIgnoreCase) == true))
                    statusText = "No Loading";
                else if (activeDowntime.IsLineStop)
                    statusText = "Line Stop";
                else
                    statusText = activeDowntime.Reason?.Description ?? "Downtime";

                downtimeStatus = new 
                {
                    id = activeDowntime.Id,
                    reason = activeDowntime.Reason?.Description ?? "Unknown",
                    startTimeUtc = activeDowntime.StartTime.ToUniversalTime().ToString("O"),
                    isRestBreak = activeDowntime.IsRestBreak,
                    isLineStop = activeDowntime.IsLineStop,
                    isNoLoading = activeDowntime.IsNoLoading
                };
            }
            else if (runningStatus != null)
            {
                statusText = "Running";
            }
            else if (dandoriStatus != null)
            {
                statusText = "Dandori";
            }

            return Ok(new
            {
                status = statusText,
                running = runningStatus,
                dandori = dandoriStatus,
                downtime = downtimeStatus
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error getting current status: {ex.Message}");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}

