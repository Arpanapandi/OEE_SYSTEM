# Script untuk setup HTTPS development certificate
# Jalankan sebagai Administrator

Write-Host "🔐 Setup HTTPS Development Certificate..." -ForegroundColor Yellow
Write-Host ""

# 1. Cek apakah development certificate sudah ada dan trusted
Write-Host "1️⃣  Mengecek development certificate..." -ForegroundColor Cyan

# Cek apakah certificate ada
$certCheck = dotnet dev-certs https --check 2>&1
$certExists = $LASTEXITCODE -eq 0

if (-not $certExists) {
    Write-Host "   ⚠️  Development certificate belum ada" -ForegroundColor Yellow
    Write-Host "   📝 Membuat development certificate..." -ForegroundColor Cyan
    
    # Buat dan trust development certificate
    dotnet dev-certs https --clean
    dotnet dev-certs https --trust
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Development certificate berhasil dibuat dan di-trust" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Gagal membuat development certificate" -ForegroundColor Red
        Write-Host "   💡 Coba jalankan manual:" -ForegroundColor Yellow
        Write-Host "      dotnet dev-certs https --clean" -ForegroundColor White
        Write-Host "      dotnet dev-certs https --trust" -ForegroundColor White
    }
} else {
    Write-Host "   ✅ Development certificate sudah ada" -ForegroundColor Green
    
    # Pastikan certificate sudah di-trust
    Write-Host "   🔍 Memverifikasi certificate sudah di-trust..." -ForegroundColor Cyan
    dotnet dev-certs https --trust --check 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ⚠️  Certificate belum di-trust, mencoba trust ulang..." -ForegroundColor Yellow
        dotnet dev-certs https --trust
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ Certificate berhasil di-trust" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  Gagal trust certificate, mungkin perlu restart browser" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ✅ Certificate sudah di-trust" -ForegroundColor Green
    }
}

Write-Host ""

# 2. Buka firewall untuk port HTTPS 6002
Write-Host "2️⃣  Membuka port 6002 (HTTPS) di Windows Firewall..." -ForegroundColor Cyan

New-NetFirewallRule -DisplayName "OEE System - HTTPS Port 6002" `
    -Direction Inbound `
    -LocalPort 6002 `
    -Protocol TCP `
    -Action Allow `
    -ErrorAction SilentlyContinue

if ($LASTEXITCODE -eq 0 -or $?) {
    Write-Host "   ✅ Port 6002 (HTTPS) berhasil dibuka di Windows Firewall!" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Port mungkin sudah terbuka atau perlu menjalankan sebagai Administrator" -ForegroundColor Yellow
}

Write-Host ""

# 3. Tampilkan IP WiFi
Write-Host "3️⃣  Mendapatkan IP WiFi..." -ForegroundColor Cyan
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

if ($ipAddress) {
    Write-Host "   ✅ IP WiFi: $ipAddress" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  IP WiFi tidak ditemukan, cek manual dengan: ipconfig" -ForegroundColor Yellow
    $ipAddress = "[IP_WIFI_PC_ANDA]"
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Setup HTTPS selesai!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 URL untuk akses dari device lain:" -ForegroundColor Cyan
Write-Host "   HTTP:  http://$ipAddress:6001" -ForegroundColor White
Write-Host "   HTTPS: https://$ipAddress:6002" -ForegroundColor White
Write-Host ""
Write-Host "📱 Catatan untuk HTTPS:" -ForegroundColor Yellow
Write-Host "   - Browser mungkin menampilkan peringatan 'Not Secure'" -ForegroundColor Yellow
Write-Host "   - Ini normal untuk development certificate" -ForegroundColor Yellow
Write-Host "   - Klik 'Advanced' → 'Proceed to [IP]' untuk melanjutkan" -ForegroundColor Yellow
Write-Host ""
Write-Host "📷 Untuk Akses Kamera di HTTPS:" -ForegroundColor Cyan
Write-Host "   1. Pastikan menggunakan URL HTTPS (https://$ipAddress:6002)" -ForegroundColor White
Write-Host "   2. Saat browser meminta izin kamera, klik 'Allow'" -ForegroundColor White
Write-Host "   3. Jika kamera masih tidak bisa diakses:" -ForegroundColor White
Write-Host "      - Restart browser setelah setup certificate" -ForegroundColor White
Write-Host "      - Cek pengaturan browser: Settings → Privacy → Camera" -ForegroundColor White
Write-Host "      - Pastikan izin kamera untuk situs ini diaktifkan" -ForegroundColor White
Write-Host ""
Write-Host "💡 Untuk device lain (tablet/PC):" -ForegroundColor Cyan
Write-Host "   - Pastikan terhubung ke WiFi yang sama" -ForegroundColor White
Write-Host "   - Gunakan URL HTTPS untuk koneksi aman dan akses kamera" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

