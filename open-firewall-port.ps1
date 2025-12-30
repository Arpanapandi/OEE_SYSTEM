# Script untuk membuka port 6001 (HTTP) dan 6002 (HTTPS) di Windows Firewall
# Jalankan sebagai Administrator

Write-Host "🔓 Membuka port 6001 (HTTP) dan 6002 (HTTPS) di Windows Firewall..." -ForegroundColor Yellow

# Tambahkan inbound rule untuk port 6001 (HTTP)
New-NetFirewallRule -DisplayName "OEE System - HTTP Port 6001" `
    -Direction Inbound `
    -LocalPort 6001 `
    -Protocol TCP `
    -Action Allow `
    -ErrorAction SilentlyContinue

# Tambahkan inbound rule untuk port 6002 (HTTPS)
New-NetFirewallRule -DisplayName "OEE System - HTTPS Port 6002" `
    -Direction Inbound `
    -LocalPort 6002 `
    -Protocol TCP `
    -Action Allow `
    -ErrorAction SilentlyContinue

if ($LASTEXITCODE -eq 0 -or $?) {
    Write-Host "✅ Port 6001 (HTTP) dan 6002 (HTTPS) berhasil dibuka di Windows Firewall!" -ForegroundColor Green
    Write-Host "📱 Aplikasi sekarang bisa diakses dari device lain di jaringan yang sama" -ForegroundColor Cyan
} else {
    Write-Host "⚠️  Port mungkin sudah terbuka atau perlu menjalankan sebagai Administrator" -ForegroundColor Yellow
}

# Tampilkan IP WiFi
$ipAddress = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {
    $_.InterfaceAlias -like '*Wi-Fi*' -or 
    $_.InterfaceAlias -like '*WLAN*' -or 
    $_.InterfaceAlias -like '*Wireless*' -or
    $_.IPAddress -like '10.14.*' -or
    $_.IPAddress -like '192.168.*'
} | Select-Object -First 1).IPAddress

if (-not $ipAddress) {
    $ipAddress = (ipconfig | Select-String "IPv4" | Select-Object -First 1) -replace '.*:\s*', ''
}

if (-not $ipAddress) {
    $ipAddress = "10.14.180.198"
}

Write-Host ""
Write-Host "🌐 URL untuk akses dari device lain:" -ForegroundColor Cyan
Write-Host "   HTTP:  http://$ipAddress:6001" -ForegroundColor White
Write-Host "   HTTPS: https://$ipAddress:6002" -ForegroundColor White
Write-Host ""
Write-Host "💡 Catatan: Pastikan PC dan device lain terhubung ke WiFi yang sama" -ForegroundColor Yellow

