using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;

namespace OeeSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestHossController : ControllerBase
{
    private readonly HossDbContext _context;
    private readonly ILogger<TestHossController> _logger;

    public TestHossController(HossDbContext context, ILogger<TestHossController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            // Test koneksi
            var canConnect = await _context.Database.CanConnectAsync();
            
            if (!canConnect)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Cannot connect to database",
                    server = "10.14.149.34",
                    database = "DB_HOSE"
                });
            }

            // Test table exists - cek dengan query yang benar
            var tableExistsQuery = await _context.Database.SqlQueryRaw<int>(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'komponen'
            ").FirstOrDefaultAsync();

            if (tableExistsQuery == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Table dbo.komponen does not exist",
                    server = "10.14.149.34",
                    database = "DB_HOSE"
                });
            }

            // Get record count
            var recordCount = await _context.Komponen.CountAsync();

            // Get sample data
            var samples = await _context.Komponen
                .Select(k => new
                {
                    k.Id,
                    k.PartNumber,
                    k.JmlKomponen
                })
                .Take(5)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                message = "Connection successful",
                server = "10.14.149.34",
                database = "DB_HOSE",
                table = "dbo.komponen",
                recordCount = recordCount,
                samples = samples
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing HOSS connection");
            return StatusCode(500, new
            {
                success = false,
                message = $"Error: {ex.Message}",
                inner = ex.InnerException?.Message,
                server = "10.14.149.34",
                database = "DB_HOSE"
            });
        }
    }

    [HttpGet("komponen/count")]
    public async Task<IActionResult> GetKomponenCount()
    {
        try
        {
            var count = await _context.Komponen.CountAsync();
            return Ok(new { success = true, count = count });
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

    [HttpGet("tables")]
    public async Task<IActionResult> ListTables()
    {
        try
        {
            // List semua tabel di database
            var tables = await _context.Database.SqlQueryRaw<string>(@"
                SELECT TABLE_SCHEMA + '.' + TABLE_NAME as TableName
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME
            ").ToListAsync();

            // Cari tabel yang mengandung 'komponen'
            var komponenTables = tables.Where(t => t.ToLower().Contains("komponen")).ToList();

            return Ok(new
            {
                success = true,
                message = "Tables retrieved successfully",
                totalTables = tables.Count,
                komponenTables = komponenTables,
                allTables = tables
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

    [HttpGet("check-table/{schema}/{table}")]
    public async Task<IActionResult> CheckTable(string schema, string table)
    {
        try
        {
            // Cek apakah tabel ada
            var tableExists = await _context.Database.SqlQueryRaw<int>(@"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = {0} AND TABLE_NAME = {1}
            ", schema, table).FirstOrDefaultAsync();

            if (tableExists > 0)
            {
                // Get column info
                var columns = await _context.Database.SqlQueryRaw<dynamic>(@"
                    SELECT 
                        COLUMN_NAME,
                        DATA_TYPE,
                        IS_NULLABLE,
                        CHARACTER_MAXIMUM_LENGTH
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = {0} AND TABLE_NAME = {1}
                    ORDER BY ORDINAL_POSITION
                ", schema, table).ToListAsync();

                // Get row count
                var rowCount = await _context.Database.SqlQueryRaw<int>($@"
                    SELECT COUNT(*) FROM [{schema}].[{table}]
                ").FirstOrDefaultAsync();

                return Ok(new
                {
                    success = true,
                    message = "Table exists",
                    schema = schema,
                    table = table,
                    rowCount = rowCount,
                    columns = columns
                });
            }
            else
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Table {schema}.{table} does not exist"
                });
            }
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
}

