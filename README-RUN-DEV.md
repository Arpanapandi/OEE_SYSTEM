# 🚀 RUN-DEV.bat - Setup Otomatis untuk Development

## 📋 Deskripsi

Script `RUN-DEV.bat` adalah solusi **one-click** untuk setup dan menjalankan aplikasi OEE System dengan LocalDB tanpa error.

## ✨ Fitur

- ✅ **Auto-detect LocalDB** - Cek apakah LocalDB sudah terinstall
- ✅ **Auto-setup instance** - Setup MSSQLLocalDB instance otomatis
- ✅ **Auto-configure** - Update appsettings.json untuk LocalDB
- ✅ **Auto-restore** - Restore dependencies otomatis
- ✅ **Auto-run** - Jalankan aplikasi sampai tampil
- ✅ **Error handling** - Troubleshooting guide jika ada error

## 🎯 Cara Menggunakan

### **Pertama Kali (Jika LocalDB belum terinstall)**

1. **Jalankan script:**
   ```cmd
   RUN-DEV.bat
   ```

2. **Pilih metode install LocalDB:**
   - **Metode 1**: Via Visual Studio Installer (paling mudah)
   - **Metode 2**: Download SQL Server Express

3. **Install LocalDB** sesuai instruksi di script

4. **Restart command prompt** (PENTING!)

5. **Jalankan script lagi:**
   ```cmd
   RUN-DEV.bat
   ```

6. **Aplikasi akan otomatis:**
   - Setup LocalDB instance
   - Update konfigurasi
   - Restore dependencies
   - Run aplikasi

### **Setelah LocalDB Terinstall**

Cukup jalankan:
```cmd
RUN-DEV.bat
```

Script akan otomatis:
- ✅ Setup instance MSSQLLocalDB
- ✅ Update appsettings.json
- ✅ Restore dependencies
- ✅ Run aplikasi

## 📱 Akses Aplikasi

Setelah aplikasi berjalan, akses di:
- **HTTP**: http://localhost:6001
- **HTTPS**: https://localhost:6002

## 🔧 Troubleshooting

### Error: "LocalDB belum terinstall"
**Solusi:**
1. Install LocalDB sesuai instruksi di script
2. Restart command prompt
3. Jalankan script lagi

### Error: "Gagal membuat instance"
**Solusi:**
1. Jalankan script sebagai Administrator
2. Atau jalankan manual:
   ```cmd
   sqllocaldb create MSSQLLocalDB
   sqllocaldb start MSSQLLocalDB
   ```

### Error: "Aplikasi gagal start"
**Solusi:**
1. Pastikan LocalDB berjalan:
   ```cmd
   sqllocaldb start MSSQLLocalDB
   ```
2. Restore packages:
   ```cmd
   dotnet restore
   ```
3. Build project:
   ```cmd
   dotnet build
   ```

### Error: "Database connection timeout"
**Solusi:**
1. Pastikan instance berjalan:
   ```cmd
   sqllocaldb info MSSQLLocalDB
   ```
2. Start instance:
   ```cmd
   sqllocaldb start MSSQLLocalDB
   ```

## 💡 Tips

- **Restart command prompt** setelah install LocalDB - Penting!
- **Jalankan sebagai Administrator** jika ada masalah permission
- **Cek log** di console untuk detail error
- **Database dibuat otomatis** saat pertama kali run

## 🎉 Setelah Setup

Proyek Anda sekarang:
- ✅ Bisa development **offline** (tanpa server)
- ✅ Database **local** (tidak perlu koneksi WiFi)
- ✅ **Mudah development** di mana saja
- ✅ **One-click setup** dengan RUN-DEV.bat

---

**Selamat! Proyek siap untuk development! 🚀**

