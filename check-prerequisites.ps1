# Script untuk check semua prerequisites yang diperlukan
# Script ini akan check dan memberikan instruksi install jika ada yang kurang

Write-Host "🔍 Checking Prerequisites for OEE System..." -ForegroundColor Cyan
Write-Host ""

$allGood = $true

# 1. Check .NET SDK
Write-Host "[1/3] Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ .NET SDK installed: $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Host "   ❌ .NET SDK not found" -ForegroundColor Red
        $allGood = $false
    }
} catch {
    Write-Host "   ❌ .NET SDK not found" -ForegroundColor Red
    $allGood = $false
}

if (-not $allGood -or $LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "   💡 Install .NET SDK:" -ForegroundColor Yellow
    Write-Host "      Download: https://dotnet.microsoft.com/download" -ForegroundColor Gray
    Write-Host "      Install .NET 8.0 SDK or later" -ForegroundColor Gray
}

# 2. Check LocalDB
Write-Host ""
Write-Host "[2/3] Checking SQL Server LocalDB..." -ForegroundColor Yellow
$localdbInstalled = $false
try {
    $localdbCheck = where.exe sqllocaldb 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ LocalDB is installed" -ForegroundColor Green
        $localdbInstalled = $true
        
        # Check instance
        $instanceInfo = sqllocaldb info MSSQLLocalDB 2>&1
        if ($LASTEXITCODE -eq 0) {
            $isRunning = $instanceInfo | Select-String -Pattern "State: Running"
            if ($isRunning) {
                Write-Host "   ✅ MSSQLLocalDB instance is RUNNING" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️  MSSQLLocalDB instance exists but not running" -ForegroundColor Yellow
                Write-Host "   🔄 Starting instance..." -ForegroundColor Cyan
                sqllocaldb start MSSQLLocalDB 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "   ✅ MSSQLLocalDB started successfully" -ForegroundColor Green
                } else {
                    Write-Host "   ⚠️  Could not start MSSQLLocalDB automatically" -ForegroundColor Yellow
                }
            }
        } else {
            Write-Host "   ⚠️  MSSQLLocalDB instance not found, will be created on first run" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ❌ LocalDB is NOT installed" -ForegroundColor Red
        $allGood = $false
    }
} catch {
    Write-Host "   ❌ LocalDB is NOT installed" -ForegroundColor Red
    $allGood = $false
}

if (-not $localdbInstalled) {
    Write-Host ""
    Write-Host "   💡 Install LocalDB:" -ForegroundColor Yellow
    Write-Host "      Download: https://go.microsoft.com/fwlink/?LinkID=866658" -ForegroundColor Gray
    Write-Host "      Or: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -ForegroundColor Gray
    Write-Host "      Select 'Express' edition → 'Basic' installation" -ForegroundColor Gray
}

# 3. Check appsettings.json
Write-Host ""
Write-Host "[3/3] Checking Configuration..." -ForegroundColor Yellow
if (Test-Path "appsettings.json") {
    $config = Get-Content "appsettings.json" | ConvertFrom-Json
    $connString = $config.ConnectionStrings.DefaultConnection
    
    if ($connString -like "*(localdb)*") {
        Write-Host "   ✅ appsettings.json configured for LocalDB" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  appsettings.json not using LocalDB" -ForegroundColor Yellow
        Write-Host "      Current: $($connString.Substring(0, [Math]::Min(50, $connString.Length)))..." -ForegroundColor Gray
    }
} else {
    Write-Host "   ❌ appsettings.json not found" -ForegroundColor Red
    $allGood = $false
}

# Summary
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
if ($allGood -and $localdbInstalled) {
    Write-Host "✅ All prerequisites are ready!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🚀 You can now run the application:" -ForegroundColor Yellow
    Write-Host "   dotnet run" -ForegroundColor White
    Write-Host ""
    Write-Host "   Or use the automated script:" -ForegroundColor Gray
    Write-Host "   .\install-and-run.bat" -ForegroundColor White
} else {
    Write-Host "⚠️  Some prerequisites are missing!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "📋 Next Steps:" -ForegroundColor Cyan
    if (-not $localdbInstalled) {
        Write-Host "   1. Install LocalDB (see instructions above)" -ForegroundColor White
        Write-Host "   2. Restart this PowerShell window" -ForegroundColor White
        Write-Host "   3. Run this script again: .\check-prerequisites.ps1" -ForegroundColor White
        Write-Host "   4. Or run: .\install-and-run.bat" -ForegroundColor White
    } else {
        Write-Host "   1. Fix the issues above" -ForegroundColor White
        Write-Host "   2. Run: .\install-and-run.bat" -ForegroundColor White
    }
}
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

