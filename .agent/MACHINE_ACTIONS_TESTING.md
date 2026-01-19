# Testing Machine Actions - OEE Detail Page

## Masalah yang Diperbaiki
Tombol-tombol Machine Actions (Running, Rest Break, Line Stop, No Loading) tidak bisa diklik karena ada bug di JavaScript `oee-logic.js`.

### Root Cause
Fungsi `handleAction` menggunakan variabel `event.currentTarget` tanpa parameter `event` yang didefinisikan dengan benar, menyebabkan error JavaScript dan tombol tidak responsif.

## Perbaikan yang Dilakukan

### 1. **File: `wwwroot/js/oee-logic.js`**

#### a. Perbaikan fungsi `handleAction` (Baris 231-270)
- **Sebelum**: Fungsi menggunakan `event.currentTarget` tanpa parameter event
- **Sesudah**: 
  - Menambahkan parameter `event = null` 
  - Menambahkan fallback logic untuk mendapatkan button reference
  - Menambahkan antiforgery token ke FormData

```javascript
handleAction: async function (action, data = {}, event = null) {
    // Get button from event or fallback to selector
    let btn;
    if (event && event.currentTarget) {
        btn = $(event.currentTarget);
    } else {
        // Fallback: find button by action type
        const btnMap = {
            'Start': '#btn-running',
            'Rest': '#btn-rest',
            'LineStop': '#btn-line-stop',
            'NoLoading': '#btn-no-loading'
        };
        btn = $(btnMap[action] || '#btn-running');
    }
    // ... rest of the code
}
```

#### b. Perbaikan event listener untuk tombol Running (Baris 484)
- **Sebelum**: `$('#btn-running').on('click', (e) => this.handleAction('Start'));`
- **Sesudah**: `$('#btn-running').on('click', (e) => this.handleAction('Start', {}, e));`

### 2. **File: `Controllers/OperatorController.cs`**
- Menambahkan `using OeeSystem.Services;` untuk mengatasi error CS0246

### 3. **File: `Program.cs`**
- Menghapus duplikasi registrasi `IOeeService`

## Cara Testing

### Prerequisites
1. Pastikan aplikasi sudah running: `dotnet run`
2. Buka browser dan akses: `http://localhost:6001`
3. Login sebagai operator (jika ada authentication)
4. Navigasi ke halaman OEE Detail untuk salah satu mesin

### Test Case 1: Tombol Running
**Langkah:**
1. Pilih **Man Power** dari dropdown
2. Pilih **Group Injection** (Merah atau Biru)
3. Klik tombol **Running** (hijau dengan icon play)

**Expected Result:**
- Tombol menampilkan spinner loading
- Status mesin berubah menjadi "AKTIF"
- Badge status di navbar berubah menjadi hijau
- Timer "Durasi sejak status terakhir" mulai berjalan
- Production timer mulai berjalan
- Tombol Running menjadi disabled (tidak bisa diklik lagi)
- Tombol lain (Rest Break, Line Stop, No Loading) tetap enabled

### Test Case 2: Tombol Rest Break
**Langkah:**
1. Pastikan mesin dalam status Running
2. Klik tombol **Rest Break** (kuning dengan icon pause)

**Expected Result:**
- Tombol menampilkan spinner loading
- Status berubah menjadi "REST BREAK"
- Downtime description muncul di header
- Timer "Durasi sejak status terakhir" reset dan mulai dari 0
- Production timer berhenti
- Tombol Rest Break menjadi disabled
- Tombol Running kembali enabled

### Test Case 3: Tombol Line Stop
**Langkah:**
1. Klik tombol **Line Stop** (merah dengan icon stop)
2. Modal "Line Stop" muncul
3. Pilih alasan dari dropdown (contoh: "Machine Failure")
4. Klik **Submit Line Stop**

**Expected Result:**
- Modal tertutup
- Status berubah menjadi "LINE STOP"
- Downtime description menampilkan alasan yang dipilih
- Timer reset dan mulai menghitung durasi downtime
- Production timer berhenti
- Tombol Line Stop menjadi disabled

### Test Case 4: Tombol No Loading
**Langkah:**
1. Klik tombol **No Loading** (biru dengan icon hourglass)
2. Modal konfirmasi muncul
3. Klik **Ya, Set No Loading**

