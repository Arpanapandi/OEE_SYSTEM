# 🏠 Development Offline - Setup Guide

Panduan lengkap untuk setup development offline menggunakan LocalDB, agar bisa bekerja di rumah tanpa koneksi ke server kantor.

---

## 🎯 Quick Start (3 Langkah)

### 1️⃣ Install LocalDB

**Download dan Install:**
- **SQL Server Express** (termasuk LocalDB): https://www.microsoft.com/en-us/sql-server/sql-server-downloads
- Pilih **"Express"** edition (gratis)
- Saat install, pastikan **"LocalDB"** tercentang

**Atau via Visual Studio Installer:**
- Buka Visual Studio Installer
- Modify → Individual Components
- Centang **"SQL Server Express LocalDB"**

### 2️⃣ Setup Aplikasi

Setelah LocalDB terinstall, jalankan di PowerShell:

```powershell
# Bypass execution policy (hanya untuk session ini)
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

# Jalankan script setup
.\setup-offline-dev.ps1
```

**ATAU** manual edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb;Trusted_Connection=True;MultipleActiveResultSets=true",
    "HossConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb_Hoss;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### 3️⃣ Jalankan Aplikasi

```powershell
dotnet run
```

Database akan **otomatis dibuat** saat pertama kali dijalankan! ✅

---

## 📋 Verifikasi LocalDB

Setelah install, verifikasi dengan:

```powershell
# Cek LocalDB terinstall
sqllocaldb info

# Cek instance MSSQLLocalDB
sqllocaldb info MSSQLLocalDB

# Start instance (jika belum running)
sqllocaldb start MSSQLLocalDB
```

Jika command `sqllocaldb` tidak ditemukan:
- Restart PowerShell setelah install
- Atau restart komputer
- Pastikan LocalDB benar-benar terinstall

---

## 🔄 Switch Antara Offline dan Online

### Development Offline (LocalDB) - Di Rumah

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb;...",
    "HossConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb_Hoss;..."
  }
}
```

### Production/Di Kantor (SQL Server)

**Opsi 1: Gunakan appsettings.Production.json**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run
```

**Opsi 2: Edit appsettings.json langsung**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SERVERVJEST;Database=OeeSystemDb;...",
    "HossConnection": "Server=.\\SERVERVJEST;Database=OeeSystemDb_Hoss;..."
  }
}
```

---

## 📁 File Penting

- **`INSTALL-LOCALDB.md`** - Panduan detail install LocalDB
- **`setup-offline-dev.ps1`** - Script otomatis setup offline dev
- **`appsettings.json`** - Config untuk development (LocalDB)
- **`appsettings.Production.json`** - Config untuk production (SQL Server)
- **`appsettings.Alternative.json`** - Backup config SQL Server

---

## 🔍 Troubleshooting

### ❌ "sqllocaldb is not recognized"

**Solusi:**
1. Pastikan LocalDB sudah terinstall
2. Restart PowerShell
3. Jika masih error, tambahkan ke PATH:
   ```
   C:\Program Files\Microsoft SQL Server\150\LocalDB\Binn
   ```

### ❌ "Execution Policy" error

**Solusi:**
```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
```

Atau edit `appsettings.json` manual tanpa script.

### ❌ "Cannot create database"

**Solusi:**
1. Pastikan LocalDB instance running:
   ```powershell
   sqllocaldb start MSSQLLocalDB
   ```
2. Cek permission folder database
3. Run PowerShell as Administrator

### ❌ "A network-related error"

**Solusi:**
1. Pastikan connection string benar: `Server=(localdb)\\MSSQLLocalDB`
2. Start instance: `sqllocaldb start MSSQLLocalDB`
3. Cek instance: `sqllocaldb info MSSQLLocalDB`

---

## ✅ Checklist Setup Offline

- [ ] LocalDB terinstall (`sqllocaldb info` berjalan)
- [ ] Instance MSSQLLocalDB ada dan running
- [ ] `appsettings.json` menggunakan LocalDB
- [ ] Aplikasi bisa dijalankan (`dotnet run`)
- [ ] Database otomatis dibuat saat first run
- [ ] Bisa development tanpa koneksi internet/WiFi

---

## 🎉 Keuntungan Development Offline

✅ **Fleksibel** - Bisa kerja di rumah tanpa WiFi  
✅ **Cepat** - Database lokal lebih cepat dari remote  
✅ **Aman** - Tidak perlu koneksi ke server kantor  
✅ **Mudah** - Database otomatis dibuat, tidak perlu setup manual  
✅ **Portable** - Database tersimpan lokal di komputer Anda  

---

## 📞 Butuh Bantuan?

1. Baca **`INSTALL-LOCALDB.md`** untuk panduan detail
2. Cek console output saat `dotnet run` untuk error detail
3. Pastikan LocalDB terinstall dan running

---

**Selamat Development Offline! 🚀**

