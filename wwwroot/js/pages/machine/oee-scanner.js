/**
 * oee-scanner.js
 * Handles Camera access, Barcode (Quagga2), and QR Code (html5-qrcode) scanning.
 * Implements waterfall strategy and auto-fill logic.
 */

(function () {
    let quaggaInitialized = false;
    let html5QrCode = null;
    let currentScanTarget = null; // 'lot-bo', 'nomor-lot', 'nama-compound'
    let scannerLibraryLoaded = false;
    let qrCodeLibraryLoaded = false;
    let availableCameras = [];
    let currentCameraId = null;
    let cameraIndex = 0;
    let scanCooldown = false;

    // Load Libraries
    function loadScannerLibrary() {
        return new Promise((resolve, reject) => {
            if (typeof Quagga !== 'undefined') {
                scannerLibraryLoaded = true;
                resolve();
                return;
            }
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://unpkg.com/quagga@0.12.1/dist/quagga.min.css';
            document.head.appendChild(link);

            const script = document.createElement('script');
            script.src = 'https://unpkg.com/quagga@0.12.1/dist/quagga.min.js';
            script.async = true;
            script.onload = () => {
                scannerLibraryLoaded = true;
                console.log('✅ Quagga2 library loaded');
                resolve();
            };
            script.onerror = () => reject(new Error('Failed to load Quagga2'));
            document.head.appendChild(script);
        });
    }

    function loadQrCodeLibrary() {
        return new Promise((resolve, reject) => {
            if (typeof Html5Qrcode !== 'undefined') {
                qrCodeLibraryLoaded = true;
                resolve();
                return;
            }
            const script = document.createElement('script');
            script.src = 'https://unpkg.com/html5-qrcode@2.3.8/html5-qrcode.min.js';
            script.async = true;
            script.onload = () => {
                qrCodeLibraryLoaded = true;
                console.log('✅ html5-qrcode library loaded');
                resolve();
            };
            script.onerror = () => reject(new Error('Failed to load html5-qrcode'));
            document.head.appendChild(script);
        });
    }

    // Initialize
    document.addEventListener('DOMContentLoaded', function () {
        console.log('🔧 Initializing scanner module...');

        // Button Event Listeners
        const scanButtons = {
            'btn-scan-nomor-lot': 'nomor-lot',
            'btn-scan-lot-bo': 'lot-bo',
            'btn-scan-nama-compound': 'nama-compound'
        };

        for (const [btnId, target] of Object.entries(scanButtons)) {
            const btn = document.getElementById(btnId);
            if (btn) {
                btn.addEventListener('click', function (e) {
                    e.preventDefault();
                    currentScanTarget = target;
                    openScanner();
                });
            }
        }

        // Edit Buttons
        const editMappings = {
            'btn-edit-nomor-lot': 'input-nomor-lot',
            'btn-edit-lot-bo': 'input-lot-bo',
            'btn-edit-nama-compound': 'input-nama-compound'
        };
        Object.keys(editMappings).forEach(btnId => {
            const btn = document.getElementById(btnId);
            if (btn) {
                btn.addEventListener('click', () => {
                    const input = document.getElementById(editMappings[btnId]);
                    if (input) {
                        input.disabled = false;
                        input.focus();
                        input.select();
                    }
                });
            }
        });

        // Stop Scanner & Modal Events
        const btnStopScanner = document.getElementById('btn-stop-scanner');
        if (btnStopScanner) btnStopScanner.addEventListener('click', stopScanner);

        const scannerModal = document.getElementById('scannerModal');
        if (scannerModal) {
            scannerModal.addEventListener('hidden.bs.modal', () => {
                stopScanner();
                const backdrop = document.getElementById('scannerModalBackdrop');
                if (backdrop) backdrop.remove();
            });
        }
    });

    // Validations & Success Handler
    async function handleScanSuccess(decodedText) {
        if (!decodedText) return;
        decodedText = decodedText.trim();

        scanCooldown = true;
        setTimeout(() => { scanCooldown = false; }, 2000);

        console.log('✅ Scan success:', decodedText, 'Target:', currentScanTarget);

        if ((currentScanTarget === 'lot-bo' || currentScanTarget === 'nomor-lot') && decodedText.length > 12) {
            alert(`Code terlalu panjang (max 12): ${decodedText}`);
            return;
        }

        // Validation for Lot BO
        if (currentScanTarget === 'lot-bo') {
            try {
                const resp = await fetch(`/api/Scanner/komponen/validate/${encodeURIComponent(decodedText)}`);
                const res = await resp.json();
                if (!res.success) {
                    alert('Lot BO tidak ditemukan: ' + decodedText);
                    return;
                }
            } catch (e) {
                // Network error or offline, allow manual check or warn
                console.warn('Validation error', e);
            }
        }

        stopScanner();

        // Close Modal
        const scannerModalEl = document.getElementById('scannerModal');
        if (scannerModalEl) {
            const modal = bootstrap.Modal.getInstance(scannerModalEl);
            if (modal) modal.hide();
        }

        // Fill Input
        let inputId;
        if (currentScanTarget === 'lot-bo') inputId = 'input-lot-bo';
        else if (currentScanTarget === 'nomor-lot') inputId = 'input-nomor-lot';
        else if (currentScanTarget === 'nama-compound') inputId = 'input-nama-compound';

        if (inputId) {
            const input = document.getElementById(inputId);
            if (input) {
                input.disabled = false;
                input.readOnly = false;
                input.value = decodedText;

                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));

                input.classList.add('border-success');
                setTimeout(() => input.classList.remove('border-success'), 2000);

                if (typeof window.showToast === 'function') window.showToast(`Scan Berhasil: ${decodedText}`, 'success');

                // Trigger Logic
                if (currentScanTarget === 'lot-bo' || currentScanTarget === 'nomor-lot') {
                    setTimeout(() => {
                        if (typeof window.checkAndLoadKomponen === 'function') window.checkAndLoadKomponen();
                    }, 500);
                }
            }
        }
    }

    // Core Scanner Logic
    window.openScanner = async function () {
        if (!document.getElementById('scannerModal')) return;

        // Reset UI
        const scannerReader = document.getElementById('scanner-reader');
        if (scannerReader) {
            scannerReader.innerHTML = '<video id="scanner-video" style="width:100%;height:auto;"></video>';
        }

        // Load libs
        if (!scannerLibraryLoaded) try { await loadScannerLibrary(); } catch (e) { }
        if (!qrCodeLibraryLoaded) try { await loadQrCodeLibrary(); } catch (e) { }

        const initializeScanner = async () => {
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                alert('Browser tidak support kamera (HTTPS required).');
                return;
            }
            try {
                // Get Cameras
                const devices = await navigator.mediaDevices.enumerateDevices();
                availableCameras = devices.filter(d => d.kind === 'videoinput');

                // Select Rear Camera
                let selectedId = null;
                if (availableCameras.length > 0) {
                    const back = availableCameras.find(c => /back|rear|environment/i.test(c.label));
                    selectedId = back ? back.deviceId : availableCameras[0].deviceId;
                }
                currentCameraId = selectedId;

                const btnSwitch = document.getElementById('btn-switch-camera');
                if (btnSwitch) {
                    btnSwitch.style.display = availableCameras.length > 1 ? 'inline-block' : 'none';
                    btnSwitch.onclick = switchCamera;
                }

                await startScanning();
            } catch (e) {
                alert('Akses kamera ditolak.');
            }
        };

        // Show Modal
        const modalEl = document.getElementById('scannerModal');
        const modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
        modalEl.addEventListener('shown.bs.modal', initializeScanner, { once: true });
        modal.show();
    };

    window.switchCamera = async function () {
        if (availableCameras.length <= 1) return;
        await stopScannerInternal();

        cameraIndex = (cameraIndex + 1) % availableCameras.length;
        currentCameraId = availableCameras[cameraIndex].deviceId;
        await startScanning();
    };

    async function startScanning() {
        const scannerReader = document.getElementById('scanner-reader');
        const container = document.getElementById('scanner-container');
        const width = container ? container.offsetWidth : 640;

        // 1. Html5Qrcode
        if (typeof Html5Qrcode !== 'undefined') {
            try {
                html5QrCode = new Html5Qrcode("scanner-reader");
                await html5QrCode.start(
                    currentCameraId ? { deviceId: { exact: currentCameraId } } : { facingMode: "environment" },
                    { fps: 10, qrbox: { width: 250, height: 250 } },
                    (text) => { if (!scanCooldown) handleScanSuccess(text); }
                );
                return;
            } catch (e) {
                // Fallback
                if (html5QrCode) { try { await html5QrCode.stop(); html5QrCode.clear(); } catch (x) { } }
            }
        }

        // 2. Quagga2
        if (typeof Quagga !== 'undefined') {
            scannerReader.innerHTML = '<div id="quagga-reader" style="width:100%;height:100%"></div>';
            Quagga.init({
                inputStream: {
                    name: "Live", type: "LiveStream",
                    target: document.querySelector('#quagga-reader'),
                    constraints: {
                        width: width, height: 480,
                        deviceId: currentCameraId ? { exact: currentCameraId } : undefined,
                        facingMode: "environment"
                    }
                },
                decoder: { readers: ["code_128_reader", "ean_reader", "code_39_reader"] }
            }, (err) => {
                if (!err) {
                    Quagga.start();
                    quaggaInitialized = true;
                    Quagga.onDetected((res) => {
                        if (!scanCooldown && res.codeResult.code) handleScanSuccess(res.codeResult.code);
                    });
                }
            });
        }
    }

    async function stopScannerInternal() {
        if (quaggaInitialized && typeof Quagga !== 'undefined') {
            try { Quagga.stop(); Quagga.offDetected(); quaggaInitialized = false; } catch (e) { }
        }
        if (html5QrCode) {
            try { await html5QrCode.stop(); html5QrCode.clear(); html5QrCode = null; } catch (e) { }
        }
    }

    window.stopScanner = stopScannerInternal;

})();
