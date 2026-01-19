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
        [FromForm] string lotBo,
        [FromForm] string nomorLot,
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
                string.IsNullOrWhiteSpace(lotBo) ||
                string.IsNullOrWhiteSpace(nomorLot) ||
                komponenId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                });
            }

            lotBo = lotBo.Trim();
            nomorLot = nomorLot.Trim();

            // 1) VALIDASI: Part Number (lotBo) HARUS terdaftar di Products
            var product = await _context.Products.FirstOrDefaultAsync(p => p.MaterialCode == lotBo);
            if (product == null)
            {
                return Ok(new { success = false, message = $"Part Number '{lotBo}' tidak terdaftar di master Product." });
            }

            // 1a) VALIDASI: Komponen di DB_HOSS
            var komponen = await _context.Komponens
                .FirstOrDefaultAsync(k => k.Id == komponenId && k.PartNumber == lotBo);

            if (komponen == null)
            {
                return Ok(new { success = false, message = "Komponen tidak ditemukan atau tidak sesuai dengan Part Number di database HOSS." });
            }

            // 2) VALIDASI: Cari WorkOrder aktif
            var activeWorkOrder = await _context.WorkOrders
                .Include(w => w.Product)
                .FirstOrDefaultAsync(w => w.Status == WorkOrderStatus.InProgress);

            if (activeWorkOrder == null)
            {
                return Ok(new { success = false, message = "Tidak ada Work Order (InProgress) aktif." });
            }

            // 3) VALIDASI: Product harus sesuai dengan WorkOrder aktif
            if (activeWorkOrder.ProductId != product.Id)
            {
                return Ok(new { success = false, message = $"Part Number '{lotBo}' tidak sesuai dengan Work Order aktif ({activeWorkOrder.Product?.MaterialCode})." });
            }

            var now = DateTime.Now;

            // 4) Cari JobRun aktif untuk mesin ini
            var activeJob = await _context.JobRuns
                .Where(j => j.MachineId == machineId && j.EndTime == null)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefaultAsync();

            var operatorUser = await _context.Users
                .Where(u => u.Role == UserRole.Operator)
                .FirstOrDefaultAsync();

            if (activeJob == null)
            {
                activeJob = new JobRun
                {
                    MachineId = machineId,
                    WorkOrderId = activeWorkOrder.Id,
                    OperatorId = operatorUser?.Id ?? 0,
                    StartTime = now,
                    EndTime = null,
                    LastStatusChangeTime = now,
                    // Simpan hasil scan
                    ScannedPartNumber = lotBo,
                    ScannedLotNumber = nomorLot,
                    ScannedKomponenId = komponenId,
                    ScannedJmlKomponen = komponen.JmlKomponen,
                    ManPowerId = manPowerId,
                    InjectionGroup = string.IsNullOrWhiteSpace(injection) ? null : injection.Trim().ToUpper()
                };
                _context.JobRuns.Add(activeJob);
            }
            else
            {
                activeJob.ScannedPartNumber = lotBo;
                activeJob.ScannedLotNumber = nomorLot;
                activeJob.ScannedKomponenId = komponenId;
                activeJob.ScannedJmlKomponen = komponen.JmlKomponen;
                activeJob.InjectionGroup = string.IsNullOrWhiteSpace(injection) ? null : injection.Trim().ToUpper();
                if (manPowerId.HasValue) activeJob.ManPowerId = manPowerId;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Data scan produksi berhasil disimpan.",
                jobRunId = activeJob.Id,
                lotBo,
                nomorLot,
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

