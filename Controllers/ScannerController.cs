using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;
using Microsoft.Data.SqlClient;
using OeeSystem.Models; // untuk WorkOrderStatus, UserRole, JobRun

namespace OeeSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScannerController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public ScannerController(
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // ✅ GET KOMPONEN BY PART NUMBER (From ApplicationDbContext)
    [HttpGet("komponen/{partNumber}")]
    public async Task<IActionResult> GetKomponenByPartNumber(string partNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                return BadRequest(new { success = false, message = "Part Number tidak boleh kosong" });
            }

            var komponen = await _context.Komponens
                .Where(k => k.PartNumber == partNumber.Trim())
                .Select(k => new
                {
                    k.Id,
                    k.PartNumber,
                    JmlKomponen = k.JmlKomponen ?? 0
                })
                .ToListAsync();

            if (komponen == null || !komponen.Any())
            {
                return Ok(new { 
                    success = false, 
                    message = $"Komponen tidak ditemukan untuk Part Number: {partNumber}", 
                    data = new List<object>() 
                });
            }

            return Ok(new { success = true, data = komponen });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                success = false, 
                message = $"Error: {ex.Message}",
                inner = ex.InnerException?.Message,
                data = new List<object>()
            });
        }
    }

    [HttpGet("komponen/validate/{partNumber}")]
    public async Task<IActionResult> ValidatePartNumber(string partNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                return Ok(new { success = false, message = "Part Number tidak boleh kosong" });
            }

            var exists = await _context.Komponens
                .AnyAsync(k => k.PartNumber == partNumber.Trim());

            return Ok(new { success = exists, message = exists ? "Part Number valid" : "Part Number tidak ditemukan di database" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                success = false, 
                message = $"Error: {ex.Message}",
                inner = ex.InnerException?.Message
            });
        }
    }

    // ✅ SUBMIT SCAN PRODUKSI
    [HttpPost("scan-produksi/submit")]
    public async Task<IActionResult> SubmitScanProduksi(
        [FromForm] string machineId,
        [FromForm] string partNumber,
        [FromForm] string lotNumber,
        [FromForm] int komponenId,
        [FromForm] int? manPowerId = null,
        [FromForm] string? namaCompound = null,
        [FromForm] string? beratAct = null,
        [FromForm] string? injection = null,
        [FromForm] int? durasiSeconds = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(machineId) ||
                string.IsNullOrWhiteSpace(partNumber) ||
                string.IsNullOrWhiteSpace(lotNumber) ||
                komponenId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "MachineId, Part Number, Lot Number, dan Komponen wajib diisi."
                });
            }

            partNumber = partNumber.Trim();
            lotNumber = lotNumber.Trim();

            // 1) Validasi Part Number & Komponen di DB_HOSS
            var komponen = await _context.Komponens
                .FirstOrDefaultAsync(k => k.Id == komponenId && k.PartNumber == partNumber);

            if (komponen == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Komponen tidak ditemukan atau tidak sesuai dengan Part Number di DB_HOSS."
                });
            }

            var now = DateTime.Now;

            // 2) Cari JobRun aktif untuk mesin ini
            var activeJob = await _context.JobRuns
                .Where(j => j.MachineId == machineId && j.EndTime == null)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefaultAsync();

            // 2a) Jika belum ada, buat JobRun baru dari WorkOrder aktif
            if (activeJob == null)
            {
                var activeWorkOrder = await _context.WorkOrders
                    .Include(w => w.Product)
                    .FirstOrDefaultAsync(w => w.Status == WorkOrderStatus.InProgress);

                if (activeWorkOrder == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Tidak ada Work Order aktif. Silakan buat Work Order dan mulai job terlebih dahulu."
                    });
                }

                var operatorUser = await _context.Users
                    .Where(u => u.Role == UserRole.Operator)
                    .FirstOrDefaultAsync();

                activeJob = new JobRun
                {
                    MachineId = machineId,
                    WorkOrderId = activeWorkOrder.Id,
                    OperatorId = operatorUser?.Id ?? 0,
                    StartTime = now,
                    EndTime = null,
                    // 3) Simpan hasil scan
                    ScannedPartNumber = partNumber,
                    ScannedLotNumber = lotNumber,
                    ScannedKomponenId = komponenId,
                    ScannedJmlKomponen = komponen.JmlKomponen,
                    // ✅ TAMBAHKAN: Simpan Man Power
                    ManPowerId = manPowerId
                };

                _context.JobRuns.Add(activeJob);
            }
            else
            {
                // 2b) Update JobRun aktif dengan hasil scan terbaru
                activeJob.ScannedPartNumber = partNumber;
                activeJob.ScannedLotNumber = lotNumber;
                activeJob.ScannedKomponenId = komponenId;
                activeJob.ScannedJmlKomponen = komponen.JmlKomponen;
                // ✅ TAMBAHKAN: Update Man Power jika ada
                if (manPowerId.HasValue)
                {
                    activeJob.ManPowerId = manPowerId;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Data scan produksi berhasil disimpan.",
                jobRunId = activeJob.Id,
                partNumber,
                lotNumber,
                durasiSeconds = durasiSeconds ?? 0,
                komponen = new
                {
                    id = komponenId,
                    partNumber = komponen.PartNumber,
                    jmlKomponen = komponen.JmlKomponen
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = $"Error: {ex.Message}"
            });
        }
    }
}

