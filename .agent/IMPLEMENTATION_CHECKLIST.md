# Implementation Checklist - Machine Actions Workflow

## Status Implementasi

### ✅ Backend - Sudah Implemented

1. **Event Structure** ✅
   - `DowntimeEvent` sudah punya `StartTime`, `EndTime`, `DurationSeconds`
   - Flags `IsRestBreak`, `IsNoLoading`, `IsLineStop` sudah ada

2. **Auto-Close Event** ✅
   - `Start()` method sudah auto-close downtime sebelumnya
   - `StartDowntime()` sudah auto-close downtime lama sebelum start baru
   - `NoLoading()` sudah auto-close downtime sebelumnya

3. **SignalR Broadcast** ✅
   - Sudah broadcast `RunningStarted`, `DowntimeStarted`, `NoLoadingStarted`
   - Sudah broadcast `OeeUpdated` dengan data lengkap

### 🔧 Backend - Perlu Diperbaiki

1. **Operating Time Calculation** ⚠️
   - Perlu pastikan hanya hitung dari RUNNING yang sudah selesai (`EndTime != null`)
   - Cek di `MachineController.OeeDetail()` atau service yang menghitung OEE

2. **Time Metrics Categories** ⚠️
   - Perlu pastikan REST BREAK & NO LOADING masuk `PlannedStop`, bukan `Downtime`
   - Perlu pastikan hanya LINE STOP yang masuk `Downtime`

### 🔧 Frontend - Perlu Diperbaiki

1. **Timer Visual Only** ⚠️
   - Pastikan timer tidak simpan durasi ke database setiap detik
   - Timer hanya untuk display

2. **Form Persistence** ⚠️
   - Man Power & Injection TIDAK reset setelah submit produksi
   - Hanya field produksi yang di-reset

3. **Button State Management** ❌ BELUM
   - Disable button sesuai status mesin
   - Implementasi `updateButtonStates()`

4. **SignalR Client** ⚠️
   - Sudah ada listener `RunningStarted`, `RunningStopped`
   - Perlu tambah listener `StatusChanged` untuk sync status

---

## Action Items

### Priority 1: Validasi Backend

- [ ] Cek `MachineController.OeeDetail()` - pastikan Operating Time calculation benar
- [ ] Cek Time Metrics calculation - pastikan kategorisasi benar
- [ ] Cek apakah ada kode yang update duration per detik (FORBIDDEN)

### Priority 2: Frontend Improvements

- [ ] Implementasi `updateButtonStates()` untuk disable button sesuai status
- [ ] Pastikan Man Power & Injection tidak reset setelah submit
- [ ] Tambah SignalR listener `StatusChanged`
- [ ] Pastikan timer tidak simpan ke database

### Priority 3: Testing

- [ ] Test workflow: RUNNING → REST BREAK → RUNNING
- [ ] Test workflow: RUNNING → LINE STOP → RUNNING
- [ ] Test workflow: RUNNING → NO LOADING → RUNNING
- [ ] Test shift end auto-close
- [ ] Test Man Power & Injection persistence

---

## Files to Check/Modify

### Backend
- [x] `Controllers/OperatorController.cs` - Machine actions handlers
- [ ] `Controllers/MachineController.cs` - OEE calculation
- [ ] `Services/OeeCalculationService.cs` (jika ada)

### Frontend
- [ ] `Views/Machine/OeeDetail.cshtml` - Timer logic & form handling
- [ ] `wwwroot/js/machine-actions.js` - Button state management

### Models
- [x] `Models/DowntimeEvent.cs` - Event structure
- [ ] `Models/JobRun.cs` - Job structure

---

**Next Steps:**
1. Check MachineController untuk OEE calculation
2. Check OeeDetail.cshtml untuk timer & form logic
3. Implement button state management
4. Test end-to-end workflow
