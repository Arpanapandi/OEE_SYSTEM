@echo off
REM Script sederhana dan reliable untuk install LocalDB
REM Script ini akan memberikan instruksi jelas dan membantu proses install

echo.
echo ========================================
echo   Install LocalDB - Easy Method
echo ========================================
echo.

REM Check if LocalDB already installed
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB is already installed!
    sqllocaldb info
    echo.
    echo Setup akan dilanjutkan...
    goto :setup
)

echo [INFO] LocalDB is NOT installed
echo.
echo ========================================
echo   INSTALL LOCALDB - STEP BY STEP
echo ========================================
echo.
echo Method yang paling reliable adalah install manual dengan wizard.
echo.
echo LANGKAH 1: Download SQL Server Express
echo.
echo   Link Download:
echo   https://go.microsoft.com/fwlink/?LinkID=866658
echo.
echo   Atau:
echo   https://www.microsoft.com/en-us/sql-server/sql-server-downloads
echo.
echo   Pilih: "Express" edition
echo.
echo LANGKAH 2: Install dengan Wizard
echo.
echo   Saat installer berjalan:
echo   1. Pilih "Basic" installation type
echo   2. LocalDB akan otomatis tercentang
echo   3. Klik "Install" dan tunggu selesai (~2-3 menit)
echo.
echo LANGKAH 3: Setelah Install
echo.
echo   Restart command prompt ini, lalu jalankan:
echo   setup-localdb-dev.bat
echo.
echo ========================================
echo.
echo Opening download page...
start https://go.microsoft.com/fwlink/?LinkID=866658
echo.
echo [INFO] Setelah download selesai, jalankan installer yang didownload
echo [INFO] Ikuti wizard dan pilih "Basic" installation
echo.
echo Press any key after installation completes to continue setup...
pause
echo.

REM Check again after user installs
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [WARNING] LocalDB still not detected
    echo [INFO] Please:
    echo   1. Make sure installation completed successfully
    echo   2. Restart this command prompt
    echo   3. Run: setup-localdb-dev.bat
    echo.
    pause
    exit /b 1
)

:setup
echo.
echo ========================================
echo   Setting up LocalDB for Development
echo ========================================
echo.

REM Setup MSSQLLocalDB instance
echo [Step 1/3] Setting up MSSQLLocalDB instance...
sqllocaldb info MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Creating MSSQLLocalDB instance...
    sqllocaldb create MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Failed to create instance
        echo [INFO] Try running as administrator
        pause
        exit /b 1
    )
    echo [OK] Instance created
) else (
    echo [OK] Instance already exists
)

echo Starting MSSQLLocalDB instance...
sqllocaldb start MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [WARNING] Failed to start automatically
    echo Trying to start manually...
    sqllocaldb start MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Failed to start instance
        pause
        exit /b 1
    )
)
echo [OK] Instance is running
echo.

REM Update appsettings.json
echo [Step 2/3] Updating appsettings.json for LocalDB...

REM Backup if needed
if exist appsettings.json (
    findstr /C:"(localdb)" appsettings.json >nul 2>&1
    if %ERRORLEVEL% NEQ 0 (
        set BACKUP_NAME=appsettings.json.backup.%date:~-4,4%%date:~-7,2%%date:~-10,2%-%time:~0,2%%time:~3,2%%time:~6,2%
        set BACKUP_NAME=!BACKUP_NAME: =0!
        copy appsettings.json "!BACKUP_NAME!" >nul
        echo [OK] Backed up to: !BACKUP_NAME!
    )
)

REM Create LocalDB config
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
echo [Step 3/3] Verifying setup...
sqllocaldb info MSSQLLocalDB | findstr /C:"State: Running" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] MSSQLLocalDB is running and ready!
) else (
    echo [WARNING] Instance may not be running
    echo [INFO] Will be started automatically on first run
)

echo.
echo ========================================
echo   Setup Complete!
echo ========================================
echo.
echo ✅ LocalDB configured for development
echo ✅ MSSQLLocalDB instance is ready
echo ✅ appsettings.json updated
echo.
echo 🚀 You can now run the application:
echo    dotnet run
echo.
echo 📱 Application will be available at:
echo    - HTTP:  http://localhost:6001
echo    - HTTPS: https://localhost:6002
echo.
pause


