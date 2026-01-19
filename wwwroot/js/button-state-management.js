// ========================================
// BUTTON STATE MANAGEMENT
// ========================================
// Fungsi untuk disable/enable button sesuai status mesin

(function () {
    'use strict';

    console.log('🚀 Loading Button State Management...');

    /**
     * Update button states berdasarkan status mesin
     * @param {string} currentStatus - Status mesin saat ini (RUNNING, REST_BREAK, LINE_STOP, NO_LOADING, IDLE)
     */
    window.updateButtonStates = function (currentStatus) {
        const btnRunning = document.getElementById('btn-running');
        const btnRest = document.querySelector('[data-bs-target="#restBreakModal"]');
        const btnLineStop = document.querySelector('[data-bs-target="#lineStopModal"]');
        const btnNoLoading = document.querySelector('[data-bs-target="#noLoadingModal"]');

        if (!btnRunning) {
            console.warn('⚠️ Button RUNNING not found');
            return;
        }

        console.log('🔄 Updating button states for status:', currentStatus);

        switch (currentStatus) {
            case 'RUNNING':
                // Saat RUNNING: disable RUNNING, enable yang lain
                btnRunning.disabled = true;
                btnRunning.classList.add('disabled');

                if (btnRest) {
                    btnRest.disabled = false;
                    btnRest.classList.remove('disabled');
                }
                if (btnLineStop) {
                    btnLineStop.disabled = false;
                    btnLineStop.classList.remove('disabled');
                }
                if (btnNoLoading) {
                    btnNoLoading.disabled = false;
                    btnNoLoading.classList.remove('disabled');
                }
                break;

            case 'REST_BREAK':
                // Saat REST BREAK: enable RUNNING, disable yang lain
                btnRunning.disabled = false;
                btnRunning.classList.remove('disabled');

                if (btnRest) {
                    btnRest.disabled = true;
                    btnRest.classList.add('disabled');
                }
                if (btnLineStop) {
                    btnLineStop.disabled = true;
                    btnLineStop.classList.add('disabled');
                }
                if (btnNoLoading) {
                    btnNoLoading.disabled = true;
                    btnNoLoading.classList.add('disabled');
                }
                break;

            case 'LINE_STOP':
                // Saat LINE STOP: enable RUNNING, disable yang lain
                btnRunning.disabled = false;
                btnRunning.classList.remove('disabled');

                if (btnRest) {
                    btnRest.disabled = true;
                    btnRest.classList.add('disabled');
                }
                if (btnLineStop) {
                    btnLineStop.disabled = true;
                    btnLineStop.classList.add('disabled');
                }
                if (btnNoLoading) {
                    btnNoLoading.disabled = true;
                    btnNoLoading.classList.add('disabled');
                }
                break;

            case 'NO_LOADING':
                // Saat NO LOADING: enable RUNNING, disable yang lain
                btnRunning.disabled = false;
                btnRunning.classList.remove('disabled');

                if (btnRest) {
                    btnRest.disabled = true;
                    btnRest.classList.add('disabled');
                }
                if (btnLineStop) {
                    btnLineStop.disabled = true;
                    btnLineStop.classList.add('disabled');
                }
                if (btnNoLoading) {
                    btnNoLoading.disabled = true;
                    btnNoLoading.classList.add('disabled');
                }
                break;

            case 'IDLE':
            default:
                // Saat IDLE: enable RUNNING, disable yang lain
                btnRunning.disabled = false;
                btnRunning.classList.remove('disabled');

                if (btnRest) {
                    btnRest.disabled = true;
                    btnRest.classList.add('disabled');
                }
                if (btnLineStop) {
                    btnLineStop.disabled = true;
                    btnLineStop.classList.add('disabled');
                }
                if (btnNoLoading) {
                    btnNoLoading.disabled = true;
                    btnNoLoading.classList.add('disabled');
                }
                break;
        }

        console.log('✅ Button states updated');
    };

    /**
     * Get current machine status dari UI
     * @returns {string} Status mesin saat ini
     */
    window.getCurrentMachineStatus = function () {
        // Cek dari status badge
        const statusBadge = document.getElementById('machine-status-badge');
        const statusDesc = document.getElementById('machine-status-description');

        if (statusBadge && statusDesc) {
            const description = statusDesc.textContent.trim();

            if (description.includes('Rest Break') || description.includes('REST BREAK')) {
                return 'REST_BREAK';
            } else if (description.includes('Line Stop') || description.includes('LINE STOP')) {
                return 'LINE_STOP';
            } else if (description.includes('No Loading') || description.includes('NO LOADING')) {
                return 'NO_LOADING';
            } else if (statusBadge.textContent.trim() === 'Aktif' && !description) {
                return 'RUNNING';
            }
        }

        // Fallback: cek dari button state
        const btnRunning = document.getElementById('btn-running');
        if (btnRunning && btnRunning.disabled) {
            return 'RUNNING';
        }

        return 'IDLE';
    };

    /**
     * Initialize button states saat page load
     */
    function initializeButtonStates() {
        // Tunggu DOM ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function () {
                setTimeout(() => {
                    const currentStatus = window.getCurrentMachineStatus();
                    console.log('🎬 Initial machine status:', currentStatus);
                    window.updateButtonStates(currentStatus);
                }, 500);
            });
        } else {
            setTimeout(() => {
                const currentStatus = window.getCurrentMachineStatus();
                console.log('🎬 Initial machine status:', currentStatus);
                window.updateButtonStates(currentStatus);
            }, 500);
        }
    }

    // Auto-initialize
    initializeButtonStates();

    console.log('✅ Button State Management Loaded');
})();
