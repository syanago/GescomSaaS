/* ==========================================================================
   ligcom-library.js — Interactivité de la bibliothèque de composants
   ==========================================================================
   - Password toggle
   - Number spinners (lc-num-input)
   - Autocomplete (input + dropdown clavier-first)
   - Tabs (underline + pills)
   - Modals (open/close) avec backdrop
   - Drawers (right md/lg)
   - Toasts (4 types, queue, auto-dismiss)
   - File drop (drag & drop visual)
   - Sticky TOC active highlight
   ========================================================================== */

(() => {
    'use strict';

    // ====================================================================
    // 1. Password toggle
    // ====================================================================
    document.querySelectorAll('[data-pwd-toggle]').forEach(btn => {
        btn.addEventListener('click', () => {
            const target = document.getElementById(btn.dataset.pwdToggle);
            if (!target) return;
            const isPwd = target.type === 'password';
            target.type = isPwd ? 'text' : 'password';
            const eyeOpen = btn.querySelector('[data-pwd-icon="open"]');
            const eyeClosed = btn.querySelector('[data-pwd-icon="closed"]');
            if (eyeOpen) eyeOpen.style.display = isPwd ? 'none' : '';
            if (eyeClosed) eyeClosed.style.display = isPwd ? '' : 'none';
        });
    });

    // ====================================================================
    // 2. Number spinners (.lc-num-input)
    // ====================================================================
    document.querySelectorAll('.lc-num-input').forEach(group => {
        const input = group.querySelector('input');
        if (!input) return;
        const step = parseFloat(input.step || '1');
        group.querySelectorAll('[data-num-step]').forEach(btn => {
            btn.addEventListener('click', () => {
                const current = parseFloat(input.value || '0');
                const dir = btn.dataset.numStep === 'up' ? 1 : -1;
                input.value = (current + dir * step).toString();
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
            });
        });
    });

    // ====================================================================
    // 3. Autocomplete générique (.lc-ac avec data-ac-source="json-id")
    // ====================================================================
    document.querySelectorAll('.lc-ac').forEach(ac => {
        const input = ac.querySelector('input[type="text"], input[type="search"], input:not([type])');
        const menu  = ac.querySelector('.lc-ac__menu');
        const sourceEl = ac.dataset.acSource ? document.getElementById(ac.dataset.acSource) : null;
        if (!input || !menu) return;

        let items = [];
        if (sourceEl) {
            try { items = JSON.parse(sourceEl.textContent); } catch { items = []; }
        }

        let activeIdx = -1;
        let visible = [];

        function score(text, q) {
            text = (text || '').toLowerCase();
            q = q.toLowerCase();
            if (!q) return 1;
            if (text.startsWith(q)) return 3;
            if (text.includes(q)) return 2;
            let i = 0;
            for (const c of q) {
                i = text.indexOf(c, i);
                if (i === -1) return 0;
                i++;
            }
            return 1;
        }

        function render() {
            const q = input.value.trim();
            const scored = items
                .map(it => ({ it, s: Math.max(score(it.code, q), score(it.label, q), q ? 0 : 1) }))
                .filter(x => x.s > 0)
                .sort((a, b) => b.s - a.s)
                .slice(0, 10);

            visible = scored.map(x => x.it);
            activeIdx = -1;

            if (visible.length === 0) {
                menu.innerHTML = '<div class="lc-ac__empty">Aucun résultat</div>';
            } else {
                menu.innerHTML = visible.map((it, i) => `
                    <div class="lc-ac__item" data-idx="${i}">
                        <span class="lc-ac__code">${it.code}</span>
                        <span class="lc-ac__label">${it.label}</span>
                        ${it.meta ? `<span class="lc-ac__meta">${it.meta}</span>` : ''}
                    </div>
                `).join('');
            }
            ac.classList.add('is-open');
        }

        function pick(idx) {
            const it = visible[idx];
            if (!it) return;
            input.value = `${it.code} — ${it.label}`;
            ac.classList.remove('is-open');
            ac.dispatchEvent(new CustomEvent('lc:autocomplete-select', { detail: it, bubbles: true }));
        }

        input.addEventListener('focus', render);
        input.addEventListener('input', render);
        input.addEventListener('keydown', (e) => {
            const nodes = menu.querySelectorAll('.lc-ac__item');
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                if (!nodes.length) return;
                nodes[activeIdx]?.classList.remove('is-active');
                activeIdx = (activeIdx + 1) % nodes.length;
                nodes[activeIdx].classList.add('is-active');
                nodes[activeIdx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                if (!nodes.length) return;
                nodes[activeIdx]?.classList.remove('is-active');
                activeIdx = (activeIdx - 1 + nodes.length) % nodes.length;
                nodes[activeIdx].classList.add('is-active');
                nodes[activeIdx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'Enter' && activeIdx >= 0) {
                e.preventDefault();
                pick(activeIdx);
            } else if (e.key === 'Escape') {
                ac.classList.remove('is-open');
            }
        });
        menu.addEventListener('click', (e) => {
            const item = e.target.closest('.lc-ac__item');
            if (item) pick(parseInt(item.dataset.idx, 10));
        });
        document.addEventListener('click', (e) => {
            if (!ac.contains(e.target)) ac.classList.remove('is-open');
        });
    });

    // ====================================================================
    // 4. Tabs (underline + pills) avec ARIA
    // ====================================================================
    document.querySelectorAll('[data-tabs]').forEach(tabs => {
        const buttons = tabs.querySelectorAll('.lc-tab');
        const panelsRoot = document.querySelector(tabs.dataset.tabsPanels) || tabs.parentElement;

        buttons.forEach(btn => {
            btn.addEventListener('click', () => {
                buttons.forEach(b => b.classList.remove('is-active'));
                btn.classList.add('is-active');
                const target = btn.dataset.tab;
                panelsRoot?.querySelectorAll('.lc-tab-panel').forEach(p => {
                    p.classList.toggle('is-active', p.dataset.tabPanel === target);
                });
            });
        });
    });

    // ====================================================================
    // 5. Modals — generic, ouverts par [data-modal-open="id"]
    // ====================================================================
    function openModal(id) {
        const modal = document.getElementById(id);
        if (!modal) return;
        modal.classList.add('lc-modal-backdrop--open');
        document.body.style.overflow = 'hidden';
    }
    function closeModal(modal) {
        modal.classList.remove('lc-modal-backdrop--open');
        document.body.style.overflow = '';
    }
    document.querySelectorAll('[data-modal-open]').forEach(btn => {
        btn.addEventListener('click', () => openModal(btn.dataset.modalOpen));
    });
    document.querySelectorAll('.lc-modal-backdrop').forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal || e.target.closest('[data-modal-close]')) {
                closeModal(modal);
            }
        });
    });

    // ====================================================================
    // 6. Drawers right
    // ====================================================================
    function openDrawer(id) {
        const drawer = document.getElementById(id);
        const backdrop = drawer?.previousElementSibling;
        if (!drawer) return;
        drawer.classList.add('lc-drawer--open');
        if (backdrop?.classList.contains('lc-drawer-backdrop')) {
            backdrop.classList.add('lc-drawer-backdrop--open');
        }
    }
    function closeDrawer(drawer) {
        drawer.classList.remove('lc-drawer--open');
        const backdrop = drawer.previousElementSibling;
        if (backdrop?.classList.contains('lc-drawer-backdrop')) {
            backdrop.classList.remove('lc-drawer-backdrop--open');
        }
    }
    document.querySelectorAll('[data-drawer-open]').forEach(btn => {
        btn.addEventListener('click', () => openDrawer(btn.dataset.drawerOpen));
    });
    document.querySelectorAll('.lc-drawer').forEach(drawer => {
        drawer.querySelectorAll('[data-drawer-close]').forEach(btn => {
            btn.addEventListener('click', () => closeDrawer(drawer));
        });
        const backdrop = drawer.previousElementSibling;
        if (backdrop?.classList.contains('lc-drawer-backdrop')) {
            backdrop.addEventListener('click', () => closeDrawer(drawer));
        }
    });

    // Esc ferme modals + drawers
    document.addEventListener('keydown', (e) => {
        if (e.key !== 'Escape') return;
        document.querySelectorAll('.lc-modal-backdrop--open').forEach(m => closeModal(m));
        document.querySelectorAll('.lc-drawer--open').forEach(d => closeDrawer(d));
    });

    // ====================================================================
    // 7. Toaster — API : LigComToast.push({title, desc, kind, timeout})
    // ====================================================================
    let toaster = document.querySelector('.lc-toaster');
    if (!toaster) {
        toaster = document.createElement('div');
        toaster.className = 'lc-toaster';
        document.body.appendChild(toaster);
    }

    const ICONS = {
        success: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>',
        warning: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>',
        error:   '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>',
        info:    '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>',
    };

    function pushToast({ title, desc, kind = 'info', timeout = 4000 } = {}) {
        const t = document.createElement('div');
        t.className = `lc-toast lc-toast--${kind}`;
        t.innerHTML = `
            <div class="lc-toast__icon">${ICONS[kind] || ICONS.info}</div>
            <div class="lc-toast__body">
                ${title ? `<div class="lc-toast__title">${title}</div>` : ''}
                ${desc ? `<div class="lc-toast__desc">${desc}</div>` : ''}
            </div>
            <button type="button" class="lc-toast__close" aria-label="Fermer">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
            </button>
        `;
        toaster.appendChild(t);

        const remove = () => {
            t.classList.add('is-leaving');
            setTimeout(() => t.remove(), 200);
        };
        t.querySelector('.lc-toast__close').addEventListener('click', remove);
        if (timeout > 0) setTimeout(remove, timeout);
        return t;
    }

    window.LigComToast = { push: pushToast };

    // ====================================================================
    // 8. File drop visual
    // ====================================================================
    document.querySelectorAll('.lc-file-drop').forEach(zone => {
        const input = zone.querySelector('input[type="file"]');
        ['dragenter', 'dragover'].forEach(ev => zone.addEventListener(ev, (e) => {
            e.preventDefault(); e.stopPropagation();
            zone.classList.add('is-dragover');
        }));
        ['dragleave', 'drop'].forEach(ev => zone.addEventListener(ev, (e) => {
            e.preventDefault(); e.stopPropagation();
            zone.classList.remove('is-dragover');
        }));
        zone.addEventListener('drop', (e) => {
            if (input && e.dataTransfer?.files?.length) {
                input.files = e.dataTransfer.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
        zone.addEventListener('click', () => input?.click());
    });

    // ====================================================================
    // 9. Sticky TOC : highlight active section sur scroll
    // ====================================================================
    const tocLinks = document.querySelectorAll('.lc-lib__nav-item');
    const sections = document.querySelectorAll('.lc-lib__section');
    if (tocLinks.length && sections.length && 'IntersectionObserver' in window) {
        const obs = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const id = entry.target.id;
                    tocLinks.forEach(l => l.classList.toggle('is-active', l.getAttribute('href') === '#' + id));
                }
            });
        }, { rootMargin: '-30% 0px -60% 0px' });
        sections.forEach(s => obs.observe(s));
    }

})();
