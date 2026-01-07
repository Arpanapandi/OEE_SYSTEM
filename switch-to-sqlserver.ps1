# Script untuk switch ke SQL Server (Production)
# Jalankan script ini untuk menggunakan SQL Server sebagai database

Write-Host "🔄 Switching to SQL Server (Production)..." -ForegroundColor Cyan
Write-Host ""

# Set environment variable untuk Production
$env:ASPNETCORE_ENVIRONMENT = "Production"
Write-Host "✅ Environment variable ASPNETCORE_ENVIRONMENT set to: Production" -ForegroundColor Green

# Cek apakah appsettings.json sudah ada
if (Test-Path "appsettings.json") {
    Write-Host "✅ appsettings.json found" -ForegroundColor Green
    
    # Cek apakah connection string sudah di-set ke SQL Server
    $appsettings = Get-Content "appsettings.json" -Raw | ConvertFrom-Json
    $defaultConn = $appsettings.ConnectionStrings.DefaultConnection
    $hossConn = $appsettings.ConnectionStrings.HossConnection
    
    if ($defaultConn -like "*10.14.149.34*" -and $hossConn -like "*10.14.149.34*") {
        Write-Host "✅ Connection strings are set to SQL Server (10.14.149.34)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Connection strings might not be set to SQL Server" -ForegroundColor Yellow
        Write-Host "   Please verify appsettings.json" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ appsettings.json not found!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🔍 Checking network connectivity to SQL Server..." -ForegroundColor Cyan
try {
    $ping = Test-Connection -ComputerName "10.14.149.34" -Count 1 -Quiet
    if ($ping) {
        Write-Host "✅ SQL Server (10.14.149.34) is reachable" -ForegroundColor Green
    } else {
        Write-Host "⚠️  SQL Server (10.14.149.34) might not be reachable" -ForegroundColor Yellow
        Write-Host "   Please check your network connection" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Could not check SQL Server connectivity: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📝 Configuration Summary:" -ForegroundColor Cyan
Write-Host "   - Environment: Production" -ForegroundColor Gray
Write-Host "   - Database: SQL Server (10.14.149.34)" -ForegroundColor Gray
Write-Host "   - DefaultConnection: OeeSystemDb" -ForegroundColor Gray
Write-Host "   - HossConnection: DB_HOSE" -ForegroundColor Gray

Write-Host ""
Write-Host "🚀 Ready to run application with SQL Server!" -ForegroundColor Green
Write-Host "   Run: dotnet run" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  WARNING: Make sure you have:" -ForegroundColor Yellow
Write-Host "   1. Network connection to SQL Server" -ForegroundColor Yellow
Write-Host "   2. Valid database credentials" -ForegroundColor Yellow
Write-Host "   3. Required permissions on databases" -ForegroundColor Yellow
Write-Host ""

