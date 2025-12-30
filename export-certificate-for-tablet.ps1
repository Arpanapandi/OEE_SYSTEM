# Script untuk export development certificate agar bisa di-install di tablet/device lain
# Jalankan sebagai Administrator

Write-Host "📱 Export Development Certificate untuk Tablet/Device Lain..." -ForegroundColor Yellow
Write-Host ""

# 1. Cek certificate
Write-Host "1️⃣  Mencari development certificate..." -ForegroundColor Cyan
$certThumbprint = (dotnet dev-certs https --check 2>&1 | Select-String -Pattern "([A-F0-9]{40})").Matches.Value

if (-not $certThumbprint) {
    Write-Host "   ❌ Development certificate tidak ditemukan!" -ForegroundColor Red
    Write-Host "   💡 Jalankan: dotnet dev-certs https --trust" -ForegroundColor Yellow
    exit 1
}

Write-Host "   ✅ Certificate ditemukan: $certThumbprint" -ForegroundColor Green
Write-Host ""

# 2. Export certificate ke file
Write-Host "2️⃣  Mengekspor certificate ke file..." -ForegroundColor Cyan

$certPath = "$PSScriptRoot\aspnetcore-dev-cert.pfx"
$certPassword = "DevCertPassword123!" # Password untuk certificate

# Export certificate
$cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $certThumbprint }

if ($cert) {
    try {
        # Export ke PFX (dengan password)
        $securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
        Export-PfxCertificate -Certificate $cert -FilePath $certPath -Password $securePassword -ErrorAction Stop
        
        Write-Host "   ✅ Certificate berhasil diekspor ke: $certPath" -ForegroundColor Green
        Write-Host "   🔑 Password: $certPassword" -ForegroundColor Yellow
        Write-Host ""
        
        # Export ke CER (tanpa password, untuk install di device)
        $cerPath = "$PSScriptRoot\aspnetcore-dev-cert.cer"
        Export-Certificate -Cert $cert -FilePath $cerPath -ErrorAction Stop
        
        Write-Host "   ✅ Certificate (CER format) berhasil diekspor ke: $cerPath" -ForegroundColor Green
        Write-Host ""
        
    } catch {
        Write-Host "   ❌ Gagal mengekspor certificate: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "   ❌ Certificate tidak ditemukan di certificate store" -ForegroundColor Red
    exit 1
}

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
    Write-Host "   ⚠️  IP WiFi tidak ditemukan" -ForegroundColor Yellow
    $ipAddress = "[IP_WIFI_PC_ANDA]"
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Certificate berhasil diekspor!" -ForegroundColor Green
Write-Host ""
Write-Host "📁 File yang dibuat:" -ForegroundColor Cyan
Write-Host "   1. $certPath (PFX - dengan password)" -ForegroundColor White
Write-Host "   2. $cerPath (CER - untuk install di tablet)" -ForegroundColor White
Write-Host ""
Write-Host "📱 Cara Install Certificate di Tablet:" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Untuk Android Tablet:" -ForegroundColor Yellow
Write-Host "   1. Copy file 'aspnetcore-dev-cert.cer' ke tablet" -ForegroundColor White
Write-Host "   2. Buka file .cer di tablet" -ForegroundColor White
Write-Host "   3. Pilih 'Install' atau 'Trust'" -ForegroundColor White
Write-Host "   4. Beri nama: 'ASP.NET Core Development Certificate'" -ForegroundColor White
Write-Host "   5. Pilih 'VPN and apps' atau 'System' untuk trust level" -ForegroundColor White
Write-Host ""
Write-Host "   Untuk iPad/iPhone:" -ForegroundColor Yellow
Write-Host "   1. Copy file 'aspnetcore-dev-cert.cer' ke iPad via AirDrop/Email" -ForegroundColor White
Write-Host "   2. Buka file .cer di iPad" -ForegroundColor White
Write-Host "   3. Settings → General → VPN & Device Management" -ForegroundColor White
Write-Host "   4. Tap certificate → Install → Trust" -ForegroundColor White
Write-Host ""
Write-Host "   Alternatif (Lebih Mudah):" -ForegroundColor Yellow
Write-Host "   - Akses https://$ipAddress:6002 dari tablet" -ForegroundColor White
Write-Host "   - Browser akan meminta trust certificate" -ForegroundColor White
Write-Host "   - Klik 'Advanced' → 'Proceed' atau 'Accept'" -ForegroundColor White
Write-Host ""
Write-Host "🌐 URL untuk akses dari tablet:" -ForegroundColor Cyan
Write-Host "   HTTPS: https://$ipAddress:6002" -ForegroundColor White
Write-Host ""
Write-Host "💡 Catatan:" -ForegroundColor Yellow
Write-Host "   - Pastikan tablet terhubung ke WiFi yang sama" -ForegroundColor White
Write-Host "   - Firewall port 6002 harus terbuka (jalankan .\open-firewall-port.ps1)" -ForegroundColor White
Write-Host "   - Setelah install certificate, restart browser di tablet" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

