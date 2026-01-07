# ⚡ Quick Install LocalDB - 3 Langkah

## 🎯 Untuk Menggunakan LocalDB (Development Offline)

### ✅ Langkah 1: Download LocalDB

**Link Download:**
- **Langsung:** https://go.microsoft.com/fwlink/?LinkID=866658
- **Atau:** https://www.microsoft.com/en-us/sql-server/sql-server-downloads

**Pilih:**
- **"Express"** edition
- Download installer

---

### ✅ Langkah 2: Install LocalDB

1. **Jalankan installer** yang didownload
2. **Pilih "Basic"** installation type
3. **LocalDB otomatis tercentang** (tidak perlu centang manual)
4. **Klik "Install"**
5. **Tunggu selesai** (~2-3 menit)

---

### ✅ Langkah 3: Run Aplikasi

**Setelah install, restart command prompt dan jalankan:**

```cmd
dotnet run
```

**ATAU gunakan script otomatis:**

```cmd
install-and-run.bat
```

---

## ✅ Verifikasi

Setelah install, verifikasi LocalDB:

```cmd
sqllocaldb info
sqllocaldb start MSSQLLocalDB
```

Jika command berjalan tanpa error = ✅ **LocalDB siap!**

---

## 🚀 Setelah Install

Aplikasi akan:
- ✅ Terhubung ke LocalDB tanpa error
- ✅ Membuat database otomatis saat first run
- ✅ Seed data dummy untuk development
- ✅ Bisa diakses di: http://localhost:6001

---

## ❌ Troubleshooting

### "sqllocaldb is not recognized"
- **Solusi:** Restart command prompt setelah install
- Atau restart komputer

### "Cannot create database"
- **Solusi:** 
  ```cmd
  sqllocaldb start MSSQLLocalDB
  ```

### "Execution Policy" error
- **Solusi:** Gunakan `install-and-run.bat` (tidak perlu execution policy)

---

**🎉 Setelah install LocalDB, aplikasi akan berjalan tanpa error!**


