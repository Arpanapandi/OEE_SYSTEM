/**
 * scw-handler.js
 * Handles SCW (Stop Call Waiting) logic.
 */

(function ($) {
    'use strict';

    $(document).ready(function () {
        const $typeSelect = $('#scw-4m-type');
        const $remarkSelect = $('#scw-remark');
        const $submitBtn = $('#btn-scw-submit');

        // 1. Filter Remarks based on 4M Type
        window.handleScw4MChange = function (typeId) {
            console.log(`[SCW] Filtering for TypeId: ${typeId}`);

            // Reset remark select
            $remarkSelect.val('');

            if (!typeId) {
                $('.scw-remark-option').hide();
                return;
            }

            // Show matching options
            let foundCount = 0;
            $('.scw-remark-option').each(function () {
                const parentId = $(this).data('parent-id');
                if (parentId == typeId) {
                    $(this).show();
                    foundCount++;
                } else {
                    $(this).hide();
                }
            });

            console.log(`[SCW] Found ${foundCount} remarks in HTML`);

            // Optional: fallback to API if no items found in HTML (sync check)
            if (foundCount === 0) {
                fetchScwRemarks(typeId);
            }
        };

        async function fetchScwRemarks(typeId) {
            try {
                const res = await fetch(`/api/Operator/GetScwRemarks?scw4MTypeId=${typeId}`);
                const data = await res.json();
                if (data.success) {
                    // Update UI if needed (already handled by display:none toggle usually)
                    console.log(`[SCW] API Sync: ${data.remarks.length} items from server`);
                }
            } catch (err) {
                console.warn('[SCW] Silent sync failed', err);
            }
        }

        // 2. Event Listeners
        $typeSelect.on('change', function () {
            window.handleScw4MChange($(this).val());
        });

        $submitBtn.on('click', async function () {
            const typeId = $typeSelect.val();
            const remarkId = $remarkSelect.val();

            if (!typeId || !remarkId) {
                if (window.showWarningNotification) {
                    window.showWarningNotification('Pilih Kategori 4M dan Alasan terlebih dahulu');
                } else {
                    alert('Pilih Kategori 4M dan Alasan terlebih dahulu');
                }
                return;
            }

            const machineId = window.OeeConfig ? window.OeeConfig.machineId : null;
            if (!machineId) return;

            const originalHtml = $submitBtn.innerHTML;
            $submitBtn.prop('disabled', true);
            $submitBtn.html('<i class="fa-solid fa-spinner fa-spin"></i>');

            try {
                const token = $('input[name="__RequestVerificationToken"]').val();
                const fd = new FormData();
                fd.append('machineId', machineId);
                fd.append('jenis4MId', typeId);
                fd.append('jenisRemarkId', remarkId);
                fd.append('__RequestVerificationToken', token);

                const res = await fetch('/Operator/Scw', {
                    method: 'POST',
                    body: fd,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                const result = await res.json();
                if (result.success) {
                    if (window.showSuccessNotification) {
                        window.showSuccessNotification(result.message || 'Data SCW berhasil disimpan');
                    } else {
                        alert('✅ ' + (result.message || 'Data SCW berhasil disimpan'));
                    }

                    // Reset form
                    $typeSelect.val('').trigger('change');
                } else {
                    if (window.showErrorNotification) {
                        window.showErrorNotification(result.message || 'Gagal menyimpan data SCW');
                    } else {
                        alert('❌ ' + (result.message || 'Gagal menyimpan data SCW'));
                    }
                }
            } catch (err) {
                console.error('[SCW] Submit error:', err);
                alert('❌ Terjadi kesalahan sistem');
            } finally {
                $submitBtn.prop('disabled', false);
                $submitBtn.html('<i class="fa-solid fa-save me-1"></i>SIMPAN');
            }
        });
    });

})(jQuery);
