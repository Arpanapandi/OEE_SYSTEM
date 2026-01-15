@echo off
REM ========================================
REM SCRIPT UNTUK RESET DATABASE DAN TEST
REM ========================================

echo.
echo ========================================
echo   RESET DATABASE DAN TEST SCW/PRODUCTION
echo ========================================
echo.

echo [1/4] Stopping application...
taskkill /F /IM dotnet.exe 2>nul
timeout /t 2 >nul

echo.
echo [2/4] Dropping database...
sqlcmd -S (localdb)\MSSQLLocalDB -Q "DROP DATABASE produksi" 2>nul
if %ERRORLEVEL% EQU 0 (
    echo    ✓ Database dropped successfully
) else (
    echo    ⚠ Database might not exist or already dropped
)

echo.
echo [3/4] Starting application...
echo    Please wait for database to be created and seeded...
echo.
start cmd /k "cd /d %~dp0 && dotnet run"

echo.
echo [4/4] Waiting for application to start...
timeout /t 10 >nul

echo.
echo ========================================
echo   TESTING CHECKLIST
echo ========================================
echo.
echo 1. Open browser: https://localhost:7002
echo 2. Navigate to OEE Detail page
echo 3. Check SCW dropdowns:
echo    - Jenis 4M should have 5 items
echo    - Jenis Remark should populate based on selection
echo 4. Test SCW submit
echo 5. Test Production Data submit
echo 6. Check Recent Product Count
echo.
echo Press any key to open browser...
pause >nul

start https://localhost:7002

echo.
echo ========================================
echo   VERIFICATION QUERIES
echo ========================================
echo.
echo Run these queries in SQL Server to verify:
echo.
echo SELECT * FROM produksi.tb_lwpmixing_Scw4MTypes;
echo SELECT * FROM produksi.tb_lwpmixing_ScwRemarks;
echo SELECT * FROM produksi.tb_lwpmixing_ScwEvents;
echo SELECT * FROM produksi.tb_lwpmixing_ProductionCounts;
echo.
pause
