# Script otomatis install LocalDB untuk development
# Jalankan dengan: powershell -ExecutionPolicy Bypass -File install-localdb-full-auto.ps1

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Auto Install LocalDB - Full Automatic" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Cek LocalDB
Write-Host "[Step 1/5] Checking LocalDB..." -ForegroundColor Yellow
$localdbExists = Get-Command sqllocaldb -ErrorAction SilentlyContinue

if ($localdbExists) {
    Write-Host "[OK] LocalDB sudah terinstall!" -ForegroundColor Green
    sqllocaldb info
    Write-Host ""
    $skipInstall = $true
} else {
    Write-Host "[INFO] LocalDB belum terinstall, akan diinstall..." -ForegroundColor Yellow
    $skipInstall = $false
}

if (-not $skipInstall) {
    # Step 2: Cek Chocolatey
    Write-Host "[Step 2/5] Checking Chocolatey..." -ForegroundColor Yellow
    $chocoExists = Get-Command choco -ErrorAction SilentlyContinue
    
    if ($chocoExists) {
        Write-Host "[OK] Chocolatey tersedia, install via Chocolatey..." -ForegroundColor Green
        Write-Host "[INFO] Menginstall SQL Server Express LocalDB..." -ForegroundColor Cyan
        Write-Host "[INFO] Ini mungkin memerlukan beberapa menit..." -ForegroundColor Gray
        Write-Host ""
        
        try {
            choco install sql-server-express-localdb -y
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[OK] Install berhasil via Chocolatey!" -ForegroundColor Green
                $installed = $true
            } else {
                Write-Host "[WARNING] Install via Chocolatey gagal" -ForegroundColor Yellow
                $installed = $false
            }
        } catch {
            Write-Host "[WARNING] Error saat install via Chocolatey" -ForegroundColor Yellow
            $installed = $false
        }
    } else {
        Write-Host "[INFO] Chocolatey tidak tersedia" -ForegroundColor Gray
        $installed = $false
    }
    
    # Step 3: Download dan install manual jika perlu
    if (-not $installed) {
        Write-Host ""
        Write-Host "[Step 3/5] Download dan Install Manual" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "  Install LocalDB - Manual Method" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Cara termudah:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "1. Via Visual Studio Installer (Recommended):" -ForegroundColor Cyan
        Write-Host "   - Buka 'Visual Studio Installer'" -ForegroundColor White
        Write-Host "   - Klik 'Modify' pada Visual Studio" -ForegroundColor White
        Write-Host "   - Tab 'Individual Components'" -ForegroundColor White
        Write-Host "   - Centang 'SQL Server Express LocalDB'" -ForegroundColor White
        Write-Host "   - Klik 'Modify'" -ForegroundColor White
        Write-Host ""
        Write-Host "2. Download SQL Server Express:" -ForegroundColor Cyan
        Write-Host "   Link: https://go.microsoft.com/fwlink/?LinkID=866658" -ForegroundColor White
        Write-Host "   Pilih 'Basic' installation" -ForegroundColor White
        Write-Host ""
        
        # Buka halaman download
        Write-Host "Membuka halaman download..." -ForegroundColor Yellow
        Start-Process "https://go.microsoft.com/fwlink/?LinkID=866658"
        
        Write-Host ""
        Write-Host "Setelah install selesai:" -ForegroundColor Yellow
        Write-Host "  1. Restart PowerShell/Command Prompt" -ForegroundColor White
        Write-Host "  2. Jalankan script ini lagi" -ForegroundColor White
        Write-Host "  3. Atau jalankan: setup-localdb-dev.bat" -ForegroundColor White
        Write-Host ""
        Write-Host "Tekan Enter setelah install selesai untuk melanjutkan..." -ForegroundColor Cyan
        Read-Host
        
        # Cek lagi setelah user install
        $localdbExists = Get-Command sqllocaldb -ErrorAction SilentlyContinue
        if (-not $localdbExists) {
            Write-Host "[ERROR] LocalDB masih belum terdeteksi" -ForegroundColor Red
            Write-Host "[INFO] Pastikan install selesai dan restart PowerShell" -ForegroundColor Yellow
            exit 1
        }
    }
    
    # Refresh PATH
    Write-Host "[INFO] Refresh PATH..." -ForegroundColor Gray
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
}

# Step 4: Setup instance
Write-Host ""
Write-Host "[Step 4/5] Setting up MSSQLLocalDB instance..." -ForegroundColor Yellow

$instanceInfo = sqllocaldb info MSSQLLocalDB 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Creating MSSQLLocalDB instance..." -ForegroundColor Cyan
    sqllocaldb create MSSQLLocalDB 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Gagal membuat instance" -ForegroundColor Red
        Write-Host "[INFO] Coba jalankan sebagai Administrator" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "[OK] Instance berhasil dibuat" -ForegroundColor Green
} else {
    Write-Host "[OK] Instance sudah ada" -ForegroundColor Green
}

Write-Host "Starting MSSQLLocalDB instance..." -ForegroundColor Cyan
sqllocaldb start MSSQLLocalDB 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Instance berjalan!" -ForegroundColor Green
} else {
    Write-Host "[WARNING] Gagal start otomatis, mencoba manual..." -ForegroundColor Yellow
    sqllocaldb start MSSQLLocalDB
}

# Step 5: Update appsettings.json
Write-Host ""
Write-Host "[Step 5/5] Updating appsettings.json..." -ForegroundColor Yellow

if (Test-Path "appsettings.json") {
    $content = Get-Content "appsettings.json" -Raw
    if ($content -notmatch "\(localdb\)") {
        $backupName = "appsettings.json.backup.$(Get-Date -Format 'yyyy-MM-dd-HHmmss')"
        Copy-Item "appsettings.json" $backupName
        Write-Host "[OK] Backup dibuat: $backupName" -ForegroundColor Green
    }
}

$config = @{
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

$config | ConvertTo-Json -Depth 10 | Set-Content "appsettings.json" -Encoding UTF8
Write-Host "[OK] appsettings.json updated!" -ForegroundColor Green

# Final verification
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Verifikasi Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$instanceState = sqllocaldb info MSSQLLocalDB 2>&1 | Select-String "State:"
if ($instanceState -match "Running") {
    Write-Host "[OK] MSSQLLocalDB instance berjalan" -ForegroundColor Green
} else {
    Write-Host "[INFO] Instance akan di-start otomatis saat aplikasi berjalan" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ✅ Setup Selesai!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "LocalDB sudah terinstall dan dikonfigurasi untuk development" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 Proyek siap digunakan! Jalankan:" -ForegroundColor Yellow
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "📱 Aplikasi akan tersedia di:" -ForegroundColor Cyan
Write-Host "   - HTTP:  http://localhost:6001" -ForegroundColor Gray
Write-Host "   - HTTPS: https://localhost:6002" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Database akan dibuat otomatis saat pertama kali run" -ForegroundColor Gray
Write-Host ""

