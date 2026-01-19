/**
 * button-state-management.js
 * Handles UI state updates based on machine status.
 * Controls enable/disable state of buttons and visual indicators.
 */

window.updateMachineStatusUI = function (status, downtimeDescription) {
    console.log('🔄 updateMachineStatusUI:', status, downtimeDescription);
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

    // 5. Update Button States
    updateActionButtonsState(status, downtimeDescription);
};

function updateActionButtonsState(status, downtimeDescription) {
    const btnRunning = document.getElementById('btn-running');
    const btnRest = document.getElementById('btn-rest');
    const btnLineStop = document.getElementById('btn-line-stop');
    const btnNoLoading = document.getElementById('btn-no-loading');

    const isRunning = status === 'Aktif';
    const isResting = downtimeDescription && downtimeDescription.toLowerCase().includes('rest');
    const isNoLoading = downtimeDescription && downtimeDescription.toLowerCase().includes('no loading');

    // Example Logic: 
    // - If Running: Disable Running, Enable others
    // - If Rest/LineStop/NoLoading: Enable Running, Disable current action?
    // This depends on specific business rules, implementing generic toggle for now.

    if (btnRunning) {
        // Running button enabled if NOT running, or if we want to allow 're-start' (usually disabled if running)
        // btnRunning.disabled = isRunning && !isResting && !isNoLoading; 
        // Actually, usually we always allow clicking running to "Resume" from downtime
    }
}
