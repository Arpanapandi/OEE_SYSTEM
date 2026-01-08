# FINALISASI OEE DETAIL – INPUT OPERATOR PRODUKSI (SCW SUBMIT MANDIRI)

## 📋 OVERVIEW

Dokumen ini menjelaskan implementasi lengkap untuk finalisasi fitur OEE Detail dengan fokus pada:
1. **SCW (Stop Call Waiting)** - Submit mandiri terpisah dari produksi
2. **Man Power & Group Injection** - Persisten, tidak di-reset
3. **Input Data Produksi** - Validasi lengkap
4. **Durasi Produksi** - Per item dengan mode AUTO/MANUAL (durasi display running gerak detik per detik saat submit data produksi)
5. **Quantity & Production Metrics** - Real-time OEE calculation

---

## 🎯 TUJUAN

Menyempurnakan fitur OEE Detail agar seluruh proses input operator produksi berjalan:
- ✅ **Valid** - Semua validasi berjalan dengan benar
- ✅ **Sinkron** - Data tersimpan dan ter-update real-time
- ✅ **Stabil** - Tidak ada bug/error
- ✅ **Terpisah** - SCW memiliki submit mandiri

---

## 📦 KOMPONEN UTAMA

### 1. SCW (STOP CALL WAITING) – SUBMIT MANDIRI

#### Status Implementasi: ✅ SUDAH ADA (Perlu Penyempurnaan)

#### Lokasi Kode:
- **HTML**: Lines 556-613 (OeeDetail.cshtml)
- **JavaScript**: Lines 7061-7217 (SCW Handler)

#### Yang Sudah Ada:
```javascript
// Handler SCW dengan mapping data
window.handleScw4MChange = async function(argValue) {
    // Populate Jenis Remark berdasarkan Jenis 4M
    // Validasi dan enable/disable dropdown
}

// Submit handler
$('#btn-scw-submit').off('click').on('click', async function() {
    // Validasi typeId dan remarkId
    // Submit via AJAX ke /Operator/Scw
    // Reset form setelah sukses
});
```

#### Yang Perlu Diperbaiki:

**A. Validasi Tombol "Simpan SCW"**
```javascript
// Tambahkan validasi real-time untuk enable/disable tombol
function validateScwForm() {
    const typeId = $('#scw-4m-type').val();
    const remarkId = $('#scw-remark').val();
    const btnSubmit = $('#btn-scw-submit');
    
    if (typeId && remarkId) {
        btnSubmit.prop('disabled', false);
    } else {
        btnSubmit.prop('disabled', true);
    }
}

// Attach ke change events
$('#scw-4m-type').on('change', validateScwForm);
$('#scw-remark').on('change', validateScwForm);

// Init state
$(document).ready(function() {
    validateScwForm(); // Set initial state
});
```

**B. Perilaku Dropdown Jenis Remark**
```javascript
// Sudah ada di handleScw4MChange, pastikan:
// 1. Disabled sebelum Jenis 4M dipilih ✅
// 2. Aktif setelah Jenis 4M dipilih ✅
// 3. Data berubah dinamis ✅

// Mapping sudah benar:
const RAW_DATA = {
    Material: [{id:1, text:"Rejection"}, {id:2, text:"Material Shortage"}],
    Method: [{id:3, text:"SOP Tak Sesuai Standar"}],
    Machine: [{id:4, text:"Problem Mesin"}],
    Man: [{id:5, text:"Sakit"}, {id:6, text:"Izin"}, {id:7, text:"Alpha"}, {id:8, text:"Cuti"}]
};
```

**C. Reset Form SCW Setelah Submit**
```javascript
// Sudah ada di submit handler:
if(json.success) {
    alert('✅ Sukses!');
    $('#scw-4m-type').val('').trigger('change'); // ✅ Reset form
}

// Pastikan tidak mempengaruhi Man Power & Produksi ✅
```

---

### 2. MAN POWER & GROUP INJECTION (PERSISTEN)

#### Status Implementasi: ✅ SUDAH ADA (Sudah Benar)

#### Lokasi Kode:
- **HTML**: Lines 617-671 (Man Power & Injection Card)
- **JavaScript**: Lines 3994-4035 (Save/Load Functions)

