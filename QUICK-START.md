# 🚀 Quick Start Guide - OEE System

## ⚠️ Masalah yang Ditemukan

1. **LocalDB tidak terinstall** - Command `sqllocaldb` tidak ditemukan
2. **PowerShell Execution Policy** - Script tidak bisa dijalankan

## ✅ Solusi Cepat

### Opsi 1: Gunakan SQL Server yang Sudah Ada

Edit `appsettings.json` dan ubah connection string ke SQL Server yang tersedia:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SERVERVJEST;Database=OeeSystemDb;Trusted_Connection=True;MultipleActiveResultSets=true;Connection Timeout=30",
    "HossConnection": "Server=.\\SERVERVJEST;Database=OeeSystemDb_Hoss;Trusted_Connection=True;MultipleActiveResultSets=true;Connection Timeout=30"
  }
}
```

Kemudian jalankan:
```powershell
dotnet run
```

### Opsi 2: Install LocalDB (Untuk Development Offline)

**Download dan Install:**
1. Download **SQL Server Express** (termasuk LocalDB):
   - https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - Pilih "Express" edition
   - Pastikan memilih "LocalDB" saat instalasi

2. Atau install melalui **Visual Studio Installer**:
   - Buka Visual Studio Installer
   - Modify installation
   - Pilih "SQL Server Express LocalDB" di bagian Individual Components

**Setelah Install:**
- Restart PowerShell
- Cek dengan: `sqllocaldb info`
- Jalankan aplikasi: `dotnet run`

### Opsi 3: Bypass PowerShell Execution Policy (Sementara)

Untuk menjalankan script helper, set execution policy untuk session ini saja:

```powershell
# Bypass untuk session ini saja (tidak permanen)
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

# Kemudian jalankan script
.\check-localdb.ps1
```

**ATAU** jalankan langsung tanpa script:

```powershell
# Set environment variable
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Jalankan aplikasi
dotnet run
```

## 📋 Checklist

- [ ] Cek apakah SQL Server instance tersedia (`.\\SERVERVJEST` atau lainnya)
- [ ] Jika tidak ada, install LocalDB atau gunakan SQL Server yang ada
- [ ] Edit `appsettings.json` dengan connection string yang benar
- [ ] Jalankan `dotnet run`

## 🔍 Troubleshooting

### Error: "server was not found or was not accessible"

**Kemungkinan penyebab:**
1. SQL Server instance tidak berjalan
2. Connection string salah
3. Database belum dibuat

**Solusi:**
1. Cek SQL Server service berjalan:
   ```powershell
   Get-Service | Where-Object {$_.Name -like "*SQL*"}
   ```

2. Cek connection string di `appsettings.json`

3. Aplikasi akan otomatis membuat database saat pertama kali dijalankan (jika menggunakan LocalDB)

### Error: "sqllocaldb is not recognized"

**Solusi:**
- Install SQL Server Express LocalDB (lihat Opsi 2 di atas)
- Atau gunakan SQL Server yang sudah ada (Opsi 1)

### Error: "Execution Policy"

**Solusi:**
```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
```

Atau jalankan command langsung tanpa script.

