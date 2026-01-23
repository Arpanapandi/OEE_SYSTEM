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
    console.log('🎛️ updateActionButtonsState input:', currentState);

    const btnRunning = document.getElementById('btn-running');
    const btnRest = document.getElementById('btn-rest');
    const btnLineStop = document.getElementById('btn-line-stop');
    const btnNoLoading = document.getElementById('btn-no-loading');

    // Strict Normalization
    const validStates = ['RUNNING', 'REST_BREAK', 'LINE_STOP', 'NO_LOADING'];
    let effectiveState = validStates.includes(currentState) ? currentState : 'STOPPED';
    console.log('   -> Effective State:', effectiveState);

    // Update Global Config for other scripts
    if (window.OeeConfig) {
        window.OeeConfig.currentState = effectiveState;
    }

    // Helper to set but state
    const setBtn = (btn, enabled, activeStyle = false) => {
        if (!btn) return;
        btn.disabled = !enabled;
        if (enabled) {
            btn.classList.remove('btn-secondary');
            if (activeStyle) btn.classList.add('btn-success');
        } else {
            // Disabled style
            // Usually Bootstrap handles disabled appearance, but we can enforce secondary if needed
            // btn.classList.add('btn-secondary'); 
            // btn.classList.remove('btn-success');
        }
    };

    // ✅ STATE MACHINE LOGIC (Strict 4-State + Stopped)
    switch (effectiveState) {
        case 'RUNNING':
            // Running: CANNOT click Run. CAN click Stops.
            setBtn(btnRunning, false); // Disabled
            if (btnRunning) { btnRunning.classList.add('btn-secondary'); btnRunning.classList.remove('btn-success'); }

            setBtn(btnRest, true);
            setBtn(btnLineStop, true);
            setBtn(btnNoLoading, true);

            // Enable production input
            enableProductionInput(true);
            break;

        case 'REST_BREAK':
            // Rest: CAN click Run (Resume). CAN switch to other stops (LineStop/NoLoading). CANNOT click Rest again.
            setBtn(btnRunning, true, true); // Active (Resume)
            setBtn(btnRest, false); // Disabled (Already Active)
            setBtn(btnLineStop, true);
            setBtn(btnNoLoading, true);

            // Disable production input
            enableProductionInput(false);
            break;

        case 'LINE_STOP':
            // LineStop: CAN click Run (Resume). CAN switch to other stops. CANNOT click LineStop again.
            setBtn(btnRunning, true, true); // Active (Resume)
            setBtn(btnRest, true); // Can switch? Usually Yes.
            setBtn(btnLineStop, false); // Disabled (Already Active)
            setBtn(btnNoLoading, true);

            // Disable production input
            enableProductionInput(false);
            break;

        case 'NO_LOADING':
            // NoLoading: CAN click Run (Resume). CAN switch to other stops. CANNOT click NoLoading again.
            setBtn(btnRunning, true, true); // Active (Resume)
            setBtn(btnRest, true);
            setBtn(btnLineStop, true);
            setBtn(btnNoLoading, false); // Disabled (Already Active)

            // Disable production input
            enableProductionInput(false);
            break;

        case 'STOPPED':
        default:
            // Stopped: CAN click Run (Start). CANNOT click Stops (Conceptually, must Run first? Or strict user rule?)
            // User: "Button disabled yang menyebabkan jadi tidak pindah status" -> This implies if Stopped, buttons might be disabled wrongly?
            // Usually, Initial State allows Start. Actions like Rest/Stop usually require an active job (which Running creates).
            // However, Start creates the Job.

            setBtn(btnRunning, true, true); // Active (Start)

            // Logic: Can we do Rest/Stop if not running? 
            // Backend requires active job. If Stopped means No Active Job, these fail.
            // So Disabled is correct.
            setBtn(btnRest, false);
            setBtn(btnLineStop, false);
            setBtn(btnNoLoading, false);

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
