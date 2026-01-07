# Script untuk menggunakan SQL Server instance yang sudah ada
# Copy appsettings.Alternative.json ke appsettings.json

Write-Host "🔄 Switching to SQL Server instance..." -ForegroundColor Cyan
Write-Host ""

# Backup appsettings.json yang ada
if (Test-Path "appsettings.json") {
    $backupName = "appsettings.json.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item "appsettings.json" $backupName
    Write-Host "✅ Backed up current appsettings.json to: $backupName" -ForegroundColor Green
}

# Copy alternative config
if (Test-Path "appsettings.Alternative.json") {
    Copy-Item "appsettings.Alternative.json" "appsettings.json" -Force
    Write-Host "✅ Updated appsettings.json to use SQL Server (.\\SERVERVJEST)" -ForegroundColor Green
} else {
    Write-Host "❌ appsettings.Alternative.json not found!" -ForegroundColor Red
    Write-Host "   Creating it now..." -ForegroundColor Yellow
    
    $altConfig = @{
        ConnectionStrings = @{
            DefaultConnection = "Server=.\\SERVERVJEST;Database=OeeSystemDb;Trusted_Connection=True;MultipleActiveResultSets=true;Connection Timeout=30"
            HossConnection = "Server=.\\SERVERVJEST;Database=OeeSystemDb_Hoss;Trusted_Connection=True;MultipleActiveResultSets=true;Connection Timeout=30"
        }
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        AllowedHosts = "*"
    }
    
    $altConfig | ConvertTo-Json -Depth 10 | Set-Content "appsettings.Alternative.json"
    Copy-Item "appsettings.Alternative.json" "appsettings.json" -Force
    Write-Host "✅ Created and applied SQL Server configuration" -ForegroundColor Green
}

Write-Host ""
Write-Host "📋 Configuration:" -ForegroundColor Cyan
$config = Get-Content "appsettings.json" | ConvertFrom-Json
Write-Host "   DefaultConnection: $($config.ConnectionStrings.DefaultConnection)" -ForegroundColor Gray
Write-Host "   HossConnection: $($config.ConnectionStrings.HossConnection)" -ForegroundColor Gray

Write-Host ""
Write-Host "🚀 Ready to run!" -ForegroundColor Green
Write-Host "   Run: dotnet run" -ForegroundColor Yellow
Write-Host ""

