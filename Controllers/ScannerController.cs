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
    private readonly HossDbContext _hossContext;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public ScannerController(
        HossDbContext hossContext,
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _hossContext = hossContext;
        _context = context;
        _configuration = configuration;
    }

    // ✅ DIAGNOSTIC: Endpoint untuk mengecek struktur tabel di db_HOSS
    [HttpGet("diagnostic/check-tables")]
    public async Task<IActionResult> CheckTables()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("HossConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(new { success = false, message = "Connection string HossConnection tidak ditemukan" });
            }

            var tables = new List<object>();
            var tableColumns = new Dictionary<string, List<object>>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // 1. Ambil daftar semua tabel di database
                var tablesQuery = @"
                    SELECT TABLE_SCHEMA, TABLE_NAME 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    ORDER BY TABLE_SCHEMA, TABLE_NAME";

                using (var command = new SqlCommand(tablesQuery, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var schema = reader.GetString(0);
                            var tableName = reader.GetString(1);
                            var fullTableName = $"{schema}.{tableName}";
                            tables.Add(new { Schema = schema, TableName = tableName, FullName = fullTableName });
                        }
                    }
                }

                // 2. Untuk setiap tabel, ambil informasi kolom
                foreach (var table in tables)
                {
                    var tableObj = (dynamic)table;
                    var fullTableName = tableObj.FullName.ToString();
                    var columns = new List<object>();

                    var columnsQuery = @"
                        SELECT 
                            COLUMN_NAME,
                            DATA_TYPE,
                            CHARACTER_MAXIMUM_LENGTH,
                            IS_NULLABLE,
                            COLUMN_DEFAULT,
                            ORDINAL_POSITION
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName
                        ORDER BY ORDINAL_POSITION";

                    using (var command = new SqlCommand(columnsQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Schema", tableObj.Schema);
                        command.Parameters.AddWithValue("@TableName", tableObj.TableName);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                columns.Add(new
                                {
                                    ColumnName = reader.IsDBNull(0) ? null : reader.GetString(0),
                                    DataType = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    MaxLength = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                    IsNullable = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    OrdinalPosition = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5)
                                });
                            }
                        }
                    }

                    tableColumns[fullTableName] = columns;
                }
            }

            return Ok(new
            {
                success = true,
                database = "db_HOSS",
                tables = tables,
                columns = tableColumns,
                message = $"Ditemukan {tables.Count} tabel di database db_HOSS"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = $"Error: {ex.Message}",
                inner = ex.InnerException?.Message
            });
        }
    }

    // ✅ DIAGNOSTIC: Endpoint untuk mengecek tabel Komponen secara spesifik
    [HttpGet("diagnostic/check-komponen")]
    public async Task<IActionResult> CheckKomponenTable()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("HossConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(new { success = false, message = "Connection string HossConnection tidak ditemukan" });
            }

            var tableInfo = new
            {
                TableName = "",
                Exists = false,
                Columns = new List<object>(),
                SampleData = new List<object>()
            };

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Cek apakah tabel Komponen ada (case-insensitive)
                var checkTableQuery = @"
                    SELECT TABLE_SCHEMA, TABLE_NAME 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_TYPE = 'BASE TABLE' 
                    AND (LOWER(TABLE_NAME) = 'komponen' OR LOWER(TABLE_NAME) = 'komponens')
                    ORDER BY TABLE_NAME";

                string? schema = null;
                string? tableName = null;

                using (var command = new SqlCommand(checkTableQuery, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            schema = reader.GetString(0);
                            tableName = reader.GetString(1);
                        }
                    }
                }

                if (string.IsNullOrEmpty(tableName))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Tabel 'Komponen' tidak ditemukan di database db_HOSS",
                        suggestion = "Periksa nama tabel yang benar di database"
                    });
                }

                // Ambil informasi kolom
                var columns = new List<object>();
                var columnsQuery = @"
                    SELECT 
                        COLUMN_NAME,
                        DATA_TYPE,
                        CHARACTER_MAXIMUM_LENGTH,
                        IS_NULLABLE,
                        COLUMN_DEFAULT,
                        ORDINAL_POSITION
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName
                    ORDER BY ORDINAL_POSITION";

                using (var command = new SqlCommand(columnsQuery, connection))
                {
                    command.Parameters.AddWithValue("@Schema", schema);
                    command.Parameters.AddWithValue("@TableName", tableName);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            columns.Add(new
                            {
                                ColumnName = reader.IsDBNull(0) ? null : reader.GetString(0),
                                DataType = reader.IsDBNull(1) ? null : reader.GetString(1),
                                MaxLength = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                IsNullable = reader.IsDBNull(3) ? null : reader.GetString(3),
                                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                                OrdinalPosition = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5)
                            });
                        }
                    }
                }

                // Ambil sample data (maksimal 5 baris)
                var sampleData = new List<object>();
                var sampleQuery = $"SELECT TOP 5 * FROM [{schema}].[{tableName}]";

                using (var command = new SqlCommand(sampleQuery, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object?>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var columnName = reader.GetName(i);
                                row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            sampleData.Add(row);
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    tableName = $"{schema}.{tableName}",
                    schema = schema,
                    actualTableName = tableName,
                    exists = true,
                    columns = columns,
                    sampleData = sampleData,
                    message = $"Tabel ditemukan: {schema}.{tableName} dengan {columns.Count} kolom"
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = $"Error: {ex.Message}",
                stackTrace = ex.StackTrace
            });
        }
    }

    [HttpGet("komponen/{partNumber}")]
    public async Task<IActionResult> GetKomponenByPartNumber(string partNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                return BadRequest(new { success = false, message = "Part Number tidak boleh kosong" });
            }

            var komponen = await _hossContext.Komponen
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

            var exists = await _hossContext.Komponen
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
        [FromForm] int komponenId)
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
            var komponen = await _hossContext.Komponen
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
                    ScannedJmlKomponen = komponen.JmlKomponen
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
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Data scan produksi berhasil disimpan.",
                jobRunId = activeJob.Id,
                partNumber,
                lotNumber,
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

