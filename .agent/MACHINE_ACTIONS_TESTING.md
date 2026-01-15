# MACHINE ACTIONS - TESTING CHECKLIST

## ✅ Perbaikan Yang Sudah Dilakukan

### Backend (OperatorController.cs)
- [x] **Start Method**: Enhanced dengan Man Power parameter, LastStatusChangeTime update, comprehensive SignalR broadcast
- [x] **StartDowntime Method**: Auto-end existing downtime, LastStatusChangeTime update, comprehensive broadcast
- [x] **NoLoading Method**: Auto-end existing downtime, LastStatusChangeTime update, comprehensive broadcast
- [x] **SignalR Broadcast**: Semua method broadcast data lengkap (LastStatusChangeTime, DowntimeDescription, RefreshOeeMetrics, dll)

### Frontend (OeeDetail.cshtml)
- [x] **Consolidated Script**: Created `machine-actions.js` dengan unified timer management
- [x] **Validation**: Added `validateMachineAction()` untuk cek Man Power & Injection
- [x] **Timer Management**: Unified `MachineTimer` object dengan reset(), start(), update()
- [x] **Success Toast**: Added success notifications untuk semua actions
- [x] **Standardized Naming**: handleRunningClick, handleRestClick, handleLineStopClick, handleNoLoadingClick
- [x] **Modals**: Added Line Stop dan No Loading modals
- [x] **Button Handlers**: Updated onclick handlers untuk semua buttons

### Controller (MachineController.cs)
- [x] **ViewBag.DowntimeReasons**: Added untuk populate modal Line Stop

---

## 📋 MANUAL TESTING CHECKLIST

### Pre-requisites
- [ ] Database seeded dengan:
  - [ ] Work Order (Status: InProgress)
  - [ ] Man Power data
  - [ ] Downtime Reasons (Planned & Unplanned)
  - [ ] Machine (Status: Aktif)
- [ ] Application running di port 6002
- [ ] Browser console open untuk monitoring

---

### TEST 1: Running Button

#### Scenario 1.1: Start Running (Tanpa Job Aktif)
**Steps**:
1. Navigate ke `/Machine/OeeDetail/M001`
2. Pastikan tidak ada job aktif
3. Pilih Man Power
4. Pilih Group Injection (Merah/Biru)
5. Click button "Running"

**Expected Results**:
- [ ] Button disabled dengan spinner "Processing..."
- [ ] Toast notification muncul: "✅ Machine Running dimulai"
- [ ] Timer "Durasi sejak status terakhir" reset ke 00:00:00
- [ ] Timer mulai berjalan detik per detik (00:00:01, 00:00:02, ...)
- [ ] Status badge berubah menjadi "AKTIF" (hijau)
- [ ] Downtime description hilang
- [ ] Button kembali enabled
- [ ] Console log: "✅ Timer started from: [timestamp]"
- [ ] Time Metrics update (Operating Time bertambah)
- [ ] Recent Downtime table tidak ada entry baru

**Database Check**:
```sql
SELECT * FROM JobRuns WHERE MachineId = 'M001' AND EndTime IS NULL;
-- Should have 1 row with LastStatusChangeTime = NOW()
```

#### Scenario 1.2: Start Running (Ada Downtime Aktif)
**Steps**:
1. Pastikan ada downtime aktif (Rest Break atau Line Stop)
2. Click button "Running"

**Expected Results**:
- [ ] Downtime aktif di-end (EndTime != NULL)
- [ ] DurationSeconds dihitung
- [ ] LastStatusChangeTime di-update
- [ ] Timer reset ke 00:00:00 dan mulai berjalan
- [ ] Status badge tetap "AKTIF"
- [ ] Downtime description hilang
- [ ] Toast: "✅ Machine Running dimulai"

**Database Check**:
```sql
SELECT * FROM DowntimeEvents WHERE EndTime IS NULL;
-- Should return 0 rows

SELECT TOP 1 * FROM DowntimeEvents ORDER BY EndTime DESC;
-- EndTime should be recent, DurationSeconds > 0
```

#### Scenario 1.3: Running Tanpa Man Power
**Steps**:
1. Clear Man Power selection
2. Click button "Running"

**Expected Results**:
- [ ] Alert muncul: "⚠️ Harap pilih Man Power dan Group Injection terlebih dahulu!"
- [ ] Request tidak dikirim ke server
- [ ] Timer tidak berubah

---

### TEST 2: Rest Break Button

