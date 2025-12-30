# Diagnostic Database db_HOSS

File ini berisi instruksi untuk mengecek struktur tabel di database `db_HOSS` sebelum implementasi.

## Endpoint Diagnostic

Setelah aplikasi berjalan, gunakan endpoint berikut untuk mengecek struktur database:

### 1. Cek Semua Tabel di db_HOSS

**URL:** `GET /api/Scanner/diagnostic/check-tables`

**Contoh:**
```
http://localhost:6001/api/Scanner/diagnostic/check-tables
```

**Response:**
- Daftar semua tabel di database `db_HOSS`
- Informasi kolom untuk setiap tabel
- Schema dan nama tabel

### 2. Cek Tabel Komponen Secara Spesifik

**URL:** `GET /api/Scanner/diagnostic/check-komponen`

**Contoh:**
```
http://localhost:6001/api/Scanner/diagnostic/check-komponen
```

**Response:**
- Nama tabel yang sebenarnya (case-sensitive)
- Schema tabel
- Daftar semua kolom dengan tipe data
- Sample data (5 baris pertama)

## Cara Menggunakan

1. **Jalankan aplikasi:**
   ```bash
   dotnet run
   ```

2. **Buka browser atau gunakan Postman/curl:**
   - Buka: `http://localhost:6001/api/Scanner/diagnostic/check-komponen`
   - Atau gunakan curl:
     ```bash
     curl http://localhost:6001/api/Scanner/diagnostic/check-komponen
     ```

3. **Periksa response JSON:**
   - Lihat nama tabel yang sebenarnya
   - Lihat struktur kolom
   - Lihat sample data untuk memahami format

4. **Sesuaikan Model dan DbContext:**
   - Update `Models/Komponen.cs` sesuai kolom yang ada
   - Update `Data/HossDbContext.cs` sesuai nama tabel dan mapping kolom

## Contoh Response

```json
{
  "success": true,
  "tableName": "dbo.Komponen",
  "schema": "dbo",
  "actualTableName": "Komponen",
  "exists": true,
  "columns": [
    {
      "columnName": "Id",
      "dataType": "int",
      "maxLength": null,
      "isNullable": "NO",
      "defaultValue": null,
      "ordinalPosition": 1
    },
    {
      "columnName": "PartNumber",
      "dataType": "nvarchar",
      "maxLength": 100,
      "isNullable": "YES",
      "defaultValue": null,
      "ordinalPosition": 2
    }
  ],
  "sampleData": [
    {
      "Id": 1,
      "PartNumber": "PN001",
      "NamaKomponen": "Komponen A",
      "KodeKomponen": "K001"
    }
  ]
}
```

## Setelah Mendapatkan Informasi

1. **Update Model `Komponen.cs`:**
   - Sesuaikan properties dengan kolom yang ada di database
   - Pastikan tipe data sesuai

2. **Update `HossDbContext.cs`:**
   - Sesuaikan `ToTable()` dengan nama tabel yang sebenarnya
   - Sesuaikan `HasColumnName()` dengan nama kolom yang sebenarnya
   - Sesuaikan `HasMaxLength()` dengan panjang maksimal kolom

3. **Test Koneksi:**
   - Pastikan connection string `HossConnection` benar
   - Test endpoint `/api/Scanner/komponen/validate/{partNumber}` dengan Part Number yang ada di database

## Troubleshooting

- **Error: Connection string tidak ditemukan**
  - Pastikan `appsettings.json` memiliki `HossConnection`
  
- **Error: Tabel tidak ditemukan**
  - Periksa nama tabel yang benar (case-sensitive di SQL Server)
  - Periksa apakah user memiliki akses ke database `db_HOSS`
  
- **Error: Timeout**
  - Periksa koneksi jaringan ke server database
  - Periksa firewall settings