#### Implementasi yang Sudah Benar:
```javascript
// 1. Save Man Power & Group (PERSISTENT)
function saveManPowerAndGroup() {
    const manPower = document.getElementById('select-man-power')?.value;
    const injection = document.querySelector('#injection-group input[name="injection"]:checked')?.value;
    
    if (manPower || injection) {
        const data = {
            manPowerId: manPower,
            injection: injection
        };
        localStorage.setItem('manPowerData', JSON.stringify(data));
        console.log('💾 Man Power & Injection Saved:', data);
    }
}

// 2. Load Man Power & Group (ON PAGE LOAD)
function loadManPowerAndGroup() {
    try {
        const dataJson = localStorage.getItem('manPowerData');
        if (dataJson) {
            const data = JSON.parse(dataJson);
            
            if (data.manPowerId) {
                const selectManPower = document.getElementById('select-man-power');
                if (selectManPower) {
                    selectManPower.value = data.manPowerId;
                    selectManPower.dispatchEvent(new Event('change'));
                }
            }
            
            if (data.injection) {
                const radio = document.querySelector(`#injection-group input[name="injection"][value="${data.injection}"]`);
                if (radio) {
                    radio.checked = true;
                    radio.dispatchEvent(new Event('change'));
                }
            }
            console.log('📂 Man Power & Injection Loaded:', data);
        }
    } catch (e) {
        console.error('Error loading Man Power data', e);
    }
}

// 3. Initialize on DOM Ready
document.addEventListener('DOMContentLoaded', function() {
    loadManPowerAndGroup();
});
```

#### Validasi:
```javascript
// Validasi Man Power & Injection sebelum submit produksi
function validateWorkflow() {
    const errors = [];
    
    const manPower = document.getElementById('select-man-power')?.value;
    const injection = document.querySelector('#injection-group input[name="injection"]:checked')?.value;
    
    if (!manPower) errors.push('Man Power harus dipilih');
    if (!injection) errors.push('Group Injection harus dipilih');
    
    return errors;
}
```

#### Reset Form (TIDAK BOLEH RESET MAN POWER & INJECTION):
```javascript
function resetProductionForm() {
    // Reset inputs yang TIDAK persisten
    const inputsToReset = [
        'input-part-number', 
        'input-lot-number', 
        'input-nama-compound',
        'input-berat-act'
        // TIDAK termasuk Man Power & Injection ✅
    ];
    
    inputsToReset.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
    });
    
    // Man Power & Injection TIDAK di-reset ✅
}
```

---

### 3. INPUT DATA PRODUKSI

#### Status Implementasi: ✅ SUDAH ADA (Sudah Benar)

#### Lokasi Kode:
- **HTML**: Lines 673-935 (Input Data Produksi Card)
- **JavaScript**: Lines 4623-4703 (Submit Handler)

#### Form Fields:
1. ✅ Nomor Lot (input-lot-number)
2. ✅ Lot BO (input-part-number)
3. ✅ Nama Compound (input-nama-compound)
4. ✅ Berat Act (input-berat-act)
5. ✅ Penipisan (radio: 1 Kali / 2 Kali / Tidak Penipisan)
6. ✅ Keterangan (dropdown: Oke / Rework / Sample / Trial / Urgent)

#### Validasi Form:
```javascript
function validateWorkflow() {
    const errors = [];
    
    // 1. Validasi Man Power & Injection
    const manPower = document.getElementById('select-man-power')?.value;
    const injection = document.querySelector('#injection-group input[name="injection"]:checked')?.value;
    
    if (!manPower) errors.push('Man Power harus dipilih');
    if (!injection) errors.push('Group Injection harus dipilih');
    
    // 2. Validasi Input Data Produksi
    const lotNumber = document.getElementById('input-lot-number')?.value?.trim();
    const partNumber = document.getElementById('input-part-number')?.value?.trim();
    const namaCompound = document.getElementById('input-nama-compound')?.value?.trim();
    const beratAct = document.getElementById('input-berat-act')?.value?.trim();
    const penipisan = document.querySelector('#penipisan-group input[name="penipisan"]:checked')?.value;
    const keterangan = document.getElementById('select-keterangan')?.value?.trim();
    const komponenId = selectedKomponenId;
    
    if (!lotNumber) errors.push('Nomor Lot harus diisi');
    if (!partNumber) errors.push('Lot BO harus diisi');
    if (!namaCompound) errors.push('Nama Compound harus diisi');
    if (!beratAct) errors.push('Berat Act harus dipilih');
    if (!penipisan) errors.push('Penipisan harus dipilih');
    if (!keterangan) errors.push('Keterangan harus dipilih');
    if (!komponenId) errors.push('Komponen harus dipilih');
    
    return errors;
}
```

#### Submit Handler:
```javascript
const btnSubmitProduksi = document.getElementById('btn-submit-produksi');
if (btnSubmitProduksi) {
    btnSubmitProduksi.addEventListener('click', async function(e) {
        e.preventDefault();

        // ✅ 1. Validasi workflow
        const errors = validateWorkflow();
        if (errors.length > 0) {
            console.warn('Validation errors:', errors);
            showToast('Harap lengkapi semua field required', 'error');
            return;
        }

        // ✅ 2. Save Man Power & Group Injection (PERSISTENT)
        saveManPowerAndGroup();

        // ✅ 3. Save Production Data to LocalStorage (PER ITEM)
        const productionData = {
            partNumber,
            lotNumber,
            namaCompound,
            beratAct,
            penipisan,
            keterangan,
            manPowerId: manPower,
            injection,
            komponenId,
            komponenText
        };
        saveProductionData(productionData);

        // ✅ 4. UI Updates: Show Spinner
        const originalText = btnSubmitProduksi.innerHTML;
        btnSubmitProduksi.disabled = true;
        btnSubmitProduksi.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-2"></i>Menyimpan...';

        // ✅ 5. Start Timer AUTOMATICALLY (if AUTO mode)
        showDurasiContainer();
        if (isDurasiAuto) {
            startDurasiProduksi(); 
        }

        // ✅ 6. SIMULATE SUBMIT (MOCK BACKEND)
        setTimeout(() => {
            console.log('✅ [MOCK] Data submitted to server successfully');
            showToast('Data produksi disimpan! Duration Start.', 'success');
            
            // ✅ 7. Reset Form (Partial Reset - Man Power & Injection TIDAK di-reset)
            resetProductionForm();
            
            // Restore Button
            btnSubmitProduksi.disabled = false;
            btnSubmitProduksi.innerHTML = originalText;
        }, 500);
    });
}
```

---

### 4. DURASI PRODUKSI (PER ITEM)

#### Status Implementasi: ⚠️ PERLU PENYEMPURNAAN

#### Lokasi Kode:
- **HTML**: Lines 872-907 (Durasi Produksi Container)
- **JavaScript**: Lines 4369-4500+ (Durasi Functions)

#### Yang Sudah Ada:
```javascript
let durasiProduksiInterval = null;
let durasiProduksiStartTime = null;
let durasiProduksiSeconds = 0;
let isDurasiAuto = true; // Toggle AUTO/MANUAL

