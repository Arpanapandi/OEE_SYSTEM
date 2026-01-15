# PERBAIKAN SCANNER - IMPLEMENTATION PLAN

## 📋 TUJUAN

Memperbaiki fungsi scanner agar memenuhi semua kriteria hasil akhir:

✅ Scanner modal muncul saat tombol scan diklik
✅ Camera permission diminta dan scanner aktif
✅ Scan berhasil dan mengisi input field
✅ Form validation memberikan feedback yang jelas
✅ Submit button bereaksi saat diklik
✅ Loading state ditampilkan saat submit
✅ Success/error message ditampilkan setelah submit
✅ Data tersimpan di database
✅ Form reset setelah submit sukses
✅ Timer durasi produksi berjalan dengan benar

---

## 🔍 ANALISIS KODE SAAT INI

### Scanner Implementation (Lines 2377-3100+)

**Yang Sudah Ada:**
1. ✅ Scanner modal HTML (lines 1048-1076)
2. ✅ Event listeners untuk tombol scan (lines 2448-2494)
3. ✅ `openScanner()` function (lines 2554-2881)
4. ✅ Dual library support (Quagga2 + html5-qrcode)
5. ✅ `handleScanSuccess()` function (lines 2964-3053)
6. ✅ `stopScanner()` function (lines 3055-3091)
7. ✅ Camera permission request
8. ✅ Input field population after scan

**Masalah yang Mungkin Terjadi:**
1. ❌ Modal tidak muncul (Bootstrap instance issue)
2. ❌ Camera permission ditolak atau tidak diminta
3. ❌ Scanner library tidak ter-load
4. ❌ Scan tidak mengisi input field
5. ❌ Form validation tidak berjalan
6. ❌ Submit button tidak bereaksi
7. ❌ Data tidak tersimpan ke database
8. ❌ Timer durasi tidak berjalan

---

## 🛠️ SOLUSI PERBAIKAN

### PERBAIKAN 1: Pastikan Modal Scanner Muncul

**File: Views/Machine/OeeDetail.cshtml**

**Lokasi: Lines 2554-2631 (openScanner function)**

**Masalah:**
- Modal mungkin tidak muncul karena Bootstrap instance issue
- Backdrop tidak terhapus dengan benar

**Solusi:**
```javascript
window.openScanner = async function() {
    console.log('🔧 openScanner called, target:', currentScanTarget);
    
    const scannerModalEl = document.getElementById('scannerModal');
    if (!scannerModalEl) {
        console.error('❌ Scanner modal element not found!');
        showToast('Error: Scanner modal tidak ditemukan', 'error');
        return;
    }
    
    // ✅ PERBAIKAN: Hapus backdrop lama jika ada
    const oldBackdrop = document.querySelector('.modal-backdrop');
    if (oldBackdrop) {
        oldBackdrop.remove();
    }
    
    // ✅ PERBAIKAN: Reset modal state
    scannerModalEl.classList.remove('show');
    scannerModalEl.style.display = 'none';
    document.body.classList.remove('modal-open');
    
    // Update modal title
    const targetLabel = document.getElementById('scanner-target-label');
    if (targetLabel) {
        if (currentScanTarget === 'lot-bo') targetLabel.textContent = 'Lot BO';
        else if (currentScanTarget === 'nomor-lot') targetLabel.textContent = 'Nomor Lot';
        else if (currentScanTarget === 'nama-compound') targetLabel.textContent = 'Nama Compound';
        else targetLabel.textContent = 'Barcode/QR Code';
    }
    
    // ✅ PERBAIKAN: Buka modal dengan Bootstrap
    try {
        let modalInstance = bootstrap.Modal.getInstance(scannerModalEl);
        if (modalInstance) {
            modalInstance.dispose(); // Hapus instance lama
        }
        
        modalInstance = new bootstrap.Modal(scannerModalEl, {
            backdrop: 'static',
            keyboard: true
        });
        
        // ✅ CRITICAL: Initialize scanner SETELAH modal shown
        scannerModalEl.addEventListener('shown.bs.modal', async function() {
            console.log('✅ Modal shown, initializing scanner...');
            await initializeScanner();
        }, { once: true });
        
        modalInstance.show();
        console.log('✅ Modal opened successfully');
        
    } catch (err) {
        console.error('❌ Error opening modal:', err);
        showToast('Gagal membuka scanner: ' + err.message, 'error');
    }
};
```

