# Script untuk memeriksa dan memastikan LocalDB berjalan
# Jalankan script ini sebelum menjalankan aplikasi

Write-Host "🔍 Checking LocalDB installation and status..." -ForegroundColor Cyan
Write-Host ""

# Cek apakah sqllocaldb command tersedia
try {
    $localdbVersion = sqllocaldb info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ LocalDB is installed" -ForegroundColor Green
        Write-Host ""
        
        # Cek status instance MSSQLLocalDB
        Write-Host "📋 Checking MSSQLLocalDB instance..." -ForegroundColor Cyan
        $instanceInfo = sqllocaldb info MSSQLLocalDB 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ MSSQLLocalDB instance exists" -ForegroundColor Green
            Write-Host ""
            
            # Cek apakah instance sedang berjalan
            $isRunning = $instanceInfo | Select-String -Pattern "State: Running"
            if ($isRunning) {
                Write-Host "✅ MSSQLLocalDB instance is RUNNING" -ForegroundColor Green
            } else {
                Write-Host "⚠️  MSSQLLocalDB instance is NOT running" -ForegroundColor Yellow
                Write-Host "   Attempting to start..." -ForegroundColor Cyan
                
                $startResult = sqllocaldb start MSSQLLocalDB 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ MSSQLLocalDB instance started successfully" -ForegroundColor Green
                } else {
                    Write-Host "❌ Failed to start MSSQLLocalDB" -ForegroundColor Red
                    Write-Host "   Error: $startResult" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "⚠️  MSSQLLocalDB instance not found" -ForegroundColor Yellow
            Write-Host "   Creating MSSQLLocalDB instance..." -ForegroundColor Cyan
            
            $createResult = sqllocaldb create MSSQLLocalDB 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ MSSQLLocalDB instance created" -ForegroundColor Green
                
                Write-Host "   Starting instance..." -ForegroundColor Cyan
                $startResult = sqllocaldb start MSSQLLocalDB 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ MSSQLLocalDB instance started" -ForegroundColor Green
                }
            } else {
                Write-Host "❌ Failed to create MSSQLLocalDB instance" -ForegroundColor Red
                Write-Host "   Error: $createResult" -ForegroundColor Red
            }
        }
        
        Write-Host ""
        Write-Host "📊 LocalDB Instances:" -ForegroundColor Cyan
        $allInstances = sqllocaldb info 2>&1
        $allInstances | ForEach-Object { 
            if ($_ -match "MSSQLLocalDB") {
                Write-Host "   ✅ $_" -ForegroundColor Green
            } else {
                Write-Host "   - $_" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "❌ LocalDB command failed" -ForegroundColor Red
        Write-Host "   Error: $localdbVersion" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ LocalDB is NOT installed or not in PATH" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 To install LocalDB:" -ForegroundColor Yellow
    Write-Host "   1. Download SQL Server Express LocalDB from Microsoft" -ForegroundColor Gray
    Write-Host "   2. Or install Visual Studio (includes LocalDB)" -ForegroundColor Gray
    Write-Host "   3. Or install SQL Server Express with LocalDB option" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Download: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -ForegroundColor Cyan
    exit 1
}

Write-Host ""
Write-Host "✅ LocalDB check completed!" -ForegroundColor Green
Write-Host "   You can now run the application with: dotnet run" -ForegroundColor Yellow
Write-Host ""

