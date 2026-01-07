@echo off
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   Auto Install LocalDB untuk Development
echo ========================================
echo.

REM Step 1: Cek apakah LocalDB sudah terinstall
echo [Step 1/5] Checking LocalDB installation...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB sudah terinstall!
    sqllocaldb info
    echo.
    goto :setup
)

echo [INFO] LocalDB belum terinstall, akan diinstall sekarang...
echo.

REM Step 2: Cek winget (Windows Package Manager)
echo [Step 2/5] Checking Windows Package Manager (winget)...
where winget >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] winget tersedia, akan install LocalDB via winget...
    echo.
    echo [INFO] Menginstall SQL Server Express LocalDB...
    echo [INFO] Ini mungkin memerlukan beberapa menit...
    echo.
    
    REM Install via winget
    winget install --id Microsoft.SQLServer.2022.Express.LocalDB --silent --accept-package-agreements --accept-source-agreements
    
    if %ERRORLEVEL% EQU 0 (
        echo [OK] Install berhasil via winget!
        echo.
        echo [INFO] Refresh PATH environment...
        call refreshenv >nul 2>&1
        goto :verify
    ) else (
        echo [WARNING] Install via winget gagal, mencoba method alternatif...
        goto :method2
    )
) else (
    echo [INFO] winget tidak tersedia, menggunakan method alternatif...
    goto :method2
)

:method2
REM Method 2: Download dan install manual
echo.
echo ========================================
echo   Method Alternatif: Manual Install
echo ========================================
echo.
echo [INFO] winget tidak tersedia atau install gagal
echo [INFO] Menggunakan method download dan install manual
echo.
echo Langkah yang akan dilakukan:
echo   1. Download SQL Server Express installer
echo   2. Install dengan opsi "Basic" (termasuk LocalDB)
echo   3. Setup instance MSSQLLocalDB
echo   4. Update appsettings.json
echo.
echo [INFO] Membuka halaman download...
start https://go.microsoft.com/fwlink/?LinkID=866658
echo.
echo ========================================
echo   INSTRUKSI INSTALL MANUAL
echo ========================================
echo.
echo 1. Download SQL Server Express dari halaman yang terbuka
echo 2. Jalankan installer yang didownload
echo 3. Pilih "Basic" installation type
echo 4. Tunggu install selesai (~2-3 menit)
echo 5. Setelah install selesai, tekan tombol apapun di sini
echo.
pause
echo.

:verify
REM Step 3: Verify installation
echo [Step 3/5] Verifying LocalDB installation...
timeout /t 3 /nobreak >nul

REM Refresh PATH
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\150\Tools\Binn;"
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\160\Tools\Binn;"

where sqllocaldb >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] LocalDB masih belum terdeteksi setelah install
    echo.
    echo [INFO] Silakan:
    echo   1. Restart command prompt ini
    echo   2. Jalankan script ini lagi: install-localdb-auto.bat
    echo   3. Atau jalankan: setup-localdb-dev.bat
    echo.
    pause
    exit /b 1
)

echo [OK] LocalDB terdeteksi!
sqllocaldb info
echo.

:setup
REM Step 4: Setup MSSQLLocalDB instance
echo [Step 4/5] Setting up MSSQLLocalDB instance...

sqllocaldb info MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Creating MSSQLLocalDB instance...
    sqllocaldb create MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Gagal membuat instance MSSQLLocalDB
        echo [INFO] Coba jalankan sebagai Administrator
        pause
        exit /b 1
    )
    echo [OK] Instance berhasil dibuat
) else (
    echo [OK] Instance sudah ada
)

echo Starting MSSQLLocalDB instance...
sqllocaldb start MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [WARNING] Gagal start otomatis, mencoba manual...
    sqllocaldb start MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Gagal start instance
        pause
        exit /b 1
    )
)
echo [OK] Instance berjalan!
echo.

REM Step 5: Update appsettings.json
echo [Step 5/5] Updating appsettings.json untuk LocalDB...

REM Backup jika perlu
if exist appsettings.json (
    findstr /C:"(localdb)" appsettings.json >nul 2>&1
    if %ERRORLEVEL% NEQ 0 (
        for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set mydate=%%c-%%a-%%b)
        for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set mytime=%%a%%b)
        set mytime=!mytime: =0!
        set BACKUP_NAME=appsettings.json.backup.!mydate!-!mytime!
        copy appsettings.json "!BACKUP_NAME!" >nul
        echo [OK] Backup dibuat: !BACKUP_NAME!
    )
)

REM Update appsettings.json
(
echo {
echo   "ConnectionStrings": {
echo     "DefaultConnection": "Server=(localdb)\MSSQLLocalDB;Database=OeeSystemDb;Trusted_Connection=True;MultipleActiveResultSets=true",
echo     "HossConnection": "Server=(localdb)\MSSQLLocalDB;Database=OeeSystemDb_Hoss;Trusted_Connection=True;MultipleActiveResultSets=true"
echo   },
echo   "Logging": {
echo     "LogLevel": {
echo       "Default": "Information",
echo       "Microsoft.AspNetCore": "Warning"
echo     }
echo   },
echo   "AllowedHosts": "*"
echo }
) > appsettings.json

echo [OK] appsettings.json sudah diupdate untuk LocalDB
echo.

REM Final verification
echo ========================================
echo   Verifikasi Setup
echo ========================================
echo.

sqllocaldb info MSSQLLocalDB | findstr /C:"State: Running" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] MSSQLLocalDB instance berjalan
) else (
    echo [WARNING] Instance mungkin tidak berjalan
    echo [INFO] Akan di-start otomatis saat aplikasi berjalan
)

echo.
echo ========================================
echo   ✅ Setup Selesai!
echo ========================================
echo.
echo LocalDB sudah terinstall dan dikonfigurasi untuk development
echo.
echo 🚀 Proyek siap digunakan! Jalankan:
echo    dotnet run
echo.
echo 📱 Aplikasi akan tersedia di:
echo    - HTTP:  http://localhost:6001
echo    - HTTPS: https://localhost:6002
echo.
echo 💡 Database akan dibuat otomatis saat pertama kali run
echo.
pause

