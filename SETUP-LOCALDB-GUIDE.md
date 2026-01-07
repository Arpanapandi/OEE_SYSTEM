# 🚀 Setup LocalDB untuk Development - Panduan Lengkap

## 📋 Overview

Panduan ini akan membantu Anda setup LocalDB untuk development offline yang fleksibel.

**Keuntungan LocalDB:**
- ✅ Development offline (tidak perlu WiFi/server)
- ✅ Database lokal di komputer Anda
- ✅ Cepat dan mudah setup
- ✅ Database otomatis dibuat saat first run
- ✅ Fleksibel untuk kerja di rumah atau kantor

---

## ⚡ Quick Setup (3 Langkah)

### 1️⃣ Install LocalDB

**Download:**
- **Link langsung:** https://go.microsoft.com/fwlink/?LinkID=866658
- **Atau:** https://www.microsoft.com/en-us/sql-server/sql-server-downloads

**Install:**
1. Download SQL Server Express
2. Pilih **"Express"** edition
3. Pilih **"Basic"** installation type
4. LocalDB otomatis tercentang
5. Klik **"Install"** dan tunggu selesai (~2-3 menit)

### 2️⃣ Setup dengan Script Otomatis

Setelah install, restart command prompt dan jalankan:

```cmd
setup-localdb-dev.bat
```

Script akan:
- ✅ Cek LocalDB terinstall
- ✅ Setup instance MSSQLLocalDB
- ✅ Update appsettings.json ke LocalDB
- ✅ Verify setup

### 3️⃣ Run Aplikasi

```cmd
dotnet run
```

Aplikasi akan:
- ✅ Terhubung ke LocalDB
- ✅ Membuat database otomatis
- ✅ Seed data dummy
- ✅ Bisa diakses di: http://localhost:6001

---

## 📁 File yang Tersedia

### Script Setup
- **`setup-localdb-dev.bat`** - Script lengkap setup LocalDB
- **`install-and-run.bat`** - Install + Setup + Run otomatis
- **`install-all-prerequisites.bat`** - Check & install semua prerequisites

### Konfigurasi
- **`appsettings.json`** - Config utama (sudah set ke LocalDB)
- **`appsettings.Development.json`** - Config untuk development
- **`appsettings.Production.json`** - Config untuk production (SQL Server)

### Dokumentasi
- **`SETUP-LOCALDB-GUIDE.md`** - Panduan ini
- **`QUICK-INSTALL-LOCALDB.md`** - Quick install guide
- **`README-OFFLINE-DEV.md`** - Panduan development offline

---

## 🔍 Verifikasi Setup

Setelah install LocalDB, verifikasi:

```cmd
# Cek LocalDB terinstall
sqllocaldb info

# Cek instance MSSQLLocalDB
sqllocaldb info MSSQLLocalDB

# Start instance
sqllocaldb start MSSQLLocalDB
```

Jika semua command berjalan tanpa error = ✅ **Setup berhasil!**

---

## 🎯 Konfigurasi yang Sudah Disiapkan

### appsettings.json (Default - LocalDB)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb;...",
    "HossConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb_Hoss;..."
  }
}
```

### appsettings.Development.json (LocalDB)
- Sudah dikonfigurasi untuk LocalDB
- Aktif saat `ASPNETCORE_ENVIRONMENT=Development`

### appsettings.Production.json (SQL Server)
- Untuk production di kantor
- Aktif saat `ASPNETCORE_ENVIRONMENT=Production`

---

## 🔄 Workflow Development

### Development Offline (Di Rumah)
```cmd
# Pastikan appsettings.json menggunakan LocalDB
dotnet run
```

### Production/Di Kantor (SQL Server)
```cmd
# Set environment ke Production
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run
```

---

## ❌ Troubleshooting

### Error: "sqllocaldb is not recognized"
**Solusi:**
1. Pastikan LocalDB sudah terinstall
2. Restart command prompt
3. Jika masih error, restart komputer

### Error: "Cannot create database"
**Solusi:**
```cmd
sqllocaldb start MSSQLLocalDB
```

### Error: "A network-related error"
**Solusi:**
1. Pastikan connection string: `Server=(localdb)\\MSSQLLocalDB`
2. Start instance: `sqllocaldb start MSSQLLocalDB`
3. Cek instance: `sqllocaldb info MSSQLLocalDB`

### Error: "Execution Policy"
**Solusi:**
- Gunakan script `.bat` (tidak perlu execution policy)
- Atau: `Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process`

---

## 📍 Lokasi Database

Database LocalDB tersimpan di:
```
C:\Users\[USERNAME]\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\
```

Untuk melihat database:
- **SQL Server Management Studio (SSMS)**
- **Azure Data Studio**
- Server name: `(localdb)\MSSQLLocalDB`
- Authentication: Windows Authentication

---

## ✅ Checklist Setup

- [ ] LocalDB terinstall (`sqllocaldb info` berjalan)
- [ ] Instance MSSQLLocalDB ada dan running
- [ ] `appsettings.json` menggunakan LocalDB
- [ ] Script `setup-localdb-dev.bat` berjalan sukses
- [ ] Aplikasi bisa dijalankan (`dotnet run`)
- [ ] Database otomatis dibuat saat first run
- [ ] Aplikasi bisa diakses di http://localhost:6001

---

## 🎉 Selesai!

Setelah setup selesai, Anda bisa:
- ✅ Development offline di rumah
- ✅ Tidak perlu koneksi ke server kantor
- ✅ Database lokal yang cepat
- ✅ Mudah switch ke SQL Server untuk production

**Selamat Development! 🚀**


