# Script untuk menjalankan aplikasi dengan Development mode (LocalDB)
# Pastikan environment variable di-set sebelum menjalankan aplikasi

Write-Host "🚀 Starting OEE System in Development Mode (LocalDB)..." -ForegroundColor Cyan
Write-Host ""

# Set environment variable
$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "✅ Environment variable set: ASPNETCORE_ENVIRONMENT = Development" -ForegroundColor Green

# Verify appsettings.Development.json exists
if (Test-Path "appsettings.Development.json") {
    Write-Host "✅ appsettings.Development.json found" -ForegroundColor Green
} else {
    Write-Host "❌ appsettings.Development.json NOT FOUND!" -ForegroundColor Red
    Write-Host "   Please create this file with LocalDB connection strings" -ForegroundColor Yellow
    exit 1
}

# Display connection strings (without password)
Write-Host ""
Write-Host "📋 Configuration:" -ForegroundColor Cyan
$devConfig = Get-Content "appsettings.Development.json" | ConvertFrom-Json
$defaultConn = $devConfig.ConnectionStrings.DefaultConnection
$hossConn = $devConfig.ConnectionStrings.HossConnection

if ($defaultConn -like "*(localdb)*") {
    Write-Host "   ✅ DefaultConnection: LocalDB" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  DefaultConnection: NOT LocalDB!" -ForegroundColor Yellow
    Write-Host "      $defaultConn" -ForegroundColor Gray
}

if ($hossConn -like "*(localdb)*") {
    Write-Host "   ✅ HossConnection: LocalDB" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  HossConnection: NOT LocalDB!" -ForegroundColor Yellow
    Write-Host "      $hossConn" -ForegroundColor Gray
}

Write-Host ""
Write-Host "🌐 Starting application..." -ForegroundColor Cyan
Write-Host "   Access at: http://localhost:6001" -ForegroundColor Yellow
Write-Host ""

# Run application
dotnet run

