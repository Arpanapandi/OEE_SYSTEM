# Script untuk menjalankan aplikasi dengan SQL Server yang sudah ada
# Script ini akan menggunakan SQL Server instance yang tersedia

Write-Host "🚀 Starting OEE System with SQL Server..." -ForegroundColor Cyan
Write-Host ""

# Cek apakah ada appsettings dengan SQL Server
if (Test-Path "appsettings.Production.json") {
    Write-Host "✅ Found appsettings.Production.json" -ForegroundColor Green
    Write-Host "   Using SQL Server connection from Production config" -ForegroundColor Gray
} else {
    Write-Host "⚠️  appsettings.Production.json not found" -ForegroundColor Yellow
    Write-Host "   Will use connection string from appsettings.json" -ForegroundColor Gray
}

Write-Host ""
Write-Host "📋 Current Configuration:" -ForegroundColor Cyan
$config = Get-Content "appsettings.json" | ConvertFrom-Json
$defaultConn = $config.ConnectionStrings.DefaultConnection
$hossConn = $config.ConnectionStrings.HossConnection

Write-Host "   DefaultConnection: $defaultConn" -ForegroundColor Gray
Write-Host "   HossConnection: $hossConn" -ForegroundColor Gray

Write-Host ""
Write-Host "🌐 Starting application..." -ForegroundColor Cyan
Write-Host "   Access at: http://localhost:6001" -ForegroundColor Yellow
Write-Host ""

# Run application
dotnet run

