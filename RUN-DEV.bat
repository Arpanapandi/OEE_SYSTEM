@echo off
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   AUTO SETUP + RUN - Development Mode
echo ========================================
echo.
echo Script ini akan otomatis:
echo   1. Setup LocalDB
echo   2. Update konfigurasi
echo   3. Run aplikasi sampai tampil
echo.

REM Step 1: Cek LocalDB
echo [Step 1/6] Checking LocalDB installation...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB sudah terinstall!
    goto :setup
)

REM LocalDB belum terinstall
echo [INFO] LocalDB belum terinstall
echo.
echo ========================================
echo   INSTALL LOCALDB DULU
echo ========================================
echo.
echo Pilih salah satu metode:
echo.
echo [1] Via Visual Studio Installer (Paling Mudah)
echo     - Buka Visual Studio Installer
echo     - Modify ^> Individual Components
echo     - Centang "SQL Server Express LocalDB"
echo     - Modify
echo.
echo [2] Download SQL Server Express
echo     - Download dari Microsoft
echo     - Install dengan pilihan "Basic"
echo.
echo ========================================
echo.
set /p INSTALL_METHOD="Pilih metode (1 atau 2): "

if "!INSTALL_METHOD!"=="1" (
    echo.
    echo [INFO] Membuka Visual Studio Installer...
    if exist "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe" (
        start "" "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe"
        echo.
        echo INSTRUKSI:
        echo   1. Klik "Modify" pada Visual Studio
        echo   2. Tab "Individual Components"
        echo   3. Centang "SQL Server Express LocalDB"
        echo   4. Klik "Modify" dan tunggu selesai
        echo   5. Setelah selesai, tutup installer
        echo   6. Restart command prompt ini
        echo   7. Jalankan script ini lagi: RUN-DEV.bat
    ) else (
        echo [ERROR] Visual Studio Installer tidak ditemukan
        echo [INFO] Gunakan metode 2
        goto :download
    )
) else (
    :download
    echo.
    echo [INFO] Membuka halaman download SQL Server Express...
    start https://go.microsoft.com/fwlink/?LinkID=866658
    echo.
    echo INSTRUKSI:
    echo   1. Download SQL Server Express
    echo   2. Install dengan pilihan "Basic"
    echo   3. Tunggu install selesai (~2-3 menit)
    echo   4. Setelah selesai, tutup installer
    echo   5. Restart command prompt ini
    echo   6. Jalankan script ini lagi: RUN-DEV.bat
)

echo.
echo ========================================
echo   TUNGGU INSTALL SELESAI
echo ========================================
echo.
echo Setelah install selesai:
echo   1. Restart command prompt ini (PENTING!)
echo   2. Jalankan script ini lagi: RUN-DEV.bat
echo.
pause
exit /b 1

:setup
REM Step 2: Setup instance MSSQLLocalDB
echo.
echo [Step 2/6] Setting up MSSQLLocalDB instance...

REM Refresh PATH untuk memastikan sqllocaldb terdeteksi
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\150\Tools\Binn;"
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\160\Tools\Binn;"
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\170\Tools\Binn;"

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
        echo [INFO] Coba jalankan sebagai Administrator
        pause
        exit /b 1
    )
)
echo [OK] Instance berjalan!
echo.

REM Step 3: Update appsettings.json
echo [Step 3/6] Updating appsettings.json untuk LocalDB...

if exist appsettings.json (
    findstr /C:"(localdb)" appsettings.json >nul 2>&1
    if %ERRORLEVEL% NEQ 0 (
        for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set d=%%c-%%a-%%b)
        for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set t=%%a%%b)
        set t=!t: =0!
        copy appsettings.json "appsettings.json.backup.!d!-!t!" >nul
        echo [OK] Backup dibuat: appsettings.json.backup.!d!-!t!
    ) else (
        echo [OK] appsettings.json sudah menggunakan LocalDB
    )
) else (
    echo [INFO] appsettings.json tidak ditemukan, akan dibuat
)

REM Update appsettings.json dari template (jika template ada)
if exist appsettings.localdb.template.json (
    copy /Y appsettings.localdb.template.json appsettings.json >nul
    echo [OK] appsettings.json updated dari template
) else (
    echo [INFO] Template tidak ditemukan, skip update (file sudah benar)
)

echo [OK] appsettings.json updated
echo.

REM Step 4: Set Environment ke Development
echo [Step 4/6] Setting environment ke Development...
set ASPNETCORE_ENVIRONMENT=Development
echo [OK] Environment: Development
echo.

REM Step 5: Restore packages
echo [Step 5/6] Checking dependencies...
dotnet restore >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Dependencies ready
) else (
    echo [INFO] Restoring packages...
    dotnet restore
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Gagal restore packages
        pause
        exit /b 1
    )
)
echo.

REM Step 6: Verify LocalDB connection
echo [Step 6/6] Verifying LocalDB connection...
sqllocaldb info MSSQLLocalDB | findstr /C:"State: Running" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] MSSQLLocalDB instance berjalan dan siap!
) else (
    echo [WARNING] Instance tidak berjalan
    echo [INFO] Starting MSSQLLocalDB instance...
    sqllocaldb start MSSQLLocalDB
    if %ERRORLEVEL% EQU 0 (
        echo [OK] Instance berhasil di-start
        REM Wait a bit for instance to fully start
        timeout /t 2 /nobreak >nul
    ) else (
        echo [ERROR] Gagal start LocalDB instance
        echo [INFO] Coba jalankan sebagai Administrator atau install LocalDB
        pause
        exit /b 1
    )
)
echo.

REM Run aplikasi
echo ========================================
echo   OEE SYSTEM - Development Mode
echo ========================================
echo.
echo ✅ LocalDB configured
echo ✅ Database akan dibuat otomatis
echo ✅ Environment: Development
echo.
echo 📱 Aplikasi akan tersedia di:
echo    - HTTP:  http://localhost:6001
echo    - HTTPS: https://localhost:6002
echo.
echo 💡 Tekan Ctrl+C untuk stop aplikasi
echo.
echo ========================================
echo.
echo Starting application...
echo.

dotnet run

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ========================================
    echo   ERROR: Aplikasi gagal start!
    echo ========================================
    echo.
    echo Troubleshooting:
    echo.
    echo 1. Pastikan LocalDB berjalan:
    echo    sqllocaldb start MSSQLLocalDB
    echo.
    echo 2. Cek appsettings.json:
    echo    type appsettings.json
    echo.
    echo 3. Restore packages:
    echo    dotnet restore
    echo.
    echo 4. Build project:
    echo    dotnet build
    echo.
    echo 5. Cek error detail di atas
    echo.
    pause
    exit /b 1
)

