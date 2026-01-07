@echo off
echo ========================================
echo   OEE SYSTEM - Local Development Setup
echo ========================================
echo.

REM Set Environment to Development
set ASPNETCORE_ENVIRONMENT=Development
set DOTNET_ENVIRONMENT=Development

echo [1/4] Checking LocalDB instance...
sqllocaldb info MSSQLLocalDB >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: LocalDB tidak terinstall atau tidak dapat diakses
    echo Silakan install SQL Server Express LocalDB dari Microsoft
    pause
    exit /b 1
)

echo [2/4] Starting LocalDB instance...
sqllocaldb start MSSQLLocalDB
if %errorlevel% neq 0 (
    echo WARNING: Gagal start LocalDB, mencoba lagi...
    timeout /t 2 /nobreak >nul
    sqllocaldb start MSSQLLocalDB
)

echo [3/4] Verifying LocalDB is running...
sqllocaldb info MSSQLLocalDB | findstr "State: Running" >nul
if %errorlevel% neq 0 (
    echo ERROR: LocalDB tidak berjalan
    pause
    exit /b 1
)
echo    LocalDB is RUNNING

echo [4/4] Starting application...
echo.
echo ========================================
echo   Application will start on:
echo   - HTTP:  http://localhost:6001
echo   - HTTPS: https://localhost:6002
echo ========================================
echo.
echo Press Ctrl+C to stop the application
echo.

dotnet run --project . --urls "http://localhost:5000;https://localhost:5001" --environment Development

pause

