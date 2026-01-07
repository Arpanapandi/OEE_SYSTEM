# ⚡ Quick Install LocalDB - 5 Menit

## 🎯 Cara Tercepat Install LocalDB

### Opsi 1: Download Langsung (Recommended - 2 menit)

1. **Download SQL Server Express LocalDB:**
   - Buka: https://go.microsoft.com/fwlink/?LinkID=866658
   - Atau: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - Pilih **"Express"** → Download

2. **Install:**
   - Jalankan installer
   - Pilih **"Basic"** installation
   - Klik **"Install"** (LocalDB otomatis tercentang)
   - Tunggu selesai (~2-3 menit)

3. **Verifikasi:**
   ```cmd
   sqllocaldb info
   ```
   Jika muncul daftar instance = ✅ Berhasil!

4. **Setup & Run:**
   ```cmd
   install-and-run.bat
   ```

---

### Opsi 2: Via Visual Studio Installer (Jika sudah punya VS)

1. Buka **Visual Studio Installer**
2. Klik **"Modify"** pada Visual Studio Anda
3. Tab **"Individual Components"**
4. Cari dan centang **"SQL Server Express LocalDB"**
5. Klik **"Modify"** → Tunggu selesai
6. Restart command prompt
7. Jalankan: `install-and-run.bat`

---

## ✅ Setelah Install

Jalankan script otomatis:
```cmd
install-and-run.bat
```

Script ini akan:
- ✅ Cek LocalDB terinstall
- ✅ Setup instance MSSQLLocalDB
- ✅ Update appsettings.json
- ✅ Run aplikasi langsung

---

## 🚀 Atau Manual Setup

Jika sudah install LocalDB, langsung jalankan:
```cmd
dotnet run
```

Database akan **otomatis dibuat** saat pertama kali run!

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

**Selamat! Sekarang bisa development offline! 🎉**

