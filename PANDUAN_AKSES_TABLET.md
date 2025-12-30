# 📱 Panduan Akses Aplikasi dari Tablet

## ✅ Checklist Sebelum Mulai

1. ✅ PC dan Tablet terhubung ke **WiFi yang sama**
2. ✅ Firewall port 6001 dan 6002 sudah terbuka
3. ✅ Aplikasi sedang berjalan (`dotnet run`)
4. ✅ IP WiFi PC: `10.14.180.198`

---

## 🌐 URL untuk Akses dari Tablet

- **HTTP**: `http://10.14.180.198:6001`
- **HTTPS**: `https://10.14.180.198:6002` ⭐ **Gunakan ini untuk kamera**

---

## 📋 Langkah-langkah Akses dari Tablet

### **Opsi 1: Akses Langsung (Paling Mudah)**

1. **Buka browser di tablet** (Chrome, Firefox, Safari, dll)

2. **Masukkan URL HTTPS**:
   ```
   https://10.14.180.198:6002
   ```

3. **Browser akan menampilkan peringatan "Not Secure"**:
   - **Chrome/Android**: 
     - Klik "Advanced" → "Proceed to 10.14.180.198 (unsafe)"
   - **Safari/iPad**: 
     - Klik "Advanced" → "Proceed to 10.14.180.198"
   - **Firefox**: 
     - Klik "Advanced" → "Accept the Risk and Continue"

4. **Aplikasi akan terbuka!** ✅

5. **Untuk akses kamera (scanner)**:
   - Saat browser meminta izin kamera, klik **"Allow"**
   - Jika tidak muncul, cek pengaturan browser:
     - **Chrome**: Settings → Site settings → Camera
     - **Safari**: Settings → Safari → Camera

---

### **Opsi 2: Install Certificate Manual (Lebih Aman)**

Jika Opsi 1 tidak bekerja atau ingin lebih aman:

#### **A. Export Certificate dari PC**

1. **Jalankan script export** (di PC):
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\export-certificate-for-tablet.ps1
   ```

2. **File yang dibuat**:
   - `aspnetcore-dev-cert.cer` ← Copy ini ke tablet

#### **B. Install Certificate di Tablet**

**Untuk Android Tablet:**
1. Copy file `aspnetcore-dev-cert.cer` ke tablet (via USB, email, atau cloud)
2. Buka file `.cer` di tablet
3. Pilih **"Install"** atau **"Trust"**
4. Beri nama: `ASP.NET Core Development Certificate`
5. Pilih **"VPN and apps"** atau **"System"** untuk trust level
6. Restart browser

**Untuk iPad/iPhone:**
1. Copy file `aspnetcore-dev-cert.cer` ke iPad (via AirDrop, email, atau cloud)
2. Buka file `.cer` di iPad
3. Settings → **General** → **VPN & Device Management**
4. Tap certificate → **Install** → **Trust**
5. Restart browser

---

## 🔧 Troubleshooting

### ❌ **Tablet tidak bisa mengakses aplikasi**

**Cek 1: Koneksi WiFi**
- Pastikan tablet dan PC terhubung ke **WiFi yang sama**
- Cek IP PC: `ipconfig` (harus `10.14.180.198`)
- Cek IP tablet: Settings → WiFi → (tap network) → IP Address

**Cek 2: Firewall**
- Pastikan firewall port 6001 dan 6002 terbuka di PC
- Jalankan: `.\open-firewall-port.ps1` (sebagai Administrator)

**Cek 3: Aplikasi berjalan**
- Pastikan aplikasi sedang berjalan: `dotnet run`
- Cek di PC: buka `http://localhost:6001` (harus bisa)

**Cek 4: Ping dari tablet**
- Di tablet, buka terminal/command prompt
- Ping PC: `ping 10.14.180.198`
- Harus ada response (jika tidak, masalah network)

---

### ❌ **Browser menampilkan "Connection Refused" atau "Can't Connect"**

**Solusi:**
1. Pastikan aplikasi sedang berjalan di PC
2. Pastikan menggunakan port yang benar:
   - HTTP: `6001`
   - HTTPS: `6002`
3. Cek firewall Windows di PC
4. Coba akses dari PC sendiri dulu: `http://localhost:6001`

---

### ❌ **Certificate tidak trusted di tablet**

**Solusi:**
1. Gunakan Opsi 2 (Install Certificate Manual) di atas
2. Atau, di browser tablet:
   - Chrome: Settings → Privacy → Security → Manage certificates
   - Safari: Settings → General → About → Certificate Trust Settings
3. Trust certificate untuk `10.14.180.198`

---

### ❌ **Kamera tidak bisa diakses di tablet**

**Solusi:**
1. **Pastikan menggunakan HTTPS**: `https://10.14.180.198:6002`
   - Bukan HTTP: `http://10.14.180.198:6001`

2. **Berikan izin kamera di browser**:
   - Saat browser meminta izin, klik **"Allow"**
   - Jika tidak muncul, cek pengaturan:
     - **Chrome**: Settings → Site settings → Camera → Allow
     - **Safari**: Settings → Safari → Camera → Allow

3. **Restart browser** setelah memberikan izin

4. **Cek console browser** (jika ada DevTools):
   - Buka DevTools (F12 atau menu)
   - Lihat tab Console untuk error terkait kamera

---

## 📞 Quick Test

**Test dari PC sendiri:**
```powershell
# Test HTTP
Start-Process "http://localhost:6001"

# Test HTTPS
Start-Process "https://localhost:6002"
```

**Test dari tablet:**
- Buka browser di tablet
- Masukkan: `https://10.14.180.198:6002`
- Harus bisa akses aplikasi

---

## ✅ Checklist Final

- [ ] PC dan tablet terhubung ke WiFi yang sama
- [ ] Aplikasi berjalan di PC (`dotnet run`)
- [ ] Firewall port 6001 dan 6002 terbuka
- [ ] Bisa akses dari tablet: `https://10.14.180.198:6002`
- [ ] Certificate trusted di tablet (jika perlu)
- [ ] Izin kamera diberikan di browser tablet
- [ ] Scanner bisa digunakan di tablet

---

## 🎯 Tips

1. **Gunakan HTTPS** untuk akses kamera (scanner)
2. **Bookmark URL** di tablet untuk akses cepat
3. **Pastikan WiFi stabil** untuk performa optimal
4. **Restart browser** jika ada masalah dengan certificate atau kamera
5. **Cek IP PC** jika berubah: `ipconfig | findstr IPv4`

---

**Masih ada masalah?** Cek console browser di tablet (F12) untuk melihat error detail.

