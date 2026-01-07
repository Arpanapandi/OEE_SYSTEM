# 🚀 START HERE - Install LocalDB & Run App

## ⚡ Quick Start (3 Langkah)

### 1️⃣ Install LocalDB (5 menit)

**Download & Install:**
- **Link langsung:** https://go.microsoft.com/fwlink/?LinkID=866658
- Atau: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
- Pilih **"Express"** edition → Download → Install
- Pilih **"Basic"** installation (LocalDB otomatis tercentang)
- Tunggu selesai (~2-3 menit)

**Verifikasi:**
Buka PowerShell/CMD dan jalankan:
```cmd
sqllocaldb info
```
Jika muncul daftar instance = ✅ **Berhasil!**

---

### 2️⃣ Setup & Run Otomatis

Setelah LocalDB terinstall, jalankan:

```cmd
install-and-run.bat
```

Script ini akan:
- ✅ Cek LocalDB terinstall
- ✅ Setup instance MSSQLLocalDB
- ✅ Update appsettings.json untuk LocalDB
- ✅ Run aplikasi langsung

**Aplikasi akan otomatis:**
- Membuat database saat pertama kali run
- Seed data dummy untuk development
- Bisa diakses di: http://localhost:6001

---

### 3️⃣ Atau Manual (Jika perlu)

Jika sudah install LocalDB, langsung:
```cmd
dotnet run
```

---

## 📋 File Penting

- **`install-and-run.bat`** - Script otomatis install + setup + run
- **`INSTALL-LOCALDB-QUICK.md`** - Panduan install cepat
- **`README-OFFLINE-DEV.md`** - Panduan lengkap development offline

---

## ❌ Troubleshooting

### "sqllocaldb is not recognized"
- **Solusi:** Restart command prompt setelah install LocalDB
- Atau restart komputer

### "Execution Policy" error
- **Solusi:** Gunakan `install-and-run.bat` (tidak perlu execution policy)

### "Cannot create database"
- **Solusi:** 
  ```cmd
  sqllocaldb start MSSQLLocalDB
  ```

---

## ✅ Checklist

- [ ] LocalDB terinstall (`sqllocaldb info` berjalan)
- [ ] Jalankan `install-and-run.bat`
- [ ] Aplikasi berjalan di http://localhost:6001
- [ ] Tidak ada error di console

---

**Selamat Development Offline! 🎉**

