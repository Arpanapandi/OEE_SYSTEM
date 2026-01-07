@echo off
REM Script lengkap untuk setup LocalDB untuk development
REM Script ini akan setup LocalDB dan konfigurasi aplikasi

echo.
echo ========================================
echo   Setup LocalDB untuk Development
echo ========================================
echo.

REM Step 1: Check LocalDB
echo [Step 1/4] Checking LocalDB installation...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] LocalDB is NOT installed!
    echo.
    echo ========================================
    echo   INSTALL LOCALDB
    echo ========================================
    echo.
    echo Please install LocalDB first:
    echo.
    echo METHOD 1: Download SQL Server Express (Recommended)
    echo   Link: https://go.microsoft.com/fwlink/?LinkID=866658
    echo   Or: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
    echo   - Select "Express" edition
    echo   - Choose "Basic" installation
    echo   - LocalDB will be automatically included
    echo.
    echo METHOD 2: Via Visual Studio Installer
    echo   - Open Visual Studio Installer
    echo   - Modify your Visual Studio installation
    echo   - Individual Components ^> SQL Server Express LocalDB
    echo.
    echo After installation:
    echo   1. Restart this command prompt
    echo   2. Run this script again: setup-localdb-dev.bat
    echo.
    echo ========================================
    echo.
    echo Opening download page...
    start https://go.microsoft.com/fwlink/?LinkID=866658
    echo.
    pause
    exit /b 1
)

echo [OK] LocalDB is installed!
echo.

REM Step 2: Setup MSSQLLocalDB instance
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
    echo [OK] MSSQLLocalDB instance created
) else (
    echo [OK] MSSQLLocalDB instance already exists
)

echo Starting MSSQLLocalDB instance...
sqllocaldb start MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [WARNING] Failed to start automatically, trying manually...
    sqllocaldb start MSSQLLocalDB
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Failed to start MSSQLLocalDB
        pause
        exit /b 1
    )
)
echo [OK] MSSQLLocalDB is running!
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
    ) else (
        echo [INFO] appsettings.json already using LocalDB
    )
) else (
    echo [INFO] appsettings.json not found, will be created
)

REM Create/Update LocalDB config
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

REM Step 4: Verify setup
echo [Step 4/4] Verifying setup...
echo.
echo Current Configuration:
type appsettings.json | findstr /C:"DefaultConnection"
type appsettings.json | findstr /C:"HossConnection"
echo.

REM Test LocalDB connection
echo Testing LocalDB connection...
sqllocaldb info MSSQLLocalDB | findstr /C:"State: Running" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] MSSQLLocalDB is running and ready!
) else (
    echo [WARNING] MSSQLLocalDB may not be running
    echo [INFO] Will be started automatically on first run
)

echo.
echo ========================================
echo   Setup Complete!
echo ========================================
echo.
echo ✅ LocalDB is configured for development
echo ✅ appsettings.json updated
echo ✅ MSSQLLocalDB instance is ready
echo.
echo 🚀 You can now run the application:
echo    dotnet run
echo.
echo 📱 Application will be available at:
echo    - HTTP:  http://localhost:6001
echo    - HTTPS: https://localhost:6002
echo.
echo 💡 The database will be created automatically on first run!
echo.
pause


