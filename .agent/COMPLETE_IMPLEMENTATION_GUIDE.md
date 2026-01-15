# IMPLEMENTASI LENGKAP - SCANNER & FORM SUBMISSION

## STATUS: READY TO IMPLEMENT

Dokumen ini berisi semua kode yang perlu ditambahkan/dimodifikasi untuk menyelesaikan perbaikan scanner dan form submission.

---

## PERBAIKAN 1: Form Validation Function

**Lokasi**: Tambahkan di bagian JavaScript, sebelum submit handler

```javascript
// ========== FORM VALIDATION ==========
function validateProductionForm() {
    console.log('🔍 Validating production form...');
    const errors = [];
    const warnings = [];
    
    // 1. Man Power & Injection
    const manPower = document.getElementById('select-man-power')?.value;
    const injection = document.querySelector('#injection-group input[name="injection"]:checked')?.value;
    
    if (!manPower || manPower === '') {
        errors.push('Man Power harus dipilih');
    }
    if (!injection) {
        errors.push('Group Injection harus dipilih');
    }
    
    // 2. Input Data Produksi
    const lotNumber = document.getElementById('input-nomor-lot')?.value?.trim();
    const lotBo = document.getElementById('input-lot-bo')?.value?.trim();
    const namaCompound = document.getElementById('input-nama-compound')?.value?.trim();
    const beratAct = document.getElementById('input-berat-act')?.value?.trim();
    const penipisan = document.querySelector('#penipisan-group input[name="penipisan"]:checked')?.value;
    const keterangan = document.getElementById('select-keterangan')?.value;
    
    if (!lotNumber || lotNumber === '') {
        errors.push('Nomor Lot harus diisi');
    }
    if (!lotBo || lotBo === '') {
        errors.push('Lot BO harus diisi');
    }
    if (!namaCompound || namaCompound === '') {
        errors.push('Nama Compound harus diisi');
    }
    if (!beratAct || beratAct === '') {
        errors.push('Berat Act harus diisi');
    }
    if (!penipisan) {
        errors.push('Penipisan harus dipilih');
    }
    if (!keterangan || keterangan === '') {
        errors.push('Keterangan harus dipilih');
    }
    
    // 3. Komponen (optional tapi recommended)
    if (!selectedKomponenId) {
        warnings.push('Komponen belum dipilih (opsional)');
    }
    
    // Show detailed feedback
    if (errors.length > 0) {
        const errorList = errors.map((e, i) => `${i + 1}. ${e}`).join('\n');
        const errorMessage = `❌ Form belum lengkap:\n\n${errorList}`;
        
        if (typeof showToast === 'function') {
            showToast(errorMessage, 'error');
        } else {
            alert(errorMessage);
        }
        
        console.warn('❌ Validation errors:', errors);
        return false;
    }
    
    if (warnings.length > 0) {
        console.warn('⚠️ Validation warnings:', warnings);
    }
    
    console.log('✅ Form validation passed');
    return true;
}
```

---

## PERBAIKAN 2: Submit Button Handler

**Lokasi**: Cari `btn-submit-produksi` event listener dan replace dengan kode ini

