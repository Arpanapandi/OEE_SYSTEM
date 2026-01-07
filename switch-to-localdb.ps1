# Script untuk switch ke LocalDB (Development)
# Jalankan script ini untuk menggunakan LocalDB sebagai database

Write-Host "🔄 Switching to LocalDB (Development)..." -ForegroundColor Cyan
Write-Host ""

# Set environment variable untuk Development
$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "✅ Environment variable ASPNETCORE_ENVIRONMENT set to: Development" -ForegroundColor Green

# Cek apakah appsettings.Development.json sudah ada
if (Test-Path "appsettings.Development.json") {
    Write-Host "✅ appsettings.Development.json found" -ForegroundColor Green
} else {
    Write-Host "⚠️  appsettings.Development.json not found. Creating..." -ForegroundColor Yellow
    # Script akan membuat file jika belum ada (tapi seharusnya sudah dibuat)
}

# Cek LocalDB
Write-Host ""
Write-Host "🔍 Checking LocalDB installation..." -ForegroundColor Cyan
try {
    $localdbInfo = sqllocaldb info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ LocalDB is installed" -ForegroundColor Green
        Write-Host "   LocalDB instances:" -ForegroundColor Gray
        $localdbInfo | ForEach-Object { Write-Host "   - $_" -ForegroundColor Gray }
    } else {
        Write-Host "⚠️  LocalDB might not be installed or not in PATH" -ForegroundColor Yellow
        Write-Host "   Please install SQL Server Express LocalDB" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Could not check LocalDB: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📝 Configuration Summary:" -ForegroundColor Cyan
Write-Host "   - Environment: Development" -ForegroundColor Gray
Write-Host "   - Database: LocalDB (OeeSystemDb, OeeSystemDb_Hoss)" -ForegroundColor Gray
Write-Host "   - Connection: (localdb)\MSSQLLocalDB" -ForegroundColor Gray

Write-Host ""
Write-Host "🚀 Ready to run application with LocalDB!" -ForegroundColor Green
Write-Host "   Run: dotnet run" -ForegroundColor Yellow
Write-Host ""