#### Scenario 2.1: Start Rest Break
**Steps**:
1. Pastikan ada job aktif dan running
2. Pilih Man Power & Injection
3. Click button "Rest Break"

**Expected Results**:
- [ ] Button disabled dengan spinner
- [ ] Toast notification: "☕ Rest Break dimulai"
- [ ] Timer reset ke 00:00:00 dan mulai berjalan
- [ ] Status badge tetap "AKTIF"
- [ ] Downtime description muncul: "Rest Break"
- [ ] Button kembali enabled
- [ ] Time Metrics update (Downtime Total bertambah, Rest Break Time bertambah)
- [ ] Recent Downtime table ada entry baru dengan status "Active"

**Database Check**:
```sql
SELECT * FROM DowntimeEvents WHERE EndTime IS NULL;
-- Should have 1 row with Reason = "Rest Break"

SELECT TOP 1 * FROM JobRuns WHERE MachineId = 'M001' AND EndTime IS NULL;
-- LastStatusChangeTime should be updated
```

#### Scenario 2.2: Rest Break Tanpa Validation
**Steps**:
1. Clear Man Power
2. Click "Rest Break"

**Expected Results**:
- [ ] Alert: "⚠️ Harap pilih Man Power dan Group Injection terlebih dahulu!"
- [ ] No server request

---

### TEST 3: Line Stop Button

#### Scenario 3.1: Start Line Stop
**Steps**:
1. Pastikan ada job aktif
2. Pilih Man Power & Injection
3. Click button "Line Stop"
4. Modal muncul
5. Pilih alasan (e.g., "Machine Breakdown")
6. Click "Submit Line Stop"

**Expected Results**:
- [ ] Modal muncul dengan dropdown alasan
- [ ] Dropdown berisi alasan kategori "Unplanned"
- [ ] Submit button disabled dengan spinner
- [ ] Toast: "🛑 Line Stop dimulai"
- [ ] Modal close otomatis
- [ ] Timer reset ke 00:00:00 dan mulai berjalan
- [ ] Status badge tetap "AKTIF"
- [ ] Downtime description muncul: [alasan yang dipilih]
- [ ] Time Metrics update (Downtime Total bertambah)
- [ ] Recent Downtime table ada entry baru

**Database Check**:
```sql
SELECT d.*, r.Description, r.Category 
FROM DowntimeEvents d
JOIN DowntimeReasons r ON d.ReasonId = r.Id
WHERE d.EndTime IS NULL;
-- Should have 1 row with Category = "Unplanned"
```

#### Scenario 3.2: Line Stop Tanpa Pilih Alasan
**Steps**:
1. Open modal
2. Click submit tanpa pilih alasan

**Expected Results**:
- [ ] Alert: "❌ Pilih alasan LINE STOP terlebih dahulu"
- [ ] Modal tetap terbuka

---

### TEST 4: No Loading Button

#### Scenario 4.1: Set No Loading
**Steps**:
1. Pastikan ada job aktif
2. Pilih Man Power & Injection
3. Click button "NoLoading"
4. Modal muncul
5. Click "Ya, Set No Loading"

**Expected Results**:
- [ ] Modal muncul dengan konfirmasi
- [ ] Submit button disabled dengan spinner
- [ ] Toast: "⏳ No Loading aktif"
- [ ] Modal close otomatis
- [ ] Timer reset ke 00:00:00 dan mulai berjalan
- [ ] Status badge tetap "AKTIF"
- [ ] Downtime description: "No Loading"
- [ ] Time Metrics update (No Loading Time bertambah)

**Database Check**:
```sql
SELECT d.*, r.Description 
FROM DowntimeEvents d
JOIN DowntimeReasons r ON d.ReasonId = r.Id
WHERE d.EndTime IS NULL;
-- Should have 1 row with Description = "No Loading"
```

---

### TEST 5: Timer Synchronization

#### Scenario 5.1: Timer Real-time Update
**Steps**:
1. Start Running
2. Observe timer selama 10 detik

**Expected Results**:
- [ ] Timer update setiap detik
- [ ] Format: HH:MM:SS (e.g., 00:00:01, 00:00:02, ...)
- [ ] Tidak ada skip atau jump
- [ ] Console log tidak ada error

#### Scenario 5.2: Timer Persistence (Page Refresh)
**Steps**:
1. Start Running
2. Wait 30 seconds
3. Refresh page (F5)

