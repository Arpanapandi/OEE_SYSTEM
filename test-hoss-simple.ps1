# Script sederhana untuk test koneksi DB_HOSS via API endpoint
# Pastikan aplikasi sedang berjalan

Write-Host "🔍 Testing Connection to DB_HOSS.dbo_komponen..." -ForegroundColor Yellow
Write-Host ""

$baseUrl = "http://localhost:6001"

Write-Host "📡 Testing via API endpoint..." -ForegroundColor Cyan
Write-Host "   URL: $baseUrl/api/TestHoss/connection" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/TestHoss/connection" -Method Get -ErrorAction Stop
    
    if ($response.success) {
        Write-Host "✅ Connection successful!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📊 Database Info:" -ForegroundColor Cyan
        Write-Host "   Server: $($response.server)" -ForegroundColor White
        Write-Host "   Database: $($response.database)" -ForegroundColor White
        Write-Host "   Table: $($response.table)" -ForegroundColor White
        Write-Host "   Record Count: $($response.recordCount)" -ForegroundColor White
        Write-Host ""
        
        if ($response.samples -and $response.samples.Count -gt 0) {
            Write-Host "📋 Sample Data (first $($response.samples.Count) records):" -ForegroundColor Cyan
            foreach ($sample in $response.samples) {
                Write-Host "   - ID: $($sample.id), Part Number: $($sample.partNumber), Jml Komponen: $($sample.jmlKomponen)" -ForegroundColor White
            }
        }
        
        Write-Host ""
        Write-Host "✅ All tests passed!" -ForegroundColor Green
    } else {
        Write-Host "❌ Connection failed: $($response.message)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Make sure:" -ForegroundColor Yellow
    Write-Host "   1. Application is running (dotnet run)" -ForegroundColor White
    Write-Host "   2. Server is accessible at $baseUrl" -ForegroundColor White
    Write-Host "   3. Connection string in appsettings.json is correct" -ForegroundColor White
    exit 1
}