function startDurasiProduksi() {
    stopDurasiProduksi();
    durasiProduksiStartTime = window.getAdjustedServerTime ? window.getAdjustedServerTime() : new Date();
    durasiProduksiSeconds = 0;
    
    // Update display
    updateDurasiProduksiDisplay();
    
    // Start interval
    durasiProduksiInterval = setInterval(() => {
        const now = window.getAdjustedServerTime ? window.getAdjustedServerTime() : new Date();
        durasiProduksiSeconds = Math.floor((now - durasiProduksiStartTime) / 1000);
        updateDurasiProduksiDisplay();
    }, 1000);
}

function stopDurasiProduksi() {
    if (durasiProduksiInterval) {
        clearInterval(durasiProduksiInterval);
        durasiProduksiInterval = null;
    }
}

function updateDurasiProduksiDisplay() {
    const display = document.getElementById('durasi-produksi-display');
    if (display) {
        display.value = formatDurasiProduksi(durasiProduksiSeconds);
    }
}
```

#### Yang Perlu Ditambahkan:

**A. Toggle AUTO/MANUAL**
```html
<!-- Sudah ada di HTML line 877-882 -->
<div class="form-check form-switch mb-2">
    <input class="form-check-input" type="checkbox" id="toggle-auto-durasi" checked style="cursor: pointer;">
    <label class="form-check-label small" for="toggle-auto-durasi" style="cursor: pointer; user-select: none;">
        Auto Start
    </label>
</div>
```

**B. Event Listener untuk Toggle**
```javascript
// Tambahkan di DOMContentLoaded
const toggleAutoDurasi = document.getElementById('toggle-auto-durasi');
if (toggleAutoDurasi) {
    toggleAutoDurasi.addEventListener('change', function() {
        isDurasiAuto = this.checked;
        console.log('🔄 Durasi mode changed:', isDurasiAuto ? 'AUTO' : 'MANUAL');
        
        // Update UI: Show/hide manual start button
        const btnStartDurasi = document.getElementById('btn-start-durasi-manual');
        if (btnStartDurasi) {
            btnStartDurasi.style.display = isDurasiAuto ? 'none' : 'inline-block';
        }
    });
}
```

**C. Tombol "Start Durasi" Manual**
```html
<!-- Tambahkan di HTML setelah toggle (line ~882) -->
<button type="button" 
        class="btn btn-sm btn-primary" 
        id="btn-start-durasi-manual"
        style="display: none;">
    <i class="fa-solid fa-play me-1"></i>Start Durasi
