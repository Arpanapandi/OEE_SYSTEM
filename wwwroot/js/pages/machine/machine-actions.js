/**
 * machine-actions.js
 * Handles machine action buttons (Running, Rest, Line Stop, No Loading).
 * Sends AJAX requests to backend and updates UI status.
 */

// Helper to get global config
function getMachineId() {
    return window.OeeConfig ? window.OeeConfig.machineId : null;
}

// Helper: Check Active Job
function hasActiveJob() {
    // Check Config first
    if (window.OeeConfig && window.OeeConfig.hasActiveJob === true) return true;

    // Check DOM Indicator (Work Order Number element presence)
    const woEl = document.getElementById('wo-number-display');
    if (woEl && woEl.textContent && woEl.textContent.trim() !== '-') return true;

    return false;
}

// 1. Handle Running Click
window.handleRunningClickDirect = async function (button) {
    console.log('🖱️ Running clicked');
    if (button.disabled) return;

    // ✅ VALIDASI WORK ORDER
    if (!hasActiveJob()) {
        Swal.fire({
            icon: 'warning',
            title: 'Tidak Ada Work Order',
            text: 'Silakan buat Work Order terlebih dahulu sebelum menjalankan mesin!',
            confirmButtonText: 'OK'
        });
        return;
    }

    // Ensure dependencies
    if (typeof window.updateMachineStatusUI !== 'function') {
        console.error('updateMachineStatusUI missing'); return;
    }

    const machineId = getMachineId();
    if (!machineId) { console.error('Machine ID missing'); return; }

    const originalText = button.innerHTML;
    const scrollPos = window.pageYOffset;

    button.disabled = true;
    button.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i>Processing...';

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const formData = new FormData();
        formData.append('machineId', machineId);
        if (token) {
            formData.append('__RequestVerificationToken', token);
        }

        const response = await fetch('/Operator/Start', {
            method: 'POST',
            body: formData,
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        });

        const result = await response.json();

        if (result.success) {
            // Success Logic
            if (result.lastStatusChangeTime && window.OeeLogic && typeof window.OeeLogic.setLastChangeTimestamp === 'function') {
                window.OeeLogic.setLastChangeTimestamp(result.lastStatusChangeTime);
            } else {
                const now = window.getAdjustedServerTime ? window.getAdjustedServerTime() : new Date();
                if (window.OeeLogic) window.OeeLogic.resetTimers(now);
            }

            // Update UI
            window.updateMachineStatusUI('Aktif', '');

            // Refresh Data
            if (window.OeeLogic && typeof window.OeeLogic.fetchTimeMetrics === 'function') {
                setTimeout(window.OeeLogic.fetchTimeMetrics, 500);
            }
        } else {
            alert('❌ Error: ' + (result.message || 'Gagal Start'));
        }
    } catch (e) {
        console.error(e);
        alert('❌ Network/Server Error');
    } finally {
        button.disabled = false;
        button.innerHTML = originalText;
        window.scrollTo({ top: scrollPos, behavior: 'instant' });
    }
};

// 2. Event Listeners for Forms (Rest, LineStop, NoLoading)
document.addEventListener('DOMContentLoaded', function () {
    // Rest Break
    const btnRest = document.getElementById('btn-rest');
    if (btnRest) {
        btnRest.addEventListener('click', async (e) => {
            e.preventDefault();
            if (!hasActiveJob()) {
                Swal.fire({
                    icon: 'warning',
                    title: 'Aksi Ditolak',
                    text: 'Tidak ada Work Order aktif. Buat jadwal terlebih dahulu.'
                });
                return;
            }

            const form = document.getElementById('rest-form');
            if (!form) return;

            await handleActionSubmit(form, '/Operator/Rest', btnRest, 'Rest Break');
        });
    }

    // Line Stop
    const formLineStop = document.getElementById('line-stop-start-form');
    if (formLineStop) {
        formLineStop.addEventListener('submit', async (e) => {
            e.preventDefault();
            if (!hasActiveJob()) {
                alert('Tidak ada Work Order aktif!'); // Modal might block swal, use alert as fallback or handle properly
                return;
            }
            const btn = document.getElementById('btn-line-stop'); // Main button
            await handleActionSubmit(formLineStop, '/Operator/LineStop', btn, 'Line Stop', 'lineStopModal');
        });
    }

    // No Loading
    const formNoLoading = document.getElementById('no-loading-form');
    if (formNoLoading) {
        formNoLoading.addEventListener('submit', async (e) => {
            e.preventDefault();
            if (!hasActiveJob()) {
                alert('Tidak ada Work Order aktif!');
                return;
            }
            const btn = document.getElementById('btn-no-loading');
            await handleActionSubmit(formNoLoading, '/Operator/NoLoading', btn, 'No Loading', 'noLoadingModal');
        });
    }
});

// Generic Handler
async function handleActionSubmit(form, url, btnIndicator, actionName, modalId = null) {
    const formData = new FormData(form);
    const token = formData.get('__RequestVerificationToken');

    // Validation
    if ((actionName === 'Rest Break' || actionName === 'Line Stop') && !formData.get('reasonId')) {
        alert(`❌ Pilih alasan ${actionName}`); return;
    }

    // UI Loading
    const originalText = btnIndicator ? btnIndicator.innerHTML : '';
    if (btnIndicator) {
        btnIndicator.disabled = true;
        btnIndicator.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i>Processing...';
    }

    const submitBtn = form.querySelector('button[type="submit"]');
    const submitOriginal = submitBtn ? submitBtn.innerHTML : '';
    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i>';
    }

    try {
        const response = await fetch(url, {
            method: 'POST',
            body: formData,
            headers: {
                'RequestVerificationToken': token || '',
                'X-Requested-With': 'XMLHttpRequest'
            }
        });

        const result = await response.json();

        if (result.success) {
            // Success
            const now = window.getAdjustedServerTime ? window.getAdjustedServerTime() : new Date();
            if (window.OeeLogic) window.OeeLogic.resetTimers(now);

            // Determine Description
            let description = actionName;
            if (modalId === 'lineStopModal') {
                const select = form.querySelector('select[name="reasonId"]');
                if (select) description = select.selectedOptions[0].text;
            } else if (modalId === 'noLoadingModal') {
                description = 'No Loading';
            } else if (actionName === 'Rest Break') {
                description = 'Rest Break';
            }

            // Update UI
            window.updateMachineStatusUI('Aktif', description);

            // Sync Timer from response if available
            if (result.lastStatusChangeTime && window.OeeLogic && typeof window.OeeLogic.setLastChangeTimestamp === 'function') {
                window.OeeLogic.setLastChangeTimestamp(result.lastStatusChangeTime);
            }

            // Hide Modal
            if (modalId) {
                const modalEl = document.getElementById(modalId);
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }

            // Refresh Data
            if (window.OeeLogic && typeof window.OeeLogic.fetchTimeMetrics === 'function') {
                setTimeout(window.OeeLogic.fetchTimeMetrics, 500);
            }

        } else {
            alert('❌ Error: ' + (result.message || 'Gagal'));
        }

    } catch (e) {
        console.error(e);
        alert('❌ Error System');
    } finally {
        if (btnIndicator) {
            btnIndicator.disabled = false;
            btnIndicator.innerHTML = originalText;
        }
        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.innerHTML = submitOriginal;
        }
    }
}
