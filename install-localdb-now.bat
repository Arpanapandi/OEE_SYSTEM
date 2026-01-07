@echo off
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   Install LocalDB - Quick Setup
echo ========================================
echo.

REM Cek LocalDB
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB sudah terinstall!
    goto :setup
)

echo [INFO] LocalDB belum terinstall
echo.
echo ========================================
echo   Install LocalDB - Cara Tercepat
echo ========================================
echo.
echo Method 1: Via Visual Studio Installer (Paling Mudah)
echo.
echo   1. Buka "Visual Studio Installer"
echo   2. Klik "Modify" pada Visual Studio yang terinstall
echo   3. Tab "Individual Components"
echo   4. Centang "SQL Server Express LocalDB"
echo   5. Klik "Modify" dan tunggu selesai
echo.
echo Method 2: Download SQL Server Express
echo.
echo   Link: https://go.microsoft.com/fwlink/?LinkID=866658
echo   Pilih "Basic" installation
echo.
echo ========================================
echo.
echo Membuka halaman download...
start https://go.microsoft.com/fwlink/?LinkID=866658
echo.
echo [INFO] Setelah install selesai:
echo   1. Restart command prompt ini
echo   2. Jalankan: setup-localdb-dev.bat
echo   3. Atau jalankan script ini lagi
echo.
pause
exit /b 1

:setup
echo.
echo ========================================
echo   Setup LocalDB untuk Development
echo ========================================
echo.

REM Setup instance
echo [1/3] Setup MSSQLLocalDB instance...
sqllocaldb info MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Creating instance...
    sqllocaldb create MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Gagal membuat instance
        echo [INFO] Coba jalankan sebagai Administrator
        pause
        exit /b 1
    )
)

sqllocaldb start MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    sqllocaldb start MSSQLLocalDB
)
echo [OK] Instance siap!
echo.

REM Update appsettings.json
echo [2/3] Update appsettings.json...
if exist appsettings.json (
    findstr /C:"(localdb)" appsettings.json >nul 2>&1
    if %ERRORLEVEL% NEQ 0 (
        for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set d=%%c-%%a-%%b)
        for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set t=%%a%%b)
        set t=!t: =0!
        copy appsettings.json "appsettings.json.backup.!d!-!t!" >nul
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

echo [OK] appsettings.json updated
echo.

REM Verify
echo [3/3] Verifikasi...
sqllocaldb info MSSQLLocalDB | findstr /C:"State: Running" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] MSSQLLocalDB berjalan
) else (
    echo [INFO] Instance akan di-start otomatis saat run
)

echo.
echo ========================================
echo   ✅ Setup Selesai!
echo ========================================
echo.
echo LocalDB sudah dikonfigurasi untuk development
echo.
echo 🚀 Jalankan aplikasi:
echo    dotnet run
echo.
pause