### PERBAIKAN 2: Pastikan Camera Permission Diminta

**Lokasi: Lines 2713-2864 (initScannerSequence)**

**Solusi:**
```javascript
async function initializeScanner() {
    try {
        console.log('🔧 Initializing scanner...');
        
        // ✅ VALIDATION: Check browser support
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            showToast('Browser tidak mendukung akses kamera', 'error');
            return;
        }
        
        // ✅ VALIDATION: Check HTTPS/localhost
        const isSecure = window.location.protocol === 'https:' || 
                        window.location.hostname === 'localhost' || 
                        window.location.hostname === '127.0.0.1';
        
        if (!isSecure) {
            showToast('Akses kamera memerlukan HTTPS atau localhost', 'error');
            return;
        }
        
        // ✅ PERBAIKAN: Request camera permission explicitly
        try {
            console.log('📷 Requesting camera permission...');
            const stream = await navigator.mediaDevices.getUserMedia({ 
                video: { facingMode: 'environment' } 
            });
            
            // Stop stream immediately (we just need permission)
            stream.getTracks().forEach(track => track.stop());
            console.log('✅ Camera permission granted');
            
        } catch (permErr) {
            console.error('❌ Camera permission denied:', permErr);
            showToast('Akses kamera ditolak. Silakan izinkan akses kamera.', 'error');
            
            // Close modal
            const scannerModalEl = document.getElementById('scannerModal');
            if (scannerModalEl) {
                const modalInstance = bootstrap.Modal.getInstance(scannerModalEl);
                if (modalInstance) modalInstance.hide();
            }
            return;
        }
        
        // ✅ Load scanner libraries
        if (!scannerLibraryLoaded) {
            console.log('📚 Loading Quagga2 library...');
            try {
                await loadScannerLibrary();
            } catch (err) {
                console.warn('⚠️ Quagga2 failed to load:', err);
            }
        }
        
        if (!qrCodeLibraryLoaded) {
            console.log('📚 Loading html5-qrcode library...');
            try {
                await loadQrCodeLibrary();
            } catch (err) {
                console.warn('⚠️ html5-qrcode failed to load:', err);
            }
        }
        
        // ✅ Check if at least one library loaded
        if (typeof Quagga === 'undefined' && typeof Html5Qrcode === 'undefined') {
            showToast('Scanner library gagal dimuat. Silakan refresh halaman.', 'error');
            return;
        }
        
        // ✅ Start scanner
        await startScanning();
        
    } catch (err) {
        console.error('❌ Error initializing scanner:', err);
        showToast('Error: ' + err.message, 'error');
    }
}
```

### PERBAIKAN 3: Pastikan Scan Mengisi Input Field

**Lokasi: Lines 2964-3053 (handleScanSuccess)**

**Masalah:**
- Input field mungkin disabled
- Event tidak ter-trigger

**Solusi:**
```javascript
async function handleScanSuccess(decodedText) {
    if (!decodedText) return;
    decodedText = decodedText.trim();
    
    console.log('✅ Scan successful:', decodedText, 'Target:', currentScanTarget);
    
    // ✅ VALIDATION: Check length for lot/part number
    if (currentScanTarget === 'lot-bo' || currentScanTarget === 'nomor-lot') {
        if (decodedText.length > 12) {
            showToast(`Code terlalu panjang (max 12 karakter): ${decodedText}`, 'error');
            return;
        }
    }
    
    // ✅ Stop scanner
    stopScanner();
    
    // ✅ Close modal
    const scannerModalEl = document.getElementById('scannerModal');
    if (scannerModalEl) {
        const modalInstance = bootstrap.Modal.getInstance(scannerModalEl);
        if (modalInstance) modalInstance.hide();
    }
    
    // ✅ PERBAIKAN: Fill input field and enable it
    let inputId;
    if (currentScanTarget === 'lot-bo') {
        inputId = 'input-lot-bo';
    } else if (currentScanTarget === 'nomor-lot') {
        inputId = 'input-nomor-lot';
    } else if (currentScanTarget === 'nama-compound') {
        inputId = 'input-nama-compound';
    }
    
    if (inputId) {
        const input = document.getElementById(inputId);
        if (input) {
            // ✅ CRITICAL: Enable input first
            input.disabled = false;
            input.readOnly = false;
            
            // ✅ Set value
            input.value = decodedText;
            
            // ✅ Trigger events
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
            
            // ✅ Visual feedback
            input.classList.add('border-success');
            setTimeout(() => {
                input.classList.remove('border-success');
            }, 2000);
            
            console.log('✅ Input field filled:', inputId, '=', decodedText);
            showToast(`Scan berhasil: ${decodedText}`, 'success');
            
            // ✅ Trigger komponen load if needed
            if (currentScanTarget === 'lot-bo' || currentScanTarget === 'nomor-lot') {
                setTimeout(() => {
                    if (typeof checkAndLoadKomponen === 'function') {
                        checkAndLoadKomponen();
                    }
                }, 500);
            }
        } else {
            console.error('❌ Input field not found:', inputId);
            showToast('Error: Input field tidak ditemukan', 'error');
        }
    }
}
```