```javascript
// ========== SUBMIT PRODUCTION DATA ==========
const btnSubmitProduksi = document.getElementById('btn-submit-produksi');
if (btnSubmitProduksi) {
    console.log('✅ Submit button found, attaching event listener...');
    
    btnSubmitProduksi.addEventListener('click', async function(e) {
        e.preventDefault();
        e.stopPropagation();
        
        console.log('🖱️ Submit Production Data button clicked');
        
        // ✅ VALIDATION
        if (!validateProductionForm()) {
            console.warn('❌ Validation failed, submit cancelled');
            return;
        }
        
        // ✅ PERBAIKAN: Show loading state
        const originalText = btnSubmitProduksi.innerHTML;
        btnSubmitProduksi.disabled = true;
        btnSubmitProduksi.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-2"></i>Menyimpan...';
        
        try {
            // ✅ Collect data
            const formData = {
                machineId: '@Model.MachineId',
                nomorLot: document.getElementById('input-nomor-lot')?.value?.trim(),
                lotBo: document.getElementById('input-lot-bo')?.value?.trim(),
                namaCompound: document.getElementById('input-nama-compound')?.value?.trim(),
                beratAct: document.getElementById('input-berat-act')?.value?.trim(),
                penipisan: document.querySelector('#penipisan-group input[name="penipisan"]:checked')?.value,
                keterangan: document.getElementById('select-keterangan')?.value,
                manPowerId: document.getElementById('select-man-power')?.value,
                injection: document.querySelector('#injection-group input[name="injection"]:checked')?.value,
                komponenId: selectedKomponenId,
                durasiProduksiSeconds: durasiProduksiSeconds || 0
            };
            
            console.log('📤 Submitting data:', formData);
            
            // ✅ PERBAIKAN: Submit to backend
            const response = await fetch('/Operator/SubmitProductionData', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                },
                body: JSON.stringify(formData)
            });
            
            const result = await response.json();
            
            if (result.success) {
                console.log('✅ Data saved successfully:', result);
                
                if (typeof showToast === 'function') {
                    showToast('✅ Data produksi berhasil disimpan!', 'success');
                } else {
                    alert('✅ Data produksi berhasil disimpan!');
                }
                
                // ✅ Save Man Power & Injection to localStorage (PERSISTENT)
                saveManPowerAndGroup();
                
                // ✅ Reset form (partial - keep Man Power & Injection)
                resetProductionForm();
                
                // ✅ Start timer if AUTO mode
                if (typeof isDurasiAuto !== 'undefined' && isDurasiAuto) {
                    if (typeof startDurasiProduksi === 'function') {
                        startDurasiProduksi();
                        console.log('✅ Timer durasi started (AUTO mode)');
                    }
                }
                
            } else {
                console.error('❌ Submit failed:', result.message);
                
                if (typeof showToast === 'function') {
                    showToast('❌ Error: ' + (result.message || 'Gagal menyimpan data'), 'error');
                } else {
                    alert('❌ Error: ' + (result.message || 'Gagal menyimpan data'));
                }
            }
            
        } catch (error) {
            console.error('❌ Submit error:', error);
            
            if (typeof showToast === 'function') {
                showToast('❌ Error: ' + error.message, 'error');
            } else {
                alert('❌ Error: ' + error.message);
            }
            
        } finally {
            // ✅ PERBAIKAN: Restore button state
            btnSubmitProduksi.disabled = false;
            btnSubmitProduksi.innerHTML = originalText;
            console.log('✅ Button state restored');
        }
    });
    
    console.log('✅ Submit button event listener attached');
} else {
    console.error('❌ Submit button not found: btn-submit-produksi');
}
```

---

## PERBAIKAN 3: Form Reset Function

**Lokasi**: Tambahkan function baru atau replace yang sudah ada

```javascript
// ========== FORM RESET ==========
function resetProductionForm() {
    console.log('🔄 Resetting production form...');
    
    // ✅ Reset input fields (TIDAK termasuk Man Power & Injection)
    const fieldsToReset = [
        'input-nomor-lot',
        'input-lot-bo',
        'input-nama-compound',
        'input-berat-act'
    ];
    
    fieldsToReset.forEach(id => {
        const input = document.getElementById(id);
        if (input) {
            input.value = '';
            input.disabled = false; // Enable for next input
            input.readOnly = false;
            console.log(`✅ Reset field: ${id}`);
        }
    });
    
    // ✅ Reset Keterangan dropdown
    const selectKeterangan = document.getElementById('select-keterangan');
    if (selectKeterangan) {
        selectKeterangan.value = '';
        console.log('✅ Reset keterangan dropdown');
    }
    
    // ✅ Reset Penipisan radio
    const penipisanRadios = document.querySelectorAll('#penipisan-group input[name="penipisan"]');
    penipisanRadios.forEach(radio => {
        radio.checked = false;
    });
    console.log('✅ Reset penipisan radio buttons');
    
    // ✅ Reset Komponen
    if (typeof selectedKomponenId !== 'undefined') {
        selectedKomponenId = null;
    }
    
    const selectKomponen = document.getElementById('select-komponen');
    if (selectKomponen) {
        selectKomponen.innerHTML = '<option value="">-- Pilih Komponen --</option>';
    }
    
    const komponenContainer = document.getElementById('komponen-container');
    if (komponenContainer) {
        komponenContainer.style.display = 'none';
    }
    console.log('✅ Reset komponen dropdown');
    
    // ✅ TIDAK RESET: Man Power & Injection (PERSISTENT)
    console.log('✅ Form reset complete (Man Power & Injection preserved)');
}
```

