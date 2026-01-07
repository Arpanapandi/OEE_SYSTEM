# 📦 Install LocalDB untuk Development Offline

Panduan lengkap untuk install SQL Server LocalDB agar bisa development offline tanpa koneksi ke server kantor.

## 🎯 Tujuan

Setelah install LocalDB, Anda bisa:
- ✅ Development di rumah tanpa WiFi
- ✅ Tidak perlu koneksi ke server kantor
- ✅ Database lokal di komputer Anda
- ✅ Bisa switch ke SQL Server saat di kantor

---

## 📥 Cara Install LocalDB

### Opsi 1: Install SQL Server Express (Recommended)

**Langkah 1: Download**
1. Buka: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
2. Klik **"Download now"** di bagian **SQL Server Express**
3. Pilih **"Express"** edition (gratis)

**Langkah 2: Install**
1. Jalankan installer yang didownload
2. Pilih **"Basic"** installation type
3. **PENTING**: Pastikan **"LocalDB"** tercentang di komponen yang akan diinstall
4. Klik **"Install"** dan tunggu sampai selesai

**Langkah 3: Verifikasi**
Buka PowerShell dan jalankan:
```powershell
sqllocaldb info
```

Jika muncul daftar instance, berarti LocalDB sudah terinstall! ✅

---

### Opsi 2: Install melalui Visual Studio Installer

Jika Anda sudah punya Visual Studio:

1. Buka **Visual Studio Installer**
2. Klik **"Modify"** pada Visual Studio yang terinstall
3. Pilih tab **"Individual components"**
4. Cari dan centang **"SQL Server Express LocalDB"**
5. Klik **"Modify"** dan tunggu instalasi selesai

---

### Opsi 3: Install Standalone LocalDB (Ringan)

Jika hanya butuh LocalDB tanpa SQL Server Express:

1. Download **SQL Server Express LocalDB**:
   - Link: https://go.microsoft.com/fwlink/?LinkID=866658
   - Atau cari "SQL Server Express LocalDB" di Microsoft Download Center

2. Jalankan installer
3. Ikuti wizard instalasi
4. Restart komputer setelah instalasi

---

## ✅ Setup Setelah Install

### Langkah 1: Verifikasi LocalDB

Buka PowerShell dan jalankan:
```powershell
# Cek LocalDB terinstall
sqllocaldb info

# Cek instance MSSQLLocalDB
sqllocaldb info MSSQLLocalDB

# Start instance (jika belum running)
sqllocaldb start MSSQLLocalDB
```

### Langkah 2: Update appsettings.json

Setelah LocalDB terinstall, ubah `appsettings.json` kembali ke LocalDB:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb;Trusted_Connection=True;MultipleActiveResultSets=true",
    "HossConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb_Hoss;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### Langkah 3: Jalankan Aplikasi

```powershell
dotnet run
```

Aplikasi akan **otomatis membuat database** saat pertama kali dijalankan! 🎉

---

## 🔄 Switch Antara LocalDB dan SQL Server

### Development Offline (LocalDB)

```powershell
# Pastikan appsettings.json menggunakan LocalDB
dotnet run
```

### Production/Di Kantor (SQL Server)

```powershell
# Set environment ke Production
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run

# Atau edit appsettings.json langsung ke SQL Server
```

---

## 🔍 Troubleshooting

### Error: "sqllocaldb is not recognized"

**Penyebab**: LocalDB belum terinstall atau tidak ada di PATH.

**Solusi**:
1. Install LocalDB (lihat Opsi 1-3 di atas)
2. Restart PowerShell setelah install
3. Jika masih error, tambahkan ke PATH:
   ```
   C:\Program Files\Microsoft SQL Server\150\LocalDB\Binn
   ```

### Error: "Cannot create/shared access a file"

**Penyebab**: Permission issue atau LocalDB instance tidak running.

**Solusi**:
```powershell
# Start LocalDB instance
sqllocaldb start MSSQLLocalDB

# Atau create instance baru
sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

### Error: "A network-related or instance-specific error"

**Penyebab**: Connection string salah atau instance tidak running.

**Solusi**:
1. Pastikan connection string: `Server=(localdb)\\MSSQLLocalDB`
2. Start instance: `sqllocaldb start MSSQLLocalDB`
3. Cek instance: `sqllocaldb info MSSQLLocalDB`

---

## 📍 Lokasi Database LocalDB

Database LocalDB tersimpan di:
```
C:\Users\[USERNAME]\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\
```

Untuk melihat database, gunakan:
- **SQL Server Management Studio (SSMS)**
- **Azure Data Studio**
- Server name: `(localdb)\MSSQLLocalDB`
- Authentication: Windows Authentication

---

## ✅ Checklist

Setelah install LocalDB, pastikan:

- [ ] `sqllocaldb info` berjalan tanpa error
- [ ] Instance `MSSQLLocalDB` ada dan running
- [ ] `appsettings.json` menggunakan LocalDB connection string
- [ ] Aplikasi bisa dijalankan dengan `dotnet run`
- [ ] Database otomatis dibuat saat pertama kali run

---

## 🎉 Selesai!

Sekarang Anda bisa development offline di rumah tanpa perlu koneksi ke server kantor!

**Tips**:
- Simpan backup `appsettings.json` untuk SQL Server (untuk di kantor)
- Gunakan `appsettings.Production.json` untuk production
- Database LocalDB hanya untuk development, tidak untuk production

