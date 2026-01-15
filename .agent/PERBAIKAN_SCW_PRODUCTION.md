# PERBAIKAN SCW & PRODUCTION DATA SUBMISSION

## ANALISIS MASALAH

### 1. SCW (Stop Call Waiting)
**Status Saat Ini:**
- ✅ Data seeding sudah ada di `Program.cs` (Scw4MTypes & ScwRemarks)
- ✅ JavaScript handler sudah ada untuk dropdown filtering
- ✅ Submit handler sudah ada
- ✅ Backend endpoint `/Operator/Scw` sudah lengkap

**Masalah:**
- Dropdown "Jenis 4M" dan "Jenis Remark" tidak menampilkan data
- Submit SCW belum berfungsi

**Root Cause:**
- Data mungkin belum ter-seed ke database
- ViewBag.Scw4MTypes dan ViewBag.ScwRemarks mungkin NULL

### 2. Man Power & Injection + Data Produksi
**Status Saat Ini:**
- ✅ Form input sudah lengkap
- ✅ JavaScript submit handler sudah ada
- ✅ Memanggil endpoint `/Operator/SubmitProductionData`

**Masalah:**
- Data tidak tersimpan ke local database
- Tidak muncul di Recent Product Count

**Root Cause:**
- Endpoint `/Operator/SubmitProductionData` mungkin belum ada atau belum lengkap
- Tidak ada ProductionCount record yang dibuat

### 3. Durasi Produksi Real-Time
**Status Saat Ini:**
- ✅ Timer sudah ada dan berjalan
- ❌ Belum menggunakan centralized sync logic

**Masalah:**
- Durasi tidak real-time atau tidak akurat

**Root Cause:**
- Belum menggunakan `getAdjustedServerTime()` untuk sinkronisasi

---

## SOLUSI PERBAIKAN

### PERBAIKAN 1: Pastikan Data SCW Ter-Seed

**File: Program.cs (sudah ada, perlu verifikasi)**

Pastikan seeding berjalan dengan baik. Tambahkan logging lebih detail:

```csharp
// Line 640-757 sudah benar, tambahkan logging
Console.WriteLine($"🔍 Checking SCW 4M Types...");
var scw4MCount = await db.Scw4MTypes.CountAsync();
Console.WriteLine($"   Found {scw4MCount} Scw4MTypes in database");

var scwRemarksCount = await db.ScwRemarks.CountAsync();
Console.WriteLine($"   Found {scwRemarksCount} ScwRemarks in database");
```

### PERBAIKAN 2: Buat Endpoint SubmitProductionData

**File: Controllers/OperatorController.cs**

Tambahkan endpoint baru setelah method `Scw`:

```csharp
// POST: Submit Production Data
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SubmitProductionData(
    string machineId,
    string nomorLot,
    string partNumber, // Lot BO
    string namaCompound,
    string beratAct,
    string penipisan,
    string keterangan,
    int? manPowerId,
    string injection,
    int? komponenId = null,
    int durasiProduksiSeconds = 0,
    int qty = 1)
{
    bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
    var now = DateTime.Now;
    
    try
    {
        // 1. Cari active job
        var activeJob = await _context.JobRuns
            .Where(j => j.MachineId == machineId && j.EndTime == null)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync();
        
        if (activeJob == null)
        {
            if (isAjax)
                return Json(new { success = false, message = "Tidak ada job aktif" });
            TempData["OperationError"] = "Tidak ada job aktif";
            return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
        }
        
        // 2. Update Man Power jika dipilih
        if (manPowerId.HasValue && manPowerId.Value > 0)
        {
            activeJob.ManPowerId = manPowerId.Value;
        }
        
        // 3. Simpan Production Data sebagai ProductionCount
        var productionCount = new ProductionCount
        {
            JobRunId = activeJob.Id,
            Timestamp = now,
            GoodCount = qty, // Default 1 untuk tracking per item
            RejectCount = 0,
            RejectReason = null,
            // Tambahkan custom fields jika ada di model
            NomorLot = nomorLot,
            LotBo = partNumber,
            NamaCompound = namaCompound,
            BeratAct = !string.IsNullOrEmpty(beratAct) ? decimal.Parse(beratAct) : 0,
            Penipisan = penipisan,
            Keterangan = keterangan,
            Injection = injection,
            KomponenId = komponenId,
            DurasiProduksiSeconds = durasiProduksiSeconds
        };
        
        _context.ProductionCounts.Add(productionCount);
        await _context.SaveChangesAsync();
        
        // 4. Broadcast event untuk real-time update
        await _hubContext.Clients.All.SendAsync("OeeUpdated", new
        {
            Type = "ProductionDataSubmitted",
            MachineId = machineId,
            Message = $"Data produksi disimpan: {nomorLot}",
            Timestamp = now,
            RefreshTimeMetrics = true
        });
        
        if (isAjax)
            return Json(new { 
                success = true, 
                message = "Data produksi berhasil disimpan",
                productionCountId = productionCount.Id
            });
        
        TempData["OperationSuccess"] = "Data produksi berhasil disimpan";
        return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error in SubmitProductionData: {ex.Message}");
        Console.WriteLine($"   StackTrace: {ex.StackTrace}");
        
        if (isAjax)
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        TempData["OperationError"] = $"Error: {ex.Message}";
        return RedirectToAction("OeeDetail", "Machine", new { id = machineId });
    }
}
```

