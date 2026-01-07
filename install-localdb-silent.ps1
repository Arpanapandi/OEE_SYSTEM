# Script PowerShell untuk install LocalDB secara otomatis dengan mode silent
# Script ini memerlukan administrator privileges

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Auto Install LocalDB (Silent Mode)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "[WARNING] This script requires administrator privileges" -ForegroundColor Yellow
    Write-Host "[INFO] Attempting to run as administrator..." -ForegroundColor Cyan
    Write-Host ""
    
    # Relaunch as administrator
    $scriptPath = $MyInvocation.MyCommand.Path
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""
    exit
}

# Check if LocalDB already installed
Write-Host "[1/4] Checking LocalDB installation..." -ForegroundColor Yellow
$localdbCheck = where.exe sqllocaldb 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] LocalDB is already installed!" -ForegroundColor Green
    sqllocaldb info
    Write-Host ""
    Write-Host "Setup will continue..." -ForegroundColor Cyan
} else {
    Write-Host "[INFO] LocalDB is NOT installed" -ForegroundColor Yellow
    Write-Host "[INFO] Starting automatic installation..." -ForegroundColor Cyan
    Write-Host ""
    
    # Create temp directory
    $tempDir = Join-Path $env:TEMP "LocalDB_Install"
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }
    
    # Download URL
    $downloadUrl = "https://go.microsoft.com/fwlink/?LinkID=866658"
    $installerPath = Join-Path $tempDir "SQLEXPR_x64_ENU.exe"
    
    # Download installer
    Write-Host "[2/4] Downloading SQL Server Express LocalDB..." -ForegroundColor Yellow
    Write-Host "  URL: $downloadUrl" -ForegroundColor Gray
    Write-Host "  Save to: $installerPath" -ForegroundColor Gray
    Write-Host ""
    
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath -UseBasicParsing
        Write-Host "[OK] Download completed!" -ForegroundColor Green
        Write-Host ""
    } catch {
        Write-Host "[ERROR] Failed to download installer: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "[INFO] Please download manually:" -ForegroundColor Yellow
        Write-Host "  1. Open: $downloadUrl" -ForegroundColor Gray
        Write-Host "  2. Save the installer" -ForegroundColor Gray
        Write-Host "  3. Run the installer" -ForegroundColor Gray
        Write-Host ""
        Start-Process $downloadUrl
        exit 1
    }
    
    # Install LocalDB with silent mode
    Write-Host "[3/4] Installing LocalDB (this may take a few minutes)..." -ForegroundColor Yellow
    Write-Host "[INFO] Installation is running in background..." -ForegroundColor Gray
    Write-Host ""
    
    $installArgs = @(
        "/ACTION=Install",
        "/FEATURES=LocalDB",
        "/IACCEPTSQLSERVERLICENSETERMS",
        "/Q",
        "/HIDECONSOLE"
    )
    
    try {
        $process = Start-Process -FilePath $installerPath -ArgumentList $installArgs -Wait -PassThru -NoNewWindow
        
        if ($process.ExitCode -eq 0) {
            Write-Host "[OK] Installation completed!" -ForegroundColor Green
        } else {
            Write-Host "[WARNING] Installation may have issues. Exit code: $($process.ExitCode)" -ForegroundColor Yellow
            Write-Host "[INFO] Trying interactive installation..." -ForegroundColor Cyan
            Start-Process -FilePath $installerPath -Wait
        }
    } catch {
        Write-Host "[ERROR] Installation failed: $_" -ForegroundColor Red
        Write-Host "[INFO] Please install manually:" -ForegroundColor Yellow
        Write-Host "  1. Run: $installerPath" -ForegroundColor Gray
        Write-Host "  2. Select 'Basic' installation" -ForegroundColor Gray
        Write-Host "  3. Wait for completion" -ForegroundColor Gray
        Start-Process $installerPath
        exit 1
    }
    
    # Clean up
    if (Test-Path $installerPath) {
        Remove-Item $installerPath -Force
    }
    
    # Wait a bit for PATH to refresh
    Start-Sleep -Seconds 3
    
    # Verify installation
    Write-Host "[4/4] Verifying installation..." -ForegroundColor Yellow
    $localdbCheck = where.exe sqllocaldb 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] LocalDB installed successfully!" -ForegroundColor Green
        sqllocaldb info
    } else {
        Write-Host "[WARNING] LocalDB may not be in PATH yet" -ForegroundColor Yellow
        Write-Host "[INFO] Please restart command prompt and verify:" -ForegroundColor Cyan
        Write-Host "  sqllocaldb info" -ForegroundColor Gray
        exit 1
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Setting up LocalDB for Development" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Setup MSSQLLocalDB instance
Write-Host "Setting up MSSQLLocalDB instance..." -ForegroundColor Yellow
$instanceInfo = sqllocaldb info MSSQLLocalDB 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Creating MSSQLLocalDB instance..." -ForegroundColor Cyan
    sqllocaldb create MSSQLLocalDB 2>&1 | Out-Null
}

Write-Host "Starting MSSQLLocalDB instance..." -ForegroundColor Cyan
sqllocaldb start MSSQLLocalDB 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] MSSQLLocalDB is ready!" -ForegroundColor Green
} else {
    Write-Host "[WARNING] Could not start MSSQLLocalDB automatically" -ForegroundColor Yellow
}

# Update appsettings.json
Write-Host ""
Write-Host "Updating appsettings.json for LocalDB..." -ForegroundColor Yellow
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

$config | ConvertTo-Json -Depth 10 | Set-Content "appsettings.json"
Write-Host "[OK] appsettings.json updated!" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ LocalDB installed and configured" -ForegroundColor Green
Write-Host "✅ MSSQLLocalDB instance is ready" -ForegroundColor Green
Write-Host "✅ appsettings.json updated for LocalDB" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 You can now run the application:" -ForegroundColor Yellow
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "Application will be available at:" -ForegroundColor Cyan
Write-Host "   - HTTP:  http://localhost:6001" -ForegroundColor Gray
Write-Host "   - HTTPS: https://localhost:6002" -ForegroundColor Gray
Write-Host ""