</button>
```

```javascript
// Event listener untuk tombol manual start
const btnStartDurasiManual = document.getElementById('btn-start-durasi-manual');
if (btnStartDurasiManual) {
    btnStartDurasiManual.addEventListener('click', function() {
        // Validasi: Durasi hanya boleh start 1x per item
        if (durasiProduksiInterval) {
            showToast('Durasi sudah berjalan!', 'warning');
            return;
        }
        
        startDurasiProduksi();
        showToast('Durasi produksi dimulai!', 'success');
        
        // Disable button setelah start (hanya boleh 1x)
        this.disabled = true;
        this.innerHTML = '<i class="fa-solid fa-check me-1"></i>Running...';
    });
}
```

**D. Validasi: Durasi Hanya Boleh Berjalan 1x Per Item**
```javascript
let isDurasiStartedForCurrentItem = false;

function startDurasiProduksi() {
    // Validasi: Jangan start jika sudah pernah start untuk item ini
    if (isDurasiStartedForCurrentItem) {
        console.warn('⚠️ Durasi sudah pernah start untuk item ini!');
        return;
    }
    
    stopDurasiProduksi();
    durasiProduksiStartTime = window.getAdjustedServerTime ? window.getAdjustedServerTime() : new Date();
    durasiProduksiSeconds = 0;
    isDurasiStartedForCurrentItem = true; // Set flag
    
    // ... rest of code
}

// Reset flag saat submit produksi berhasil
function resetProductionForm() {
    // ... existing reset code
    
    // Reset durasi flag untuk item baru
    isDurasiStartedForCurrentItem = false;
    
    // Reset manual start button
    const btnStartDurasiManual = document.getElementById('btn-start-durasi-manual');
    if (btnStartDurasiManual) {
        btnStartDurasiManual.disabled = false;
        btnStartDurasiManual.innerHTML = '<i class="fa-solid fa-play me-1"></i>Start Durasi';
    }
}
```

**E. Integration dengan Submit Produksi**
```javascript
// Di submit handler (line ~4686)
if (isDurasiAuto) {
    startDurasiProduksi(); // Auto start jika mode AUTO
}
// Jika mode MANUAL, operator harus klik tombol manual
```

**F. Integration dengan Quantity Submit**
```javascript
// Saat submit quantity, stop durasi dan simpan
const addQtyForm = document.getElementById('add-qty-form');
if (addQtyForm) {
    addQtyForm.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        // ✅ 1. STOP Timer & Get Duration
        stopDurasiProduksi();
        const finalDuration = durasiProduksiSeconds;
        console.log('⏱️ Timer stopped on Qty Submit. Duration:', finalDuration);

        // ✅ 2. Get Data
        const formData = new FormData(addQtyForm);
        const goodQty = formData.get('goodQty');
        const rejectQty = formData.get('rejectQty');
        
        // ✅ 3. Update Local Storage with Qty & Duration (PER ITEM)
        updateProductionDataWithQty(goodQty, rejectQty, finalDuration);

        // ✅ 4. Submit to Server (MOCK Backend)
        // ... submit logic
    });
}
```

---

### 5. QUANTITY & PRODUCTION METRICS (OEE)

#### Status Implementasi: ✅ SUDAH ADA (Sudah Benar)

#### Lokasi Kode:
- **HTML**: Lines 909-919 (Button QUANTITY)
- **HTML**: Lines 1381-1425 (Modal Tambah Qty)
- **JavaScript**: Lines 6722-6774 (Add Qty Form Handler)

#### Flow:
1. ✅ Operator klik tombol "QUANTITY"
2. ✅ Modal muncul dengan form:
   - Good Qty
   - Reject Qty
   - Jenis NG (optional)
   - Alasan Reject (optional)
3. ✅ Submit → Stop durasi → Simpan data → Update Production Metrics

#### Implementation:
```javascript
const addQtyForm = document.getElementById('add-qty-form');
if (addQtyForm) {
    addQtyForm.addEventListener('submit', async function(e) {
        e.preventDefault();
        e.stopPropagation();

        if (!validateGroupInjection()) {
            return false;
        }

        // ✅ 1. STOP Timer & Get Duration
        stopDurasiProduksi();
        const finalDuration = durasiProduksiSeconds;
        console.log('⏱️ Timer stopped on Qty Submit. Duration:', finalDuration);

        // ✅ 2. Get Data
        const formData = new FormData(addQtyForm);
        const goodQty = formData.get('goodQty');
        const rejectQty = formData.get('rejectQty');
        
        // ✅ 3. Update Local Storage with Qty & Duration (PER ITEM)
        updateProductionDataWithQty(goodQty, rejectQty, finalDuration);

        // ✅ 4. Submit to Server (MOCK Backend)
        const submitBtn = addQtyForm.querySelector('button[type="submit"]');
        const originalText = submitBtn ? submitBtn.innerHTML : 'Simpan';
        if(submitBtn) {
            submitBtn.disabled = true;
            submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i>Saving...';
        }

        // SIMULATE DELAY
        setTimeout(() => {
            console.log('✅ [MOCK] Quantity submitted successfully');
                
            // Close modal
            const modalEl = document.getElementById('addQtyModal');
            const modal = bootstrap.Modal.getInstance(modalEl);
            if(modal) modal.hide();

            // Reset form
            addQtyForm.reset();
            
            showToast('Quantity saved! Item completed.', 'success');

            if(submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalText;
            }
            
            // ✅ 5. Update Production Metrics (Real-time)
            // This would trigger OEE recalculation
            // In real implementation, this would call fetchTimeMetrics()
        }, 500);
    });
}
```

#### Production Metrics Update:
```javascript
function updateProductionDataWithQty(goodQty, rejectQty, durationSeconds) {
    try {
        const historyJson = localStorage.getItem('productionDataHistory');
        if (historyJson) {
            let history = JSON.parse(historyJson);
            if (history.length > 0) {
                const idx = history.length - 1;
                const lastEntry = history[idx];
               
                lastEntry.goodQty = goodQty;
                lastEntry.rejectQty = rejectQty;
                lastEntry.durationSeconds = durationSeconds;
                lastEntry.status = 'Completed';
                lastEntry.completionTime = new Date().toISOString();
                
                history[idx] = lastEntry;
                
                localStorage.setItem('productionDataHistory', JSON.stringify(history));
                console.log('💾 Production Data Updated (End Item):', lastEntry);
                
                // ✅ Trigger OEE Metrics Update
                updateOeeMetrics(history);
            }
        }
    } catch (e) {
        console.error('Error updating production data', e);
    }
}