### PERBAIKAN 3: Update Model ProductionCount

**File: Models/ProductionCount.cs**

Tambahkan properties untuk data produksi:

```csharp
public class ProductionCount
{
    public int Id { get; set; }
    public int JobRunId { get; set; }
    public JobRun? JobRun { get; set; }
    
    public DateTime Timestamp { get; set; }
    public int GoodCount { get; set; }
    public int RejectCount { get; set; }
    public string? RejectReason { get; set; }
    
    // ✅ TAMBAHKAN: Production Data Fields
    public string? NomorLot { get; set; }
    public string? LotBo { get; set; }
    public string? NamaCompound { get; set; }
    public decimal BeratAct { get; set; }
    public string? Penipisan { get; set; }
    public string? Keterangan { get; set; }
    public string? Injection { get; set; }
    public int? KomponenId { get; set; }
    public int DurasiProduksiSeconds { get; set; }
}
```

### PERBAIKAN 4: Centralized Sync Logic untuk Durasi

**File: Views/Machine/OeeDetail.cshtml**

Update timer logic untuk menggunakan server-adjusted time:

```javascript
// ✅ PERBAIKAN: Centralized Sync Logic untuk Durasi Produksi
let durasiProduksiStartTime = null; // Timestamp UTC
let durasiProduksiSeconds = 0;
let durasiProduksiInterval = null;
let isDurasiAuto = true;

function startDurasiProduksi() {
    // Stop existing timer
    if (durasiProduksiInterval) {
        clearInterval(durasiProduksiInterval);
    }
    
    // ✅ CRITICAL: Gunakan server-adjusted time
    if (typeof window.getAdjustedServerTime === 'function') {
        durasiProduksiStartTime = window.getAdjustedServerTime();
        console.log('✅ Durasi started with server-adjusted time:', durasiProduksiStartTime.toISOString());
    } else {
        durasiProduksiStartTime = new Date();
        console.warn('⚠️ Using local time for durasi (server-adjusted not available)');
    }
    
    // Start interval
    durasiProduksiInterval = setInterval(() => {
        if (durasiProduksiStartTime) {
            const now = typeof window.getAdjustedServerTime === 'function' 
                ? window.getAdjustedServerTime() 
                : new Date();
            
            const elapsed = Math.floor((now - durasiProduksiStartTime) / 1000);
            durasiProduksiSeconds = Math.max(0, elapsed);
            updateDurasiProduksiDisplay();
        }
    }, 1000);
    
    console.log('✅ Durasi produksi timer started');
}

function updateDurasiProduksiDisplay() {
    const displayEl = document.getElementById('durasi-produksi-display');
    const hiddenEl = document.getElementById('hidden-durasi-seconds');
    
    if (displayEl) {
        const hours = Math.floor(durasiProduksiSeconds / 3600);
        const minutes = Math.floor((durasiProduksiSeconds % 3600) / 60);
        const seconds = durasiProduksiSeconds % 60;
        
        displayEl.value = 
            String(hours).padStart(2, '0') + ':' + 
            String(minutes).padStart(2, '0') + ':' + 
            String(seconds).padStart(2, '0');
    }
    
    if (hiddenEl) {
        hiddenEl.value = durasiProduksiSeconds;
    }
}

function stopDurasiProduksi() {
    if (durasiProduksiInterval) {
        clearInterval(durasiProduksiInterval);
        durasiProduksiInterval = null;
    }
    console.log('⏸️ Durasi produksi timer stopped at:', durasiProduksiSeconds, 'seconds');
}

function resetDurasiProduksi() {
    stopDurasiProduksi();
    durasiProduksiSeconds = 0;
    durasiProduksiStartTime = null;
    updateDurasiProduksiDisplay();
    console.log('🔄 Durasi produksi timer reset');
}
```

