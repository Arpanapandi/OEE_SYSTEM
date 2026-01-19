/**
 * OEE Scanner Logic - Advanced Waterfall Implementation
 * Attempts Html5Qrcode first (QR + Barcode), falls back to Quagga2 (Barcode)
 */

(function (window, $) {
    'use strict';

    const OeeScanner = {
        state: {
            currentInput: null,
            isScanning: false,
            html5QrCode: null,
            quaggaInitialized: false,
            currentCameraId: null,
            availableCameras: [],
            cameraIndex: 0,
            scanCooldown: false,
            targetDisplayId: null
        },

        init: function () {
            console.log('🔍 OeeScanner.init()');
            this.setupListeners();
        },

        setupListeners: function () {
            // Scan Buttons
            $('#btn-scan-nomor-lot').on('click', (e) => {
                e.preventDefault();
                this.open('input-nomor-lot', 'Nomor Lot');
            });
            $('#btn-scan-lot-bo').on('click', (e) => {
                e.preventDefault();
                this.open('input-lot-bo', 'Lot BO');
            });
            $('#btn-scan-nama-compound').on('click', (e) => {
                e.preventDefault();
                this.open('input-nama-compound', 'Nama Compound');
            });

            // Modal Controls
            $('#btn-stop-scanner').on('click', () => this.stop());
            $('#scannerModal').on('hidden.bs.modal', () => this.stop());

            // Switch Camera
            $('#btn-switch-camera').on('click', () => this.switchCamera());

            // Berat Act Buttons Logic
            this.setupBeratActButtons();
        },

        setupBeratActButtons: function () {
            console.log('⚖️ Setting up Berat Act buttons...');

            // Event delegation for plus/minus buttons (more robust)
            $(document).on('click', '#btn-increment-berat-act', (e) => {
                e.preventDefault();
                this.adjustBeratAct(0.01);
            });

            $(document).on('click', '#btn-decrement-berat-act', (e) => {
                e.preventDefault();
                this.adjustBeratAct(-0.01);
            });
        },

        adjustBeratAct: function (delta) {
            const $input = $('#input-berat-act');
            if ($input.length) {
                const currentValue = parseFloat($input.val()) || 0;
                const newValue = Math.max(0, currentValue + delta);
                $input.val(newValue.toFixed(2));

                // Trigger events for validation/ui
                $input.trigger('input');
                $input.trigger('change');

                console.log(`⚖️ Berat Act adjusted: ${newValue.toFixed(2)}`);

                if (typeof window.checkAllInputsComplete === 'function') {
                    window.checkAllInputsComplete();
                }
            }
        },

        open: function (inputId, label) {
            console.log(`📸 Opening scanner for: ${label} (${inputId})`);
            this.state.targetDisplayId = inputId;
            this.state.currentInput = $(`#${inputId}`);
            $('#scanner-target-label').text(label);

            const modalEl = document.getElementById('scannerModal');
            if (!modalEl) {
                console.error('❌ scannerModal not found in DOM');
                return;
            }

            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();

            // Auto-start when modal is ready
            $(modalEl).one('shown.bs.modal', () => {
                this.initializeScanner();
            });
        },

        initializeScanner: async function () {
            try {
                console.log('🔧 Initializing camera access...');

                // 1. Check Browser Support
                if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                    throw new Error('Browser tidak mendukung akses kamera. Gunakan HTTPS/localhost.');
                }

                // 2. Request Permission Explicitly
                console.log('📷 Requesting camera permission...');
                const stream = await navigator.mediaDevices.getUserMedia({
                    video: { facingMode: 'environment' }
                });

                // Stop initial stream
                stream.getTracks().forEach(track => track.stop());
                console.log('✅ Camera permission granted');

                // 3. Enumerate Cameras
                const devices = await navigator.mediaDevices.enumerateDevices();
                this.state.availableCameras = devices.filter(device => device.kind === 'videoinput');
                console.log(`📷 Found ${this.state.availableCameras.length} cameras`);

                // 4. Select Back Camera by default
                if (this.state.availableCameras.length > 0) {
                    const backCamera = this.state.availableCameras.find(cam =>
                        /back|rear|environment/i.test(cam.label)
                    );

                    if (backCamera) {
                        this.state.currentCameraId = backCamera.deviceId;
                        this.state.cameraIndex = this.state.availableCameras.indexOf(backCamera);
                    } else {
                        // Fallback to second camera if available, assuming it might be back
                        this.state.cameraIndex = this.state.availableCameras.length > 1 ? 1 : 0;
                        this.state.currentCameraId = this.state.availableCameras[this.state.cameraIndex].deviceId;
                    }

                    // Show switch button if multiple cameras
                    if (this.state.availableCameras.length > 1) {
                        $('#btn-switch-camera').show();
                    } else {
                        $('#btn-switch-camera').hide();
                    }
                }

                // 5. Start scanning logic
                await this.startScanning();

            } catch (err) {
                console.error('❌ Scanner Init Error:', err);
                if (window.showToast) window.showToast(err.message, 'error');
                else alert('❌ ' + err.message);

                // Close modal on fatal error
                const modal = bootstrap.Modal.getInstance(document.getElementById('scannerModal'));
                if (modal) modal.hide();
            }
        },

        startScanning: async function () {
            const readerId = "scanner-reader";
            const $reader = $('#' + readerId);
            if (!$reader.length) return;

            // Waterfall Step 1: Html5Qrcode (Primary)
            if (typeof window.Html5Qrcode !== 'undefined') {
                try {
                    console.log('🚀 Starting Html5Qrcode...');
                    this.state.html5QrCode = new window.Html5Qrcode(readerId);

                    const config = {
                        fps: 10,
                        qrbox: { width: 250, height: 250 },
                        aspectRatio: 1.0,
                        // Support common formats
                        formatsToSupport: [
                            Html5QrcodeSupportedFormats.QR_CODE,
                            Html5QrcodeSupportedFormats.CODE_128,
                            Html5QrcodeSupportedFormats.CODE_39
                        ]
                    };

                    const cameraSource = this.state.currentCameraId
                        ? { deviceId: { exact: this.state.currentCameraId } }
                        : { facingMode: "environment" };

                    await this.state.html5QrCode.start(
                        cameraSource,
                        config,
                        (decodedText) => this.handleSuccess(decodedText)
                    );

                    this.state.isScanning = true;
                    console.log('✅ Html5Qrcode started');
                    return;

                } catch (err) {
                    console.warn('⚠️ Html5Qrcode failed/stopped, trying Quagga...', err);
                    if (this.state.html5QrCode) {
                        try { await this.state.html5QrCode.stop(); } catch (e) { }
                        this.state.html5QrCode = null;
                    }
                }
            }

            // Waterfall Step 2: Quagga2 (Fallback)
            if (typeof window.Quagga !== 'undefined') {
                try {
                    console.log('🚀 Starting Quagga...');
                    $reader.html('<div id="quagga-reader" style="width:100%; height:100%;"></div>');

                    window.Quagga.init({
                        inputStream: {
                            name: "Live",
                            type: "LiveStream",
                            target: document.querySelector('#quagga-reader'),
                            constraints: {
                                width: 640,
                                height: 480,
                                facingMode: "environment",
                                deviceId: this.state.currentCameraId ? { exact: this.state.currentCameraId } : undefined
                            }
                        },
                        decoder: {
                            readers: ["code_128_reader", "code_39_reader", "ean_reader"]
                        }
                    }, (err) => {
                        if (err) {
                            console.error('❌ Quagga init failed:', err);
                            return;
                        }
                        window.Quagga.start();
                        this.state.quaggaInitialized = true;
                        this.state.isScanning = true;
                        console.log('✅ Quagga started');
                    });

                    window.Quagga.onDetected((res) => {
                        if (res.codeResult && res.codeResult.code) {
                            this.handleSuccess(res.codeResult.code);
                        }
                    });

                } catch (err) {
                    console.error('❌ Quagga fallback failed:', err);
                }
            }
        },

        switchCamera: async function () {
            if (this.state.availableCameras.length < 2) return;

            console.log('🔄 Switching camera...');
            await this.stop(false); // Stop but don't reset all state

            this.state.cameraIndex = (this.state.cameraIndex + 1) % this.state.availableCameras.length;
            this.state.currentCameraId = this.state.availableCameras[this.state.cameraIndex].deviceId;

            console.log(`📷 Switched to camera: ${this.state.availableCameras[this.state.cameraIndex].label}`);
            await this.startScanning();
        },

        handleSuccess: function (code) {
            if (this.state.scanCooldown) return;
            this.state.scanCooldown = true;

            console.log('🎯 Scanned Successfully:', code);

            // Populate target
            if (this.state.currentInput) {
                this.state.currentInput.val(code);
                this.state.currentInput.addClass('is-valid');
                this.state.currentInput.trigger('input');
                this.state.currentInput.trigger('change');
            }

            if (window.showToast) window.showToast(`✅ Scanned: ${code}`, 'success');

            // Close modal after delay to show success
            setTimeout(() => {
                this.stop();
                const modal = bootstrap.Modal.getInstance(document.getElementById('scannerModal'));
                if (modal) modal.hide();
                this.state.scanCooldown = false;
            }, 500);

            // Trigger external logic if any (like checkAndLoadKomponen)
            if (typeof window.checkAndLoadKomponen === 'function') {
                window.checkAndLoadKomponen();
            }
        },

        stop: async function (fullReset = true) {
            console.log('🛑 Stopping scanner...');

            // Stop Html5QrCode
            if (this.state.html5QrCode) {
                try {
                    await this.state.html5QrCode.stop();
                    console.log('✅ Html5QrCode stopped');
                } catch (err) {
                    console.warn('⚠️ Error stopping Html5QrCode:', err);
                }
                this.state.html5QrCode = null;
            }

            // Stop Quagga
            if (this.state.quaggaInitialized) {
                try {
                    window.Quagga.stop();
                    console.log('✅ Quagga stopped');
                } catch (err) {
                    console.warn('⚠️ Error stopping Quagga:', err);
                }
                this.state.quaggaInitialized = false;
            }

            // UI Cleanup
            $('#scanner-reader').empty();
            this.state.isScanning = false;

            if (fullReset) {
                this.state.availableCameras = [];
                this.state.currentCameraId = null;
            }
        }
    };

    window.OeeScanner = OeeScanner;
    $(document).ready(() => OeeScanner.init());

})(window, jQuery);
