@echo off
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   INSTALL LOCALDB - COMPLETE SETUP
echo ========================================
echo.
echo Script ini akan:
echo   1. Install LocalDB (jika belum terinstall)
echo   2. Setup instance MSSQLLocalDB
echo   3. Update appsettings.json untuk LocalDB
echo   4. Verify setup
echo.
echo ========================================
echo.

REM Step 1: Cek LocalDB
echo [Step 1/4] Checking LocalDB installation...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB sudah terinstall!
    goto :setup
)

echo [INFO] LocalDB belum terinstall
echo.
echo ========================================
echo   INSTALL LOCALDB
echo ========================================
echo.
echo Saya akan membantu install LocalDB dengan cara termudah.
echo.
echo PILIHAN METODE INSTALL:
echo.
echo [1] Via Visual Studio Installer (Paling Mudah - Recommended)
echo     - Jika Anda punya Visual Studio terinstall
echo     - Install hanya memerlukan beberapa klik
echo.
echo [2] Download SQL Server Express (Universal)
echo     - Download installer dari Microsoft
echo     - Install dengan pilihan "Basic"
echo     - Cocok untuk semua Windows
echo.
echo ========================================
echo.
set /p METHOD="Pilih metode (1 atau 2): "

if "%METHOD%"=="1" (
    echo.
    echo [INFO] Membuka Visual Studio Installer...
    echo.
    echo INSTRUKSI:
    echo   1. Di Visual Studio Installer, klik "Modify" pada Visual Studio Anda
    echo   2. Buka tab "Individual Components"
    echo   3. Cari dan centang "SQL Server Express LocalDB"
    echo   4. Klik "Modify" dan tunggu install selesai
    echo   5. Setelah selesai, tutup installer dan tekan Enter di sini
    echo.
    start "" "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe"
    pause
    goto :verify_install
) else if "%METHOD%"=="2" (
    echo.
    echo [INFO] Membuka halaman download SQL Server Express...
    echo.
    echo INSTRUKSI:
    echo   1. Download SQL Server Express dari halaman yang terbuka
    echo   2. Jalankan installer yang didownload
    echo   3. Pilih "Basic" installation type
    echo   4. Tunggu install selesai (~2-3 menit)
    echo   5. Setelah selesai, tekan Enter di sini
    echo.
    start https://go.microsoft.com/fwlink/?LinkID=866658
    pause
    goto :verify_install
) else (
    echo [ERROR] Pilihan tidak valid
    pause
    exit /b 1
)

:verify_install
echo.
echo [INFO] Verifying installation...
timeout /t 2 /nobreak >nul

REM Refresh PATH
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\150\Tools\Binn;"
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\160\Tools\Binn;"
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\170\Tools\Binn;"

where sqllocaldb >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] LocalDB masih belum terdeteksi setelah install
    echo.
    echo [INFO] Silakan:
    echo   1. Pastikan install selesai dengan sukses
    echo   2. Restart command prompt ini
    echo   3. Jalankan script ini lagi: INSTALL-LOCALDB-COMPLETE.bat
    echo.
    pause
    exit /b 1
)

echo [OK] LocalDB terdeteksi!
sqllocaldb info
echo.

:setup
REM Step 2: Setup instance
echo.
echo [Step 2/4] Setting up MSSQLLocalDB instance...

sqllocaldb info MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Creating MSSQLLocalDB instance...
    sqllocaldb create MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Gagal membuat instance MSSQLLocalDB
        echo [INFO] Coba jalankan script ini sebagai Administrator
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

REM Step 3: Update appsettings.json
echo [Step 3/4] Updating appsettings.json untuk LocalDB...

if exist appsettings.json (
    findstr /C:"(localdb)" appsettings.json >nul 2>&1
    if %ERRORLEVEL% NEQ 0 (
        for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set d=%%c-%%a-%%b)
        for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set t=%%a%%b)
        set t=!t: =0!
        copy appsettings.json "appsettings.json.backup.!d!-!t!" >nul
        echo [OK] Backup dibuat: appsettings.json.backup.!d!-!t!
    )
)

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

echo [OK] appsettings.json updated untuk LocalDB
echo.

REM Step 4: Verify
echo [Step 4/4] Verifying setup...
echo.
echo Current Configuration:
type appsettings.json | findstr /C:"DefaultConnection"
type appsettings.json | findstr /C:"HossConnection"
echo.

sqllocaldb info MSSQLLocalDB | findstr /C:"State: Running" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] MSSQLLocalDB instance berjalan dan siap!
) else (
    echo [INFO] Instance akan di-start otomatis saat aplikasi berjalan
)

echo.
echo ========================================
echo   ✅ SETUP SELESAI!
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

