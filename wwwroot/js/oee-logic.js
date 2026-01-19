/**
 * OEE Detail Logic v2.0 - Refactored for Industrial Standards
 * Features:
 * 1. UI State Machine with strict status management
 * 2. Time-stamp based duration calculations (no double counting)
 * 3. Persistence for Operator Data (Man Power, Injection)
 * 4. Production Scanner Stability
 */

(function (window, $) {
    'use strict';

    const OeeApp = {
        config: {
            machineId: '',
            antiforgeryToken: '',
            refreshInterval: 30000 // 30s fallback refresh
        },

        state: {
            status: 'IDLE', // RUNNING, REST_BREAK, LINE_STOP, NO_LOADING, IDLE
            lastStatusChange: null,
            isNoLoading: false,
            timers: {
                main: null,
                production: null
            },
            production: {
                startTime: null,
                elapsedSeconds: 0
            }
        },

        init: function (config) {
            this.config = Object.assign(this.config, config);
            console.log('🚀 OeeApp Initialized:', this.config);

            this.restorePersistence();
            this.setupEventListeners();
            this.initSignalR();
            this.refreshData();

            // Start the main clock UI
            this.startMainUIClock();
        },

        // ========== PERSISTENCE ==========
        savePersistence: function () {
            const data = {
                manPowerId: $('#select-man-power').val(),
                injection: $('input[name="injection"]:checked').val()
            };
            localStorage.setItem(`oee_persist_${this.config.machineId}`, JSON.stringify(data));
            console.log('💾 Persistence saved:', data);
        },

        restorePersistence: function () {
            const stored = localStorage.getItem(`oee_persist_${this.config.machineId}`);
            if (stored) {
                const data = JSON.parse(stored);
                if (data.manPowerId) $('#select-man-power').val(data.manPowerId);
                if (data.injection) {
                    $(`#injection-${data.injection}`).prop('checked', true);
                }
                console.log('📂 Persistence restored:', data);
            }
        },

        // ========== UI UPDATES ==========
        updateUIState: function (data) {
            // data contains: MachineStatus, ActiveDowntimeDescription, LastStatusChangeTime, etc.
            const statusBadge = $('#machine-status-badge');
            const downtiDesc = $('#downtime-description');
            const navbarDot = $('#navbar-status-dot');
            const navbarText = $('#navbar-status-text');

            const isAktif = data.MachineStatus === 'Aktif' && !data.ActiveDowntimeDescription;
            const statusText = isAktif ? 'RUNNING' : (data.ActiveDowntimeDescription || 'IDLE');

            // Update State
            this.state.status = isAktif ? 'RUNNING' : (data.ActiveDowntimeDescription === 'No Loading' ? 'NO_LOADING' : (data.ActiveDowntimeDescription === 'Rest Break' ? 'REST_BREAK' : 'LINE_STOP'));
            this.state.lastStatusChange = new Date(data.LastStatusChangeTime);

            // UI Visuals
            statusBadge.text(isAktif ? 'AKTIF' : 'TIDAK AKTIF');
            statusBadge.removeClass('bg-success bg-warning').addClass(isAktif ? 'bg-success' : 'bg-warning');

            if (data.ActiveDowntimeDescription) {
                downtiDesc.text(data.ActiveDowntimeDescription).show();
            } else {
                downtiDesc.hide();
            }

            if (navbarDot.length && navbarText.length) {
                navbarText.text(isAktif ? 'AKTIF' : (data.ActiveDowntimeDescription ? 'DOWNTIME' : 'IDLE'));
                const color = isAktif ? '#28a745' : (data.ActiveDowntimeDescription ? '#dc3545' : '#ffc107');
                navbarDot.css('background', color).css('box-shadow', `0 0 10px ${color}`);
            }

            this.updateButtonStates();
            this.startMachineTimer();

            // Handle No Loading UI behavior (shroud the inputs)
            const currentJobCard = $('#current-job-card');
            if (data.ActiveDowntimeDescription === 'No Loading') {
                currentJobCard.addClass('opacity-25').css('pointer-events', 'none');
            } else {
                currentJobCard.removeClass('opacity-25').css('pointer-events', 'auto');
            }
        },

        updateButtonStates: function () {
            const status = this.state.status;
            const btns = {
                running: $('#btn-running'),
                rest: $('#btn-rest'),
                lineStop: $('#btn-line-stop'),
                noLoading: $('#btn-no-loading')
            };

            // ✅ PERBAIKAN: Reset all buttons to enabled first
            $('.machine-action-btn').prop('disabled', false).removeClass('disabled');

            // ✅ PERBAIKAN: Hanya disable tombol yang sesuai dengan status AKTIF saat ini
            // Tombol Running HANYA disabled saat status RUNNING
            // Tombol lain disabled sesuai status mereka
            switch (status) {
                case 'RUNNING':
                    btns.running.prop('disabled', true).addClass('disabled');
                    break;
                case 'REST_BREAK':
                    btns.rest.prop('disabled', true).addClass('disabled');
                    // ✅ CRITICAL: Running button HARUS enabled agar bisa stop Rest Break
                    btns.running.prop('disabled', false).removeClass('disabled');
                    break;
                case 'LINE_STOP':
                    btns.lineStop.prop('disabled', true).addClass('disabled');
                    // ✅ CRITICAL: Running button HARUS enabled agar bisa stop Line Stop
                    btns.running.prop('disabled', false).removeClass('disabled');
                    break;
                case 'NO_LOADING':
                    btns.noLoading.prop('disabled', true).addClass('disabled');
                    // ✅ CRITICAL: Running button HARUS enabled agar bisa stop No Loading
                    btns.running.prop('disabled', false).removeClass('disabled');
                    break;
            }

            console.log('🔄 Button states updated. Status:', status, 'Running enabled:', !btns.running.prop('disabled'));
        },

        // ========== TIMERS ==========
        startMachineTimer: function () {
            if (this.state.timers.main) clearInterval(this.state.timers.main);

            const display = $('#since-last-status');
            if (!display.length) return;

            const update = () => {
                if (!this.state.lastStatusChange) return;
                const now = new Date();
                const diff = Math.floor((now - this.state.lastStatusChange) / 1000);
                display.text(this.formatTime(diff));
            };

            this.state.timers.main = setInterval(update, 1000);
            update();
        },

        startProductionTimer: function (resume = false) {
            if (this.state.timers.production) clearInterval(this.state.timers.production);

            const display = $('#durasi-produksi-display');
            const hidden = $('#hidden-durasi-seconds');
            const statusText = $('#timer-status-text');

            if (!resume) {
                this.state.production.startTime = new Date();
                localStorage.setItem(`oee_prod_start_${this.config.machineId}`, this.state.production.startTime.toISOString());
            } else {
                const stored = localStorage.getItem(`oee_prod_start_${this.config.machineId}`);
                this.state.production.startTime = stored ? new Date(stored) : new Date();
            }

            const update = () => {
                const now = new Date();
                const diff = Math.floor((now - this.state.production.startTime) / 1000);
                this.state.production.elapsedSeconds = diff;
                if (display.length) display.val(this.formatTime(diff));
                if (hidden.length) hidden.val(diff);
            };

            this.state.timers.production = setInterval(update, 1000);
            update();

            if (statusText.length) {
                statusText.html('<i class="fa-solid fa-spinner fa-spin me-1"></i>Running').attr('class', 'small text-primary mt-1 fw-bold');
            }
        },

        stopProductionTimer: function () {
            if (this.state.timers.production) clearInterval(this.state.timers.production);
            this.state.timers.production = null;
            this.state.production.startTime = null;
            localStorage.removeItem(`oee_prod_start_${this.config.machineId}`);

            const statusText = $('#timer-status-text');
            if (statusText.length) {
                statusText.html('<i class="fa-solid fa-stop-circle me-1"></i>Stopped').attr('class', 'small text-danger mt-1 fw-bold');
            }
        },

        formatTime: function (seconds) {
            if (isNaN(seconds) || seconds < 0) return "00:00:00";
            const h = Math.floor(seconds / 3600).toString().padStart(2, '0');
            const m = Math.floor((seconds % 3600) / 60).toString().padStart(2, '0');
            const s = (seconds % 60).toString().padStart(2, '0');
            return `${h}:${m}:${s}`;
        },

        startMainUIClock: function () {
            const clockEl = $('#realtimeClock');
            if (!clockEl.length) return;
            setInterval(() => {
                const now = new Date();
                clockEl.text(now.getHours().toString().padStart(2, '0') + ':' +
                    now.getMinutes().toString().padStart(2, '0') + ':' +
                    now.getSeconds().toString().padStart(2, '0'));
            }, 1000);
        },

        // ========== ACTIONS ==========
        validateInputs: function () {
            const mp = $('#select-man-power').val();
            const inj = $('input[name="injection"]:checked').val();
            if (!mp || !inj) {
                alert('⚠️ Harap pilih Man Power dan Group Injection terlebih dahulu!');
                return false;
            }
            return true;
        },

        handleAction: async function (action, data = {}, event = null) {
            // ✅ PERBAIKAN: Hanya validasi untuk action selain 'Start' (Running)
            // Running harus bisa diklik untuk menghentikan Rest Break/Line Stop/No Loading
            if (action !== 'Start' && !this.validateInputs()) return;

            // Get button from event or fallback to selector
            let btn;
            if (event && event.currentTarget) {
                btn = $(event.currentTarget);
            } else {
                // Fallback: find button by action type
                const btnMap = {
                    'Start': '#btn-running',
                    'Rest': '#btn-rest',
                    'LineStop': '#btn-line-stop',
                    'NoLoading': '#btn-no-loading'
                };
                btn = $(btnMap[action] || '#btn-running');
            }

            const originalHtml = btn.html();
            btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin"></i>');

            try {
                const fd = new FormData();
                fd.append('machineId', this.config.machineId);
                fd.append('manPowerId', $('#select-man-power').val());
                fd.append('injection', $('input[name="injection"]:checked').val());
                fd.append('__RequestVerificationToken', this.config.antiforgeryToken);

                for (const key in data) fd.append(key, data[key]);

                const response = await fetch(`/Operator/${action}`, {
                    method: 'POST',
                    body: fd,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                const result = await response.json();
                if (result.success) {
                    if (action === 'Start') {
                        this.startProductionTimer();
                    } else {
                        this.stopProductionTimer();
                    }
                    this.refreshData();
                    if (window.showToast) window.showToast('✅ Status updated', 'success');
                } else {
                    alert('❌ Error: ' + result.message);
                }
            } catch (err) {
                console.error(err);
                alert('❌ Connection Error');
            } finally {
                btn.prop('disabled', false).html(originalHtml);
            }
        },

        submitProductionData: async function () {
            if (!this.validateInputs()) return;

            const lot = $('#input-nomor-lot').val();
            const bo = $('#input-lot-bo').val();
            const berat = $('#input-berat-act').val();

            if (!lot && !bo) { alert('⚠️ Nomor Lot / Lot BO harus diisi!'); return; }
            if (!berat) { alert('⚠️ Berat Act harus diisi!'); return; }

            const btn = $('#btn-submit-produksi');
            const originalHtml = btn.html();
            btn.prop('disabled', true).text('Sending...');

            try {
                const fd = new FormData();
                fd.append('machineId', this.config.machineId);
                fd.append('manPowerId', $('#select-man-power').val());
                fd.append('injection', $('input[name="injection"]:checked').val());
                fd.append('nomorLot', lot);
                fd.append('lotBo', bo);
                fd.append('namaCompound', $('#input-nama-compound').val());
                fd.append('beratAct', berat);
                fd.append('penipisan', $('input[name="penipisan"]:checked').val() || '');
                fd.append('keterangan', $('#select-keterangan').val());
                fd.append('durasiProduksiSeconds', this.state.production.elapsedSeconds);
                fd.append('qty', 1);

                const response = await fetch('/Operator/SubmitProductionData', {
                    method: 'POST',
                    body: fd
                });

                const result = await response.json();
                if (result.success) {
                    alert('✅ Data berhasil disimpan');
                    this.resetProductionForm();
                    this.startProductionTimer(); // Restart for next item
                    this.refreshData();
                } else {
                    alert('❌ GAGAL: ' + result.message);
                }
            } catch (err) {
                console.error(err);
                alert('❌ Connection Error');
            } finally {
                btn.prop('disabled', false).html(originalHtml);
            }
        },

        resetProductionForm: function () {
            $('#input-nomor-lot, #input-lot-bo, #input-nama-compound, #input-berat-act').val('');
            $('#select-keterangan').val('');
            $('input[name="penipisan"]').prop('checked', false);
        },

        refreshData: async function () {
            try {
                const resp = await fetch(`/Operator/GetOperatorData?machineId=${this.config.machineId}`);
                if (!resp.ok) return;
                const data = await resp.json();
                this.updateUIState(data);
                this.updateTableData(data);
                this.updateKpiCards(data);
            } catch (e) { console.error('Refresh error:', e); }
        },

        updateKpiCards: function (data) {
            $('#oee-value').text(Math.round(data.Oee || 0) + '%');
            $('#availability-value').text(Math.round(data.Availability || 0) + '%');
            $('#performance-value').text(Math.round(data.Performance || 0) + '%');
            $('#quality-value').text(Math.round(data.Quality || 0) + '%');
            $('#total-good').text(data.TotalGood);
            $('#total-reject').text(data.TotalReject);

            const progress = data.TargetQuantity > 0 ? (data.TotalGood / data.TargetQuantity * 100) : 0;
            $('#progress-bar').css('width', progress + '%');
            $('#progress-text').text(`${data.TotalGood} / ${data.TargetQuantity}`);
        },

        updateTableData: function (data) {
            const tbody = $('#recent-downtime-tbody');
            if (!tbody.length || !data.RecentDowntimes) return;
            tbody.empty();
            data.RecentDowntimes.forEach(dt => {
                const start = new Date(dt.StartTime).toLocaleString();
                const duration = dt.IsClosed ? this.formatTime(dt.DurationSeconds) : 'Ongoing';
                tbody.append(`
                    <tr>
                        <td>
                            <div class="small">${dt.ReasonDescription || '-'}</div>
                            <div class="text-secondary" style="font-size: 0.7rem;">${dt.ReasonCategory || '-'}</div>
                        </td>
                        <td class="small">${start}</td>
                        <td><span class="${dt.IsClosed ? 'text-success' : 'text-danger'}">${duration}</span></td>
                        <td><span class="badge ${dt.IsClosed ? 'bg-success' : 'bg-danger'}">${dt.IsClosed ? 'Closed' : 'Active'}</span></td>
                    </tr>
                `);
            });
        },

        // ========== SCW LOGIC ==========
        initScw: function () {
            $('#scw-4m-type').on('change', (e) => {
                const parentId = $(e.currentTarget).val();
                const remarkSelect = $('#scw-remark');

                remarkSelect.val('').find('option:not(:first)').hide();

                if (parentId) {
                    remarkSelect.find(`option[data-parent-id="${parentId}"]`).show();
                    remarkSelect.find('option:first').text('-- Pilih Remark --');
                } else {
                    remarkSelect.find('option:first').text('-- Pilih Jenis 4M terlebih dahulu --');
                }
            });

            $('#btn-scw-submit').on('click', () => this.submitScw());
        },

        submitScw: async function () {
            const typeId = $('#scw-4m-type').val();
            const remarkId = $('#scw-remark').val();

            if (!typeId || !remarkId) {
                alert('⚠️ Harap pilih Jenis 4M dan Remark!');
                return;
            }

            const btn = $('#btn-scw-submit');
            const originalHtml = btn.html();
            btn.prop('disabled', true).text('Saving...');

            try {
                const fd = new FormData();
                fd.append('machineId', this.config.machineId);
                fd.append('scw4MTypeId', typeId);
                fd.append('scwRemarkId', remarkId);

                const resp = await fetch('/Operator/SubmitSCW', {
                    method: 'POST',
                    body: fd
                });

                const result = await resp.json();
                if (result.success) {
                    alert('✅ SCW berhasil disimpan');
                    $('#scw-4m-type, #scw-remark').val('').trigger('change');
                    this.refreshData();
                } else {
                    alert('❌ GAGAL: ' + result.message);
                }
            } catch (err) {
                console.error(err);
                alert('❌ Connection Error');
            } finally {
                btn.prop('disabled', false).html(originalHtml);
            }
        },

        submitQuantity: async function (form) {
            const formData = new FormData(form);
            formData.append('injection', $('input[name="injection"]:checked').val() || '');
            formData.append('manPowerId', $('#select-man-power').val() || '');

            const btn = $(form).find('button[type="submit"]');
            const originalHtml = btn.html();
            btn.prop('disabled', true).text('Saving...');

            try {
                const resp = await fetch('/Operator/AddQuantity', {
                    method: 'POST',
                    body: formData
                });

                const result = await resp.json();
                if (result.success) {
                    alert('✅ Quantity updated');
                    bootstrap.Modal.getInstance($(form).closest('.modal')[0]).hide();
                    form.reset();
                    this.refreshData();
                } else {
                    alert('❌ GAGAL: ' + result.message);
                }
            } catch (err) {
                console.error(err);
                alert('❌ Connection Error');
            } finally {
                btn.prop('disabled', false).html(originalHtml);
            }
        },

        // ========== SIGNALR ==========
        initSignalR: function () {
            if (window.oeeHubConnection) return;
            const connection = new signalR.HubConnectionBuilder()
                .withUrl("/oeeHub")
                .withAutomaticReconnect()
                .build();

            connection.on("OeeUpdated", (data) => {
                console.log("📡 SignalR Update:", data);
                this.refreshData();
            });

            connection.start().catch(err => console.error("SignalR Connection Error:", err));
            window.oeeHubConnection = connection;
        },

        setupEventListeners: function () {
            $('#select-man-power, input[name="injection"]').on('change', () => this.savePersistence());

            $('#btn-running').on('click', (e) => this.handleAction('Start', {}, e));

            // Handlers exposed to window for HTML onclick compatibility
            window.handleRestClick = (btn) => {
                const reasonId = $(btn).closest('form').find('[name="reasonId"]').val();
                this.handleAction('Rest', { reasonId });
            };

            window.handleLineStopClick = (btn) => {
                const reasonId = $(btn).closest('form').find('[name="reasonId"]').val() || $(btn).closest('.modal').find('select[name="reasonId"]').val();
                if (!reasonId) { alert('Pilih alasan!'); return; }
                this.handleAction('LineStop', { reasonId });
                const modalEl = $(btn).closest('.modal')[0];
                if (modalEl) bootstrap.Modal.getInstance(modalEl).hide();
            };

            window.handleNoLoadingClick = (btn) => {
                this.handleAction('NoLoading');
                const modalEl = $(btn).closest('.modal')[0];
                if (modalEl) bootstrap.Modal.getInstance(modalEl).hide();
            };

            $('#btn-submit-produksi').on('click', () => this.submitProductionData());
            $('#btn-reset-durasi').on('click', () => this.stopProductionTimer());

            $('#add-qty-form').on('submit', (e) => {
                e.preventDefault();
                this.submitQuantity(e.currentTarget);
            });

            this.initScw();
        }
    };

    window.OeeApp = OeeApp;

})(window, jQuery);
