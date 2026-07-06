/* ==========================================================================
   ligcom-shell.js — Orchestration du shell LigCom
   ==========================================================================
   - Thème 3 modes (system / light / dark) avec écoute prefers-color-scheme
   - Sidebar collapse desktop / drawer mobile
   - Dropdowns header
   - Statut de connexion + drawer de sync
   - Banner offline + modal de résolution de conflit
   - Raccourcis clavier (Ctrl+K)

   API publique : window.LigCom (setThemeMode, setSyncState, etc.)
   ========================================================================== */

(() => {
    'use strict';

    const root = document.documentElement;
    const shell = document.querySelector('[data-shell]');
    const mql = window.matchMedia('(prefers-color-scheme: dark)');

    // ====================================================================
    // 1. THÈME — 3 modes : 'system' (auto), 'light', 'dark'
    // ====================================================================
    function getThemeMode() {
        try { return localStorage.getItem('lc-theme-mode') || 'system'; } catch { return 'system'; }
    }
    function resolveTheme(mode) {
        if (mode === 'light' || mode === 'dark') return mode;
        return mql.matches ? 'dark' : 'light';
    }
    function syncThemeUI(mode, theme) {
        // Header icons
        const lightIcon = document.querySelector('[data-theme-icon="light"]');
        const darkIcon  = document.querySelector('[data-theme-icon="dark"]');
        if (lightIcon) lightIcon.style.display = theme === 'dark' ? 'none' : '';
        if (darkIcon)  darkIcon.style.display  = theme === 'dark' ? '' : 'none';
        // Cards de la page Paramètres > Apparence
        document.querySelectorAll('[data-theme-card]').forEach(card => {
            const isActive = card.dataset.themeCard === mode;
            card.classList.toggle('is-active', isActive);
            card.setAttribute('aria-checked', String(isActive));
        });
    }
    function applyTheme() {
        const mode = getThemeMode();
        const theme = resolveTheme(mode);
        root.setAttribute('data-bs-theme', theme);
        syncThemeUI(mode, theme);
    }
    function setThemeMode(mode) {
        if (!['system', 'light', 'dark'].includes(mode)) mode = 'system';
        try { localStorage.setItem('lc-theme-mode', mode); } catch {}
        applyTheme();
    }

    // Écoute le changement de thème OS quand mode = 'system'
    mql.addEventListener('change', () => {
        if (getThemeMode() === 'system') applyTheme();
    });

    // Toggle bouton header : cycle light → dark → system → light
    document.querySelector('[data-theme-toggle]')?.addEventListener('click', () => {
        const order = ['light', 'dark', 'system'];
        const next = order[(order.indexOf(getThemeMode()) + 1) % order.length];
        setThemeMode(next);
    });

    // Init : applique l'état UI sans changer le data-bs-theme déjà posé par le pre-script <head>
    applyTheme();

    // Sync icône avec valeur initiale (déjà appliquée inline dans <head>)
    applyTheme(root.getAttribute('data-bs-theme') || 'light');

    // ====================================================================
    // 2. SIDEBAR : collapse desktop + drawer mobile
    // ====================================================================
    const sidebar = document.getElementById('lc-sidebar');
    const sidebarBackdrop = document.querySelector('[data-sidebar-backdrop]');

    document.querySelector('[data-sidebar-collapse]')?.addEventListener('click', () => {
        shell?.classList.toggle('lc-shell--collapsed');
        try { localStorage.setItem('lc-sidebar-collapsed', shell.classList.contains('lc-shell--collapsed') ? '1' : '0'); } catch {}
    });

    try {
        if (localStorage.getItem('lc-sidebar-collapsed') === '1') {
            shell?.classList.add('lc-shell--collapsed');
        }
    } catch {}

    document.querySelector('[data-sidebar-open]')?.addEventListener('click', () => {
        sidebar?.classList.add('lc-sidebar--open');
        sidebarBackdrop?.classList.add('lc-sidebar-backdrop--open');
    });

    function closeMobileSidebar() {
        sidebar?.classList.remove('lc-sidebar--open');
        sidebarBackdrop?.classList.remove('lc-sidebar-backdrop--open');
    }
    document.querySelector('[data-sidebar-close]')?.addEventListener('click', closeMobileSidebar);
    sidebarBackdrop?.addEventListener('click', closeMobileSidebar);

    // ====================================================================
    // 3. DROPDOWNS HEADER (custom léger sans Bootstrap pour densité contrôlée)
    // ====================================================================
    document.addEventListener('click', (e) => {
        const toggle = e.target.closest('[data-dropdown-toggle]');
        if (toggle) {
            const dd = toggle.closest('[data-dropdown]');
            const wasOpen = dd.classList.contains('lc-dropdown--open');
            // Ferme tous les autres
            document.querySelectorAll('.lc-dropdown--open').forEach(d => d.classList.remove('lc-dropdown--open'));
            if (!wasOpen) dd.classList.add('lc-dropdown--open');
            e.stopPropagation();
            return;
        }
        if (!e.target.closest('.lc-dropdown__menu')) {
            document.querySelectorAll('.lc-dropdown--open').forEach(d => d.classList.remove('lc-dropdown--open'));
        }
    });

    // ====================================================================
    // 4. STATUT DE CONNEXION (online / syncing / offline)
    //    + mode hors ligne FORCÉ par l'utilisateur (persistant)
    // ====================================================================
    const syncBadge = document.querySelector('[data-sync-status]');
    const offlineBanner = document.querySelector('[data-offline-banner]');

    function isOfflineForced() {
        try { return localStorage.getItem('lc-offline-forced') === '1'; } catch { return false; }
    }

    /**
     * Applique réellement le forçage hors ligne (persistance + UI).
     * À ne pas appeler directement depuis l'UI : passer par requestOfflineModeChange()
     * qui exige l'auth admin.
     */
    function applyOfflineForced(forced) {
        try { localStorage.setItem('lc-offline-forced', forced ? '1' : '0'); } catch {}
        syncOfflineToggleUI(forced);
        if (forced) {
            setSyncState('offline');
        } else {
            setSyncState(navigator.onLine ? 'online' : 'offline');
        }
    }

    /**
     * Demande un changement de mode hors ligne.
     * Ouvre la modal d'authentification admin.
     * Le changement n'est appliqué qu'après vérification serveur réussie.
     */
    function requestOfflineModeChange(targetForced) {
        const modal = document.querySelector('[data-offline-auth-modal]');
        if (!modal) {
            // Si la modal n'existe pas (page sans layout), on bascule directement (fallback)
            applyOfflineForced(targetForced);
            return;
        }
        modal.dataset.targetForced = String(targetForced);
        const targetLabel = modal.querySelector('[data-offline-auth-target-label]');
        if (targetLabel) {
            targetLabel.textContent = targetForced
                ? 'activer le mode hors ligne forcé'
                : 'revenir en mode automatique (en ligne)';
        }
        // Reset état modal
        const errBox = modal.querySelector('[data-offline-auth-error]');
        if (errBox) errBox.classList.add('d-none');
        const pwd = modal.querySelector('#lc-offline-auth-password');
        if (pwd) { pwd.value = ''; setTimeout(() => pwd.focus(), 50); }
        modal.classList.add('lc-modal-backdrop--open');
    }

    function closeOfflineAuthModal() {
        document.querySelector('[data-offline-auth-modal]')?.classList.remove('lc-modal-backdrop--open');
    }

    // Ecran plein affiche pendant la bascule reelle de mode (le serveur redemarre).
    // Le mode reel etant desormais gere serveur, on retire l'ancien drapeau client.
    function showRestartOverlay(goingOffline) {
        if (document.getElementById('lc-restart-overlay')) return;
        try { localStorage.removeItem('lc-offline-forced'); } catch (e) {}
        if (!document.getElementById('lc-spin-style')) {
            const st = document.createElement('style');
            st.id = 'lc-spin-style';
            st.textContent = '@keyframes lc-spin{to{transform:rotate(360deg)}}';
            document.head.appendChild(st);
        }
        const ov = document.createElement('div');
        ov.id = 'lc-restart-overlay';
        ov.style.cssText = 'position:fixed;inset:0;z-index:99999;background:rgba(15,23,42,0.92);color:#fff;display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center;padding:24px;font-family:system-ui,sans-serif';
        ov.innerHTML =
            '<div style="width:44px;height:44px;border:4px solid rgba(255,255,255,0.25);border-top-color:#fff;border-radius:50%;animation:lc-spin 0.9s linear infinite;margin-bottom:20px"></div>' +
            '<h2 style="margin:0 0 8px;font-weight:600">Bascule en mode ' + (goingOffline ? 'Hors ligne (SQLite)' : 'En ligne (SQL Server)') + '…</h2>' +
            '<p style="margin:0;opacity:0.85;max-width:440px">L\'application redémarre pour appliquer le changement de base de données. Cette page se rechargera automatiquement.</p>' +
            '<p id="lc-restart-hint" style="margin-top:16px;opacity:0.6;font-size:13px"></p>';
        document.body.appendChild(ov);
        waitForRestart();
    }

    function waitForRestart() {
        const hint = document.getElementById('lc-restart-hint');
        let sawDown = false;
        let attempts = 0;
        const timer = setInterval(function () {
            attempts++;
            fetch('/health/live', { cache: 'no-store' })
                .then(function (res) {
                    if (!res.ok) throw new Error('not-ready');
                    if (sawDown) { clearInterval(timer); window.location.href = '/'; }
                })
                .catch(function () {
                    sawDown = true;
                    if (hint) { hint.textContent = 'Redémarrage en cours…'; }
                })
                .finally(function () {
                    if (sawDown && attempts > 10 && hint) {
                        hint.textContent = 'Si l\'application ne revient pas seule (mode développement), relancez-la puis rechargez la page.';
                    }
                    if (attempts > 120) { clearInterval(timer); }
                });
        }, 1500);
    }

    /**
     * Anti-rétrofuite : si l'utilisateur tente de passer le toggle directement
     * (par devtools, ancien code), on force le rollback à l'état réel.
     */
    function setOfflineForced(forced) {
        // Conservé pour compat ancienne API. Délègue maintenant à requestOfflineModeChange.
        if (forced === isOfflineForced()) return;
        requestOfflineModeChange(forced);
    }
    function syncOfflineToggleUI(forced) {
        // Toggle dans le drawer sync
        const t = document.querySelector('[data-offline-toggle]');
        if (t) t.checked = forced;
        // Pill statut dans le menu profil
        const pill = document.querySelector('[data-offline-status-pill]');
        if (pill) {
            pill.textContent = forced ? 'Hors ligne' : 'En ligne';
            pill.classList.toggle('lc-badge--warning', forced);
            pill.classList.toggle('lc-badge--gray', !forced);
        }
        // Libelle dynamique du raccourci (menu profil) : decrit l'action a venir.
        const shortcutLabel = document.querySelector('[data-offline-toggle-shortcut] span:first-of-type');
        if (shortcutLabel) {
            shortcutLabel.textContent = forced ? 'Passer En ligne' : 'Passer Hors ligne';
        }
    }

    function setSyncState(state, payload) {
        if (!syncBadge) return;
        // Si l'utilisateur a forcé le mode hors ligne, on ignore les états online/syncing.
        if (isOfflineForced() && state !== 'offline') state = 'offline';

        syncBadge.dataset.state = state;
        syncBadge.dataset.forced = isOfflineForced() ? 'true' : 'false';
        const label = syncBadge.querySelector('.lc-sync-status__label');
        if (!label) return;

        if (state === 'online') {
            label.textContent = 'En ligne';
        } else if (state === 'syncing') {
            const done = payload?.done ?? 0;
            const total = payload?.total ?? 0;
            label.textContent = total > 0
                ? `Synchronisation… ${done}/${total}`
                : 'Synchronisation…';
        } else if (state === 'offline') {
            const pending = payload?.pending ?? 0;
            const suffix = isOfflineForced() ? ' (manuel)' : '';
            label.textContent = pending > 0
                ? `Hors ligne — ${pending} modification${pending > 1 ? 's' : ''} en attente${suffix}`
                : `Hors ligne${suffix}`;
        }

        // Banner haut de page
        if (offlineBanner) {
            const isOffline = (state === 'offline');
            const bannerDismissed = sessionStorage.getItem('lc-banner-dismissed') === '1';
            if (isOffline && !bannerDismissed) {
                offlineBanner.hidden = false;
                shell?.classList.add('lc-shell--banner-on');
            } else {
                offlineBanner.hidden = true;
                shell?.classList.remove('lc-shell--banner-on');
            }
        }

        // Désactive les actions impossibles offline (boutons marqués [data-online-only])
        document.querySelectorAll('[data-online-only]').forEach(btn => {
            if (state === 'offline') {
                btn.setAttribute('disabled', '');
                btn.setAttribute('data-tip', 'Indisponible hors ligne — sera possible une fois reconnecté');
            } else {
                btn.removeAttribute('disabled');
                btn.removeAttribute('data-tip');
            }
        });
    }

    // Banner close
    document.querySelector('[data-offline-banner-close]')?.addEventListener('click', () => {
        sessionStorage.setItem('lc-banner-dismissed', '1');
        if (offlineBanner) offlineBanner.hidden = true;
        shell?.classList.remove('lc-shell--banner-on');
    });

    // Détection navigateur online/offline (le check forcé est dans setSyncState)
    window.addEventListener('online', () => setSyncState('online'));
    window.addEventListener('offline', () => {
        sessionStorage.removeItem('lc-banner-dismissed');
        setSyncState('offline', { pending: 0 });
    });

    // === Toggle "Travailler hors ligne" === (drawer sync + menu profil)
    // Chaque clic ouvre la modal admin AVANT d'appliquer le changement.
    const offlineToggleEl = document.querySelector('[data-offline-toggle]');
    offlineToggleEl?.addEventListener('change', (e) => {
        const target = e.target.checked;
        // On rollback immédiatement la checkbox visuelle ; elle sera recochée
        // si la verification serveur passe.
        e.target.checked = isOfflineForced();
        requestOfflineModeChange(target);
    });
    document.querySelector('[data-offline-toggle-shortcut]')?.addEventListener('click', (e) => {
        e.preventDefault();
        document.querySelectorAll('.lc-dropdown--open').forEach(d => d.classList.remove('lc-dropdown--open'));
        requestOfflineModeChange(!isOfflineForced());
    });

    // === Modal d'auth : Cancel ===
    document.querySelector('[data-offline-auth-cancel]')?.addEventListener('click', closeOfflineAuthModal);
    document.querySelector('[data-offline-auth-modal]')?.addEventListener('click', (e) => {
        if (e.target.matches('[data-offline-auth-modal]')) closeOfflineAuthModal();
    });

    // === Modal d'auth : password visibility toggle ===
    document.querySelector('[data-pwd-toggle-inline]')?.addEventListener('click', (e) => {
        const btn = e.currentTarget;
        const input = btn.parentElement.querySelector('input');
        if (!input) return;
        const showing = input.type === 'text';
        input.type = showing ? 'password' : 'text';
        const open = btn.querySelector('[data-pwd-icon="open"]');
        const closed = btn.querySelector('[data-pwd-icon="closed"]');
        if (open) open.style.display = showing ? '' : 'none';
        if (closed) closed.style.display = showing ? 'none' : '';
    });

    // === Modal d'auth : Submit (verify password + permission côté serveur) ===
    document.querySelector('[data-offline-auth-form]')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const modal = document.querySelector('[data-offline-auth-modal]');
        if (!modal) return;
        const targetForced = modal.dataset.targetForced === 'true';
        const password = modal.querySelector('#lc-offline-auth-password')?.value || '';
        const submitBtn = modal.querySelector('[data-offline-auth-submit]');
        const errBox = modal.querySelector('[data-offline-auth-error]');
        const errMsg = modal.querySelector('[data-offline-auth-error-msg]');

        // UI : disable bouton + cache erreur précédente
        if (submitBtn) { submitBtn.disabled = true; submitBtn.classList.add('btn--loading'); }
        errBox?.classList.add('d-none');

        try {
            // CSRF : Identity utilise le cookie ; pour minimal API on POST direct.
            // L'auth est cookie-based ; on envoie credentials: 'same-origin'.
            const r = await fetch('/api/v1/offline-mode/verify', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify({ enable: targetForced, password })
            });

            if (r.ok) {
                // Bascule REELLE : le serveur a enregistre le nouveau mode et redemarre.
                // On affiche un ecran d'attente puis on recharge une fois l'app revenue.
                closeOfflineAuthModal();
                showRestartOverlay(targetForced);
                return;
            }

            // Affiche le message d'erreur serveur
            const data = await r.json().catch(() => ({}));
            if (errBox && errMsg) {
                errMsg.textContent = data.error || (
                    r.status === 401 ? 'Mot de passe incorrect.' :
                    r.status === 403 ? 'Action réservée aux administrateurs.' :
                    'Erreur de vérification (' + r.status + ').'
                );
                errBox.classList.remove('d-none');
            }
        } catch (err) {
            if (errBox && errMsg) {
                errMsg.textContent = 'Erreur réseau : impossible de joindre le serveur pour valider.';
                errBox.classList.remove('d-none');
            }
        } finally {
            if (submitBtn) { submitBtn.disabled = false; submitBtn.classList.remove('btn--loading'); }
        }
    });

    // État initial : applique le forçage si actif, sinon état navigateur
    syncOfflineToggleUI(isOfflineForced());
    setSyncState(isOfflineForced() ? 'offline' : (navigator.onLine ? 'online' : 'offline'));

    // ====================================================================
    // 5. SYNC DRAWER (panneau latéral droit)
    // ====================================================================
    const syncDrawer = document.querySelector('[data-sync-drawer]');
    const syncBackdrop = document.querySelector('[data-sync-backdrop]');

    function openSyncDrawer() {
        syncDrawer?.classList.add('lc-drawer--open');
        syncBackdrop?.classList.add('lc-drawer-backdrop--open');
    }
    function closeSyncDrawer() {
        syncDrawer?.classList.remove('lc-drawer--open');
        syncBackdrop?.classList.remove('lc-drawer-backdrop--open');
    }

    document.querySelector('[data-sync-open]')?.addEventListener('click', openSyncDrawer);
    document.querySelector('[data-sync-close]')?.addEventListener('click', closeSyncDrawer);
    syncBackdrop?.addEventListener('click', closeSyncDrawer);

    // ====================================================================
    // 6. CONFLICT RESOLUTION MODAL
    // ====================================================================
    const conflictModal = document.querySelector('[data-conflict-modal]');

    function openConflict(payload) {
        if (!conflictModal) return;
        if (payload?.entityLabel) {
            const el = conflictModal.querySelector('[data-conflict-entity]');
            if (el) el.textContent = payload.entityLabel;
        }
        conflictModal.classList.add('lc-modal-backdrop--open');
    }
    function closeConflict() {
        conflictModal?.classList.remove('lc-modal-backdrop--open');
    }

    conflictModal?.querySelectorAll('[data-conflict-close], [data-conflict-action]').forEach(btn => {
        btn.addEventListener('click', () => closeConflict());
    });
    conflictModal?.addEventListener('click', (e) => {
        if (e.target === conflictModal) closeConflict();
    });

    // ====================================================================
    // 7. RACCOURCIS CLAVIER : Ctrl+K = focus recherche · Esc = ferme overlays
    // ====================================================================
    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
            e.preventDefault();
            const search = document.querySelector('[data-global-search]');
            search?.focus();
            search?.select();
        }
        if (e.key === 'Escape') {
            closeSyncDrawer();
            closeConflict();
            closeOfflineAuthModal();
            closeMobileSidebar();
            document.querySelectorAll('.lc-dropdown--open').forEach(d => d.classList.remove('lc-dropdown--open'));
        }
    });

    // ====================================================================
    // 8. API PUBLIQUE — pour PWA service worker / IndexedDB
    // ====================================================================
    window.LigCom = {
        // Thème
        setThemeMode,
        getThemeMode,
        // Synchro / offline (toggle nécessite auth admin via la modal)
        setSyncState,
        requestOfflineModeChange,   // ouvre la modal d'auth admin
        isOfflineForced,
        openSyncDrawer,
        closeSyncDrawer,
        openConflict,
        closeConflict,
        // Helpers console :
        //   LigCom.setThemeMode('dark')
        //   LigCom.requestOfflineModeChange(true)   // ouvre la modal admin
        //   LigCom.setSyncState('syncing', {done:3, total:12})
    };

})();
