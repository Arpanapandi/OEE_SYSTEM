@echo off
REM Script untuk install semua prerequisites yang diperlukan
REM Script ini akan check dan memberikan instruksi install

echo.
echo ========================================
echo   Install All Prerequisites
echo ========================================
echo.

REM Check .NET SDK
echo [1/3] Checking .NET SDK...
dotnet --version >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] .NET SDK is installed
    dotnet --version
) else (
    echo [ERROR] .NET SDK is NOT installed!
    echo.
    echo Please install .NET SDK:
    echo   Download: https://dotnet.microsoft.com/download
    echo   Install .NET 8.0 SDK or later
    echo.
    echo Opening download page...
    start https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo.

REM Check LocalDB
echo [2/3] Checking SQL Server LocalDB...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB is installed
    echo.
    echo Checking MSSQLLocalDB instance...
    sqllocaldb info MSSQLLocalDB >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo [OK] MSSQLLocalDB instance exists
        sqllocaldb start MSSQLLocalDB >nul 2>&1
        if %ERRORLEVEL% EQU 0 (
            echo [OK] MSSQLLocalDB is running
        ) else (
            echo [WARNING] Could not start MSSQLLocalDB automatically
            echo Try manually: sqllocaldb start MSSQLLocalDB
        )
    ) else (
        echo [INFO] MSSQLLocalDB instance will be created on first run
    )
) else (
    echo [ERROR] LocalDB is NOT installed!
    echo.
    echo ========================================
    echo   INSTALL LOCALDB
    echo ========================================
    echo.
    echo Please install SQL Server Express LocalDB:
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
    echo   2. Run this script again: install-all-prerequisites.bat
    echo   3. Or run: install-and-run.bat
    echo.
    echo ========================================
    echo.
    echo Opening download page...
    start https://go.microsoft.com/fwlink/?LinkID=866658
    echo.
    pause
    exit /b 1
)

echo.

REM Check appsettings.json
echo [3/3] Checking Configuration...
if exist appsettings.json (
    findstr /C:"(localdb)" appsettings.json >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo [OK] appsettings.json configured for LocalDB
    ) else (
        echo [WARNING] appsettings.json not using LocalDB
        echo Will be updated when running install-and-run.bat
    )
) else (
    echo [WARNING] appsettings.json not found
    echo Will be created when running install-and-run.bat
)

echo.
echo ========================================
echo   Prerequisites Check Complete
echo ========================================
echo.

REM If all good, offer to run the app
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo ✅ All prerequisites are ready!
    echo.
    echo Would you like to setup and run the application now? (Y/N)
    set /p RUN_APP="> "
    if /i "%RUN_APP%"=="Y" (
        echo.
        echo Running setup and starting application...
        call install-and-run.bat
    ) else (
        echo.
        echo To run the application later, use:
        echo   install-and-run.bat
        echo   Or: dotnet run
        echo.
        pause
    )
) else (
    echo ⚠️  Please install LocalDB first (see instructions above)
    echo.
    echo After installation, run:
    echo   install-all-prerequisites.bat
    echo   Or: install-and-run.bat
    echo.
    pause
)

