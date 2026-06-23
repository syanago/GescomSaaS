/* ================================================================
   SalesDocuments & PurchaseDocuments — Edit UX enhancements
   ================================================================
   Module generique partage entre les deux ecrans Edit.
   Active via : <div data-doc-edit-root data-document-id="..."
                     data-currency="EUR" data-partner-scope="customers|suppliers"
                     data-partner-entity="client|fournisseur"></div>

   Sous-modules :
   1. Recap totaux sticky a droite (recalcul live des lignes)
   2. Combobox tiers async (recherche fuzzy via /api/v1/partners)
   3. Auto-save de l'en-tete (debounce 1s sur blur, 200ms sur submit)
   4. Raccourcis clavier : Ctrl+S sauve, Esc ferme dropdown,
      Enter sur dernier champ ligne ajoute la ligne
   ================================================================ */

(() => {
    'use strict';

    const root = document.querySelector('[data-doc-edit-root]');
    if (!root) return;

    const documentId    = root.dataset.documentId;
    const currencyCode  = root.dataset.currency || 'EUR';
    const partnerScope  = root.dataset.partnerScope || 'customers';
    const partnerEntity = root.dataset.partnerEntity || 'tiers';

    // ===============================================================
    // 1. RECAP TOTAUX STICKY (sidebar droite)
    // ===============================================================
    const totalsBox = root.querySelector('[data-doc-edit-totals]');
    if (totalsBox) {
        const subtotalEl = totalsBox.querySelector('[data-totals-subtotal]');
        const taxEl      = totalsBox.querySelector('[data-totals-tax]');
        const totalEl    = totalsBox.querySelector('[data-totals-total]');
        const linesEl    = totalsBox.querySelector('[data-totals-lines]');
        const articlesEl = totalsBox.querySelector('[data-totals-articles]');

        function formatMoney(value) {
            return value.toLocaleString('fr-FR', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            }) + ' ' + currencyCode;
        }

        function formatQty(value) {
            const rounded = Math.round(value * 100) / 100;
            return rounded.toLocaleString('fr-FR', {
                minimumFractionDigits: 0,
                maximumFractionDigits: 2
            });
        }

        function recalcTotals() {
            const rows = document.querySelectorAll('[data-line-row]:not(.row-deleting)');
            let subtotal = 0;
            let tax = 0;
            let articleCount = 0;

            rows.forEach(row => {
                const lineSubtotal = parseFloat(row.dataset.lineSubtotal || '0');
                const lineTax      = parseFloat(row.dataset.lineTax || '0');
                const lineQty      = parseFloat(row.dataset.lineQuantity || '0');
                subtotal     += lineSubtotal;
                tax          += lineTax;
                articleCount += lineQty;
            });

            const total = subtotal + tax;

            if (subtotalEl) subtotalEl.textContent = formatMoney(subtotal);
            if (taxEl)      taxEl.textContent      = formatMoney(tax);
            if (totalEl)    totalEl.textContent    = formatMoney(total);
            if (linesEl)    linesEl.textContent    = rows.length;
            if (articlesEl) articlesEl.textContent = formatQty(articleCount);
        }

        recalcTotals();
        // Recalcul en cas de mutation (suppression de ligne sans page reload — futur)
        // ou en cas d'edition inline (custom event 'doc-line-updated')
        const linesTbody = document.querySelector('[data-doc-edit-lines-tbody]');
        if (linesTbody) {
            if ('MutationObserver' in window) {
                new MutationObserver(recalcTotals).observe(linesTbody, { childList: true });
            }
            linesTbody.addEventListener('doc-line-updated', recalcTotals);
        }
    }

    // ===============================================================
    // 2. COMBOBOX TIERS ASYNC (recherche fuzzy live)
    // ===============================================================
    const partnerInput    = document.querySelector('[data-partner-async-input]');
    const partnerHidden   = document.querySelector('[data-partner-async-hidden]');
    const partnerDropdown = document.querySelector('[data-partner-async-dropdown]');

    if (partnerInput && partnerDropdown) {
        let partnersCache = null;
        let activeIndex = -1;
        let visibleResults = [];

        async function loadPartners() {
            if (partnersCache) return partnersCache;
            try {
                const r = await fetch(`/api/v1/partners?scope=${encodeURIComponent(partnerScope)}`, {
                    headers: { 'Accept': 'application/json' }
                });
                if (!r.ok) return [];
                partnersCache = await r.json();
                return partnersCache;
            } catch {
                return [];
            }
        }

        function fuzzyScore(text, query) {
            if (!query) return 1;
            text = (text || '').toLowerCase();
            query = query.toLowerCase();
            if (text.startsWith(query)) return 3;
            if (text.includes(query)) return 2;
            // fuzzy : toutes les lettres de query dans l'ordre
            let i = 0;
            for (const c of query) {
                i = text.indexOf(c, i);
                if (i === -1) return 0;
                i++;
            }
            return 1;
        }

        function renderDropdown(items, query) {
            if (items.length === 0) {
                partnerDropdown.innerHTML = `<div class="px-3 py-2 small text-muted">Aucun resultat. <button type="button" class="btn btn-link btn-sm p-0" data-partner-create-trigger>+ Creer le ${partnerEntity}</button></div>`;
                partnerDropdown.style.display = 'block';
                return;
            }
            partnerDropdown.innerHTML = items.slice(0, 12).map((p, idx) => `
                <div class="gc-ac-item" data-idx="${idx}" data-partner-id="${p.id}" data-partner-display="${p.code} - ${p.name}">
                    <span class="gc-ac-key">${p.code}</span>
                    <span class="gc-ac-label">${p.name}</span>
                    ${p.email ? `<span class="ms-auto small text-muted">${p.email}</span>` : ''}
                </div>
            `).join('');
            partnerDropdown.style.display = 'block';
            activeIndex = -1;
            visibleResults = items.slice(0, 12);
        }

        async function refreshDropdown() {
            const query = partnerInput.value.trim();
            const partners = await loadPartners();
            const scored = partners
                .map(p => ({ p, score: Math.max(
                    fuzzyScore(p.code, query),
                    fuzzyScore(p.name, query),
                    query ? 0 : 1 // tout afficher si pas de query
                )}))
                .filter(x => x.score > 0)
                .sort((a, b) => b.score - a.score);
            renderDropdown(scored.map(x => x.p), query);
        }

        function selectPartner(idx) {
            const p = visibleResults[idx];
            if (!p) return;
            if (partnerHidden) partnerHidden.value = p.id;
            partnerInput.value = `${p.code} - ${p.name}`;
            partnerDropdown.style.display = 'none';
            const lookupHidden = document.querySelector('input[name="PartnerEntry.Lookup"]');
            if (lookupHidden) lookupHidden.value = partnerInput.value;
        }

        partnerInput.addEventListener('focus', () => {
            refreshDropdown();
        });
        partnerInput.addEventListener('input', () => {
            // Quand l'utilisateur tape, on invalide le hidden (selection a refaire)
            if (partnerHidden) partnerHidden.value = '';
            refreshDropdown();
        });
        partnerInput.addEventListener('keydown', (e) => {
            const items = partnerDropdown.querySelectorAll('.gc-ac-item');
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                if (items.length === 0) return;
                items[activeIndex]?.classList.remove('ac-active');
                activeIndex = (activeIndex + 1) % items.length;
                items[activeIndex].classList.add('ac-active');
                items[activeIndex].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                if (items.length === 0) return;
                items[activeIndex]?.classList.remove('ac-active');
                activeIndex = (activeIndex - 1 + items.length) % items.length;
                items[activeIndex].classList.add('ac-active');
                items[activeIndex].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'Enter') {
                if (activeIndex >= 0) {
                    e.preventDefault();
                    selectPartner(activeIndex);
                }
            } else if (e.key === 'Escape') {
                partnerDropdown.style.display = 'none';
            }
        });
        partnerDropdown.addEventListener('click', (e) => {
            const item = e.target.closest('.gc-ac-item');
            if (item) {
                selectPartner(parseInt(item.dataset.idx, 10));
            } else if (e.target.closest('[data-partner-create-trigger]')) {
                document.querySelector('[data-partner-create-button]')?.click();
                partnerDropdown.style.display = 'none';
            }
        });
        document.addEventListener('click', (e) => {
            if (!partnerInput.contains(e.target) && !partnerDropdown.contains(e.target)) {
                partnerDropdown.style.display = 'none';
            }
        });
    }

    // ===============================================================
    // 3. AUTO-SAVE DE L'EN-TETE (debounce 1s sur blur)
    // ===============================================================
    const headerForm = document.querySelector('[data-autosave-form]');
    const autosaveStatus = document.querySelector('[data-autosave-status]');
    if (headerForm && autosaveStatus) {
        let saveTimer = null;
        let savingNow = false;
        let dirty = false;

        function setStatus(text, kind = 'idle') {
            autosaveStatus.textContent = text;
            autosaveStatus.dataset.kind = kind;
        }

        async function doSave() {
            if (savingNow) return;
            savingNow = true;
            setStatus('Enregistrement...', 'saving');
            try {
                const formData = new FormData(headerForm);
                const r = await fetch(headerForm.action || window.location.href, {
                    method: 'POST',
                    body: formData,
                    headers: { 'X-Autosave': 'true', 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (r.ok) {
                    setStatus(`Enregistre a ${new Date().toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}`, 'saved');
                    dirty = false;
                } else {
                    setStatus('Erreur de sauvegarde', 'error');
                }
            } catch {
                setStatus('Erreur reseau', 'error');
            } finally {
                savingNow = false;
            }
        }

        function scheduleSave() {
            if (!dirty) return;
            clearTimeout(saveTimer);
            saveTimer = setTimeout(doSave, 1000);
        }

        function markDirty() {
            dirty = true;
            setStatus('Modifications non enregistrees', 'dirty');
            scheduleSave();
        }

        // Branche les listeners
        headerForm.querySelectorAll('input, select, textarea').forEach(el => {
            // Skip readonly et hidden
            if (el.readOnly || el.type === 'hidden') return;
            el.addEventListener('change', markDirty);
            el.addEventListener('blur', () => {
                if (dirty) {
                    clearTimeout(saveTimer);
                    saveTimer = setTimeout(doSave, 200); // save rapide sur blur
                }
            });
        });

        // Confirme avant de quitter si modifs en cours
        window.addEventListener('beforeunload', (e) => {
            if (dirty || savingNow) {
                e.preventDefault();
                e.returnValue = '';
            }
        });
    }

    // ===============================================================
    // 4. COMBOBOX ARTICLE ASYNC (recherche fuzzy live + selection -> select natif)
    // ===============================================================
    const productInput    = document.querySelector('[data-product-async-input]');
    const productDropdown = document.querySelector('[data-product-async-dropdown]');
    const productSelect   = document.querySelector('[data-stock-tracking-select]'); // select natif existant

    if (productInput && productDropdown && productSelect) {
        let productsCache = null;
        let activeIdx = -1;
        let visibleProducts = [];

        async function loadProducts() {
            if (productsCache) return productsCache;
            try {
                const r = await fetch(`/api/v1/products?trackedOnly=false`, {
                    headers: { 'Accept': 'application/json' }
                });
                if (!r.ok) return [];
                productsCache = await r.json();
                return productsCache;
            } catch {
                return [];
            }
        }

        function fuzzyScore(text, query) {
            if (!query) return 1;
            text = (text || '').toLowerCase();
            query = query.toLowerCase();
            if (text.startsWith(query)) return 3;
            if (text.includes(query)) return 2;
            let i = 0;
            for (const c of query) {
                i = text.indexOf(c, i);
                if (i === -1) return 0;
                i++;
            }
            return 1;
        }

        function renderProducts(items) {
            if (items.length === 0) {
                productDropdown.innerHTML = '<div class="px-3 py-2 small text-muted">Aucun article correspondant.</div>';
                productDropdown.style.display = 'block';
                return;
            }
            productDropdown.innerHTML = items.slice(0, 15).map((p, idx) => {
                const stock = p.trackStock ? '<span class="badge bg-secondary ms-2" style="font-size:.65rem">stock</span>' : '';
                const tax   = p.taxCodeLabel ? `<span class="ms-auto small text-muted">${p.taxCodeLabel}</span>` : '';
                return `
                    <div class="gc-ac-item" data-idx="${idx}" data-product-id="${p.id}">
                        <span class="gc-ac-key">${p.sku}</span>
                        <span class="gc-ac-label">${p.label}${stock}</span>
                        ${tax}
                    </div>
                `;
            }).join('');
            productDropdown.style.display = 'block';
            activeIdx = -1;
            visibleProducts = items.slice(0, 15);
        }

        async function refreshProducts() {
            const query = productInput.value.trim();
            const products = await loadProducts();
            const scored = products
                .filter(p => p.isActive !== false)
                .map(p => ({ p, score: Math.max(
                    fuzzyScore(p.sku, query),
                    fuzzyScore(p.label, query),
                    query ? 0 : 1
                )}))
                .filter(x => x.score > 0)
                .sort((a, b) => b.score - a.score);
            renderProducts(scored.map(x => x.p));
        }

        function pickProduct(idx) {
            const p = visibleProducts[idx];
            if (!p) return;
            // Verifie que l'option existe dans le select natif
            const option = Array.from(productSelect.options).find(o => o.value === p.id);
            if (option) {
                productSelect.value = p.id;
                productSelect.dispatchEvent(new Event('change', { bubbles: true }));
                productInput.value = `${p.sku} - ${p.label}`;
            } else {
                // L'option n'existe pas (article inactif ?), on alerte
                productInput.classList.add('is-invalid');
                setTimeout(() => productInput.classList.remove('is-invalid'), 1200);
            }
            productDropdown.style.display = 'none';
        }

        productInput.addEventListener('focus', refreshProducts);
        productInput.addEventListener('input', refreshProducts);
        productInput.addEventListener('keydown', (e) => {
            const items = productDropdown.querySelectorAll('.gc-ac-item');
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                if (items.length === 0) return;
                items[activeIdx]?.classList.remove('ac-active');
                activeIdx = (activeIdx + 1) % items.length;
                items[activeIdx].classList.add('ac-active');
                items[activeIdx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                if (items.length === 0) return;
                items[activeIdx]?.classList.remove('ac-active');
                activeIdx = (activeIdx - 1 + items.length) % items.length;
                items[activeIdx].classList.add('ac-active');
                items[activeIdx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'Enter') {
                if (activeIdx >= 0) {
                    e.preventDefault();
                    pickProduct(activeIdx);
                }
            } else if (e.key === 'Escape') {
                productDropdown.style.display = 'none';
            }
        });
        productDropdown.addEventListener('click', (e) => {
            const item = e.target.closest('.gc-ac-item');
            if (item) pickProduct(parseInt(item.dataset.idx, 10));
        });
        document.addEventListener('click', (e) => {
            if (!productInput.contains(e.target) && !productDropdown.contains(e.target)) {
                productDropdown.style.display = 'none';
            }
        });
    }

    // ===============================================================
    // 5. EDITION INLINE DES LIGNES (click sur cellule → input → POST AJAX)
    // ===============================================================
    function showInlineToast(message, kind) {
        const toast = document.createElement('div');
        toast.className = `alert alert-${kind === 'error' ? 'danger' : 'success'} position-fixed shadow`;
        toast.style.cssText = 'top:80px; right:24px; z-index:9999; min-width:280px;';
        toast.innerHTML = `<i class="bi bi-${kind === 'error' ? 'exclamation-circle-fill' : 'check-circle-fill'} me-2"></i>${message}`;
        document.body.appendChild(toast);
        setTimeout(() => toast.remove(), 4000);
    }

    function fmtFR(value, decimals, suffix) {
        const n = (typeof value === 'number') ? value : parseFloat(value);
        const formatted = n.toLocaleString('fr-FR', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals
        });
        return formatted + (suffix ? ' ' + suffix : '');
    }

    function applyServerLineUpdate(tr, payload) {
        const updates = {
            quantity:     { raw: payload.line.quantity,     display: fmtFR(payload.line.quantity, 2) },
            unitPrice:    { raw: payload.line.unitPrice,    display: fmtFR(payload.line.unitPrice, 2, currencyCode) },
            discountRate: { raw: payload.line.discountRate, display: fmtFR(payload.line.discountRate, 2, '%') },
            taxRate:      { raw: payload.line.taxRate,      display: fmtFR(payload.line.taxRate, 2, '%') }
        };

        for (const [field, u] of Object.entries(updates)) {
            const td = tr.querySelector(`[data-line-field="${field}"]`);
            if (!td) continue;
            td.dataset.rawValue = String(u.raw);
            const display = td.querySelector('[data-cell-display]');
            if (display) display.textContent = u.display;
        }

        // Cellule total ligne
        const totalCell = tr.querySelector('[data-line-total-display]');
        if (totalCell) totalCell.textContent = fmtFR(payload.line.total, 2, currencyCode);

        // Datasets sur le tr (sources du recap sticky)
        tr.dataset.lineQuantity = String(payload.line.quantity);
        tr.dataset.lineSubtotal = String(payload.line.subtotal);
        tr.dataset.lineTax      = String(payload.line.tax);

        // Notifie le recap sticky
        const tbody = document.querySelector('[data-doc-edit-lines-tbody]');
        if (tbody) tbody.dispatchEvent(new CustomEvent('doc-line-updated', { bubbles: true }));
    }

    async function commitInlineEdit(cell, newValue) {
        const tr = cell.closest('tr');
        const lineId = tr?.dataset.lineId;
        const field = cell.dataset.lineField;
        if (!tr || !lineId || !field) return false;

        // Compose les 4 valeurs depuis le tr (raw values)
        const readField = (f) => parseFloat(tr.querySelector(`[data-line-field="${f}"]`)?.dataset.rawValue || '0');
        const payload = {
            quantity:     readField('quantity'),
            unitPrice:    readField('unitPrice'),
            discountRate: readField('discountRate'),
            taxRate:      readField('taxRate')
        };
        payload[field] = newValue;

        // FormData (Razor Pages valide l'antiforgery via le champ standard)
        const fd = new FormData();
        fd.append('lineId', lineId);
        fd.append('quantity',     String(payload.quantity));
        fd.append('unitPrice',    String(payload.unitPrice));
        fd.append('discountRate', String(payload.discountRate));
        fd.append('taxRate',      String(payload.taxRate));
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (token) fd.append('__RequestVerificationToken', token);

        cell.classList.add('cell-saving');

        try {
            const r = await fetch(window.location.pathname + '?handler=UpdateLine', {
                method: 'POST',
                body: fd,
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
            });
            const data = await r.json().catch(() => ({}));
            cell.classList.remove('cell-saving');

            if (!r.ok || !data?.ok) {
                cell.classList.add('cell-save-error');
                setTimeout(() => cell.classList.remove('cell-save-error'), 1500);
                showInlineToast(data?.error || 'Erreur de mise a jour', 'error');
                return false;
            }

            applyServerLineUpdate(tr, data);
            cell.classList.add('cell-save-ok');
            setTimeout(() => cell.classList.remove('cell-save-ok'), 800);
            return true;
        } catch {
            cell.classList.remove('cell-saving');
            cell.classList.add('cell-save-error');
            setTimeout(() => cell.classList.remove('cell-save-error'), 1500);
            showInlineToast('Erreur reseau lors de la sauvegarde', 'error');
            return false;
        }
    }

    function startCellEdit(cell) {
        if (cell.querySelector('input')) return; // déjà en édition
        const span = cell.querySelector('[data-cell-display]');
        if (!span) return;
        const rawValue = cell.dataset.rawValue || '0';

        const input = document.createElement('input');
        input.type = 'text';
        input.inputMode = 'decimal';
        input.value = rawValue.replace('.', ',');
        input.className = 'form-control form-control-sm cell-input';
        input.setAttribute('aria-label', cell.title || 'Valeur de la ligne');

        span.style.display = 'none';
        cell.appendChild(input);
        input.focus();
        input.select();

        let resolved = false;
        const cleanup = () => {
            if (resolved) return;
            resolved = true;
            input.remove();
            span.style.display = '';
        };

        const finish = async (commit) => {
            if (resolved) return;
            if (!commit) { cleanup(); return; }

            const parsed = parseFloat(input.value.replace(',', '.'));
            if (isNaN(parsed)) { cleanup(); return; }

            const oldValue = parseFloat(rawValue);
            if (Math.abs(parsed - oldValue) < 0.0001) { cleanup(); return; }

            // Désactive l'input le temps du POST
            input.disabled = true;
            const ok = await commitInlineEdit(cell, parsed);
            cleanup();
        };

        input.addEventListener('blur', () => finish(true));
        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') { e.preventDefault(); finish(true); }
            else if (e.key === 'Escape') { e.preventDefault(); finish(false); }
        });
    }

    document.querySelectorAll('td.editable-cell').forEach(cell => {
        cell.addEventListener('click', () => startCellEdit(cell));
    });

    // ===============================================================
    // 6. DRAG & DROP REORDER (HTML5 native, sans dep externe)
    // ===============================================================
    (function setupDragDropReorder() {
        const tbody = document.querySelector('[data-doc-edit-lines-tbody]');
        if (!tbody) return;

        let draggedRow = null;

        const cleanupTargets = () => {
            tbody.querySelectorAll('.drop-target-above, .drop-target-below').forEach(r => {
                r.classList.remove('drop-target-above', 'drop-target-below');
            });
        };

        async function commitReorder(orderedIds) {
            tbody.classList.add('reorder-saving');

            const fd = new FormData();
            orderedIds.forEach(id => fd.append('lineIds', id));
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) fd.append('__RequestVerificationToken', token);

            try {
                const r = await fetch(window.location.pathname + '?handler=ReorderLines', {
                    method: 'POST',
                    body: fd,
                    headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
                });
                const data = await r.json().catch(() => ({}));
                tbody.classList.remove('reorder-saving');

                if (!r.ok || !data?.ok) {
                    showInlineToast(data?.error || 'Erreur lors du reordonnancement', 'error');
                    return false;
                }
                showInlineToast('Ordre des lignes enregistre', 'success');
                return true;
            } catch {
                tbody.classList.remove('reorder-saving');
                showInlineToast('Erreur reseau lors du reordonnancement', 'error');
                return false;
            }
        }

        tbody.querySelectorAll('tr[data-line-row]').forEach(tr => {
            // Drag uniquement si la ligne porte un handle (= statut editable)
            const handle = tr.querySelector('[data-drag-handle]');
            if (!handle) return;

            tr.setAttribute('draggable', 'true');

            tr.addEventListener('dragstart', (e) => {
                draggedRow = tr;
                tr.classList.add('row-dragging');
                e.dataTransfer.effectAllowed = 'move';
                try { e.dataTransfer.setData('text/plain', tr.dataset.lineId || ''); } catch { /* IE legacy */ }
            });

            tr.addEventListener('dragend', () => {
                tr.classList.remove('row-dragging');
                cleanupTargets();
                draggedRow = null;
            });

            tr.addEventListener('dragover', (e) => {
                if (!draggedRow || draggedRow === tr) return;
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';
                const rect = tr.getBoundingClientRect();
                const middle = rect.top + rect.height / 2;
                const before = e.clientY < middle;
                tr.classList.toggle('drop-target-above', before);
                tr.classList.toggle('drop-target-below', !before);
            });

            tr.addEventListener('dragleave', () => {
                tr.classList.remove('drop-target-above', 'drop-target-below');
            });

            tr.addEventListener('drop', async (e) => {
                e.preventDefault();
                if (!draggedRow || draggedRow === tr) return;

                const before = tr.classList.contains('drop-target-above');
                cleanupTargets();

                // Reorganise le DOM optimistiquement
                const previousOrder = Array.from(tbody.querySelectorAll('tr[data-line-row]'));
                if (before) {
                    tbody.insertBefore(draggedRow, tr);
                } else {
                    tbody.insertBefore(draggedRow, tr.nextSibling);
                }

                // Calcule la nouvelle sequence d'IDs
                const orderedIds = Array.from(tbody.querySelectorAll('tr[data-line-row]'))
                    .map(r => r.dataset.lineId)
                    .filter(Boolean);

                const ok = await commitReorder(orderedIds);

                // Rollback si echec serveur (restaure l'ordre prealable)
                if (!ok) {
                    previousOrder.forEach(r => tbody.appendChild(r));
                }
            });
        });
    })();

    // ===============================================================
    // 7. SUPPRESSION SOFT AVEC UNDO TOAST (5s)
    // ===============================================================
    (function setupSoftDelete() {
        const tbody = document.querySelector('[data-doc-edit-lines-tbody]');
        if (!tbody) return;

        function createUndoToast(label) {
            const toast = document.createElement('div');
            toast.className = 'alert alert-secondary position-fixed shadow undo-toast';
            toast.style.cssText = 'bottom:24px; right:24px; z-index:9999; min-width:340px; padding:.85rem 1rem;';
            toast.innerHTML = `
                <div class="d-flex align-items-center justify-content-between gap-3 mb-2">
                    <div class="small">
                        <i class="bi bi-trash3-fill me-2 text-danger"></i>
                        <strong>Ligne supprimée</strong>${label ? ` <span class="text-muted">${label}</span>` : ''}
                    </div>
                    <button type="button" class="btn btn-sm btn-outline-primary" data-undo-button>
                        <i class="bi bi-arrow-counterclockwise me-1"></i>Annuler
                    </button>
                </div>
                <div class="progress" style="height:3px">
                    <div class="progress-bar bg-primary undo-progress" role="progressbar" style="width:100%"></div>
                </div>
            `;
            document.body.appendChild(toast);
            // Animation : barre qui se vide en 5s
            requestAnimationFrame(() => {
                const bar = toast.querySelector('.undo-progress');
                bar.style.transition = 'width 5s linear';
                bar.style.width = '0%';
            });
            return toast;
        }

        tbody.querySelectorAll('form[data-delete-line-form]').forEach(form => {
            form.addEventListener('submit', (e) => {
                const tr = form.closest('tr[data-line-row]');
                if (!tr) return; // fallback POST classique

                e.preventDefault();
                const label = form.dataset.lineLabel || '';

                // Hide la ligne (sans la retirer du DOM, pour pouvoir la restaurer)
                tr.classList.add('row-deleting');
                // Notifie le recap totaux pour masquer la valeur de cette ligne
                tbody.dispatchEvent(new CustomEvent('doc-line-updated', { bubbles: true }));

                // Toast undo
                const toast = createUndoToast(label);
                let undone = false;
                const undoButton = toast.querySelector('[data-undo-button]');

                const finalize = () => {
                    if (undone) return;
                    // Soumet le form classique : POST → redirect → page rechargee avec totaux a jour
                    form.submit();
                };

                const timer = setTimeout(finalize, 5000);

                undoButton.addEventListener('click', () => {
                    undone = true;
                    clearTimeout(timer);
                    tr.classList.remove('row-deleting');
                    tbody.dispatchEvent(new CustomEvent('doc-line-updated', { bubbles: true }));
                    toast.remove();
                    showInlineToast('Suppression annulée', 'success');
                });
            });
        });
    })();

    // ===============================================================
    // 8. BULK EDIT (selection multi-lignes via checkboxes + apply remise/taxe)
    // ===============================================================
    (function setupBulkEdit() {
        const tbody = document.querySelector('[data-doc-edit-lines-tbody]');
        const bar = document.querySelector('[data-bulk-actions]');
        if (!tbody || !bar) return;

        const countEl = bar.querySelector('[data-bulk-count]');
        const selectAll = document.querySelector('[data-bulk-select-all]');

        function getSelectedIds() {
            return Array.from(tbody.querySelectorAll('[data-bulk-select]:checked'))
                .map(cb => cb.dataset.lineId)
                .filter(Boolean);
        }

        function refreshBar() {
            const selected = getSelectedIds();
            if (selected.length === 0) {
                bar.setAttribute('hidden', '');
            } else {
                bar.removeAttribute('hidden');
                if (countEl) countEl.textContent = String(selected.length);
            }
            // Etat du select-all (none / some / all)
            if (selectAll) {
                const all = tbody.querySelectorAll('[data-bulk-select]');
                if (selected.length === 0) {
                    selectAll.checked = false;
                    selectAll.indeterminate = false;
                } else if (selected.length === all.length) {
                    selectAll.checked = true;
                    selectAll.indeterminate = false;
                } else {
                    selectAll.checked = false;
                    selectAll.indeterminate = true;
                }
            }
        }

        // Click checkbox individuelle
        tbody.addEventListener('change', (e) => {
            if (e.target.matches('[data-bulk-select]')) refreshBar();
        });

        // Select all
        if (selectAll) {
            selectAll.addEventListener('change', () => {
                tbody.querySelectorAll('[data-bulk-select]').forEach(cb => {
                    cb.checked = selectAll.checked;
                });
                refreshBar();
            });
        }

        // Désélectionner
        bar.querySelector('[data-bulk-clear]')?.addEventListener('click', () => {
            tbody.querySelectorAll('[data-bulk-select]:checked').forEach(cb => cb.checked = false);
            refreshBar();
        });

        async function commitBulk(payload) {
            const selected = getSelectedIds();
            if (selected.length === 0) return;

            const fd = new FormData();
            selected.forEach(id => fd.append('lineIds', id));
            if (payload.discountRate !== undefined) fd.append('discountRate', String(payload.discountRate));
            if (payload.taxRate !== undefined) fd.append('taxRate', String(payload.taxRate));
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) fd.append('__RequestVerificationToken', token);

            bar.classList.add('bulk-saving');

            try {
                const r = await fetch(window.location.pathname + '?handler=BulkUpdateLines', {
                    method: 'POST',
                    body: fd,
                    headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
                });
                const data = await r.json().catch(() => ({}));
                bar.classList.remove('bulk-saving');

                if (!r.ok || !data?.ok) {
                    showInlineToast(data?.error || 'Erreur lors de la mise a jour groupee', 'error');
                    return;
                }

                // Met a jour le DOM ligne par ligne (réutilise applyServerLineUpdate)
                (data.lines || []).forEach(l => {
                    const tr = tbody.querySelector(`tr[data-line-id="${l.id}"]`);
                    if (tr) applyServerLineUpdate(tr, { line: l });
                });

                // Décoche les checkboxes apres succes
                tbody.querySelectorAll('[data-bulk-select]:checked').forEach(cb => cb.checked = false);
                refreshBar();

                showInlineToast(`${data.count} ligne(s) mise(s) a jour`, 'success');
            } catch {
                bar.classList.remove('bulk-saving');
                showInlineToast('Erreur reseau lors de la mise a jour groupee', 'error');
            }
        }

        bar.querySelector('[data-bulk-apply-discount]')?.addEventListener('click', () => {
            const raw = window.prompt('Appliquer quel taux de remise (en %, 0 a 100) ?', '10');
            if (raw === null) return;
            const parsed = parseFloat(raw.replace(',', '.'));
            if (isNaN(parsed) || parsed < 0 || parsed > 100) {
                showInlineToast('Valeur invalide. Saisis un nombre entre 0 et 100.', 'error');
                return;
            }
            commitBulk({ discountRate: parsed });
        });

        bar.querySelector('[data-bulk-apply-tax]')?.addEventListener('click', () => {
            const raw = window.prompt('Appliquer quel taux de taxe (en %, 0 a 100) ?', '20');
            if (raw === null) return;
            const parsed = parseFloat(raw.replace(',', '.'));
            if (isNaN(parsed) || parsed < 0 || parsed > 100) {
                showInlineToast('Valeur invalide. Saisis un nombre entre 0 et 100.', 'error');
                return;
            }
            commitBulk({ taxRate: parsed });
        });
    })();

    // ===============================================================
    // 9. RACCOURCIS CLAVIER
    // ===============================================================
    document.addEventListener('keydown', (e) => {
        // Ctrl+S / Cmd+S -> soumet le form principal
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            const form = document.querySelector('[data-autosave-form]');
            if (form) {
                form.requestSubmit();
            }
        }
        // Esc -> ferme popovers/dropdowns
        if (e.key === 'Escape') {
            document.querySelectorAll('[data-partner-async-dropdown], [data-product-async-dropdown]').forEach(d => d.style.display = 'none');
        }
    });

    // Enter dans le formulaire d'ajout de ligne soumet (au lieu de blur input)
    const addLineForm = document.querySelector('[data-add-line-form]');
    if (addLineForm) {
        addLineForm.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && e.target.tagName !== 'TEXTAREA') {
                const allInputs = Array.from(addLineForm.querySelectorAll('input:not([type=hidden]):not([readonly]), select'));
                const visibleInputs = allInputs.filter(i => i.offsetParent !== null);
                const last = visibleInputs[visibleInputs.length - 1];
                if (e.target === last) {
                    e.preventDefault();
                    addLineForm.requestSubmit();
                }
            }
        });
    }

    // ===============================================================
    // 10. CSS hooks (recap sticky, dropdowns, edition inline, drag&drop, soft-delete, bulk bar)
    // ===============================================================
    const style = document.createElement('style');
    style.textContent = `
        .doc-edit-totals-sticky {
            position: sticky;
            top: 70px;
        }
        [data-autosave-status] {
            font-size: .75rem;
            color: var(--gc-text-muted);
            display: inline-flex;
            align-items: center;
            gap: 4px;
        }
        [data-autosave-status][data-kind="saving"]  { color: #b45309; }
        [data-autosave-status][data-kind="saved"]   { color: var(--gc-pill-green-fg); }
        [data-autosave-status][data-kind="dirty"]   { color: #6b7280; }
        [data-autosave-status][data-kind="error"]   { color: #dc2626; }
        [data-autosave-status][data-kind="saving"]::before  { content: 'O '; }
        [data-autosave-status][data-kind="saved"]::before   { content: 'V '; }
        [data-autosave-status][data-kind="dirty"]::before   { content: '* '; }
        [data-autosave-status][data-kind="error"]::before   { content: '! '; }

        /* Combobox dropdown LigFin (partner + product) */
        [data-partner-async-dropdown],
        [data-product-async-dropdown] {
            position: absolute;
            z-index: 1000;
            background: #fff;
            border: 1px solid var(--gc-border);
            border-radius: 8px;
            box-shadow: 0 8px 20px rgba(0,0,0,.12);
            max-height: 320px;
            overflow-y: auto;
            min-width: 100%;
            display: none;
            margin-top: 2px;
        }
        [data-partner-async-dropdown] .gc-ac-item,
        [data-product-async-dropdown] .gc-ac-item {
            padding: .5rem .85rem;
            cursor: pointer;
            font-size: .85rem;
            display: flex;
            gap: .75rem;
            align-items: baseline;
            border-bottom: 1px solid var(--gc-border-light);
        }
        [data-partner-async-dropdown] .gc-ac-item:last-child,
        [data-product-async-dropdown] .gc-ac-item:last-child { border-bottom: none; }
        [data-partner-async-dropdown] .gc-ac-item:hover,
        [data-partner-async-dropdown] .gc-ac-item.ac-active,
        [data-product-async-dropdown] .gc-ac-item:hover,
        [data-product-async-dropdown] .gc-ac-item.ac-active {
            background: var(--gc-brand-50);
        }
        [data-partner-async-dropdown] .gc-ac-key,
        [data-product-async-dropdown] .gc-ac-key {
            font-family: var(--gc-mono, ui-monospace, SFMono-Regular, Consolas, monospace);
            font-weight: 600;
            color: var(--gc-brand-700, var(--gc-brand));
            min-width: 90px;
        }

        /* === Edition inline des cellules de ligne === */
        td.editable-cell {
            cursor: pointer;
            position: relative;
            transition: background-color .12s ease, box-shadow .12s ease;
        }
        td.editable-cell:hover {
            background: var(--gc-brand-50);
            box-shadow: inset 0 0 0 1px var(--gc-brand-200, #bfdbfe);
        }
        td.editable-cell::after {
            content: '✎';
            position: absolute;
            top: 4px;
            right: 6px;
            color: var(--gc-brand);
            font-size: .65rem;
            opacity: 0;
            transition: opacity .15s;
            pointer-events: none;
        }
        td.editable-cell:hover::after {
            opacity: .55;
        }
        td.editable-cell.cell-saving {
            background: #fef3c7;
            box-shadow: inset 0 0 0 1px #fbbf24;
        }
        td.editable-cell.cell-save-ok {
            background: var(--gc-pill-green-bg, #dcfce7);
            box-shadow: inset 0 0 0 1px #86efac;
            transition: background .8s ease-out, box-shadow .8s ease-out;
        }
        td.editable-cell.cell-save-error {
            background: #fee2e2;
            box-shadow: inset 0 0 0 1px #fca5a5;
        }
        td.editable-cell .cell-input {
            width: 100%;
            margin: 0;
            padding: 2px 6px;
            height: auto;
            min-height: 1.7rem;
            font: inherit;
            text-align: right;
            border: 1px solid var(--gc-brand);
            border-radius: 4px;
        }
        td.editable-cell .cell-input:focus {
            outline: none;
            box-shadow: 0 0 0 3px var(--gc-brand-50);
        }

        /* === Drag & drop reorder des lignes === */
        tr[data-line-row][draggable="true"] {
            cursor: move;
        }
        [data-drag-handle] {
            cursor: grab;
            color: #94a3b8;
            user-select: none;
            font-size: 1.05rem;
            line-height: 1;
            padding: 0 4px;
            transition: color .12s;
        }
        [data-drag-handle]:hover {
            color: var(--gc-brand);
        }
        [data-drag-handle]:active {
            cursor: grabbing;
        }
        tr.row-dragging {
            opacity: .45;
            background: var(--gc-brand-50);
        }
        tr.drop-target-above td {
            border-top: 2px solid var(--gc-brand) !important;
        }
        tr.drop-target-below td {
            border-bottom: 2px solid var(--gc-brand) !important;
        }
        [data-doc-edit-lines-tbody].reorder-saving {
            opacity: .65;
            pointer-events: none;
            transition: opacity .15s;
        }

        /* === Soft-delete avec undo toast === */
        tr.row-deleting {
            opacity: .35;
            text-decoration: line-through;
            pointer-events: none;
            transition: opacity .2s ease-out;
            background: #fef2f2;
        }
        tr.row-deleting [data-drag-handle] {
            visibility: hidden;
        }
        .undo-toast {
            animation: undo-toast-slide-in .2s ease-out;
        }
        @keyframes undo-toast-slide-in {
            from { transform: translateY(20px); opacity: 0; }
            to   { transform: translateY(0);    opacity: 1; }
        }

        /* === Bulk actions bar === */
        .bulk-actions-bar {
            display: flex;
            align-items: center;
            padding: .65rem 1rem;
            margin: .5rem 1rem 0;
            background: var(--gc-brand-50);
            border: 1px solid var(--gc-brand-200, #bfdbfe);
            border-radius: 6px;
            font-size: .875rem;
            gap: .75rem;
            animation: bulk-bar-slide-down .15s ease-out;
        }
        .bulk-actions-bar.bulk-saving {
            opacity: .65;
            pointer-events: none;
            transition: opacity .15s;
        }
        [data-bulk-actions][hidden] {
            display: none !important;
        }
        @keyframes bulk-bar-slide-down {
            from { transform: translateY(-6px); opacity: 0; }
            to   { transform: translateY(0);    opacity: 1; }
        }
        /* Highlight de la ligne selectionnee */
        tr[data-line-row]:has([data-bulk-select]:checked) {
            background: var(--gc-brand-50);
        }
        [data-partner-async-dropdown] .gc-ac-label {
            color: var(--gc-text);
        }
    `;
    document.head.appendChild(style);
})();
