# Script untuk memperbaiki masalah HTTPS di tablet
# Jalankan sebagai Administrator

Write-Host "🔧 Memperbaiki Masalah HTTPS untuk Tablet..." -ForegroundColor Yellow
Write-Host ""

# 1. Cek dan buka firewall
Write-Host "1️⃣  Membuka firewall untuk port HTTPS 6002..." -ForegroundColor Cyan
New-NetFirewallRule -DisplayName "OEE System - HTTPS Port 6002" `
    -Direction Inbound `
    -LocalPort 6002 `
    -Protocol TCP `
    -Action Allow `
    -ErrorAction SilentlyContinue

if ($LASTEXITCODE -eq 0 -or $?) {
    Write-Host "   ✅ Port 6002 (HTTPS) berhasil dibuka!" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Port mungkin sudah terbuka" -ForegroundColor Yellow
}

Write-Host ""

# 2. Export certificate untuk tablet
Write-Host "2️⃣  Mengekspor certificate untuk tablet..." -ForegroundColor Cyan

$certThumbprint = (dotnet dev-certs https --check 2>&1 | Select-String -Pattern "([A-F0-9]{40})").Matches.Value

if ($certThumbprint) {
    $cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $certThumbprint }
    
    if ($cert) {
        $cerPath = "$PSScriptRoot\aspnetcore-dev-cert.cer"
        try {
            Export-Certificate -Cert $cert -FilePath $cerPath -ErrorAction Stop
            Write-Host "   ✅ Certificate berhasil diekspor ke: $cerPath" -ForegroundColor Green
        } catch {
            Write-Host "   ⚠️  Gagal export certificate: $_" -ForegroundColor Yellow
        }
    }
}

Write-Host ""

# 3. Tampilkan IP dan instruksi
$ipAddress = (ipconfig | Select-String "IPv4" | Select-Object -First 1) -replace '.*:\s*', ''

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Setup selesai!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 URL untuk akses dari tablet:" -ForegroundColor Cyan
Write-Host "   HTTP:  http://$ipAddress:6001" -ForegroundColor White
Write-Host "   HTTPS: https://$ipAddress:6002" -ForegroundColor White
Write-Host ""
Write-Host "📱 Cara Akses HTTPS di Tablet:" -ForegroundColor Yellow
Write-Host "   1. Buka browser di tablet" -ForegroundColor White
Write-Host "   2. Masukkan: https://$ipAddress:6002" -ForegroundColor White
Write-Host "   3. Jika muncul peringatan 'Not Secure':" -ForegroundColor White
Write-Host "      - Chrome/Android: Klik 'Advanced' → 'Proceed to $ipAddress (unsafe)'" -ForegroundColor White
Write-Host "      - Safari/iPad: Klik 'Advanced' → 'Proceed to $ipAddress'" -ForegroundColor White
Write-Host "   4. Aplikasi akan terbuka via HTTPS" -ForegroundColor White
Write-Host ""
Write-Host "📷 Untuk Akses Kamera (Scanner):" -ForegroundColor Yellow
Write-Host "   - Pastikan menggunakan HTTPS (https://$ipAddress:6002)" -ForegroundColor White
Write-Host "   - Saat browser meminta izin kamera, klik 'Allow'" -ForegroundColor White
Write-Host "   - Jika kamera tidak muncul, refresh halaman" -ForegroundColor White
Write-Host ""
Write-Host "💡 Troubleshooting:" -ForegroundColor Cyan
Write-Host "   - Jika HTTPS tidak bisa: Pastikan firewall port 6002 terbuka" -ForegroundColor White
Write-Host "   - Jika kamera error: Pastikan menggunakan HTTPS, bukan HTTP" -ForegroundColor White
Write-Host "   - Jika certificate error: Klik 'Advanced' → 'Proceed' di browser" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