---

## PERBAIKAN 4: Timer Durasi Functions

**Lokasi**: Cari atau tambahkan timer functions

```javascript
// ========== TIMER DURASI PRODUKSI ==========
// Global variables
let durasiProduksiInterval = null;
let durasiProduksiStartTime = null;
let durasiProduksiSeconds = 0;
let isDurasiAuto = true;

function startDurasiProduksi() {
    console.log('⏱️ Starting durasi produksi timer...');
    
    // Stop existing timer
    if (durasiProduksiInterval) {
        clearInterval(durasiProduksiInterval);
        durasiProduksiInterval = null;
    }
    
    // ✅ PERBAIKAN: Use server-adjusted time
    if (typeof window.getAdjustedServerTime === 'function') {
        durasiProduksiStartTime = window.getAdjustedServerTime();
        console.log('✅ Using server-adjusted time:', durasiProduksiStartTime.toISOString());
    } else {
        durasiProduksiStartTime = new Date();
        console.warn('⚠️ Using local time (server-adjusted not available)');
    }
    
    durasiProduksiSeconds = 0;
    
    // ✅ Show durasi container
    const durasiContainer = document.getElementById('durasi-produksi-container');
    if (durasiContainer) {
        durasiContainer.style.display = 'block';
    }
    
    // ✅ Update display immediately
    updateDurasiProduksiDisplay();
    
    // ✅ Start interval
    durasiProduksiInterval = setInterval(() => {
        const now = typeof window.getAdjustedServerTime === 'function' 
            ? window.getAdjustedServerTime() 
            : new Date();
        
        durasiProduksiSeconds = Math.floor((now - durasiProduksiStartTime) / 1000);
        updateDurasiProduksiDisplay();
    }, 1000);
    
    console.log('✅ Durasi timer started');
}

function updateDurasiProduksiDisplay() {
    const displayEl = document.getElementById('durasi-produksi-display');
    if (displayEl) {
        const hours = Math.floor(durasiProduksiSeconds / 3600);
        const minutes = Math.floor((durasiProduksiSeconds % 3600) / 60);
        const seconds = durasiProduksiSeconds % 60;
        
        displayEl.value = 
            String(hours).padStart(2, '0') + ':' + 
            String(minutes).padStart(2, '0') + ':' + 
            String(seconds).padStart(2, '0');
    }
    
    // Update hidden input if exists
    const hiddenEl = document.getElementById('hidden-durasi-seconds');
    if (hiddenEl) {
        hiddenEl.value = durasiProduksiSeconds;
    }
}

function stopDurasiProduksi() {
    if (durasiProduksiInterval) {
        clearInterval(durasiProduksiInterval);
        durasiProduksiInterval = null;
    }
    console.log('⏸️ Durasi timer stopped at:', durasiProduksiSeconds, 'seconds');
}

function resetDurasiProduksi() {
    stopDurasiProduksi();
    durasiProduksiSeconds = 0;
    durasiProduksiStartTime = null;
    updateDurasiProduksiDisplay();
    
    // Hide container
    const durasiContainer = document.getElementById('durasi-produksi-container');
    if (durasiContainer) {
        durasiContainer.style.display = 'none';
    }
    
    console.log('🔄 Durasi timer reset');
}
```

---

## PERBAIKAN 5: Backend Endpoint (C#)

**File**: `Controllers/OperatorController.cs`

**Lokasi**: Tambahkan setelah method `Scw`

