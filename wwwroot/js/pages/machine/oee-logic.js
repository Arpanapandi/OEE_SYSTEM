/**
 * oee-logic.js
 * Main Controller for OEE Detail Page.
 * Handles Initialization, SignalR, Timers, and Production Data Logic.
 */

window.OeeLogic = (function () {
    // Config
    let config = {}; // { machineId, shiftKey, ... }

    // State
    const state = {
        serverTimeOffset: 0,
        runningTimerInterval: null,
        sinceLastChangeInterval: null,
        timeMetricsInterval: null,
        lastChangeTimestamp: null,

        // Production Timer
        durasiProduksiInterval: null,
        durasiProduksiStartTime: null,
        durasiProduksiSeconds: 0,
        isDurasiAuto: true // Will be toggled by machine status events
    };

    // Public Methods
    function init(cfg) {
        config = cfg;
        console.log('🚀 OEE Logic Initialized', config);

        // 1. Server Time
        initializeServerTime();

        // 2. SignalR
        initSignalR();

        // 3. Timers
        if (config.lastStatusChange) {
            state.lastChangeTimestamp = new Date(config.lastStatusChange);
            resetTimers(state.lastChangeTimestamp);
        }

        // 4. Polling
        state.timeMetricsInterval = setInterval(fetchTimeMetrics, 30000);

        // 5. Init Production Logic
        initProductionLogic();
    }

    // --- Utilities ---
    function getAdjustedServerTime() {
        return new Date(Date.now() + state.serverTimeOffset);
    }
    // Expose globally for other scripts
    window.getAdjustedServerTime = getAdjustedServerTime;

    function initializeServerTime() {
        if (config.serverTime) {
            const server = new Date(config.serverTime);
            if (!isNaN(server)) {
                state.serverTimeOffset = server.getTime() - Date.now();
                console.log('⏱️ Server Sync Offset:', state.serverTimeOffset);
            }
        }
    }

    function showToast(msg, type = 'info') {
        const container = document.querySelector('.toast-container');
        if (!container) return;

        const colors = { success: 'bg-success', error: 'bg-danger', warning: 'bg-warning', info: 'bg-primary' };
        const html = `
            <div class="toast align-items-center text-white ${colors[type] || colors.info} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex"><div class="toast-body">${msg}</div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div>
            </div>`;
        container.insertAdjacentHTML('beforeend', html);
        const el = container.lastElementChild;
        new bootstrap.Toast(el, { delay: 3000 }).show();
        el.addEventListener('hidden.bs.toast', () => el.remove());
    }
    window.showToast = showToast; // Expose

    // --- Timers ---
    function resetTimers(startTime) {
        if (state.sinceLastChangeInterval) clearInterval(state.sinceLastChangeInterval);
        state.lastChangeTimestamp = startTime instanceof Date ? startTime : new Date(startTime);

        updateTimerDisplay(); // immediate
        state.sinceLastChangeInterval = setInterval(updateTimerDisplay, 1000);

        // Reset Status Display text
        const el = document.getElementById('since-last-status');
        if (el) el.textContent = '00:00:00';
    }

    function updateTimerDisplay() {
        // Target 1: Legacy Text Element
        const elStats = document.getElementById('since-last-status');

        // Target 2: New Main Timer Input (Moved to Machine Actions)
        const elMain = document.getElementById('durasi-produksi-display');

        if ((!elStats && !elMain) || !state.lastChangeTimestamp) return;

        const now = getAdjustedServerTime();
        const diff = Math.max(0, Math.floor((now - state.lastChangeTimestamp) / 1000));

        const h = Math.floor(diff / 3600).toString().padStart(2, '0');
        const m = Math.floor((diff % 3600) / 60).toString().padStart(2, '0');
        const s = (diff % 60).toString().padStart(2, '0');
        const timeStr = `${h}:${m}:${s}`;

        if (elStats) elStats.textContent = timeStr;
        if (elMain) elMain.value = timeStr; // Input element uses .value
    }

    // --- Data Handlers ---
    async function fetchTimeMetrics() {
        if (!config.machineId) return;
        try {
            const res = await fetch(`/Operator/GetOperatorData?machineId=${config.machineId}`);
            if (res.ok) applyMetrics(await res.json());
        } catch (e) { console.error('Metrics Fetch Error', e); }
    }

    function applyMetrics(data) {
        if (!data) return;

        // Time Cards
        const setTxt = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = val; };
        setTxt('planned-production-time', data.PlannedProductionTime || '-');
        setTxt('operating-time', data.OperatingTime || '-');
        setTxt('downtime-time', data.Downtime || '-');

        // OEE
        setTxt('oee-value', (data.OEE || 0).toFixed(1) + '%');
        setTxt('availability-value', (data.Availability || 0).toFixed(1) + '%');
        setTxt('performance-value', (data.Performance || 0).toFixed(1) + '%');
        setTxt('quality-value', (data.Quality || 0).toFixed(1) + '%');
    }

    // --- SignalR ---
    function initSignalR() {
        if (typeof signalR === 'undefined') return;

        const conn = new signalR.HubConnectionBuilder()
            .withUrl("/oeeHub")
            .withAutomaticReconnect()
            .build();

        conn.on("ReceiveRealTimeSync", (data) => {
            if (data.MachineId === config.machineId) {
                applyMetrics(data);
                if (data.MachineStatus && typeof window.updateMachineStatusUI === 'function') {
                    window.updateMachineStatusUI(data.MachineStatus, data.ActiveDowntimeDescription);
                }
            }
        });

        conn.on("OeeUpdated", (data) => {
            if (data.machineId === config.machineId) fetchTimeMetrics();
        });

        conn.start().then(() => {
            console.log('✅ SignalR Connected');
            conn.invoke("JoinMachineGroup", config.machineId);
        }).catch(err => console.error('SignalR Error', err));
    }

    // --- Production Logic ---
    function initProductionLogic() {
        return ProductionModule.init();
    }

    // --- Sub-Module: Production ---
    const ProductionModule = {
        init: function () {
            // Load Persistence
            this.loadPrefs();

            // Listeners
            document.addEventListener('change', (e) => {
                if (e.target.matches('#select-man-power') || e.target.matches('input[name="injection"]')) {
                    this.savePrefs();
                    this.checkCompleteness();
                }
                if (e.target.matches('.prod-input')) this.checkCompleteness();
            });

            // Penipisan Toggle
            const penipisanGroup = document.getElementById('penipisan-group');
            if (penipisanGroup) {
                penipisanGroup.addEventListener('change', (e) => {
                    const isNg = e.target.value === 'NG';
                    const container = document.getElementById('keterangan-container');
                    if (container) container.style.display = isNg ? 'block' : 'none';
                    if (!isNg) {
                        const sel = document.getElementById('select-keterangan');
                        if (sel) sel.value = '';
                    }
                    this.checkCompleteness();
                });
            }

            // Inputs Validation
            document.querySelectorAll('.prod-input').forEach(el => {
                el.addEventListener('input', this.checkCompleteness.bind(this));
            });

            // Auto Timer Persistence
            if (localStorage.getItem('durasiProduksiStartTime')) {
                this.startTimer(true);
            }

            // Submit
            const btnSubmit = document.getElementById('btn-submit-produksi');
            if (btnSubmit) btnSubmit.addEventListener('click', this.handleSubmit.bind(this));

            // Reset Timer Btn
            const btnReset = document.getElementById('btn-reset-durasi');
            if (btnReset) btnReset.addEventListener('click', this.resetTimer.bind(this));

            // Standalone Quantity Modal Submit
            const btnSubmitModalQty = document.getElementById('btn-modal-submit-qty');
            if (btnSubmitModalQty) btnSubmitModalQty.addEventListener('click', this.handleStandaloneQtySubmit.bind(this));

            // Direct Qty Buttons
            const btnIncQty = document.getElementById('btn-increment-qty');
            const btnDecQty = document.getElementById('btn-decrement-qty');
            if (btnIncQty) btnIncQty.addEventListener('click', () => {
                const el = document.getElementById('input-qty');
                if (el) el.value = parseInt(el.value || 0) + 1;
                this.checkCompleteness();
            });
            if (btnDecQty) btnDecQty.addEventListener('click', () => {
                const el = document.getElementById('input-qty');
                if (el) el.value = Math.max(1, parseInt(el.value || 1) - 1);
                this.checkCompleteness();
            });

            // Modal Confirm Submit (Main Form)
            const btnConfirmMain = document.getElementById('btn-confirm-submit-produksi');
            if (btnConfirmMain) btnConfirmMain.addEventListener('click', this.handleConfirmedSubmit.bind(this));

            // Modal Input Change (Sync Total)
            const modalGood = document.getElementById('input-modal-good');
            const modalNg = document.getElementById('input-modal-ng');
            const updateModalTotal = () => {
                const total = (parseInt(modalGood?.value) || 0) + (parseInt(modalNg?.value) || 0);
                const display = document.getElementById('total-qty-display');
                if (display) display.textContent = total;
            };
            if (modalGood) modalGood.addEventListener('input', updateModalTotal);
            if (modalNg) modalNg.addEventListener('input', updateModalTotal);
            if (modalGood) modalGood.addEventListener('change', updateModalTotal);
            if (modalNg) modalNg.addEventListener('change', updateModalTotal);
        },

        savePrefs: function () {
            const mp = document.getElementById('select-man-power')?.value;
            const inj = document.querySelector('input[name="injection"]:checked')?.value;
            if (mp) localStorage.setItem('lastManPower', mp);
            if (inj) localStorage.setItem('lastInjection', inj);
        },

        loadPrefs: function () {
            const mp = localStorage.getItem('lastManPower');
            const inj = localStorage.getItem('lastInjection');

            if (mp) {
                const el = document.getElementById('select-man-power');
                if (el) el.value = mp;
            }
            if (inj) {
                const el = document.querySelector(`input[name="injection"][value="${inj}"]`);
                if (el) el.checked = true;
            }
            this.checkCompleteness();
        },

        checkCompleteness: function () {
            const ids = ['input-nomor-lot', 'input-lot-bo', 'input-nama-compound', 'input-berat-act', 'select-man-power', 'input-qty'];
            let complete = ids.every(id => document.getElementById(id)?.value?.trim() && document.getElementById(id)?.value != '0');

            // Radios
            if (!document.querySelector('input[name="injection"]:checked')) complete = false;
            if (!document.querySelector('input[name="penipisan"]:checked')) complete = false;

            // Keterangan if NG
            const isNg = document.querySelector('input[name="penipisan"][value="NG"]')?.checked;
            if (isNg && !document.getElementById('select-keterangan')?.value) complete = false;

            const btn = document.getElementById('btn-submit-produksi');
            if (btn) {
                btn.disabled = !complete;
                btn.classList.toggle('btn-primary', complete);
                btn.classList.toggle('btn-secondary', !complete);
            }
            return complete;
        },

        startTimer: function (restore = false) {
            if (state.durasiProduksiInterval) clearInterval(state.durasiProduksiInterval);

            if (!restore) {
                state.durasiProduksiStartTime = getAdjustedServerTime();
                localStorage.setItem('durasiProduksiStartTime', state.durasiProduksiStartTime.toISOString());
            } else {
                const stored = localStorage.getItem('durasiProduksiStartTime');
                state.durasiProduksiStartTime = stored ? new Date(stored) : getAdjustedServerTime();
            }

            this.updateTimerUI();
            state.durasiProduksiInterval = setInterval(this.updateTimerUI.bind(this), 1000);
        },

        resetTimer: function () {
            if (state.durasiProduksiInterval) clearInterval(state.durasiProduksiInterval);
            state.durasiProduksiSeconds = 0;
            state.durasiProduksiStartTime = null;
            localStorage.removeItem('durasiProduksiStartTime');
            this.updateTimerUI();
        },

        updateTimerUI: function () {
            // CONFLICT RESOLUTION: 
            // The ID 'durasi-produksi-display' is now used for the Machine Status Timer (Running Duration).
            // We disable the Production Module's item-level timer from hijacking this display.

            // If we need an item-level timer later, create a new element ID (e.g., 'item-production-timer').
            /*
            const display = document.getElementById('durasi-produksi-display');
            if (!display) return;

            if (state.durasiProduksiStartTime) {
                const now = getAdjustedServerTime();
                state.durasiProduksiSeconds = Math.max(0, Math.floor((now - state.durasiProduksiStartTime) / 1000));
            } else {
                state.durasiProduksiSeconds = 0;
            }

            const s = state.durasiProduksiSeconds;
            const hh = Math.floor(s / 3600).toString().padStart(2, '0');
            const mm = Math.floor((s % 3600) / 60).toString().padStart(2, '0');
            const ss = (s % 60).toString().padStart(2, '0');
            display.value = `${hh}:${mm}:${ss}`;
            */
        },

        handleSubmit: function (e) {
            e.preventDefault();
            if (!this.checkCompleteness()) return;

            // Instead of immediate submit, show modal
            const mainQty = document.getElementById('input-qty')?.value || 1;
            const modalGood = document.getElementById('input-modal-good');
            const modalNg = document.getElementById('input-modal-ng');

            if (modalGood) modalGood.value = mainQty;
            if (modalNg) modalNg.value = 0;

            const display = document.getElementById('total-qty-display');
            if (display) display.textContent = mainQty;

            if (window.bootstrap) {
                const modal = new bootstrap.Modal(document.getElementById('modal-input-produksi-qty'));
                modal.show();
            }
        },

        handleConfirmedSubmit: async function (e) {
            const btn = document.getElementById('btn-confirm-submit-produksi');
            const original = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Saving...';

            try {
                const goodQty = parseInt(document.getElementById('input-modal-good').value) || 0;
                const rejectQty = parseInt(document.getElementById('input-modal-ng').value) || 0;
                const remark = document.getElementById('input-modal-keterangan')?.value || '';

                const formData = new FormData();
                formData.append('machineId', config.machineId);
                formData.append('nomorLot', document.getElementById('input-nomor-lot').value);
                formData.append('lotBo', document.getElementById('input-lot-bo').value);
                formData.append('namaCompound', document.getElementById('input-nama-compound').value);
                formData.append('beratAct', document.getElementById('input-berat-act').value);
                formData.append('manPowerId', document.getElementById('select-man-power').value);
                formData.append('injection', document.querySelector('input[name="injection"]:checked').value);
                formData.append('penipisan', document.querySelector('input[name="penipisan"]:checked').value);
                formData.append('keterangan', document.getElementById('select-keterangan').value);
                formData.append('durasiProduksiSeconds', state.durasiProduksiSeconds);
                formData.append('goodQty', goodQty);
                formData.append('rejectQty', rejectQty);
                formData.append('rejectReason', remark);
                formData.append('__RequestVerificationToken', document.querySelector('input[name="__RequestVerificationToken"]').value);

                const res = await fetch('/Operator/SubmitProductionData', {
                    method: 'POST', body: formData
                });
                const result = await res.json();

                if (result.success) {
                    showToast('✅ Data Saved', 'success');
                    this.clearForm();

                    // Close Modal
                    const modalEl = document.getElementById('modal-input-produksi-qty');
                    const modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();

                    // Reset Logic
                    this.resetTimer();
                    this.startTimer(); // Start new timer for new item

                    // Trigger refresh
                    if (typeof window.OeeLogic.fetchTimeMetrics === 'function') {
                        window.OeeLogic.fetchTimeMetrics();
                    }
                } else {
                    showToast('❌ ' + result.message, 'error');
                }
            } catch (e) {
                console.error(e);
                showToast('❌ Error submitting data', 'error');
            } finally {
                btn.disabled = false;
                btn.innerHTML = original;
            }
        },

        handleStandaloneQtySubmit: async function (e) {
            e.preventDefault();
            const btn = document.getElementById('btn-modal-submit-qty');
            const goodInput = document.getElementById('modal-good-qty');
            const rejectInput = document.getElementById('modal-reject-qty');
            const remarkInput = document.getElementById('modal-qty-keterangan');

            const goodQty = parseInt(goodInput.value) || 0;
            const rejectQty = parseInt(rejectInput.value) || 0;
            const remark = remarkInput.value || '';

            if (goodQty === 0 && rejectQty === 0) {
                showToast('❌ Masukan jumlah Good atau NG', 'warning');
                return;
            }

            const original = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Saving...';

            try {
                const formData = new FormData();
                formData.append('machineId', config.machineId);
                formData.append('goodQty', goodQty);
                formData.append('rejectQty', rejectQty);
                formData.append('rejectReason', remark);

                // Get ManPower and Injection if available
                const mp = document.getElementById('select-man-power')?.value;
                const inj = document.querySelector('input[name="injection"]:checked')?.value;
                if (mp) formData.append('manPowerId', mp);
                if (inj) formData.append('injection', inj);

                formData.append('__RequestVerificationToken', document.querySelector('input[name="__RequestVerificationToken"]').value);

                const res = await fetch('/Operator/AddQuantity', {
                    method: 'POST', body: formData
                });
                const result = await res.json();

                if (result.success) {
                    showToast(`✅ Berhasil: ${goodQty} Good, ${rejectQty} NG`, 'success');

                    // Reset fields
                    goodInput.value = 1;
                    rejectInput.value = 0;
                    remarkInput.value = '';

                    // Close Modal
                    const modalEl = document.getElementById('standaloneQtyModal');
                    if (window.bootstrap) {
                        const modal = bootstrap.Modal.getInstance(modalEl);
                        if (modal) modal.hide();
                    }

                    // Trigger refresh
                    if (typeof window.OeeLogic.fetchTimeMetrics === 'function') {
                        window.OeeLogic.fetchTimeMetrics();
                    }
                } else {
                    showToast('❌ ' + result.message, 'error');
                }
            } catch (e) {
                console.error(e);
                showToast('❌ Error submitting data', 'error');
            } finally {
                btn.disabled = false;
                btn.innerHTML = original;
            }
        },

        clearForm: function () {
            // Keep ManPower/Injection
            document.getElementById('input-nomor-lot').value = '';
            document.getElementById('input-lot-bo').value = '';
            document.getElementById('input-nama-compound').value = '';
            document.getElementById('input-berat-act').value = '';
            document.getElementById('select-keterangan').value = '';
            document.getElementById('input-qty').value = '1';
            const modalKet = document.getElementById('input-modal-keterangan');
            if (modalKet) modalKet.value = '';

            // Reset Penipisan to OK
            const okRadio = document.querySelector('input[name="penipisan"][value="OK"]');
            if (okRadio) okRadio.checked = true;

            // Hide Keterangan
            const cont = document.getElementById('keterangan-container');
            if (cont) cont.style.display = 'none';

            this.checkCompleteness();
        }
    };

    // Check and Load Component wrapper (for external calls from Scanner)
    window.checkAndLoadKomponen = function () {
        const pNum = document.getElementById('input-lot-bo')?.value;
        const lNum = document.getElementById('input-nomor-lot')?.value;
        if (pNum && lNum) {
            // Logic to load component if needed
            // Kept simple for now as per previous logic
        }
        ProductionModule.checkCompleteness();
    }

    window.checkAllInputsComplete = function () {
        return ProductionModule.checkCompleteness();
    }

    return {
        init: init,
        resetTimers: resetTimers,
        fetchTimeMetrics: fetchTimeMetrics
    };

})();