### PERBAIKAN 4: Form Validation dengan Feedback

**Lokasi: Production Data Submit Handler**

**Solusi:**
```javascript
// ✅ PERBAIKAN: Validation function dengan feedback jelas
function validateProductionForm() {
    const errors = [];
    const warnings = [];
    
    // 1. Man Power & Injection
    const manPower = document.getElementById('select-man-power')?.value;
    const injection = document.querySelector('#injection-group input[name="injection"]:checked')?.value;
    
    if (!manPower) errors.push('Man Power harus dipilih');
    if (!injection) errors.push('Group Injection harus dipilih');
    
    // 2. Input Data Produksi
    const lotNumber = document.getElementById('input-nomor-lot')?.value?.trim();
    const lotBo = document.getElementById('input-lot-bo')?.value?.trim();
    const namaCompound = document.getElementById('input-nama-compound')?.value?.trim();
    const beratAct = document.getElementById('input-berat-act')?.value?.trim();
    const penipisan = document.querySelector('#penipisan-group input[name="penipisan"]:checked')?.value;
    const keterangan = document.getElementById('select-keterangan')?.value;
    
    if (!lotNumber) errors.push('Nomor Lot harus diisi');
    if (!lotBo) errors.push('Lot BO harus diisi');
    if (!namaCompound) errors.push('Nama Compound harus diisi');
    if (!beratAct) errors.push('Berat Act harus diisi');
    if (!penipisan) errors.push('Penipisan harus dipilih');
    if (!keterangan) errors.push('Keterangan harus dipilih');
    
    // ✅ PERBAIKAN: Show detailed feedback
    if (errors.length > 0) {
        const errorMessage = '❌ Form belum lengkap:\n\n' + errors.map((e, i) => `${i + 1}. ${e}`).join('\n');
        showToast(errorMessage, 'error');
        console.warn('Validation errors:', errors);
        return false;
    }
    
    if (warnings.length > 0) {
        console.warn('Validation warnings:', warnings);
    }
    
    console.log('✅ Form validation passed');
    return true;
}
```

### PERBAIKAN 5: Submit Button dengan Loading State

**Lokasi: Production Data Submit Handler**

**Solusi:**
```javascript
const btnSubmitProduksi = document.getElementById('btn-submit-produksi');
if (btnSubmitProduksi) {
    btnSubmitProduksi.addEventListener('click', async function(e) {
        e.preventDefault();
        e.stopPropagation();
        
        console.log('🖱️ Submit button clicked');
        
        // ✅ VALIDATION
        if (!validateProductionForm()) {
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
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(formData)
            });
            
            const result = await response.json();
            
            if (result.success) {
                console.log('✅ Data saved successfully');
                showToast('✅ Data produksi berhasil disimpan!', 'success');
                
                // ✅ Reset form (partial - keep Man Power & Injection)
                resetProductionForm();
                
                // ✅ Start timer if AUTO mode
                if (isDurasiAuto) {
                    startDurasiProduksi();
                }
                
            } else {
                console.error('❌ Submit failed:', result.message);
                showToast('❌ Error: ' + (result.message || 'Gagal menyimpan data'), 'error');
            }
            
        } catch (error) {
            console.error('❌ Submit error:', error);
            showToast('❌ Error: ' + error.message, 'error');
            
        } finally {
            // ✅ PERBAIKAN: Restore button state
            btnSubmitProduksi.disabled = false;
            btnSubmitProduksi.innerHTML = originalText;
        }
    });
}
```

