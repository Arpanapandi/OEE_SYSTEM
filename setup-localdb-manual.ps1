# Script untuk setup LocalDB secara manual
# Jika sqllocaldb command tidak tersedia, script ini akan memberikan instruksi

Write-Host "🔍 Checking LocalDB Installation..." -ForegroundColor Cyan
Write-Host ""

# Cek apakah LocalDB terinstall dengan cara lain
$localDbPath = "$env:ProgramFiles\Microsoft SQL Server\150\LocalDB\Binn\SqlLocalDB.exe"
$localDbPathAlt = "${env:ProgramFiles(x86)}\Microsoft SQL Server\150\LocalDB\Binn\SqlLocalDB.exe"

if (Test-Path $localDbPath) {
    Write-Host "✅ Found LocalDB at: $localDbPath" -ForegroundColor Green
    & $localDbPath info
} elseif (Test-Path $localDbPathAlt) {
    Write-Host "✅ Found LocalDB at: $localDbPathAlt" -ForegroundColor Green
    & $localDbPathAlt info
} else {
    Write-Host "❌ LocalDB not found in standard locations" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 SOLUSI:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Opsi 1: Install SQL Server Express LocalDB" -ForegroundColor Cyan
    Write-Host "   Download: https://go.microsoft.com/fwlink/?LinkID=866658" -ForegroundColor Gray
    Write-Host "   Atau install melalui Visual Studio Installer" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Opsi 2: Gunakan SQL Server yang sudah ada" -ForegroundColor Cyan
    Write-Host "   Edit appsettings.json dan ubah connection string ke:" -ForegroundColor Gray
    Write-Host "   Server=.\\SERVERVJEST;Database=OeeSystemDb;..." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Opsi 3: Gunakan SQL Server Express" -ForegroundColor Cyan
    Write-Host "   Jika sudah install SQL Server Express, gunakan:" -ForegroundColor Gray
    Write-Host "   Server=.\\SQLEXPRESS;Database=OeeSystemDb;..." -ForegroundColor Gray
    Write-Host ""
}

