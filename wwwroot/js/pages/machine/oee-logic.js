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
        // Machine Status Timer (TOTAL RUNNING TIME)
        sinceLastChangeInterval: null,
        lastChangeTimestamp: null,

        // Timestamp when metrics were last fetched (for client-side ticking)
        metricsTimestamp: null,

        // ✅ NEW: Current State dari backend
        currentState: 'STOPPED',

        // Item Production Timer (Manual/Auto per production item)
        durasiProduksiInterval: null,
        durasiProduksiStartTime: null,
        durasiProduksiSeconds: 0,
        isDurasiAuto: true, // Will be toggled by machine status events

        // Time Metrics State (Real-time Accumulation)
        metrics: {
            plannedSeconds: 43200, // Default 12h
            operatingSeconds: 0,
            downtimeSeconds: 0,
            restSeconds: 0,
            noLoadingSeconds: 0,
            shiftEnd: null,
            isRunning: false,
            hasActiveDowntime: false,
            hasActiveRest: false,
            hasActiveNoLoading: false
        }
    };

    // Public Methods
    function init(cfg) {
        config = cfg;
        console.log('🚀 OEE Logic Initialized', config);

        // 1. Server Time
        initializeServerTime();

        // 2. ✅ Initialize Current State
        state.currentState = config.currentState || 'STOPPED';
        console.log('📊 Initial State:', state.currentState);

        // 3. SignalR
        initSignalR();

        // 4. Timers
        if (config.lastStatusChange) {
            state.lastChangeTimestamp = new Date(config.lastStatusChange);
            resetTimers(state.lastChangeTimestamp);
        }

        // 5. Polling
        state.timeMetricsInterval = setInterval(fetchTimeMetrics, 10000); // 10s for better responsiveness

        // 6. Init Production Logic
        initProductionLogic();

        // 7. Init Machine Action Timer
        MachineTimer.init();

        // 8. ✅ Update Button States based on CurrentState
        if (window.updateActionButtonsState) {
            window.updateActionButtonsState(state.currentState);
        }
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
    }

    function updateTimerDisplay() {
        const now = getAdjustedServerTime();

        // --- 1. Main Machine Status Timer ---
        const diff = Math.max(0, Math.floor((now - state.lastChangeTimestamp) / 1000));
        const format = (s) => {
            const h = Math.floor(s / 3600).toString().padStart(2, '0');
            const m = Math.floor((s % 3600) / 60).toString().padStart(2, '0');
            const sec = (s % 60).toString().padStart(2, '0');
            return `${h}:${m}:${sec}`;
        };
        const timeStr = format(diff);

        // Target 1: Machine Action Timer (TOTAL RUNNING TIME)
        const elActions = document.getElementById('durasi-produksi-display');
        if (elActions) {
            if (elActions.tagName === 'INPUT') {
                if (elActions.value !== timeStr) elActions.value = timeStr;
            } else {
                if (elActions.textContent !== timeStr) elActions.textContent = timeStr;
            }
        }
        const elStats = document.getElementById('since-last-status');
        if (elStats && elStats.textContent !== timeStr) elStats.textContent = timeStr;

        // --- 2. Real-time Time Metrics (Client-Side Ticking) ---
        const m = state.metrics;

        // Calculate elapsed time (seconds) since last metric fetch
        // Use client time `Date.now()` for constant ticking, assuming metricsTimestamp is also client time
        let elapsedTick = 0;
        if (state.metricsTimestamp) {
            elapsedTick = Math.max(0, (Date.now() - state.metricsTimestamp) / 1000);
        }

        // Apply ticking based on state
        const liveOperating = m.operatingSeconds + (m.isRunning ? elapsedTick : 0);
        const liveDowntime = m.downtimeSeconds + (m.hasActiveDowntime ? elapsedTick : 0);
        const liveRest = m.restSeconds + (m.hasActiveRest ? elapsedTick : 0);
        const liveNoLoading = m.noLoadingSeconds + (m.hasActiveNoLoading ? elapsedTick : 0);

        // Helper to display formatted time
        const setVal = (id, seconds) => {
            const el = document.getElementById(id);
            if (el) el.textContent = format(Math.floor(seconds));
        };

        // Display Metrics
        setVal('operating-time', liveOperating);
        setVal('downtime-total', liveDowntime);
        setVal('rest-break-time', liveRest);
        setVal('no-loading-time', liveNoLoading);

        // Update Progress Bars (Use live values)
        if (m.plannedSeconds > 0) {
            const updateBar = (id, s) => {
                const el = document.getElementById(id);
                if (el) el.style.width = (s / m.plannedSeconds * 100).toFixed(1) + '%';
            };
            updateBar('operating-progress-bar', liveOperating);
            updateBar('downtime-progress-bar', liveDowntime);
            updateBar('rest-break-progress-bar', liveRest);
            updateBar('no-loading-progress-bar', liveNoLoading);
        }
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

        // Capture client timestamp for ticking sync
        state.metricsTimestamp = Date.now();

        // 1. Time Cards - Try both PascalCase and camelCase
        const setTxt = (id, val) => {
            const el = document.getElementById(id);
            if (el && val !== undefined && val !== null && el.textContent !== val.toString()) {
                el.textContent = val;
            }
        };

        // Update Planned Production (static value)
        setTxt('planned-production-time', data.PlannedProductionTime || data.plannedProductionTime || '-');

        // Update State for Real-time Ticking
        state.metrics.plannedSeconds = data.PlannedProductionTimeSeconds || data.plannedProductionTimeSeconds || 43200;
        state.metrics.operatingSeconds = data.OperatingTimeSeconds || data.operatingTimeSeconds || 0;
        state.metrics.downtimeSeconds = data.DowntimeSeconds || data.downtimeSeconds || 0;
        state.metrics.restSeconds = data.RestBreakTimeSeconds || data.restBreakTimeSeconds || 0;
        state.metrics.noLoadingSeconds = data.NoLoadingTimeSeconds || data.noLoadingTimeSeconds || 0;

        state.metrics.shiftEnd = data.ShiftEnd ? new Date(data.ShiftEnd) : null;
        state.metrics.isRunning = data.IsRunning || false;
        state.metrics.hasActiveDowntime = data.HasActiveDowntime || false;
        state.metrics.hasActiveRest = data.HasActiveRestBreak || false;
        state.metrics.hasActiveNoLoading = data.HasActiveNoLoading || false;

        // ✅ NEW: Update CurrentState from backend (Handle both PascalCase and camelCase)
        const incomingState = data.CurrentState || data.currentState;
        if (incomingState && incomingState !== state.currentState) {
            console.log(`🔄 State Changed: ${state.currentState} → ${incomingState}`);
            state.currentState = incomingState;

            // Update button states
            if (window.updateActionButtonsState) {
                window.updateActionButtonsState(state.currentState);
            }
        }

        console.log('📊 Metrics State Updated:', {
            operating: state.metrics.operatingSeconds,
            downtime: state.metrics.downtimeSeconds,
            rest: state.metrics.restSeconds,
            noLoading: state.metrics.noLoadingSeconds,
            isRunning: state.metrics.isRunning,
            hasDowntime: state.metrics.hasActiveDowntime,
            hasRest: state.metrics.hasActiveRest,
            hasNoLoading: state.metrics.hasActiveNoLoading,
            currentState: state.currentState // ✅ NEW
        });

        // Immediately update UI with new state
        updateTimerDisplay();

        // 2. OEE - Handle various case formats
        const getVal = (p) => data[p] ?? data[p.toLowerCase()] ?? data[p.charAt(0).toLowerCase() + p.slice(1)] ?? 0;

        setTxt('oee-value', (getVal('Oee')).toFixed(0) + '%');
        setTxt('availability-value', (getVal('Availability')).toFixed(0) + '%');
        setTxt('performance-value', (getVal('Performance')).toFixed(0) + '%');
        setTxt('quality-value', (getVal('Quality')).toFixed(0) + '%');

        // 3. Work Order Details
        const woNumber = data.WorkOrderNumber || data.workOrderNumber;
        const productName = data.ProductName || data.productName;
        const hasJob = !!woNumber && woNumber !== '-';

        // Sync to Global Config for Machine Actions (machine-actions.js)
        if (window.OeeConfig) {
            window.OeeConfig.hasActiveJob = hasJob;
        }

        // Toggle WO Content Visibility
        const activeContent = document.getElementById('wo-active-content');
        const emptyContent = document.getElementById('wo-empty-content');
        if (activeContent && emptyContent) {
            activeContent.style.display = hasJob ? 'block' : 'none';
            emptyContent.style.display = hasJob ? 'none' : 'block';
        }

        if (hasJob) {
            setTxt('wo-number-display', woNumber);
            setTxt('wo-product-name-display', productName);
            setTxt('current-qty', data.TotalGood ?? data.totalGood ?? 0);
            setTxt('target-qty', data.TargetQuantity ?? data.targetQuantity ?? 0);
            setTxt('total-good-wo', data.TotalGood ?? data.totalGood ?? 0);
            setTxt('total-reject-wo', data.TotalReject ?? data.totalReject ?? 0);
            setTxt('est-completion-wo', data.EstimatedCompletion || data.estimatedCompletion || '-');

            // 4. Update Product Image
            const imageUrl = data.ProductImageUrl || data.productImageUrl;
            const imgEl = document.getElementById('wo-product-image');
            if (imgEl && imageUrl && (!imgEl.src.includes(imageUrl))) {
                console.log('🖼️ Syncing Product Image:', imageUrl);
                imgEl.src = imageUrl;
            }
        }

        // 5. Timer Sync
        const lastChange = data.LastStatusChangeTime || data.lastStatusChangeTime;
        if (lastChange) {
            applyMetricsToTimer(lastChange);
        }
    }

    function applyMetricsToTimer(isoString) {
        if (!isoString) return;
        const serverTime = new Date(isoString);
        if (isNaN(serverTime)) return;

        // Only update if difference is significant (> 2s) to avoid jumping
        if (!state.lastChangeTimestamp || Math.abs(state.lastChangeTimestamp - serverTime) > 2000) {
            console.log('🔄 Syncing Timer via Metrics:', isoString);
            resetTimers(serverTime);
        }
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
            console.log('🔔 OeeUpdated received:', data);
            if (data.MachineId === config.machineId || data.machineId === config.machineId) {
                if (data.LastStatusChangeTime) {
                    applyMetricsToTimer(data.LastStatusChangeTime);
                }
                fetchTimeMetrics();
            }
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
            } else {
                // Start timer untuk item pertama
                this.startTimer();
            }

            // Submit - DISABLED: menggunakan onclick handler di HTML
            // const btnSubmit = document.getElementById('btn-submit-produksi');
            // if (btnSubmit) btnSubmit.addEventListener('click', this.handleSubmit.bind(this));

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

            // Berat Act Increment/Decrement Buttons
            const btnIncBerat = document.getElementById('btn-increment-berat-act');
            const btnDecBerat = document.getElementById('btn-decrement-berat-act');
            if (btnIncBerat) btnIncBerat.addEventListener('click', () => {
                const el = document.getElementById('input-berat-act');
                if (el) {
                    el.value = (parseFloat(el.value) || 0) + 0.5;
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    this.checkCompleteness();
                }
            });
            if (btnDecBerat) btnDecBerat.addEventListener('click', () => {
                const el = document.getElementById('input-berat-act');
                if (el) {
                    el.value = Math.max(0, (parseFloat(el.value) || 0) - 0.5);
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    this.checkCompleteness();
                }
            });
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
            const textIds = ['input-nomor-lot', 'input-lot-bo', 'input-nama-compound', 'select-man-power'];
            let complete = textIds.every(id => document.getElementById(id)?.value?.trim() && document.getElementById(id)?.value != '0');

            // Validasi Berat Act (harus > 0)
            const beratAct = parseFloat(document.getElementById('input-berat-act')?.value);
            if (!beratAct || beratAct <= 0) complete = false;

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
            console.log('⏱️ startTimer called, restore:', restore);
            if (state.durasiProduksiInterval) clearInterval(state.durasiProduksiInterval);

            if (!restore) {
                state.durasiProduksiStartTime = getAdjustedServerTime();
                localStorage.setItem('durasiProduksiStartTime', state.durasiProduksiStartTime.toISOString());
                console.log('  - New timer started at:', state.durasiProduksiStartTime);
            } else {
                const stored = localStorage.getItem('durasiProduksiStartTime');
                state.durasiProduksiStartTime = stored ? new Date(stored) : getAdjustedServerTime();
                console.log('  - Timer restored from:', state.durasiProduksiStartTime);
            }

            this.updateTimerUI();
            state.durasiProduksiInterval = setInterval(this.updateTimerUI.bind(this), 1000);
            console.log('  - Interval ID:', state.durasiProduksiInterval);
        },

        resetTimer: function () {
            if (state.durasiProduksiInterval) clearInterval(state.durasiProduksiInterval);
            state.durasiProduksiSeconds = 0;
            state.durasiProduksiStartTime = null;
            localStorage.removeItem('durasiProduksiStartTime');
            this.updateTimerUI();
        },

        updateTimerUI: function () {
            // Target: item-production-timer (in _ProductionInput.cshtml)
            const display = document.getElementById('item-production-timer');
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
            const timeString = `${hh}:${mm}:${ss}`;

            display.textContent = timeString;
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
                    if (window.bootstrap) {
                        let modal = bootstrap.Modal.getInstance(modalEl);
                        if (!modal) {
                            modal = new bootstrap.Modal(modalEl);
                        }
                        modal.hide();
                    }

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

                    // Reset fields to 0
                    goodInput.value = 0;
                    rejectInput.value = 0;
                    remarkInput.value = '';

                    // Close Modal
                    const modalEl = document.getElementById('standaloneQtyModal');
                    if (window.bootstrap) {
                        let modal = bootstrap.Modal.getInstance(modalEl);
                        if (!modal) {
                            modal = new bootstrap.Modal(modalEl);
                        }
                        modal.hide();
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

            const qtyInput = document.getElementById('input-qty');
            if (qtyInput) qtyInput.value = '0';

            const mGood = document.getElementById('input-modal-good');
            if (mGood) mGood.value = '0';
            const mNg = document.getElementById('input-modal-ng');
            if (mNg) mNg.value = '0';
            const mGood2 = document.getElementById('modal-good-qty');
            if (mGood2) mGood2.value = '0';
            const mNg2 = document.getElementById('modal-reject-qty');
            if (mNg2) mNg2.value = '0';

            const modalKet = document.getElementById('input-modal-keterangan');
            if (modalKet) modalKet.value = '';
            const modalKet2 = document.getElementById('modal-qty-keterangan');
            if (modalKet2) modalKet2.value = '';

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

    // Global submit handler untuk onclick
    window.handleProductionSubmit = async function () {
        console.log('🔵 handleProductionSubmit called');

        // Check completeness
        if (!ProductionModule.checkCompleteness()) {
            showToast('⚠️ Lengkapi semua data terlebih dahulu', 'warning');
            return;
        }

        const btn = document.getElementById('btn-submit-produksi');
        if (!btn) return;

        const original = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Menyimpan...';

        try {
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
            formData.append('goodQty', 0); // Tidak ada quantity
            formData.append('rejectQty', 0);
            formData.append('rejectReason', '');
            formData.append('__RequestVerificationToken', document.querySelector('input[name="__RequestVerificationToken"]').value);

            console.log('📤 Sending request to /Operator/SubmitProductionData');
            console.log('FormData contents:');
            for (let [key, value] of formData.entries()) {
                console.log(`  ${key}: ${value}`);
            }

            const res = await fetch('/Operator/SubmitProductionData', {
                method: 'POST',
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'  // ✅ PENTING: Agar server return JSON, bukan redirect HTML
                }
            });

            console.log('📥 Response status:', res.status, res.statusText);
            console.log('📥 Response headers:', Object.fromEntries(res.headers.entries()));

            // Check if response is OK
            if (!res.ok) {
                const errorText = await res.text();
                console.error('Server error:', errorText);
                showToast(`❌ Server error: ${res.status} ${res.statusText}`, 'error');
                btn.disabled = false;
                btn.innerHTML = original;
                return;
            }

            // Check if response is JSON
            const contentType = res.headers.get('content-type');
            if (!contentType || !contentType.includes('application/json')) {
                const htmlResponse = await res.text();
                console.error('Expected JSON but got:', htmlResponse.substring(0, 200));
                showToast('❌ Server mengembalikan response yang tidak valid', 'error');
                btn.disabled = false;
                btn.innerHTML = original;
                return;
            }

            const result = await res.json();

            if (result.success) {
                // ✅ ALERT: Konfirmasi submit sukses
                alert('✅ Data produksi berhasil disimpan!\n\nTimer durasi produksi tetap berjalan.');

                showToast('✅ Data berhasil disimpan!', 'success');
                ProductionModule.clearForm();

                // PASTIKAN timer tetap berjalan
                console.log('🔍 Checking timer status after submit...');
                console.log('  - durasiProduksiStartTime:', state.durasiProduksiStartTime);
                console.log('  - durasiProduksiInterval:', state.durasiProduksiInterval);
                console.log('  - durasiProduksiSeconds:', state.durasiProduksiSeconds);

                // Jika interval hilang, restart timer
                if (!state.durasiProduksiInterval) {
                    console.log('⚠️ Timer interval hilang! Restarting timer...');
                    ProductionModule.startTimer(true); // Restore dari localStorage
                } else {
                    console.log('✅ Timer masih berjalan');
                }

                // Trigger refresh
                if (typeof window.OeeLogic.fetchTimeMetrics === 'function') {
                    window.OeeLogic.fetchTimeMetrics();
                }
            } else {
                showToast('❌ ' + (result.message || 'Gagal menyimpan data'), 'error');
            }
        } catch (e) {
            console.error('Error detail:', e);
            showToast('❌ Error: ' + e.message, 'error');
        } finally {
            btn.disabled = false;
            btn.innerHTML = original;
        }
    }

    // --- Sub-Module: Machine Action Timer (Resume Logic) ---
    const MachineTimer = {
        interval: null,
        startTime: null,
        accumulatedSeconds: 0,

        init: function () {
            // Bind buttons
            const btnRunning = document.getElementById('btn-running');
            if (btnRunning) {
                btnRunning.addEventListener('click', () => {
                    console.log('▶️ Machine Timer Triggered: START/RESUME');
                    this.startMachineTimer();
                });
            }

            const btnStop = document.getElementById('btn-line-stop');
            if (btnStop) {
                btnStop.addEventListener('click', () => {
                    console.log('⏹️ Machine Timer Triggered: PAUSE (Line Stop)');
                    this.stopMachineTimer();
                });
            }

            const btnRest = document.getElementById('btn-rest');
            if (btnRest) {
                btnRest.addEventListener('click', () => {
                    console.log('⏹️ Machine Timer Triggered: PAUSE (Rest)');
                    this.stopMachineTimer();
                });
            }

            const btnNoLoading = document.getElementById('btn-no-loading');
            if (btnNoLoading) {
                btnNoLoading.addEventListener('click', () => {
                    console.log('⏹️ Machine Timer Triggered: PAUSE (No Loading)');
                    this.stopMachineTimer();
                });
            }
        },

        startMachineTimer: function () {
            if (this.interval) clearInterval(this.interval);

            // Resume: offset is current Time
            this.startTime = getAdjustedServerTime();
            console.log(`▶️ Timer starting. Accumulated: ${this.accumulatedSeconds}s`);

            this.updateUI();
            this.interval = setInterval(this.updateUI.bind(this), 1000);
        },

        stopMachineTimer: function () {
            if (this.interval) clearInterval(this.interval);
            this.interval = null;

            if (this.startTime) {
                const now = getAdjustedServerTime();
                const sessionSeconds = Math.max(0, Math.floor((now - this.startTime) / 1000));
                this.accumulatedSeconds += sessionSeconds;
                this.startTime = null; // Reset session start
            }
            console.log(`⏸️ Timer paused. New Accumulated: ${this.accumulatedSeconds}s`);
        },

        updateUI: function () {
            const display = document.getElementById('machine-running-timer');
            if (!display) return;

            let totalSeconds = this.accumulatedSeconds;

            if (this.startTime) {
                const now = getAdjustedServerTime();
                const currentSession = Math.max(0, Math.floor((now - this.startTime) / 1000));
                totalSeconds += currentSession;
            }

            const hh = Math.floor(totalSeconds / 3600).toString().padStart(2, '0');
            const mm = Math.floor((totalSeconds % 3600) / 60).toString().padStart(2, '0');
            const ss = (totalSeconds % 60).toString().padStart(2, '0');

            display.value = `${hh}:${mm}:${ss}`;
        }
    };

    return {
        init: init,
        resetTimers: resetTimers,
        fetchTimeMetrics: fetchTimeMetrics,
        setLastChangeTimestamp: function (ts) { applyMetricsToTimer(ts); }
    };

})();