### PERBAIKAN 5: Migration untuk ProductionCount

**Buat migration baru:**

```bash
dotnet ef migrations add AddProductionDataFieldsToProductionCount
dotnet ef database update
```

**Atau tambahkan manual di ApplicationDbContext.cs:**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ... existing code ...
    
    // ✅ TAMBAHKAN: Configure ProductionCount
    modelBuilder.Entity<ProductionCount>(entity =>
    {
        entity.ToTable("tb_lwpmixing_ProductionCounts", "produksi");
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.NomorLot).HasMaxLength(50);
        entity.Property(e => e.LotBo).HasMaxLength(50);
        entity.Property(e => e.NamaCompound).HasMaxLength(100);
        entity.Property(e => e.BeratAct).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Penipisan).HasMaxLength(50);
        entity.Property(e => e.Keterangan).HasMaxLength(200);
        entity.Property(e => e.Injection).HasMaxLength(50);
    });
}
```

---

## CHECKLIST IMPLEMENTASI

### Step 1: Verifikasi Data SCW
- [ ] Jalankan aplikasi dan cek console log untuk seeding SCW
- [ ] Buka SQL Server dan query: `SELECT * FROM produksi.tb_lwpmixing_Scw4MTypes`
- [ ] Buka SQL Server dan query: `SELECT * FROM produksi.tb_lwpmixing_ScwRemarks`
- [ ] Jika kosong, hapus database dan jalankan ulang aplikasi

### Step 2: Update Model ProductionCount
- [ ] Tambahkan properties baru di `Models/ProductionCount.cs`
- [ ] Update `ApplicationDbContext.cs` untuk configure table
- [ ] Jalankan migration atau hapus database untuk recreate

### Step 3: Tambahkan Endpoint SubmitProductionData
- [ ] Tambahkan method di `Controllers/OperatorController.cs`
- [ ] Test endpoint dengan Postman atau browser

### Step 4: Update JavaScript Timer
- [ ] Replace timer logic di `Views/Machine/OeeDetail.cshtml`
- [ ] Test timer berjalan dengan benar

### Step 5: Testing End-to-End
- [ ] Test SCW dropdown muncul data
- [ ] Test SCW submit berhasil
- [ ] Test Production Data submit berhasil
- [ ] Test data muncul di Recent Product Count
- [ ] Test durasi real-time berjalan

---

## EXPECTED RESULTS

### 1. SCW
- Dropdown "Jenis 4M" menampilkan 5 items: Material, Methode, Machine, Man, No Problem
- Dropdown "Jenis Remark" menampilkan items sesuai kategori yang dipilih
- Submit SCW berhasil dan data tersimpan ke `tb_lwpmixing_ScwEvents`

### 2. Production Data
- Form Man Power & Injection + Data Produksi lengkap
- Submit berhasil dan data tersimpan ke `tb_lwpmixing_ProductionCounts`
- Data muncul di Recent Product Count dengan durasi yang benar

### 3. Durasi Real-Time
- Timer berjalan real-time menggunakan server-adjusted time
- Durasi akurat dan sinkron dengan backend
- Timer reset setelah submit

---

## TROUBLESHOOTING

### Jika SCW Dropdown Masih Kosong:
1. Cek console log saat aplikasi start
2. Cek database: `SELECT * FROM produksi.tb_lwpmixing_Scw4MTypes`
3. Jika kosong, hapus database: `DROP DATABASE produksi`
4. Restart aplikasi untuk recreate database

### Jika Submit Production Data Error:
1. Cek console log di browser (F12)
2. Cek console log di terminal aplikasi
3. Pastikan endpoint `/Operator/SubmitProductionData` sudah ada
4. Pastikan model ProductionCount sudah update

### Jika Durasi Tidak Real-Time:
1. Cek apakah `window.getAdjustedServerTime` tersedia
2. Cek console log untuk timer start/stop
3. Pastikan interval tidak di-clear oleh kode lain