### PERBAIKAN 6: Pastikan Data Tersimpan ke Database

**File: Controllers/OperatorController.cs**

**Lokasi: Setelah method Scw**

**Solusi:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SubmitProductionData(
    [FromBody] ProductionDataDto data)
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

### PERBAIKAN 7: Form Reset Setelah Submit

**Lokasi: resetProductionForm function**

**Solusi:**
```javascript
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
        }
    });
    
    // ✅ Reset Keterangan dropdown
    const selectKeterangan = document.getElementById('select-keterangan');
    if (selectKeterangan) {
        selectKeterangan.value = '';
    }
    
    // ✅ Reset Penipisan radio
    const penipisanRadios = document.querySelectorAll('#penipisan-group input[name="penipisan"]');
    penipisanRadios.forEach(radio => radio.checked = false);
    
    // ✅ Reset Komponen
    selectedKomponenId = null;
    const selectKomponen = document.getElementById('select-komponen');
    if (selectKomponen) {
        selectKomponen.innerHTML = '<option value="">-- Pilih Komponen --</option>';
    }
    const komponenContainer = document.getElementById('komponen-container');
    if (komponenContainer) {
        komponenContainer.style.display = 'none';
    }
    
    // ✅ TIDAK RESET: Man Power & Injection (PERSISTENT)
    console.log('✅ Form reset complete (Man Power & Injection preserved)');
}
```

### PERBAIKAN 8: Timer Durasi Produksi

**Lokasi: Durasi functions**

**Solusi:**
```javascript
// ✅ Global variables
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
    console.log('🔄 Durasi timer reset');
}
```

---

## ✅ CHECKLIST IMPLEMENTASI

### 1. Scanner Modal
- [ ] Modal muncul saat tombol scan diklik
- [ ] Modal title update sesuai target
- [ ] Modal close dengan benar
- [ ] Backdrop dihapus setelah close

### 2. Camera Permission
- [ ] Permission diminta saat modal dibuka
- [ ] Error message jika permission ditolak
- [ ] Fallback jika browser tidak support

### 3. Scanner Functionality
- [ ] Library ter-load dengan benar
- [ ] Scanner aktif setelah permission granted
- [ ] Scan berhasil detect barcode/QR code
- [ ] Scanner stop setelah scan berhasil

### 4. Input Field Population
- [ ] Input field enabled setelah scan
- [ ] Value ter-isi dengan benar
- [ ] Event triggered untuk validation
- [ ] Visual feedback (border success)

### 5. Form Validation
- [ ] Validation berjalan saat submit
- [ ] Error message jelas dan detail
- [ ] Required fields ter-check semua
- [ ] Toast notification muncul

### 6. Submit Button
- [ ] Button bereaksi saat diklik
- [ ] Loading state ditampilkan
- [ ] Button disabled saat submit
- [ ] Button restored setelah submit

### 7. Data Persistence
- [ ] Data tersimpan ke database
- [ ] ProductionCount record created
- [ ] Man Power updated jika ada
- [ ] Real-time broadcast berjalan

### 8. Form Reset
- [ ] Form reset setelah submit sukses
- [ ] Man Power & Injection TIDAK reset
- [ ] Input fields di-clear
- [ ] Komponen dropdown reset

### 9. Timer Durasi
- [ ] Timer start otomatis (AUTO mode)
- [ ] Timer berjalan per detik
- [ ] Display format HH:mm:ss
- [ ] Timer stop saat submit quantity

---

## 🚀 URUTAN IMPLEMENTASI

1. **Perbaiki Modal Scanner** (PERBAIKAN 1)
2. **Perbaiki Camera Permission** (PERBAIKAN 2)
3. **Perbaiki Scan Success Handler** (PERBAIKAN 3)
4. **Tambahkan Form Validation** (PERBAIKAN 4)
5. **Perbaiki Submit Button** (PERBAIKAN 5)
6. **Buat Backend Endpoint** (PERBAIKAN 6)
7. **Perbaiki Form Reset** (PERBAIKAN 7)
8. **Perbaiki Timer Durasi** (PERBAIKAN 8)

---

## 📝 TESTING CHECKLIST

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

**Status**: Ready for Implementation
**Priority**: HIGH
**Estimated Time**: 2-3 hours