**Expected Results**:
- [ ] Timer continue dari waktu yang benar (sekitar 00:00:30+)
- [ ] Tidak reset ke 00:00:00
- [ ] Sinkron dengan backend LastStatusChangeTime

---

### TEST 6: OEE Calculation Integration

#### Scenario 6.1: OEE Metrics Update After Actions
**Steps**:
1. Note initial OEE metrics (OEE%, Availability%, Performance%, Quality%)
2. Start Running for 60 seconds
3. Start Rest Break for 30 seconds
4. Start Running again
5. Observe OEE metrics

**Expected Results**:
- [ ] Availability% berubah (karena downtime bertambah)
- [ ] Operating Time bertambah saat Running
- [ ] Downtime Total bertambah saat Rest Break
- [ ] Rest Break Time bertambah
- [ ] OEE% recalculated correctly
- [ ] Formula: OEE = Availability × Performance × Quality

**Formula Verification**:
```
Availability = (Operating Time / Planned Production Time) × 100
Performance = (Standar CT × Total Output) / Operating Time × 100
Quality = (Good Count / Total Count) × 100
OEE = (Availability / 100) × (Performance / 100) × (Quality / 100) × 100
```

---

### TEST 7: Real-time Updates (SignalR)

#### Scenario 7.1: Multi-tab Synchronization
**Steps**:
1. Open 2 browser tabs dengan URL yang sama
2. Di Tab 1: Start Running
3. Observe Tab 2

**Expected Results**:
- [ ] Tab 2 timer update otomatis
- [ ] Tab 2 status badge update
- [ ] Tab 2 downtime description update
- [ ] Tab 2 Time Metrics update
- [ ] Console log di Tab 2: "📡 ReceiveRealTimeSync received"

---

### TEST 8: Switch Between Actions

#### Scenario 8.1: Running → Rest Break → Running
**Steps**:
1. Start Running (wait 10s)
2. Start Rest Break (wait 10s)
3. Start Running again

**Expected Results**:
- [ ] Each action resets timer ke 00:00:00
- [ ] Timer berjalan dari 0 setiap kali
- [ ] Downtime description update sesuai action
- [ ] Database: DowntimeEvents closed correctly
- [ ] LastStatusChangeTime updated setiap action

#### Scenario 8.2: Rest Break → Line Stop
**Steps**:
1. Start Rest Break
2. Immediately start Line Stop

**Expected Results**:
- [ ] Rest Break downtime di-end otomatis
- [ ] Line Stop downtime created
- [ ] Timer reset
- [ ] No duplicate downtime entries

---

### TEST 9: Time Metrics Real-time

#### Scenario 9.1: Operating Time Increment
**Steps**:
1. Start Running
2. Observe "Operating Time" di Time Metrics card
3. Wait 60 seconds

**Expected Results**:
- [ ] Operating Time bertambah setiap detik
- [ ] Format: HH:MM:SS
- [ ] Increment smooth (tidak jump)

#### Scenario 9.2: Downtime Total Increment
**Steps**:
1. Start Rest Break
2. Observe "Downtime Total"
3. Wait 60 seconds

**Expected Results**:
- [ ] Downtime Total bertambah setiap detik
- [ ] Rest Break Time bertambah setiap detik

---

### TEST 10: Recent Downtime Table

#### Scenario 10.1: New Entry After Downtime
**Steps**:
1. Start Line Stop dengan alasan "Machine Breakdown"
2. Wait 30 seconds
3. Start Running
4. Check "Recent Downtimes" table

**Expected Results**:
- [ ] Table ada entry baru
- [ ] Reason: "Machine Breakdown"
- [ ] Category: "Unplanned"
- [ ] Duration: ~00:00:30
- [ ] Status: "Closed" (badge hijau)

---

## 🐛 Known Issues & Limitations

1. **Timer Drift**: Client-side timer mungkin drift jika tab inactive. Solusi: Refresh dari server setiap 2 detik.
2. **Modal Validation**: Form validation hanya client-side. Backend juga validate.
3. **Race Condition**: Multiple rapid clicks bisa create duplicate entries. Solusi: Disable button saat processing.

---

## 📊 Performance Metrics

Target:
- [ ] Timer update latency < 100ms
- [ ] SignalR broadcast latency < 500ms
- [ ] Page load time < 2s
- [ ] No memory leaks after 1 hour operation

---

## ✅ Sign-off

**Tested By**: _________________
**Date**: _________________
**Environment**: Development / Staging / Production
**Result**: PASS / FAIL

**Notes**:
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
