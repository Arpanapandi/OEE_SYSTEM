# 📥 Install LocalDB - Manual Method (Paling Reliable)

## ✅ Cara yang Paling Mudah dan Reliable

### Langkah 1: Download SQL Server Express

**Link Download:**
- **Langsung:** https://go.microsoft.com/fwlink/?LinkID=866658
- **Atau:** https://www.microsoft.com/en-us/sql-server/sql-server-downloads

**Pilih:**
- **"Express"** edition
- Download installer (~200-300 MB)

---

### Langkah 2: Install dengan Wizard

1. **Jalankan installer** yang didownload
2. **Pilih "Basic"** installation type
   - Ini adalah opsi termudah
   - LocalDB otomatis tercentang
   - Tidak perlu konfigurasi manual
3. **Klik "Install"**
4. **Tunggu selesai** (~2-3 menit)
   - Progress bar akan menunjukkan status
   - Jangan tutup installer selama proses

---

### Langkah 3: Verifikasi Install

Setelah install selesai, **restart command prompt** dan jalankan:

```cmd
sqllocaldb info
```

Jika muncul daftar instance = ✅ **Berhasil!**

---

### Langkah 4: Setup untuk Development

Jalankan script setup:

```cmd
setup-localdb-dev.bat
```

**ATAU:**

```cmd
install-localdb-easy.bat
```

Script akan:
- ✅ Setup instance MSSQLLocalDB
- ✅ Update appsettings.json
- ✅ Verify setup

---

### Langkah 5: Run Aplikasi

```cmd
dotnet run
```

Aplikasi akan:
- ✅ Terhubung ke LocalDB
- ✅ Membuat database otomatis
- ✅ Seed data dummy
- ✅ Bisa diakses di: http://localhost:6001

---

## 🔍 Troubleshooting

### "sqllocaldb is not recognized"

**Solusi:**
1. **Restart command prompt** setelah install
2. Jika masih error, **restart komputer
3. Cek PATH environment variable

### Installer tidak berjalan

**Solusi:**
1. Klik kanan installer → "Run as administrator"
2. Pastikan Windows Update terbaru
3. Cek antivirus tidak memblokir

### "Cannot create database"

**Solusi:**
```cmd
sqllocaldb start MSSQLLocalDB
```

### Install gagal di tengah jalan

**Solusi:**
1. Uninstall SQL Server Express yang partial
2. Restart komputer
3. Install ulang dengan "Basic" option

---

## ✅ Checklist

- [ ] Download SQL Server Express
- [ ] Install dengan "Basic" option
- [ ] Restart command prompt
- [ ] Verify: `sqllocaldb info`
- [ ] Run: `setup-localdb-dev.bat`
- [ ] Run: `dotnet run`
- [ ] Aplikasi bisa diakses di http://localhost:6001

---

## 💡 Tips

- **Gunakan "Basic" installation** - Paling mudah dan reliable
- **Restart command prompt** setelah install - Penting untuk refresh PATH
- **Jalankan setup script** setelah install - Otomatis konfigurasi semua

---

**🎉 Setelah install, development akan lebih mudah dan fleksibel!**