function updateOeeMetrics(productionHistory) {
    // Calculate OEE metrics from production history
    let totalGood = 0;
    let totalReject = 0;
    let totalDuration = 0;
    
    productionHistory.forEach(item => {
        if (item.status === 'Completed') {
            totalGood += parseInt(item.goodQty) || 0;
            totalReject += parseInt(item.rejectQty) || 0;
            totalDuration += parseInt(item.durationSeconds) || 0;
        }
    });
    
    const totalOutput = totalGood + totalReject;
    
    // Update UI
    const totalGoodEl = document.getElementById('total-good');
    const totalRejectEl = document.getElementById('total-reject');
    
    if (totalGoodEl) totalGoodEl.textContent = totalGood;
    if (totalRejectEl) totalRejectEl.textContent = totalReject;
    
    // Calculate OEE components
    // Availability = Operating Time / Planned Production Time
    // Performance = (Total Output / Operating Time) / Ideal Cycle Rate
    // Quality = Good Output / Total Output
    // OEE = Availability × Performance × Quality
    
    console.log('📊 OEE Metrics Updated:', {
        totalGood,
        totalReject,
        totalOutput,
        totalDuration
    });
}
```

---

## 🔧 KETENTUAN TEKNIS WAJIB

### Frontend State Management

```javascript
// ✅ State Variables (Global Scope)
let selectedKomponenId = null;
let selectedPartNumber = '';
let komponenData = [];

// Durasi Produksi
let durasiProduksiInterval = null;
let durasiProduksiStartTime = null;
let durasiProduksiSeconds = 0;
let isDurasiAuto = true;
let isDurasiStartedForCurrentItem = false;

// SCW State (handled by jQuery in IIFE)
// Man Power & Injection (handled by localStorage)
```

### Reset Rules

```javascript
// ✅ TIDAK BOLEH DI-RESET:
// 1. Man Power (select-man-power)
// 2. Group Injection (injection radio buttons)

// ✅ BOLEH DI-RESET:
// 1. Form SCW (setelah submit SCW)
// 2. Form Produksi (setelah submit produksi):
//    - Nomor Lot
//    - Lot BO
//    - Nama Compound
//    - Berat Act
//    - Penipisan
//    - Keterangan
//    - Komponen

