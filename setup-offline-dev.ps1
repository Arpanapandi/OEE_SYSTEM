# Script untuk setup development offline dengan LocalDB
# Script ini akan membantu setup setelah LocalDB terinstall

Write-Host "🔧 Setting up Offline Development with LocalDB..." -ForegroundColor Cyan
Write-Host ""

# Cek apakah LocalDB terinstall
Write-Host "📋 Checking LocalDB installation..." -ForegroundColor Cyan
try {
    $localdbCheck = sqllocaldb info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ LocalDB is installed!" -ForegroundColor Green
        Write-Host ""
        
        # Cek instance MSSQLLocalDB
        Write-Host "📋 Checking MSSQLLocalDB instance..." -ForegroundColor Cyan
        $instanceInfo = sqllocaldb info MSSQLLocalDB 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ MSSQLLocalDB instance exists" -ForegroundColor Green
            
            # Cek apakah running
            $isRunning = $instanceInfo | Select-String -Pattern "State: Running"
            if ($isRunning) {
                Write-Host "✅ MSSQLLocalDB is RUNNING" -ForegroundColor Green
            } else {
                Write-Host "⚠️  MSSQLLocalDB is not running. Starting..." -ForegroundColor Yellow
                sqllocaldb start MSSQLLocalDB 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ MSSQLLocalDB started successfully" -ForegroundColor Green
                }
            }
        } else {
            Write-Host "⚠️  MSSQLLocalDB instance not found. Creating..." -ForegroundColor Yellow
            sqllocaldb create MSSQLLocalDB 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ MSSQLLocalDB instance created" -ForegroundColor Green
                sqllocaldb start MSSQLLocalDB 2>&1 | Out-Null
            }
        }
    } else {
        Write-Host "❌ LocalDB is NOT installed!" -ForegroundColor Red
        Write-Host ""
        Write-Host "💡 Please install LocalDB first:" -ForegroundColor Yellow
        Write-Host "   1. Download SQL Server Express: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -ForegroundColor Gray
        Write-Host "   2. Or install via Visual Studio Installer (Individual Components > SQL Server Express LocalDB)" -ForegroundColor Gray
        Write-Host "   3. After installation, restart PowerShell and run this script again" -ForegroundColor Gray
        Write-Host ""
        Write-Host "   See INSTALL-LOCALDB.md for detailed instructions" -ForegroundColor Cyan
        exit 1
    }
} catch {
    Write-Host "❌ Error checking LocalDB: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 LocalDB might not be installed. See INSTALL-LOCALDB.md for installation guide" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "📝 Updating appsettings.json for LocalDB..." -ForegroundColor Cyan

# Backup appsettings.json jika belum pakai LocalDB
$currentConfig = Get-Content "appsettings.json" | ConvertFrom-Json
$currentConn = $currentConfig.ConnectionStrings.DefaultConnection

if ($currentConn -notlike "*(localdb)*") {
    # Backup current config
    $backupName = "appsettings.json.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item "appsettings.json" $backupName
    Write-Host "✅ Backed up current config to: $backupName" -ForegroundColor Green
    
    # Update to LocalDB
    $localDbConfig = @{
        ConnectionStrings = @{
            DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=OeeSystemDb;Trusted_Connection=True;MultipleActiveResultSets=true"
            HossConnection = "Server=(localdb)\MSSQLLocalDB;Database=OeeSystemDb_Hoss;Trusted_Connection=True;MultipleActiveResultSets=true"
        }
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        AllowedHosts = "*"
    }
    
    $localDbConfig | ConvertTo-Json -Depth 10 | Set-Content "appsettings.json"
    Write-Host "✅ Updated appsettings.json to use LocalDB" -ForegroundColor Green
} else {
    Write-Host "✅ appsettings.json already using LocalDB" -ForegroundColor Green
}

Write-Host ""
Write-Host "📋 Current Configuration:" -ForegroundColor Cyan
$config = Get-Content "appsettings.json" | ConvertFrom-Json
Write-Host "   DefaultConnection: $($config.ConnectionStrings.DefaultConnection)" -ForegroundColor Gray
Write-Host "   HossConnection: $($config.ConnectionStrings.HossConnection)" -ForegroundColor Gray

Write-Host ""
Write-Host "✅ Setup completed!" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 You can now run the application:" -ForegroundColor Yellow
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "💡 The database will be created automatically on first run!" -ForegroundColor Cyan
Write-Host ""

