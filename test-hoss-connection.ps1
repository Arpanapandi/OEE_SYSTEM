# Script untuk test koneksi ke DB_HOSS tabel dbo_komponen
# Pastikan aplikasi sudah di-build

Write-Host "🔍 Testing Connection to DB_HOSS.dbo_komponen..." -ForegroundColor Yellow
Write-Host ""

# Build aplikasi terlebih dahulu
Write-Host "1️⃣  Building application..." -ForegroundColor Cyan
dotnet build --no-incremental 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "   ✅ Build successful" -ForegroundColor Green
Write-Host ""

# Buat file C# sederhana untuk test koneksi
$testScript = @"
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OeeSystem.Data;
using OeeSystem.Models;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var configuration = builder.Build();

var connectionString = configuration.GetConnectionString("HossConnection");

Console.WriteLine("📡 Connection String:");
Console.WriteLine($"   Server: 10.14.149.34");
Console.WriteLine($"   Database: DB_HOSS");
Console.WriteLine($"   User: usrvelasto");
Console.WriteLine("");

var optionsBuilder = new DbContextOptionsBuilder<HossDbContext>();
optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
{
    sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(30),
        errorNumbersToAdd: null);
});

try
{
    using var context = new HossDbContext(optionsBuilder.Options);
    
    Console.WriteLine("🔌 Testing connection...");
    var canConnect = context.Database.CanConnect();
    
    if (canConnect)
    {
        Console.WriteLine("   ✅ Connection successful!");
        Console.WriteLine("");
        
        Console.WriteLine("📊 Testing table dbo_komponen...");
        var tableExists = context.Database.ExecuteSqlRaw(@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'komponen'
        ");
        
        var komponenCount = context.Komponen.Count();
        Console.WriteLine($"   ✅ Table dbo_komponen exists");
        Console.WriteLine($"   📈 Total records: {komponenCount}");
        Console.WriteLine("");
        
        if (komponenCount > 0)
        {
            Console.WriteLine("📋 Sample data (first 5 records):");
            var samples = context.Komponen
                .Select(k => new { k.Id, k.PartNumber, k.JmlKomponen })
                .Take(5)
                .ToList();
            
            foreach (var item in samples)
            {
                Console.WriteLine($"   - ID: {item.Id}, Part Number: {item.PartNumber}, Jml Komponen: {item.JmlKomponen ?? 0}");
            }
        }
        else
        {
            Console.WriteLine("   ⚠️  Table is empty");
        }
        
        Console.WriteLine("");
        Console.WriteLine("✅ All tests passed!");
    }
    else
    {
        Console.WriteLine("   ❌ Cannot connect to database");
        Environment.Exit(1);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"   ❌ Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
    Environment.Exit(1);
}
"@

$testScript | Out-File -FilePath "TestHossConnection.cs" -Encoding UTF8

Write-Host "2️⃣  Running connection test..." -ForegroundColor Cyan
Write-Host ""

# Jalankan test menggunakan dotnet-script atau compile langsung
dotnet run --project . --no-build -- TestHossConnection 2>&1

# Cleanup
Remove-Item "TestHossConnection.cs" -ErrorAction SilentlyContinue

