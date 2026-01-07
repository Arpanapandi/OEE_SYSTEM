@echo off
REM Script batch untuk setup development offline (tidak perlu execution policy)
REM Script ini akan membantu setup LocalDB untuk development offline

echo.
echo ========================================
echo   Setup Development Offline - LocalDB
echo ========================================
echo.

REM Cek apakah LocalDB terinstall
echo [1/3] Checking LocalDB installation...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] LocalDB is NOT installed!
    echo.
    echo Please install LocalDB first:
    echo   1. Download SQL Server Express: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
    echo   2. Or install via Visual Studio Installer (Individual Components ^> SQL Server Express LocalDB)
    echo   3. After installation, restart and run this script again
    echo.
    echo See INSTALL-LOCALDB.md for detailed instructions
    echo.
    pause
    exit /b 1
)

echo [OK] LocalDB is installed!
echo.

REM Cek dan start instance MSSQLLocalDB
echo [2/3] Checking MSSQLLocalDB instance...
sqllocaldb info MSSQLLocalDB >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Creating MSSQLLocalDB instance...
    sqllocaldb create MSSQLLocalDB
)

echo Starting MSSQLLocalDB instance...
sqllocaldb start MSSQLLocalDB >nul 2>&1
echo [OK] MSSQLLocalDB is ready!
echo.

REM Update appsettings.json
echo [3/3] Updating appsettings.json for LocalDB...

REM Backup current config
if exist appsettings.json (
    set BACKUP_NAME=appsettings.json.backup.%date:~-4,4%%date:~-7,2%%date:~-10,2%-%time:~0,2%%time:~3,2%%time:~6,2%
    set BACKUP_NAME=!BACKUP_NAME: =0!
    copy appsettings.json "!BACKUP_NAME!" >nul
    echo [OK] Backed up current config
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

echo [OK] Updated appsettings.json to use LocalDB
echo.

echo ========================================
echo   Setup Completed!
echo ========================================
echo.
echo You can now run the application:
echo   dotnet run
echo.
echo The database will be created automatically on first run!
echo.
pause