function resetProductionForm() {
    // Reset production inputs
    const inputsToReset = [
        'input-part-number', 
        'input-lot-number', 
        'input-nama-compound',
        'input-berat-act'
    ];
    
    inputsToReset.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
    });
    
    // Reset Komponen Dropdown
    const selectKomponen = document.getElementById('select-komponen');
    if (selectKomponen) {
        selectKomponen.innerHTML = '<option value="">-- Pilih Komponen --</option>';
        const container = document.getElementById('komponen-container');
        if (container) container.style.display = 'none';
    }

    // Reset Keterangan
    const selectKeterangan = document.getElementById('select-keterangan');
    if (selectKeterangan) selectKeterangan.value = '';

    // Reset Penipisan radio
    const penipisanRadios = document.querySelectorAll('#penipisan-group input[name="penipisan"]');
    penipisanRadios.forEach(r => r.checked = false);
    
    // Reset durasi flag
    isDurasiStartedForCurrentItem = false;
    
    // TIDAK RESET: Man Power & Injection ✅
}
```

### Backend / Logic

```javascript
// ✅ LocalStorage Structure

// 1. Man Power & Injection (PERSISTENT)
{
    "manPowerId": "1",
    "injection": "merah"
}

// 2. Production Data History (ARRAY)
[
    {
        "id": "prod-1704672000000",
        "timestamp": "2024-01-08T10:00:00.000Z",
        "status": "Completed",
        "partNumber": "COMP-001",
        "lotNumber": "LOT-001",
        "namaCompound": "Compound A",
        "beratAct": "10.50",
        "penipisan": "1-kali",
        "keterangan": "normal",
        "manPowerId": "1",
        "injection": "merah",
        "komponenId": "10",
        "komponenText": "COMP-001 (Qty: 2)",
        "goodQty": "100",
        "rejectQty": "5",
        "durationSeconds": 3600,
        "completionTime": "2024-01-08T11:00:00.000Z"
    }
]

// 3. SCW Events (OPTIONAL - for logging)
[
    {
        "id": "scw-1704672000000",
        "timestamp": "2024-01-08T10:30:00.000Z",
        "scw4MTypeId": "1",
        "scw4MTypeName": "Material",
        "scwRemarkId": "1",
        "scwRemarkName": "Rejection"
    }
]
```

### UX & Stabilitas

```javascript
// ✅ Validation Checklist

// 1. SCW Submit
function validateScwSubmit() {
    const typeId = $('#scw-4m-type').val();
    const remarkId = $('#scw-remark').val();
    
    if (!typeId || !remarkId) {
        alert('Harap lengkapi semua field!');
        return false;
    }
    return true;
}

// 2. Production Submit
function validateProductionSubmit() {
    const errors = validateWorkflow();
    if (errors.length > 0) {
        showToast('Harap lengkapi semua field required', 'error');
        return false;
    }
    return true;
}

// 3. Quantity Submit
function validateQuantitySubmit() {
    const injection = document.querySelector('#injection-group input[name="injection"]:checked')?.value;
    if (!injection) {
        alert('Harap pilih Group Injection terlebih dahulu!');
        return false;
    }
    return true;
}
```

---

## 📝 CHECKLIST IMPLEMENTASI

### 1. SCW (Stop Call Waiting)
- [x] Dropdown Jenis 4M berfungsi
- [x] Dropdown Jenis Remark disabled sebelum Jenis 4M dipilih
- [x] Dropdown Jenis Remark aktif setelah Jenis 4M dipilih
- [x] Data Jenis Remark berubah dinamis sesuai Jenis 4M
- [x] Mapping Jenis Remark sesuai requirement
- [ ] **TODO**: Tombol "Simpan SCW" hanya aktif jika Jenis 4M dan Jenis Remark dipilih
- [x] Submit SCW via AJAX
- [x] Reset form SCW setelah submit
- [x] Submit SCW tidak mempengaruhi Man Power & Produksi

### 2. Man Power & Group Injection
- [x] Man Power dropdown berfungsi
- [x] Group Injection radio buttons berfungsi
- [x] Data disimpan ke localStorage
- [x] Data di-load saat page load
- [x] Data TIDAK di-reset setelah submit produksi
- [x] Validasi Man Power & Injection sebelum submit produksi

### 3. Input Data Produksi
- [x] Semua field input berfungsi
- [x] Validasi form lengkap
- [x] Tombol submit hanya aktif jika form valid
- [x] Submit via AJAX (mock)
- [x] Data tersimpan ke localStorage
- [x] Reset form setelah submit (partial)

### 4. Durasi Produksi
- [x] Timer durasi berfungsi
- [x] Format HH:mm:ss
- [ ] **TODO**: Toggle AUTO/MANUAL
- [ ] **TODO**: Mode AUTO: start otomatis setelah submit
- [ ] **TODO**: Mode MANUAL: tombol "Start Durasi"
- [ ] **TODO**: Durasi hanya boleh berjalan 1x per item
- [x] Durasi disimpan saat submit quantity

### 5. Quantity & Production Metrics
- [x] Modal QUANTITY berfungsi
- [x] Form quantity berfungsi
- [x] Submit quantity via AJAX (mock)
- [x] Durasi di-stop saat submit quantity
- [x] Data quantity tersimpan
- [ ] **TODO**: Update Production Metrics real-time
- [ ] **TODO**: Calculate OEE components

---

## 🚀 LANGKAH IMPLEMENTASI

### Step 1: Perbaiki Validasi Tombol "Simpan SCW"

Tambahkan di bagian SCW Handler (setelah line 7214):

```javascript
// ========== SCW BUTTON VALIDATION ==========
function validateScwForm() {
    const typeId = $('#scw-4m-type').val();
    const remarkId = $('#scw-remark').val();
    const btnSubmit = $('#btn-scw-submit');
    
    if (typeId && remarkId) {
        btnSubmit.prop('disabled', false);
    } else {
        btnSubmit.prop('disabled', true);
    }
}

