@echo off
REM Script untuk test koneksi database (tidak perlu execution policy)

echo.
echo ========================================
echo   Testing Database Connections
echo ========================================
echo.

REM Test LocalDB
echo [1/2] Testing LocalDB...
where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] LocalDB is installed
    sqllocaldb info MSSQLLocalDB >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo [OK] MSSQLLocalDB instance exists
        sqllocaldb start MSSQLLocalDB >nul 2>&1
        if %ERRORLEVEL% EQU 0 (
            echo [OK] MSSQLLocalDB is RUNNING
            echo [OK] LocalDB connection: READY
        ) else (
            echo [WARNING] Could not start MSSQLLocalDB
        )
    ) else (
        echo [INFO] MSSQLLocalDB instance will be created on first run
    )
) else (
    echo [ERROR] LocalDB is NOT installed
    echo [INFO] Install: https://go.microsoft.com/fwlink/?LinkID=866658
)

echo.

REM Test SQL Server
echo [2/2] Testing SQL Server (.\\SERVERVJEST)...
echo [INFO] Testing connection (this may take a few seconds)...
echo [WARNING] If this hangs, SQL Server is not accessible

REM Use sqlcmd if available, otherwise skip
where sqlcmd >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    sqlcmd -S ".\\SERVERVJEST" -Q "SELECT 1" -l 3 >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo [OK] SQL Server (.\\SERVERVJEST) connection: SUCCESS
    ) else (
        echo [ERROR] SQL Server (.\\SERVERVJEST) connection: FAILED
        echo [INFO] Possible reasons:
        echo    - SQL Server instance not running
        echo    - Instance name incorrect
        echo    - SQL Server not installed
        echo    - Windows Authentication issue
    )
) else (
    echo [INFO] sqlcmd not available, skipping SQL Server test
    echo [INFO] Assuming SQL Server connection may fail
)

echo.
echo ========================================
echo   Recommendation
echo ========================================
echo.

where sqllocaldb >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Use LocalDB for development (offline)
    echo [INFO] Update appsettings.json to use LocalDB
    echo [INFO] Connection: Server=(localdb)\MSSQLLocalDB
) else (
    echo [WARNING] LocalDB not installed
    echo [INFO] Option 1: Install LocalDB for offline development
    echo [INFO] Option 2: Fix SQL Server connection
    echo.
    echo [INFO] To install LocalDB:
    echo   1. Download: https://go.microsoft.com/fwlink/?LinkID=866658
    echo   2. Install with "Basic" option
    echo   3. Restart and run: install-and-run.bat
)

echo.
echo ========================================
echo.
pause

