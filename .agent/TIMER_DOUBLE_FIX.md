# Perbaikan Timer Ganda - Final Fix

## Tanggal: 2026-01-15 14:17

## Masalah Utama
**Timer Ganda di "Current Job"**: Saat klik Running, ada DUA timer yang berjalan bersamaan:
1. Timer dari variabel lokal (`runningTimerInterval`, `sinceLastChangeInterval`)
2. Timer dari variabel global (`window.runningTimerInterval`, `window.sinceLastChangeInterval`)

Ini menyebabkan durasi "bentrok" dan tidak akurat.

## Akar Masalah
Deklarasi variabel lokal di baris ~5043-5046:
```javascript
let runningStartTimeUtc = null;
let runningTimerInterval = null;
let lastChangeTimestamp = null;
let sinceLastChangeInterval = null;
```

Variabel lokal ini **berbeda** dengan variabel global `window.*`, sehingga:
- `runningTimerInterval` ≠ `window.runningTimerInterval`
- `sinceLastChangeInterval` ≠ `window.sinceLastChangeInterval`

Ketika fungsi `startRunningTimer()` atau `updateSinceLastChange()` dipanggil, mereka membuat interval baru di variabel lokal, tetapi fungsi `stopAllTimers()` hanya membersihkan variabel global. Hasilnya: **timer lokal tetap berjalan di background**.

## Solusi yang Diterapkan

### 1. Hapus Deklarasi Variabel Lokal
```javascript
// SEBELUM (SALAH):
let runningStartTimeUtc = null;
let runningTimerInterval = null;
let lastChangeTimestamp = null;
let sinceLastChangeInterval = null;

// SESUDAH (BENAR):
// ✅ CRITICAL: Semua variabel timer HARUS menggunakan window. prefix
// window.runningStartTimeUtc - Timestamp UTC saat Running dimulai
// window.runningTimerInterval - Interval ID untuk timer Running
// window.lastChangeTimestamp - Timestamp terakhir status berubah
// window.sinceLastChangeInterval - Interval ID untuk timer non-running
```

### 2. Update Semua Fungsi Timer Menggunakan `window.` Prefix

#### `startRunningTimer()`
```javascript
// SEBELUM:
runningStartTimeUtc = new Date(startTimeUtc);
runningTimerInterval = setInterval(() => { ... }, 1000);

// SESUDAH:
window.runningStartTimeUtc = new Date(startTimeUtc);
window.runningTimerInterval = setInterval(() => { ... }, 1000);
```

#### `stopRunningTimer()`
```javascript
// SEBELUM:
if (runningTimerInterval) {
    clearInterval(runningTimerInterval);
    runningTimerInterval = null;
}

// SESUDAH:
if (window.runningTimerInterval) {
    clearInterval(window.runningTimerInterval);
    window.runningTimerInterval = null;
}
```

#### `updateSinceLastChange()`
```javascript
// SEBELUM (untuk non-running states):
sinceLastChangeInterval = setInterval(() => { ... }, 1000);

// SESUDAH:
window.sinceLastChangeInterval = setInterval(() => { ... }, 1000);
```

### 3. Konsistensi di Seluruh File
Semua referensi ke timer variables sekarang menggunakan `window.` prefix:
- ✅ `window.runningStartTimeUtc`
- ✅ `window.runningTimerInterval`
- ✅ `window.lastChangeTimestamp`
- ✅ `window.sinceLastChangeInterval`

## Alur Kerja yang Benar

### Saat Klik "Running"
1. `handleRunningClickDirect()` dipanggil
2. `window.stopAllTimers()` → Membersihkan SEMUA timer yang ada
3. Set `window.runningStartTimeUtc = nowAdjusted`
4. Start `window.runningTimerInterval` → **HANYA 1 timer berjalan**
5. Timer update display setiap 1 detik

### Saat Klik "Rest Break"
1. `handleRestClickFinal()` dipanggil
2. `window.stopAllTimers()` → Stop timer Running
3. POST ke server `/Operator/Rest`
4. `window.refreshAllData()` → Fetch data terbaru
5. `updateSinceLastChange(seconds, isRunning=false)` → Start timer Rest Break
6. `window.sinceLastChangeInterval` → **HANYA 1 timer berjalan**

### Saat Klik "Line Stop"
1. `submitLineStopFinal()` dipanggil
2. `window.stopAllTimers()` → Stop semua timer
3. POST ke server `/Operator/LineStop`
4. Display reset ke `00:00:00`
5. `window.refreshAllData()` → Start timer baru untuk downtime

## Hasil yang Diharapkan
✅ **HANYA 1 timer aktif** pada satu waktu di "Current Job"
✅ Saat Running → Timer Running berjalan
✅ Saat Rest Break → Timer Running STOP, Timer Rest Break START
✅ Saat Line Stop → Semua timer STOP, Timer Downtime START
✅ Tidak ada timer "hantu" di background
✅ Durasi akurat dan tidak bentrok

## File yang Dimodifikasi
- `Views/Machine/OeeDetail.cshtml`

## Testing Checklist
- [ ] Klik "Running" → Hanya durasi Running yang bertambah
- [ ] Klik "Rest Break" → Durasi Running stop, durasi Rest Break mulai
- [ ] Klik "Line Stop" → Semua durasi stop, durasi Downtime mulai
- [ ] Refresh halaman → Timer resume dengan benar
- [ ] Buka Console → Tidak ada interval ID duplikat
- [ ] Check `window.runningTimerInterval` dan `window.sinceLastChangeInterval` → Hanya salah satu yang ada nilai (bukan null)
