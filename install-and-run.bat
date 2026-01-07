@echo off
REM Script lengkap: Install LocalDB, Setup, dan Run Aplikasi
REM Script ini akan membantu install LocalDB dan langsung setup + run aplikasi

echo.
echo ========================================
echo   Install LocalDB + Setup + Run App
echo ========================================
echo.

REM Step 1: Cek LocalDB
echo [Step 1/4] Checking LocalDB installation...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB is already installed!
    goto :setup
)

REM LocalDB tidak terinstall
echo [INFO] LocalDB is NOT installed.
echo.
echo ========================================
echo   INSTALL LOCALDB - QUICK GUIDE
echo ========================================
echo.
echo METHOD 1: Download SQL Server Express (Recommended - 2 menit)
echo.
echo   Step 1: Download
echo     Link: https://go.microsoft.com/fwlink/?LinkID=866658
echo     Or: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
echo.
echo   Step 2: Install
echo     - Run the installer
echo     - Select "Basic" installation type
echo     - LocalDB will be automatically included
echo     - Wait for installation to complete (~2-3 minutes)
echo.
echo   Step 3: Verify
echo     - Restart this command prompt
echo     - Run: sqllocaldb info
echo     - If you see instance list, installation is successful!
echo.
echo METHOD 2: Via Visual Studio Installer
echo   - Open Visual Studio Installer
echo   - Click "Modify" on your Visual Studio installation
echo   - Go to "Individual Components" tab
echo   - Check "SQL Server Express LocalDB"
echo   - Click "Modify"
echo.
echo ========================================
echo.
echo After installation:
echo   1. Restart this command prompt
echo   2. Run this script again: install-and-run.bat
echo.
echo ========================================
echo.
echo Opening download page...
start https://go.microsoft.com/fwlink/?LinkID=866658
echo.
pause
exit /b 1

:setup
REM Step 2: Setup LocalDB instance
echo.
echo [Step 2/4] Setting up MSSQLLocalDB instance...
sqllocaldb info MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Creating MSSQLLocalDB instance...
    sqllocaldb create MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Failed to create MSSQLLocalDB instance
        pause
        exit /b 1
    )
)

echo Starting MSSQLLocalDB instance...
sqllocaldb start MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [WARNING] Failed to start MSSQLLocalDB, trying to start manually...
    sqllocaldb start MSSQLLocalDB
)
echo [OK] MSSQLLocalDB is ready!
echo.

REM Step 3: Update appsettings.json
echo [Step 3/4] Updating appsettings.json for LocalDB...

REM Backup current config if exists and not already LocalDB
if exist appsettings.json (
    findstr /C:"(localdb)" appsettings.json >nul 2>&1
    if %ERRORLEVEL% NEQ 0 (
        set BACKUP_NAME=appsettings.json.backup.%date:~-4,4%%date:~-7,2%%date:~-10,2%-%time:~0,2%%time:~3,2%%time:~6,2%
        set BACKUP_NAME=!BACKUP_NAME: =0!
        copy appsettings.json "!BACKUP_NAME!" >nul
        echo [OK] Backed up current config to: !BACKUP_NAME!
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

echo [OK] appsettings.json updated for LocalDB
echo.

REM Step 4: Run application
echo [Step 4/4] Starting application...
echo.
echo ========================================
echo   Starting OEE System Application
echo ========================================
echo.
echo The database will be created automatically on first run.
echo Access at: http://localhost:6001
echo.
echo Press Ctrl+C to stop the application
echo.
echo ========================================
echo.

dotnet run

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Application failed to start!
    echo.
    echo Common issues:
    echo   1. LocalDB instance not running - try: sqllocaldb start MSSQLLocalDB
    echo   2. Database connection error - check appsettings.json
    echo   3. Missing dependencies - run: dotnet restore
    echo.
    pause
    exit /b 1
)