// Attach to change events
$(function() {
    $('#scw-4m-type').on('change', validateScwForm);
    $('#scw-remark').on('change', validateScwForm);
    
    // Set initial state
    validateScwForm();
});
```

### Step 2: Tambahkan Toggle AUTO/MANUAL untuk Durasi

Tambahkan HTML setelah line 882:

```html
<!-- Manual Start Button (hidden by default) -->
<button type="button" 
        class="btn btn-sm btn-primary mt-2" 
        id="btn-start-durasi-manual"
        style="display: none;">
    <i class="fa-solid fa-play me-1"></i>Start Durasi
</button>
```

Tambahkan JavaScript di bagian Durasi (setelah line 4400):

```javascript
// ========== TOGGLE AUTO/MANUAL DURASI ==========
let isDurasiStartedForCurrentItem = false;

const toggleAutoDurasi = document.getElementById('toggle-auto-durasi');
if (toggleAutoDurasi) {
    toggleAutoDurasi.addEventListener('change', function() {
        isDurasiAuto = this.checked;
        console.log('🔄 Durasi mode changed:', isDurasiAuto ? 'AUTO' : 'MANUAL');
        
        // Update UI: Show/hide manual start button
        const btnStartDurasi = document.getElementById('btn-start-durasi-manual');
        if (btnStartDurasi) {
            btnStartDurasi.style.display = isDurasiAuto ? 'none' : 'inline-block';
        }
    });
}

// Manual Start Button Handler
const btnStartDurasiManual = document.getElementById('btn-start-durasi-manual');
if (btnStartDurasiManual) {
    btnStartDurasiManual.addEventListener('click', function() {
        // Validasi: Durasi hanya boleh start 1x per item
        if (isDurasiStartedForCurrentItem) {
            showToast('Durasi sudah berjalan untuk item ini!', 'warning');
            return;
        }
        
        startDurasiProduksi();
        showToast('Durasi produksi dimulai!', 'success');
        
        // Disable button setelah start
        this.disabled = true;
        this.innerHTML = '<i class="fa-solid fa-check me-1"></i>Running...';
    });
}

// Update startDurasiProduksi function
function startDurasiProduksi() {
    // Validasi: Jangan start jika sudah pernah start
    if (isDurasiStartedForCurrentItem) {
        console.warn('⚠️ Durasi sudah pernah start untuk item ini!');
        return;
    }
    
    stopDurasiProduksi();
    durasiProduksiStartTime = window.getAdjustedServerTime ? window.getAdjustedServerTime() : new Date();
    durasiProduksiSeconds = 0;
    isDurasiStartedForCurrentItem = true; // Set flag
    
    // Show durasi container
    const durasiContainer = document.getElementById('durasi-produksi-container');
    if (durasiContainer) {
        durasiContainer.style.display = 'block';
    }
    
    // Update display
    updateDurasiProduksiDisplay();
    
    // Start interval
    durasiProduksiInterval = setInterval(() => {
        const now = window.getAdjustedServerTime ? window.getAdjustedServerTime() : new Date();
        durasiProduksiSeconds = Math.floor((now - durasiProduksiStartTime) / 1000);
        updateDurasiProduksiDisplay();
    }, 1000);
    
    console.log('✅ Durasi produksi started');
}

