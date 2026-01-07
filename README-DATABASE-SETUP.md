# Setup Database - LocalDB untuk Development

Dokumentasi ini menjelaskan cara setup database menggunakan LocalDB untuk development dan cara switch ke SQL Server untuk production.

## 📋 Daftar Isi

1. [Persyaratan](#persyaratan)
2. [Setup LocalDB untuk Development](#setup-localdb-untuk-development)
3. [Switch ke SQL Server untuk Production](#switch-ke-sql-server-untuk-production)
4. [Cara Kerja](#cara-kerja)
5. [Troubleshooting](#troubleshooting)

---

## 🔧 Persyaratan

### Untuk Development (LocalDB):
- **SQL Server LocalDB** (biasanya sudah terinstall dengan Visual Studio atau SQL Server Express)
- Untuk cek apakah LocalDB sudah terinstall, jalankan di PowerShell:
  ```powershell
  sqllocaldb info
  ```

### Untuk Production (SQL Server):
- Akses ke SQL Server (10.14.149.34)
- Kredensial database yang valid
- Koneksi WiFi/Network ke server database

---

## 🚀 Setup LocalDB untuk Development

### Langkah 1: Pastikan LocalDB Terinstall

Jalankan di PowerShell untuk cek LocalDB:
```powershell
sqllocaldb info
```

Jika belum terinstall, download dan install:
- **SQL Server Express** (termasuk LocalDB): https://www.microsoft.com/en-us/sql-server/sql-server-downloads
- Atau install melalui **Visual Studio Installer** (pilih "SQL Server Express LocalDB")

### Langkah 2: Set Environment Variable (Opsional)

Untuk menggunakan LocalDB secara default, set environment variable:
```powershell
# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Atau set secara permanen
[System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development", "User")
```

### Langkah 3: Jalankan Aplikasi

Aplikasi akan otomatis menggunakan `appsettings.Development.json` jika environment variable `ASPNETCORE_ENVIRONMENT=Development`.

```powershell
dotnet run
```

**Database akan dibuat otomatis** saat pertama kali aplikasi dijalankan:
- `OeeSystemDb` - Database utama (ApplicationDbContext)
- `OeeSystemDb_Hoss` - Database untuk HossDbContext (dummy data komponen)

### Langkah 4: Verifikasi Database

Database LocalDB tersimpan di:
```
C:\Users\[USERNAME]\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\
```

Untuk melihat database, gunakan **SQL Server Management Studio (SSMS)** atau **Azure Data Studio**:
- Server name: `(localdb)\MSSQLLocalDB`
- Authentication: Windows Authentication

---

## 🔄 Switch ke SQL Server untuk Production

### Metode 1: Ubah Environment Variable

Set environment variable ke `Production`:
```powershell
# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Production"

# Atau set secara permanen
[System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "User")
```

Aplikasi akan menggunakan `appsettings.json` (SQL Server).

### Metode 2: Edit appsettings.json Langsung

Edit `appsettings.json` dan pastikan connection string mengarah ke SQL Server:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=10.14.149.34;Database=OeeSystemDb;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;Connection Timeout=60;Command Timeout=120;Encrypt=True;MultipleActiveResultSets=true",
    "HossConnection": "Server=10.14.149.34;Database=DB_HOSE;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;Connection Timeout=60;Command Timeout=120;Encrypt=True;MultipleActiveResultSets=true"
  }
}
```

### Metode 3: Gunakan appsettings.Production.json (Recommended)

Buat file `appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=10.14.149.34;Database=OeeSystemDb;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;Connection Timeout=60;Command Timeout=120;Encrypt=True;MultipleActiveResultSets=true",
    "HossConnection": "Server=10.14.149.34;Database=DB_HOSE;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;Connection Timeout=60;Command Timeout=120;Encrypt=True;MultipleActiveResultSets=true"
  }
}
```

Set environment variable:
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
```

---

## ⚙️ Cara Kerja

### 1. Configuration Priority

ASP.NET Core membaca configuration dengan urutan berikut (yang terakhir menang):
1. `appsettings.json`
2. `appsettings.{Environment}.json` (misal: `appsettings.Development.json`)
3. Environment Variables
4. Command-line arguments

### 2. Fallback Mechanism

Di `Program.cs`, aplikasi memiliki fallback ke LocalDB jika connection string tidak ditemukan:

```csharp
// DefaultConnection - fallback ke LocalDB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDbV2;Trusted_Connection=True;MultipleActiveResultSets=true";

// HossConnection - fallback ke LocalDB
var hossConnectionString = builder.Configuration.GetConnectionString("HossConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDb_Hoss;Trusted_Connection=True;MultipleActiveResultSets=true";
```

### 3. Auto-Create Database

Aplikasi akan otomatis:
- Membuat database jika belum ada (untuk LocalDB)
- Membuat tabel `komponen` di HossDbContext (untuk LocalDB)
- Seed dummy data untuk komponen (untuk LocalDB)

---

## 🔍 Troubleshooting

### Error: "Cannot open database requested by the login"

**Penyebab**: Database belum dibuat atau connection string salah.

**Solusi**:
1. Pastikan LocalDB berjalan:
   ```powershell
   sqllocaldb start MSSQLLocalDB
   ```
2. Pastikan connection string benar di `appsettings.Development.json`
3. Restart aplikasi untuk trigger auto-create database

### Error: "A network-related or instance-specific error occurred"

**Penyebab**: SQL Server tidak bisa diakses atau connection string salah.

**Solusi**:
1. Cek koneksi WiFi/Network ke server database
2. Verifikasi connection string di `appsettings.json`
3. Test koneksi dengan SQL Server Management Studio (SSMS)

### Error: "Login failed for user"

**Penyebab**: Kredensial database salah atau user tidak memiliki permission.

**Solusi**:
1. Verifikasi username dan password di connection string
2. Pastikan user memiliki permission untuk database tersebut
3. Hubungi DBA untuk verifikasi akses

### Database tidak ter-update setelah perubahan model

**Penyebab**: Database schema tidak sinkron dengan model.

**Solusi**:
1. Hapus database LocalDB dan restart aplikasi (akan auto-create)
2. Atau gunakan EF Core Migrations:
   ```powershell
   dotnet ef migrations add UpdateSchema
   dotnet ef database update
   ```

### HossDbContext tidak berfungsi di LocalDB

**Penyebab**: Tabel `komponen` belum dibuat atau dummy data belum di-seed.

**Solusi**:
1. Restart aplikasi (akan auto-create tabel dan seed dummy data)
2. Atau manual create tabel:
   ```sql
   CREATE TABLE [dbo].[komponen] (
       [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
       [Part_Number] NVARCHAR(100) NULL,
       [jml_komponen] INT NULL
   )
   ```

---

## 📝 Quick Reference

### Development (LocalDB)
```powershell
# Set environment
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Jalankan aplikasi
dotnet run
```

### Production (SQL Server)
```powershell
# Set environment
$env:ASPNETCORE_ENVIRONMENT = "Production"

# Jalankan aplikasi
dotnet run
```

### Cek Database yang Digunakan
Lihat console output saat aplikasi start:
- `INFO: Database connection successful` - Database terhubung
- `WARNING: Database connection timeout` - Database tidak terhubung

---

## 📚 File Konfigurasi

- **`appsettings.json`** - Konfigurasi default (SQL Server untuk production)
- **`appsettings.Development.json`** - Konfigurasi untuk development (LocalDB)
- **`Program.cs`** - Logic untuk auto-create database dan fallback mechanism

---

## ✅ Checklist Setup

- [ ] SQL Server LocalDB terinstall
- [ ] `appsettings.Development.json` sudah dibuat
- [ ] Environment variable `ASPNETCORE_ENVIRONMENT=Development` sudah di-set
- [ ] Aplikasi berhasil dijalankan dengan `dotnet run`
- [ ] Database `OeeSystemDb` dan `OeeSystemDb_Hoss` sudah dibuat otomatis
- [ ] Dummy data komponen sudah ter-seed di HossDbContext
- [ ] Aplikasi bisa diakses tanpa error

---

**Catatan**: 
- Data di LocalDB tersimpan lokal di komputer Anda
- Data di SQL Server tersimpan di server (10.14.149.34)
- Pastikan backup data sebelum switch database
- Untuk production, selalu gunakan SQL Server, bukan LocalDB

