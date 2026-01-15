// ========================================
// MACHINE ACTIONS CONSOLIDATED SCRIPT
// ========================================
// This script consolidates all machine action handlers with:
// 1. Man Power validation
// 2. Unified timer management
// 3. Success toast notifications
// 4. Standardized function naming

(function () {
    'use strict';

    console.log('🚀 Loading Machine Actions Consolidated Script...');

    // ========== GLOBAL VALIDATION ==========
    window.validateMachineAction = function () {
        const mp = document.getElementById('select-man-power')?.value;
        const inj = document.querySelector('input[name="injection"]:checked')?.value;

        if (!mp || !inj) {
            alert('⚠️ Harap pilih Man Power dan Group Injection terlebih dahulu!');
            return false;
        }
        return true;
    };

    // ========== UNIFIED TIMER MANAGEMENT ==========
    window.MachineTimer = {
        interval: null,
        startTime: null,

        reset: function () {
            if (this.interval) {
                clearInterval(this.interval);
                this.interval = null;
            }
            this.startTime = null;

            const timerEl = document.getElementById('since-last-status');
            if (timerEl) {
                timerEl.textContent = '00:00:00';
            }
            console.log('✅ Timer reset to 00:00:00');
        },

        start: function (startTimeUtc) {
            this.reset();

            if (startTimeUtc) {
                this.startTime = new Date(startTimeUtc);
            } else {
                this.startTime = new Date();
            }

            this.update();
            this.interval = setInterval(() => this.update(), 1000);
            console.log('✅ Timer started from:', this.startTime.toISOString());
        },

        update: function () {
            const timerEl = document.getElementById('since-last-status');
            if (!timerEl || !this.startTime) return;

            const now = new Date();
            const elapsedSeconds = Math.max(0, Math.floor((now - this.startTime) / 1000));

            const hours = Math.floor(elapsedSeconds / 3600);
            const minutes = Math.floor((elapsedSeconds % 3600) / 60);
            const seconds = elapsedSeconds % 60;

            timerEl.textContent =
                String(hours).padStart(2, '0') + ':' +
                String(minutes).padStart(2, '0') + ':' +
                String(seconds).padStart(2, '0');
        }
    };

    // ========== RUNNING BUTTON HANDLER ==========
    window.handleRunningClick = async function (button) {
        console.log('🖱️ Running button clicked');

        if (button.disabled) {
            console.log('⚠️ Button is disabled');
            return false;
        }

        const machineId = button.getAttribute('data-machine-id') || '@Html.Raw(Model.MachineId)';
        const originalText = button.innerHTML;
        const scrollPosition = window.pageYOffset || document.documentElement.scrollTop;

        button.disabled = true;
        button.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i>Processing...';

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('machineId', machineId);

            // ✅ PERBAIKAN: Include Man Power jika sudah dipilih
            const manPowerId = document.getElementById('select-man-power')?.value;
            if (manPowerId) {
                formData.append('manPowerId', manPowerId);
            }

            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            const response = await fetch('/Operator/Start', {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': token || '',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (response.ok) {
                const result = await response.json();

                if (!result.success) {
                    alert('❌ Error: ' + (result.message || 'Gagal start RUNNING'));
                    button.innerHTML = originalText;
                    button.disabled = false;
                    return false;
                }

                // ✅ CRITICAL: Reset timer ke 00:00:00 dan start
                window.MachineTimer.start(result.lastStatusChangeTime);

                // ✅ Update UI
                if (typeof window.updateMachineStatusUI === 'function') {
                    window.updateMachineStatusUI('Aktif', '');
                }

                // ✅ SUCCESS TOAST
                if (typeof showToast === 'function') {
                    showToast('✅ Machine Running dimulai', 'success');
                }

                button.disabled = false;
                button.innerHTML = originalText;

                // Restore scroll
                requestAnimationFrame(() => {
                    window.scrollTo({ top: scrollPosition, behavior: 'instant' });
                });

                // Refresh metrics
                setTimeout(() => {
                    if (typeof window.refreshAllData === 'function') {
                        window.refreshAllData();
                    }
                }, 500);
            } else {
                const errorText = await response.text();
                console.error('❌ Response error:', errorText);
                alert('❌ Error: Gagal start RUNNING');
                button.innerHTML = originalText;
                button.disabled = false;
            }
        } catch (error) {
            console.error('❌ Error:', error);
            alert('❌ Error: ' + error.message);
            button.innerHTML = originalText;
            button.disabled = false;
        }

        return false;
    };

    // ========== REST BREAK BUTTON HANDLER ==========
    window.handleRestClick = async function (button) {
        console.log('🖱️ Rest Break button clicked');

        // ✅ VALIDASI: Cek Man Power & Injection
        if (!window.validateMachineAction()) {
            return false;
        }

        if (button.disabled) {
            console.log('⚠️ Button is disabled');
            return false;
        }

        const form = button.closest('form');
        if (!form) {
            console.error('❌ Form not found');
            return false;
        }

        const formData = new FormData(form);
        const token = formData.get('__RequestVerificationToken');
        const reasonId = formData.get('reasonId');

        if (!reasonId) {
            alert('❌ Silakan pilih alasan REST BREAK');
            return false;
        }

        const originalText = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i>Processing...';

        try {
            const response = await fetch('/Operator/Rest', {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': token || '',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const result = await response.json();

            if (result.success) {
                // ✅ CRITICAL: Reset timer dan start
                window.MachineTimer.start(result.lastStatusChangeTime);

                // ✅ Update UI
                if (typeof window.updateMachineStatusUI === 'function') {
                    window.updateMachineStatusUI('Aktif', result.downtimeDescription || 'Rest Break');
                }

                // ✅ SUCCESS TOAST
                if (typeof showToast === 'function') {
                    showToast('☕ Rest Break dimulai', 'warning');
                }

                button.disabled = false;
                button.innerHTML = originalText;

                // Refresh metrics
                setTimeout(() => {
                    if (typeof window.refreshAllData === 'function') {
                        window.refreshAllData();
                    }
                }, 500);
            } else {
                alert('❌ Error: ' + (result.message || 'Gagal start REST BREAK'));
                button.disabled = false;
                button.innerHTML = originalText;
            }
        } catch (error) {
            console.error('❌ Error:', error);
            alert('❌ Terjadi error saat start REST BREAK');
            button.disabled = false;
            button.innerHTML = originalText;
        }

        return false;
    };

    // ========== LINE STOP BUTTON HANDLER ==========
    window.handleLineStopClick = async function (button) {
        console.log('🖱️ Line Stop submit clicked');

        // ✅ VALIDASI: Cek Man Power & Injection
        if (!window.validateMachineAction()) {
            return false;
        }

        const modal = button.closest('.modal');
        const form = button.closest('form');

        if (!form) {
            console.error('❌ Form not found');
            return false;
        }

        const formData = new FormData(form);
        const reasonId = formData.get('reasonId');

        if (!reasonId) {
            alert('❌ Pilih alasan LINE STOP terlebih dahulu');
            return false;
        }

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (token) {
            formData.append('__RequestVerificationToken', token);
        }

        const originalText = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i>Processing...';

        try {
            const response = await fetch('/Operator/LineStop', {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': token || '',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const result = await response.json();

            if (result.success) {
                // ✅ CRITICAL: Reset timer dan start
                window.MachineTimer.start(result.lastStatusChangeTime);

                // ✅ Update UI
                const reasonSelect = form.querySelector('select[name="reasonId"]');
                const reasonText = reasonSelect?.selectedOptions[0]?.text || 'Line Stop';

                if (typeof window.updateMachineStatusUI === 'function') {
                    window.updateMachineStatusUI('Aktif', reasonText);
                }

                // ✅ SUCCESS TOAST
                if (typeof showToast === 'function') {
                    showToast('🛑 Line Stop dimulai', 'danger');
                }

                // Close modal
                if (modal && typeof bootstrap !== 'undefined') {
                    const modalInstance = bootstrap.Modal.getInstance(modal);
                    if (modalInstance) modalInstance.hide();
                }

                button.disabled = false;
                button.innerHTML = originalText;

                // Refresh metrics
                setTimeout(() => {
                    if (typeof window.refreshAllData === 'function') {
                        window.refreshAllData();
                    }
                }, 500);
            } else {
                alert('❌ Error: ' + (result.message || 'Gagal start LINE STOP'));
                button.disabled = false;
                button.innerHTML = originalText;
            }
        } catch (error) {
            console.error('❌ Error:', error);
            alert('❌ Terjadi error saat start LINE STOP');
            button.disabled = false;
            button.innerHTML = originalText;
        }

        return false;
    };

    // ========== NO LOADING BUTTON HANDLER ==========
    window.handleNoLoadingClick = async function (button) {
        console.log('🖱️ No Loading submit clicked');

        // ✅ VALIDASI: Cek Man Power & Injection
        if (!window.validateMachineAction()) {
            return false;
        }

        const modal = button.closest('.modal');
        const machineId = '@Html.Raw(Model.MachineId)';

        const formData = new FormData();
        formData.append('machineId', machineId);

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (token) {
            formData.append('__RequestVerificationToken', token);
        }

        const originalText = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i>Processing...';

        try {
            const response = await fetch('/Operator/NoLoading', {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': token || '',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const result = await response.json();

            if (result.success) {
                // ✅ CRITICAL: Reset timer dan start
                window.MachineTimer.start(result.lastStatusChangeTime);

                // ✅ Update UI
                if (typeof window.updateMachineStatusUI === 'function') {
                    window.updateMachineStatusUI('Aktif', 'No Loading');
                }

                // ✅ SUCCESS TOAST
                if (typeof showToast === 'function') {
                    showToast('⏳ No Loading aktif', 'info');
                }

                // Close modal
                if (modal && typeof bootstrap !== 'undefined') {
                    const modalInstance = bootstrap.Modal.getInstance(modal);
                    if (modalInstance) modalInstance.hide();
                }

                button.disabled = false;
                button.innerHTML = originalText;

                // Refresh metrics
                setTimeout(() => {
                    if (typeof window.refreshAllData === 'function') {
                        window.refreshAllData();
                    }
                }, 500);
            } else {
                alert('❌ Error: ' + (result.message || 'Gagal set NO LOADING'));
                button.disabled = false;
                button.innerHTML = originalText;
            }
        } catch (error) {
            console.error('❌ Error:', error);
            alert('❌ Terjadi error saat set NO LOADING');
            button.disabled = false;
            button.innerHTML = originalText;
        }

        return false;
    };

    console.log('✅ Machine Actions Consolidated Script Loaded');
})();