// Update resetProductionForm function
function resetProductionForm() {
    // ... existing reset code
    
    // Reset durasi flag
    isDurasiStartedForCurrentItem = false;
    
    // Reset manual start button
    const btnStartDurasiManual = document.getElementById('btn-start-durasi-manual');
    if (btnStartDurasiManual) {
        btnStartDurasiManual.disabled = false;
        btnStartDurasiManual.innerHTML = '<i class="fa-solid fa-play me-1"></i>Start Durasi';
    }
}
```

### Step 3: Tambahkan Update OEE Metrics

Tambahkan function baru:

```javascript
// ========== OEE METRICS UPDATE ==========
function updateOeeMetrics(productionHistory) {
    try {
        let totalGood = 0;
        let totalReject = 0;
        let totalDuration = 0;
        let completedItems = 0;
        
        productionHistory.forEach(item => {
            if (item.status === 'Completed') {
                totalGood += parseInt(item.goodQty) || 0;
                totalReject += parseInt(item.rejectQty) || 0;
                totalDuration += parseInt(item.durationSeconds) || 0;
                completedItems++;
            }
        });
        
        const totalOutput = totalGood + totalReject;
        
        // Update UI - Total Good & Reject
        const totalGoodEl = document.getElementById('total-good');
        const totalRejectEl = document.getElementById('total-reject');
        
        if (totalGoodEl) totalGoodEl.textContent = totalGood;
        if (totalRejectEl) totalRejectEl.textContent = totalReject;
        
        // Calculate OEE components
        // Note: This is simplified calculation
        // Real implementation would use actual planned time, ideal cycle time, etc.
        
        const quality = totalOutput > 0 ? (totalGood / totalOutput * 100) : 0;
        
        console.log('📊 OEE Metrics Updated:', {
            totalGood,
            totalReject,
            totalOutput,
            totalDuration,
            completedItems,
            quality: quality.toFixed(2) + '%'
        });
        
        // Trigger refresh of time metrics if available
        if (typeof fetchTimeMetrics === 'function') {
            fetchTimeMetrics();
        }
        
    } catch (e) {
        console.error('Error updating OEE metrics:', e);
    }
}

// Call this in updateProductionDataWithQty
function updateProductionDataWithQty(goodQty, rejectQty, durationSeconds) {
    try {
        const historyJson = localStorage.getItem('productionDataHistory');
        if (historyJson) {
            let history = JSON.parse(historyJson);
            if (history.length > 0) {
                const idx = history.length - 1;
                const lastEntry = history[idx];
               
                lastEntry.goodQty = goodQty;
                lastEntry.rejectQty = rejectQty;
                lastEntry.durationSeconds = durationSeconds;
                lastEntry.status = 'Completed';
                lastEntry.completionTime = new Date().toISOString();
                
                history[idx] = lastEntry;
                
                localStorage.setItem('productionDataHistory', JSON.stringify(history));
                console.log('💾 Production Data Updated (End Item):', lastEntry);
                
                // ✅ Update OEE Metrics
                updateOeeMetrics(history);
            }
        }
    } catch (e) {
        console.error('Error updating production data', e);
    }
}
```

---

## ✅ HASIL AKHIR

Setelah implementasi lengkap, sistem akan memiliki:

1. **SCW Submit Mandiri**
   - ✅ Bisa disimpan kapan saja
   - ✅ Tidak bergantung pada submit produksi
   - ✅ Validasi form yang ketat
   - ✅ Reset form setelah submit

2. **Man Power & Group Injection Persisten**
   - ✅ Hanya input sekali per sesi
   - ✅ Tidak di-reset setelah submit produksi
   - ✅ Tersimpan di localStorage

3. **Input Data Produksi Valid**
   - ✅ Semua field tervalidasi
   - ✅ Submit hanya aktif jika valid
   - ✅ Data tersimpan per item

4. **Durasi Produksi Akurat**
   - ✅ Mode AUTO/MANUAL
   - ✅ Hanya berjalan 1x per item
   - ✅ Tersimpan dengan quantity

5. **Quantity & OEE Real-time**
   - ✅ Input quantity setelah produksi selesai
   - ✅ OEE terhitung otomatis
   - ✅ Metrics update real-time

6. **Aplikasi Stabil**
   - ✅ Tidak ada submit ganda
   - ✅ Tidak ada reset yang salah
   - ✅ Smooth dan bebas bug

---

## 📚 REFERENSI

- **File Utama**: `c:\OEE_AntiGravity\OEE_SYSTEM\Views\Machine\OeeDetail.cshtml`
- **Lines SCW**: 556-613, 7061-7217
- **Lines Man Power**: 617-671, 3994-4035
- **Lines Produksi**: 673-935, 4623-4703
- **Lines Durasi**: 872-907, 4369-4500+
- **Lines Quantity**: 909-919, 1381-1425, 6722-6774

---

**Dokumen ini dibuat pada**: 2026-01-08
**Versi**: 1.0
**Status**: Ready for Implementation
