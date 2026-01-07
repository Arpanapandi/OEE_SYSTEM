@echo off
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   AUTO INSTALL LOCALDB - Best Method
echo ========================================
echo.

REM Step 1: Cek LocalDB
echo [Step 1/4] Checking LocalDB installation...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB sudah terinstall!
    sqllocaldb info
    echo.
    echo Setup akan dilanjutkan...
    goto :setup
)

echo [INFO] LocalDB belum terinstall, akan diinstall otomatis...
echo.

REM Step 2: Cek metode terbaik
echo [Step 2/4] Finding best installation method...

REM Method 1: Cek Visual Studio Installer (Paling Mudah)
if exist "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe" (
    echo [OK] Visual Studio Installer ditemukan - Method terbaik!
    echo.
    echo [INFO] Membuka Visual Studio Installer...
    echo.
    echo ========================================
    echo   INSTALL VIA VISUAL STUDIO INSTALLER
    echo ========================================
    echo.
    echo INSTRUKSI (Ikuti langkah-langkah ini):
    echo.
    echo 1. Di Visual Studio Installer yang terbuka:
    echo    - Klik "Modify" pada Visual Studio yang terinstall
    echo.
    echo 2. Buka tab "Individual Components"
    echo.
    echo 3. Cari dan centang "SQL Server Express LocalDB"
    echo    (Gunakan kotak pencarian jika perlu)
    echo.
    echo 4. Klik "Modify" di kanan bawah
    echo.
    echo 5. Tunggu install selesai (~1-2 menit)
    echo.
    echo 6. Setelah selesai:
    echo    - Tutup Visual Studio Installer
    echo    - Restart command prompt ini
    echo    - Jalankan: RUN-DEV.bat
    echo.
    echo ========================================
    echo.
    start "" "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe"
    echo.
    echo [INFO] Visual Studio Installer sudah dibuka
    echo [INFO] Ikuti instruksi di atas untuk install LocalDB
    echo.
    pause
    exit /b 0
)

REM Method 2: Cek winget (Windows Package Manager)
echo [INFO] Visual Studio Installer tidak ditemukan
echo [INFO] Mencoba Windows Package Manager (winget)...
where winget >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] winget tersedia!
    echo.
    echo [INFO] Menginstall SQL Server Express LocalDB via winget...
    echo [INFO] Ini mungkin memerlukan beberapa menit...
    echo.
    winget install --id Microsoft.SQLServer.2022.Express.LocalDB --silent --accept-package-agreements --accept-source-agreements
    
    if %ERRORLEVEL% EQU 0 (
        echo.
        echo [OK] Install berhasil via winget!
        echo [INFO] Refresh PATH...
        timeout /t 3 /nobreak >nul
        goto :verify_install
    ) else (
        echo [WARNING] Install via winget gagal, menggunakan method alternatif...
    )
)

REM Method 3: Cek Chocolatey
echo [INFO] winget tidak tersedia atau gagal
echo [INFO] Mencoba Chocolatey...
where choco >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Chocolatey tersedia!
    echo.
    echo [INFO] Menginstall SQL Server Express LocalDB via Chocolatey...
    echo [INFO] Ini mungkin memerlukan beberapa menit...
    echo.
    choco install sql-server-express-localdb -y
    
    if %ERRORLEVEL% EQU 0 (
        echo.
        echo [OK] Install berhasil via Chocolatey!
        echo [INFO] Refresh PATH...
        timeout /t 3 /nobreak >nul
        goto :verify_install
    ) else (
        echo [WARNING] Install via Chocolatey gagal, menggunakan method manual...
    )
)

REM Method 4: Download Manual (Fallback)
echo [INFO] Package manager tidak tersedia
echo [INFO] Menggunakan method download manual...
echo.
echo ========================================
echo   DOWNLOAD SQL SERVER EXPRESS
echo ========================================
echo.
echo INSTRUKSI:
echo.
echo 1. Download SQL Server Express dari halaman yang terbuka
echo    Link: https://go.microsoft.com/fwlink/?LinkID=866658
echo.
echo 2. Jalankan installer yang didownload
echo.
echo 3. Pilih "Basic" installation type
echo    (LocalDB akan otomatis tercentang)
echo.
echo 4. Klik "Install" dan tunggu selesai (~2-3 menit)
echo.
echo 5. Setelah selesai:
echo    - Tutup installer
echo    - Restart command prompt ini
echo    - Jalankan: RUN-DEV.bat
echo.
echo ========================================
echo.
echo [INFO] Membuka halaman download...
start https://go.microsoft.com/fwlink/?LinkID=866658
echo.
pause
exit /b 0

:verify_install
echo.
echo [Step 3/4] Verifying installation...

REM Refresh PATH
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\150\Tools\Binn;"
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\160\Tools\Binn;"
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server\170\Tools\Binn;"

where sqllocaldb >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] LocalDB masih belum terdeteksi setelah install
    echo.
    echo [INFO] Silakan:
    echo   1. Pastikan install selesai dengan sukses
    echo   2. Restart command prompt ini
    echo   3. Jalankan: RUN-DEV.bat
    echo.
    pause
    exit /b 1
)

echo [OK] LocalDB terdeteksi!
sqllocaldb info
echo.

:setup
echo [Step 4/4] Setting up MSSQLLocalDB instance...

sqllocaldb info MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Creating MSSQLLocalDB instance...
    sqllocaldb create MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Gagal membuat instance
        echo [INFO] Coba jalankan sebagai Administrator
        pause
        exit /b 1
    )
    echo [OK] Instance berhasil dibuat
) else (
    echo [OK] Instance sudah ada
)

sqllocaldb start MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    sqllocaldb start MSSQLLocalDB
)
echo [OK] Instance berjalan!
echo.

echo ========================================
echo   ✅ INSTALL SELESAI!
echo ========================================
echo.
echo LocalDB sudah terinstall dan dikonfigurasi
echo.
echo 🚀 Jalankan aplikasi dengan:
echo    RUN-DEV.bat
echo.
pause
