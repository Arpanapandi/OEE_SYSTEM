# Script untuk test koneksi database
# Script ini akan test koneksi ke SQL Server dan LocalDB

Write-Host "🔍 Testing Database Connections..." -ForegroundColor Cyan
Write-Host ""

# Test 1: LocalDB
Write-Host "[1/2] Testing LocalDB..." -ForegroundColor Yellow
$localdbCheck = where.exe sqllocaldb 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ LocalDB is installed" -ForegroundColor Green
    
    $instanceInfo = sqllocaldb info MSSQLLocalDB 2>&1
    if ($LASTEXITCODE -eq 0) {
        $isRunning = $instanceInfo | Select-String -Pattern "State: Running"
        if ($isRunning) {
            Write-Host "   ✅ MSSQLLocalDB is RUNNING" -ForegroundColor Green
            Write-Host "   ✅ LocalDB connection: READY" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  MSSQLLocalDB exists but not running" -ForegroundColor Yellow
            Write-Host "   🔄 Starting MSSQLLocalDB..." -ForegroundColor Cyan
            sqllocaldb start MSSQLLocalDB 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "   ✅ MSSQLLocalDB started" -ForegroundColor Green
                Write-Host "   ✅ LocalDB connection: READY" -ForegroundColor Green
            } else {
                Write-Host "   ❌ Failed to start MSSQLLocalDB" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "   ⚠️  MSSQLLocalDB instance not found" -ForegroundColor Yellow
        Write-Host "   💡 Will be created on first run" -ForegroundColor Gray
    }
} else {
    Write-Host "   ❌ LocalDB is NOT installed" -ForegroundColor Red
    Write-Host "   💡 Install: https://go.microsoft.com/fwlink/?LinkID=866658" -ForegroundColor Gray
}

Write-Host ""

# Test 2: SQL Server (.\\SERVERVJEST)
Write-Host "[2/2] Testing SQL Server (.\\SERVERVJEST)..." -ForegroundColor Yellow
try {
    $sqlConnection = New-Object System.Data.SqlClient.SqlConnection
    $sqlConnection.ConnectionString = "Server=.\\SERVERVJEST;Database=master;Trusted_Connection=True;Connection Timeout=5"
    $sqlConnection.Open()
    $sqlConnection.Close()
    Write-Host "   ✅ SQL Server (.\\SERVERVJEST) connection: SUCCESS" -ForegroundColor Green
} catch {
    Write-Host "   ❌ SQL Server (.\\SERVERVJEST) connection: FAILED" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host "   💡 Possible reasons:" -ForegroundColor Yellow
    Write-Host "      - SQL Server instance not running" -ForegroundColor Gray
    Write-Host "      - Instance name incorrect" -ForegroundColor Gray
    Write-Host "      - SQL Server not installed" -ForegroundColor Gray
    Write-Host "      - Windows Authentication issue" -ForegroundColor Gray
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "📋 Recommendation:" -ForegroundColor Yellow

$localdbInstalled = (where.exe sqllocaldb 2>&1) -and ($LASTEXITCODE -eq 0)
if ($localdbInstalled) {
    Write-Host "✅ Use LocalDB for development (offline)" -ForegroundColor Green
    Write-Host "   Update appsettings.json to use LocalDB" -ForegroundColor Gray
    Write-Host "   Connection: Server=(localdb)\MSSQLLocalDB" -ForegroundColor Gray
} else {
    Write-Host "⚠️  LocalDB not installed" -ForegroundColor Yellow
    Write-Host "   Option 1: Install LocalDB for offline development" -ForegroundColor Gray
    Write-Host "   Option 2: Fix SQL Server connection" -ForegroundColor Gray
}
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