```csharp
// POST: Submit Production Data
[HttpPost]
public async Task<IActionResult> SubmitProductionData([FromBody] ProductionDataDto data)
{
    bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
    
    try
    {
        Console.WriteLine($"📥 SubmitProductionData called for machine: {data.MachineId}");
        
        // 1. Find active job
        var activeJob = await _context.JobRuns
            .Where(j => j.MachineId == data.MachineId && j.EndTime == null)
            .OrderByDescending(j => j.StartTime)
            .FirstOrDefaultAsync();
        
        if (activeJob == null)
        {
            Console.WriteLine($"❌ No active job found for machine: {data.MachineId}");
            if (isAjax)
                return Json(new { success = false, message = "Tidak ada job aktif" });
            return BadRequest("Tidak ada job aktif");
        }
        
        Console.WriteLine($"✅ Active job found: {activeJob.Id}");
        
        // 2. Update Man Power if provided
        if (data.ManPowerId.HasValue && data.ManPowerId.Value > 0)
        {
            activeJob.ManPowerId = data.ManPowerId.Value;
            Console.WriteLine($"✅ Man Power updated: {data.ManPowerId}");
        }
        
        // 3. Create ProductionCount record
        var productionCount = new ProductionCount
        {
            JobRunId = activeJob.Id,
            Timestamp = DateTime.Now,
            GoodCount = 1, // Default untuk tracking per item
            RejectCount = 0,
            NomorLot = data.NomorLot,
            LotBo = data.LotBo,
            NamaCompound = data.NamaCompound,
            BeratAct = !string.IsNullOrEmpty(data.BeratAct) ? decimal.Parse(data.BeratAct) : 0,
            Penipisan = data.Penipisan,
            Keterangan = data.Keterangan,
            Injection = data.Injection,
            KomponenId = data.KomponenId,
            DurasiProduksiSeconds = data.DurasiProduksiSeconds
        };
        
        _context.ProductionCounts.Add(productionCount);
        await _context.SaveChangesAsync();
        
        Console.WriteLine($"✅ ProductionCount created: ID={productionCount.Id}");
        
        // 4. Broadcast real-time update
        await _hubContext.Clients.All.SendAsync("OeeUpdated", new
        {
            Type = "ProductionDataSubmitted",
            MachineId = data.MachineId,
            Message = $"Data produksi disimpan: {data.NomorLot}",
            Timestamp = DateTime.Now
        });
        
        if (isAjax)
            return Json(new { 
                success = true, 
                message = "Data produksi berhasil disimpan",
                productionCountId = productionCount.Id
            });
        
        return Ok(new { success = true });
        
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error in SubmitProductionData: {ex.Message}");
        Console.WriteLine($"   StackTrace: {ex.StackTrace}");
        
        if (isAjax)
            return Json(new { success = false, message = ex.Message });
        return BadRequest(ex.Message);
    }
}

// DTO class
public class ProductionDataDto
{
    public string MachineId { get; set; }
    public string NomorLot { get; set; }
    public string LotBo { get; set; }
    public string NamaCompound { get; set; }
    public string BeratAct { get; set; }
    public string Penipisan { get; set; }
    public string Keterangan { get; set; }
    public int? ManPowerId { get; set; }
    public string Injection { get; set; }
    public int? KomponenId { get; set; }
    public int DurasiProduksiSeconds { get; set; }
}
```

---

## PERBAIKAN 6: Update ProductionCount Model (jika belum ada)

**File**: `Models/ProductionCount.cs`

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
    
    // ✅ Production Data Fields
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

---

## TESTING CHECKLIST

Setelah implementasi, test dengan urutan:

1. ✅ Klik tombol "Scan" → Modal muncul
2. ✅ Browser minta permission kamera → Izinkan
3. ✅ Scanner aktif dan menampilkan video
4. ✅ Scan barcode/QR code → Terdeteksi
5. ✅ Modal close otomatis
6. ✅ Input field terisi dengan hasil scan
7. ✅ Lengkapi semua field required
8. ✅ Klik "Submit Data Produksi"
9. ✅ Loading state muncul
10. ✅ Success message muncul
11. ✅ Form reset (kecuali Man Power & Injection)
12. ✅ Timer durasi mulai berjalan
13. ✅ Check database → Data tersimpan

---

**Status**: READY TO IMPLEMENT
**Priority**: HIGH
**Estimated Time**: 1-2 hours
