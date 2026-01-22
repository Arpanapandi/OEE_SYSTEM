/**
 * button-state-management.js
 * Handles UI state updates based on machine CurrentState.
 * Controls enable/disable state of buttons and visual indicators.
 * 
 * ✅ PRODUCTION-READY: State-based button management
 */

// ✅ Global function untuk update UI berdasarkan CurrentState
window.updateMachineStatusUI = function (status, downtimeDescription, currentState) {
    console.log('🔄 updateMachineStatusUI:', { status, downtimeDescription, currentState });

    const statusBadgeEl = document.getElementById('machine-status-badge');
    const downtimeDescEl = document.getElementById('downtime-description');
    const machineStatusInfoEl = document.getElementById('machine-status-info');

    // Normalize status check
    const isAktif = status === 'Aktif' || status === 'Running';

    // 1. Update Badge
    if (statusBadgeEl) {
        statusBadgeEl.textContent = isAktif ? 'AKTIF' : 'TIDAK AKTIF';
        statusBadgeEl.className = `badge ${isAktif ? 'bg-success' : 'bg-warning'}`;
    }

    // 2. Update Downtime Description
    if (downtimeDescEl) {
        if (downtimeDescription && downtimeDescription.trim()) {
            downtimeDescEl.textContent = downtimeDescription;
            downtimeDescEl.style.display = 'inline';
        } else {
            downtimeDescEl.style.display = 'none';
        }
    }

    // 3. Update Admin Status Info
    if (machineStatusInfoEl) {
        machineStatusInfoEl.style.display = isAktif ? 'none' : 'inline';
    }

    // 4. Update Navbar (Optional Sync)
    const navbarStatusDot = document.getElementById('navbar-status-dot');
    const navbarStatusText = document.getElementById('navbar-status-text');
    if (navbarStatusDot && navbarStatusText) {
        navbarStatusText.textContent = isAktif ? 'AKTIF' : (status || 'STOP').toUpperCase();
        navbarStatusDot.style.background = isAktif ? '#28a745' : '#ffc107';
    }

    // 5. ✅ UPDATE BUTTON STATES BASED ON CURRENT STATE
    updateActionButtonsState(currentState || 'STOPPED');
};

// ✅ CORE FUNCTION: State-based button management
function updateActionButtonsState(currentState) {
    console.log('🎛️ updateActionButtonsState:', currentState);

    const btnRunning = document.getElementById('btn-running');
    const btnRest = document.getElementById('btn-rest');
    const btnLineStop = document.getElementById('btn-line-stop');
    const btnNoLoading = document.getElementById('btn-no-loading');

    // Validate state
    const validStates = ['RUNNING', 'REST_BREAK', 'LINE_STOP', 'NO_LOADING', 'STOPPED'];
    if (!validStates.includes(currentState)) {
        console.warn('⚠️ Invalid state:', currentState, '- defaulting to STOPPED');
        currentState = 'STOPPED';
    }

    // ✅ STATE MACHINE LOGIC
    switch (currentState) {
        case 'RUNNING':
            // Mesin sedang produksi
            if (btnRunning) {
                btnRunning.disabled = true;
                btnRunning.classList.add('btn-secondary');
                btnRunning.classList.remove('btn-success');
            }
            if (btnRest) btnRest.disabled = false;
            if (btnLineStop) btnLineStop.disabled = false;
            if (btnNoLoading) btnNoLoading.disabled = false;

            // Enable production input
            enableProductionInput(true);
            break;

        case 'REST_BREAK':
            // Mesin istirahat (planned)
            if (btnRunning) {
                btnRunning.disabled = false;
                btnRunning.classList.remove('btn-secondary');
                btnRunning.classList.add('btn-success');
            }
            if (btnRest) btnRest.disabled = true;
            if (btnLineStop) btnLineStop.disabled = false;
            if (btnNoLoading) btnNoLoading.disabled = false;

            // Disable production input
            enableProductionInput(false);
            break;

        case 'LINE_STOP':
            // Mesin downtime (unplanned)
            if (btnRunning) {
                btnRunning.disabled = false;
                btnRunning.classList.remove('btn-secondary');
                btnRunning.classList.add('btn-success');
            }
            if (btnRest) btnRest.disabled = false;
            if (btnLineStop) btnLineStop.disabled = true;
            if (btnNoLoading) btnNoLoading.disabled = false;

            // Disable production input
            enableProductionInput(false);
            break;

        case 'NO_LOADING':
            // Mesin no loading (planned)
            if (btnRunning) {
                btnRunning.disabled = false;
                btnRunning.classList.remove('btn-secondary');
                btnRunning.classList.add('btn-success');
            }
            if (btnRest) btnRest.disabled = false;
            if (btnLineStop) btnLineStop.disabled = false;
            if (btnNoLoading) btnNoLoading.disabled = true;

            // Disable production input
            enableProductionInput(false);
            break;

        case 'STOPPED':
        default:
            // Mesin belum start atau sudah selesai
            if (btnRunning) {
                btnRunning.disabled = false;
                btnRunning.classList.remove('btn-secondary');
                btnRunning.classList.add('btn-success');
            }
            if (btnRest) btnRest.disabled = true;
            if (btnLineStop) btnLineStop.disabled = true;
            if (btnNoLoading) btnNoLoading.disabled = true;

            // Disable production input
            enableProductionInput(false);
            break;
    }
}

// ✅ Helper: Enable/Disable production input based on state
function enableProductionInput(enabled) {
    const productionInputs = document.querySelectorAll('#production-form input, #production-form select, #production-form button');
    productionInputs.forEach(input => {
        if (input.id === 'btn-submit-produksi') {
            input.disabled = !enabled;
        }
    });

    // Visual feedback
    const productionCard = document.getElementById('production-input-card');
    if (productionCard) {
        if (enabled) {
            productionCard.classList.remove('opacity-50');
        } else {
            productionCard.classList.add('opacity-50');
        }
    }
}

// ✅ Export untuk digunakan di module lain
window.updateActionButtonsState = updateActionButtonsState;
