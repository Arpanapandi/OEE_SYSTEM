# 📥 Install LocalDB - Panduan Lengkap

## 🎯 Tujuan
Install LocalDB agar proyek bisa berjalan **tanpa koneksi server**, mempermudah development offline.

---

## ✅ Cara Install (Pilih Salah Satu)

### **Method 1: Via Visual Studio Installer** (Paling Mudah - Recommended)

Jika Anda punya Visual Studio terinstall:

1. **Buka Visual Studio Installer**
   - Cari di Start Menu: "Visual Studio Installer"
   - Atau jalankan: `C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe`

2. **Modify Visual Studio**
   - Klik **"Modify"** pada Visual Studio yang terinstall

3. **Install LocalDB**
   - Buka tab **"Individual Components"**
   - Cari **"SQL Server Express LocalDB"**
   - **Centang** checkbox
   - Klik **"Modify"** dan tunggu selesai (~1-2 menit)

4. **Restart Command Prompt**
   - Tutup dan buka kembali PowerShell/CMD

5. **Setup Project**
   ```cmd
   INSTALL-LOCALDB-COMPLETE.bat
   ```

---

### **Method 2: Download SQL Server Express** (Universal)

Cocok untuk semua Windows, tidak perlu Visual Studio:

1. **Download SQL Server Express**
   - Link: https://go.microsoft.com/fwlink/?LinkID=866658
   - Atau: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - Pilih **"Express"** edition

2. **Install**
   - Jalankan installer yang didownload
   - Pilih **"Basic"** installation type
   - LocalDB akan otomatis tercentang
   - Klik **"Install"** dan tunggu selesai (~2-3 menit)

3. **Restart Command Prompt**
   - Tutup dan buka kembali PowerShell/CMD

4. **Setup Project**
   ```cmd
   INSTALL-LOCALDB-COMPLETE.bat
   ```

---

## 🚀 Setup Otomatis

Setelah LocalDB terinstall, jalankan script setup:

```cmd
INSTALL-LOCALDB-COMPLETE.bat
```

Script akan:
- ✅ Cek apakah LocalDB sudah terinstall
- ✅ Setup instance `MSSQLLocalDB`
- ✅ Update `appsettings.json` untuk LocalDB
- ✅ Verify setup

**ATAU** jika LocalDB sudah terinstall:

```cmd
setup-localdb-dev.bat
```

---

## 🔍 Verifikasi Install

Setelah install, verifikasi dengan:

```cmd
sqllocaldb info
```

Jika muncul daftar instance = ✅ **Berhasil!**

---

## 🎯 Run Aplikasi

Setelah setup selesai:

```cmd
dotnet run
```

Aplikasi akan:
- ✅ Terhubung ke LocalDB (tidak perlu server)
- ✅ Membuat database otomatis
- ✅ Seed data dummy
- ✅ Bisa diakses di: http://localhost:6001

---

## 🔧 Troubleshooting

### "sqllocaldb is not recognized"

**Solusi:**
1. **Restart command prompt** setelah install
2. Jika masih error, **restart komputer**
3. Cek PATH environment variable

### "Cannot create database"

**Solusi:**
```cmd
sqllocaldb start MSSQLLocalDB
```

### Install gagal

**Solusi:**
1. Uninstall SQL Server Express yang partial
2. Restart komputer
3. Install ulang dengan "Basic" option

### Connection error saat run

**Solusi:**
1. Pastikan instance berjalan:
   ```cmd
   sqllocaldb start MSSQLLocalDB
   ```
2. Cek `appsettings.json` menggunakan LocalDB:
   ```json
   "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;..."
   ```

---

## 📋 Checklist

- [ ] Install LocalDB (Method 1 atau 2)
- [ ] Restart command prompt
- [ ] Verify: `sqllocaldb info`
- [ ] Run: `INSTALL-LOCALDB-COMPLETE.bat`
- [ ] Run: `dotnet run`
- [ ] Aplikasi bisa diakses di http://localhost:6001

---

## 💡 Tips

- **Gunakan "Basic" installation** - Paling mudah dan reliable
- **Restart command prompt** setelah install - Penting untuk refresh PATH
- **Jalankan setup script** setelah install - Otomatis konfigurasi semua
- **Development offline** - Tidak perlu koneksi server setelah setup

---

## 🎉 Setelah Setup

Proyek Anda sekarang:
- ✅ Bisa development **offline** (tanpa server)
- ✅ Database **local** (tidak perlu koneksi WiFi)
- ✅ **Mudah development** di mana saja
- ✅ Bisa **switch ke SQL Server** untuk production dengan mudah

---

**Selamat! Proyek siap untuk development offline! 🚀**

