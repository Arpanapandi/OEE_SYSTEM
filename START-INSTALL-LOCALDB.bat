@echo off
echo.
echo ========================================
echo   Install LocalDB - Quick Start
echo ========================================
echo.

REM Cek LocalDB
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB sudah terinstall!
    echo.
    echo Setup akan dilanjutkan...
    timeout /t 2 /nobreak >nul
    call setup-localdb-dev.bat
    exit /b 0
)

echo [INFO] LocalDB belum terinstall
echo.
echo Membuka opsi install...
echo.

REM Cek Visual Studio Installer
if exist "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe" (
    echo [OK] Visual Studio Installer ditemukan
    echo [INFO] Membuka Visual Studio Installer...
    echo.
    echo INSTRUKSI:
    echo   1. Klik "Modify" pada Visual Studio
    echo   2. Tab "Individual Components"
    echo   3. Centang "SQL Server Express LocalDB"
    echo   4. Klik "Modify"
    echo   5. Setelah selesai, jalankan: setup-localdb-dev.bat
    echo.
    start "" "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe"
) else (
    echo [INFO] Visual Studio Installer tidak ditemukan
    echo [INFO] Membuka halaman download SQL Server Express...
    echo.
    echo INSTRUKSI:
    echo   1. Download SQL Server Express
    echo   2. Install dengan pilihan "Basic"
    echo   3. Setelah selesai, jalankan: setup-localdb-dev.bat
    echo.
    start https://go.microsoft.com/fwlink/?LinkID=866658
)

echo.
echo ========================================
echo   Setelah Install Selesai
echo ========================================
echo.
echo Jalankan script ini lagi untuk setup otomatis:
echo   START-INSTALL-LOCALDB.bat
echo.
echo ATAU jalankan setup langsung:
echo   setup-localdb-dev.bat
echo.
pause