**Expected Result:**
- Modal tertutup
- Status berubah menjadi "NO LOADING"
- Current Job card menjadi opacity 25% dan tidak bisa diklik
- Timer reset dan mulai menghitung
- Tombol No Loading menjadi disabled

### Test Case 5: Validasi Input
**Langkah:**
1. **JANGAN** pilih Man Power atau Injection
2. Coba klik salah satu tombol action

**Expected Result:**
- Alert muncul: "⚠️ Harap pilih Man Power dan Group Injection terlebih dahulu!"
- Tidak ada request ke server
- Status mesin tidak berubah

### Test Case 6: Persistence
**Langkah:**
1. Pilih Man Power dan Injection
2. Klik Running
3. Refresh halaman (F5)

**Expected Result:**
- Man Power dan Injection yang dipilih tetap tersimpan (localStorage)
- Status mesin tetap "RUNNING"
- Timer melanjutkan dari waktu terakhir

## Debugging Tips

### Jika tombol masih tidak bisa diklik:

1. **Buka Browser Console** (F12 → Console tab)
   - Cek apakah ada error JavaScript
   - Cek apakah `window.OeeApp` sudah ter-initialize
   - Ketik: `window.OeeApp` dan tekan Enter, seharusnya menampilkan object

2. **Cek Network Tab** (F12 → Network tab)
   - Klik tombol Running
   - Lihat apakah ada request ke `/Operator/Start`
   - Cek response status (200 = OK, 400/500 = Error)

3. **Cek Element Inspector** (F12 → Elements tab)
   - Klik kanan pada tombol → Inspect
   - Cek apakah ada attribute `disabled` atau class `disabled`
   - Cek apakah ada CSS yang menghalangi klik (z-index, pointer-events)

4. **Cek jQuery**
   - Di console, ketik: `$('#btn-running').length`
   - Seharusnya return `1` (artinya element ditemukan)
   - Ketik: `$('#btn-running').prop('disabled')`
   - Seharusnya return `false` (artinya tidak disabled)

### Common Issues:

**Issue 1: "validateInputs() returns false"**
- **Cause**: Man Power atau Injection belum dipilih
- **Fix**: Pilih kedua input tersebut sebelum klik tombol

**Issue 2: "401 Unauthorized"**
- **Cause**: Antiforgery token tidak valid
- **Fix**: Refresh halaman untuk mendapatkan token baru

**Issue 3: "404 Not Found"**
- **Cause**: Route `/Operator/Start` tidak ditemukan
- **Fix**: Pastikan `OperatorController.cs` memiliki method `Start`

**Issue 4: "SignalR connection error"**
- **Cause**: SignalR hub tidak bisa connect
- **Fix**: Ini tidak mempengaruhi fungsi tombol, hanya real-time update yang tidak jalan

## Checklist Sebelum Testing
- [ ] Build berhasil tanpa error (`dotnet build`)
- [ ] Aplikasi running di port 6001/6002
- [ ] Database connection OK
- [ ] Browser console tidak ada error saat load halaman
- [ ] Man Power dropdown terisi dengan data
- [ ] Injection radio buttons berfungsi

## Expected Behavior Summary

| Action | Button State After | Status Display | Timer Behavior |
|--------|-------------------|----------------|----------------|
| **Running** | Running: Disabled<br>Others: Enabled | AKTIF (Green) | Machine timer: Running<br>Production timer: Running |
| **Rest Break** | Rest: Disabled<br>Others: Enabled | REST BREAK (Yellow) | Machine timer: Running<br>Production timer: Stopped |
| **Line Stop** | Line Stop: Disabled<br>Others: Enabled | LINE STOP (Red) | Machine timer: Running<br>Production timer: Stopped |
| **No Loading** | No Loading: Disabled<br>Others: Enabled | NO LOADING (Blue) | Machine timer: Running<br>Production timer: Stopped<br>Job card: Disabled |

## Notes
- Semua action memerlukan Man Power dan Injection dipilih terlebih dahulu
- Hanya satu status yang bisa aktif pada satu waktu
- Timer "Durasi sejak status terakhir" selalu berjalan selama ada job aktif
- Production timer hanya berjalan saat status RUNNING
