# 🔧 Fix Database Connection Error

## ❌ Error yang Terjadi

```
The maximum number of retries (3) was exceeded while executing database operations
```

**Penyebab:**
- LocalDB belum terinstall
- SQL Server `.\\SERVERVJEST` tidak bisa diakses

## ✅ SOLUSI: Install LocalDB (Recommended)

### Langkah 1: Install LocalDB

**Download & Install:**
1. Buka: https://go.microsoft.com/fwlink/?LinkID=866658
2. Atau: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
3. Pilih **"Express"** edition
4. Download dan jalankan installer
5. Pilih **"Basic"** installation (LocalDB otomatis tercentang)
6. Tunggu instalasi selesai (~2-3 menit)

### Langkah 2: Setup & Run

Setelah install, restart command prompt dan jalankan:

```cmd
install-and-run.bat
```

Script akan:
- ✅ Setup LocalDB instance
- ✅ Update appsettings.json ke LocalDB
- ✅ Run aplikasi langsung

### Langkah 3: Verifikasi

Aplikasi akan:
- ✅ Terhubung ke LocalDB tanpa error
- ✅ Membuat database otomatis
- ✅ Seed data dummy
- ✅ Bisa diakses di: http://localhost:6001

---

## 🔄 Alternatif: Fix SQL Server Connection

Jika ingin menggunakan SQL Server `.\\SERVERVJEST`:

1. **Cek SQL Server Service:**
   ```cmd
   Get-Service | Where-Object {$_.Name -like "*SQL*"}
   ```

2. **Pastikan SQL Server berjalan:**
   - Buka Services (services.msc)
   - Cari "SQL Server (SERVERVJEST)" atau "SQL Server (MSSQLSERVER)"
   - Start service jika stopped

3. **Cek instance name:**
   - Buka SQL Server Configuration Manager
   - Cek instance name yang benar
   - Update connection string di appsettings.json

4. **Test connection:**
   ```cmd
   sqlcmd -S ".\\SERVERVJEST" -Q "SELECT 1"
   ```

---

## 📋 Quick Fix Commands

### Install LocalDB & Run App:
```cmd
install-all-prerequisites.bat
```

### Setup & Run dengan LocalDB:
```cmd
install-and-run.bat
```

### Check Prerequisites:
```cmd
check-prerequisites.ps1
```

---

## ✅ Setelah Fix

Aplikasi akan berjalan tanpa error di:
- **HTTP:** http://localhost:6001
- **HTTPS:** https://localhost:6002

---

**💡 Recommendation: Install LocalDB untuk development offline yang fleksibel!**

