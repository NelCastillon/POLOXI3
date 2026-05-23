// AMS Enterprise Shell — global keyboard shortcuts and utilities
window.amsShell = (function () {
    'use strict';

    var _ref     = null;
    var _handler = null;
    var _portals = {};

    var kpiAllLabels = ['all', 'total', 'total users', 'total documents', 'total events', 'total invoices', 'total payments', 'total portal users'];

    function norm(value) {
        return (value || '').toString().trim().toLowerCase();
    }

    function getKpiLabel(card) {
        var label = card.querySelector('[class*="kpi-label"], [class*="kpi-lbl"], [class$="-kl"], [class*="-kl"], .pc-kl, .api-kl, .pu-kl, .pr-kl, .pd-kl, .pa-kl, .fin-kpi-label');
        if (label && label.textContent) return label.textContent.trim();

        var text = (card.textContent || '').replace(/\s+/g, ' ').trim();
        return text.replace(/^[\$€£]?[\d,\.]+\s*(%|hrs?|days?|d)?\s*/i, '').trim();
    }

    function getFilterTokens(label) {
        var lower = norm(label);
        if (!lower || kpiAllLabels.some(function (x) { return lower === x || lower.indexOf(x + ' ') === 0; })) return [];

        var mappings = [
            ['active 30', ['active']],
            ['pending invite', ['pending']],
            ['pending amount', ['pending']],
            ['pending', ['pending']],
            ['paid amount', ['paid']],
            ['paid', ['paid']],
            ['open requests', ['open']],
            ['open tasks', ['open']],
            ['open', ['open']],
            ['in progress', ['in progress']],
            ['fulfilled', ['fulfilled']],
            ['resolved', ['resolved']],
            ['suspended', ['suspended']],
            ['draft', ['draft']],
            ['drafts', ['draft']],
            ['sent', ['sent']],
            ['accepted', ['accepted']],
            ['expired', ['expired']],
            ['urgent', ['urgent']],
            ['overdue', ['overdue']],
            ['unread', ['unread']],
            ['unassigned', ['unassigned']],
            ['escalated', ['escalated']],
            ['email', ['email']],
            ['sms', ['sms']],
            ['portal', ['portal']],
            ['internal', ['internal note', 'internal']],
            ['mfa', ['mfa', 'shield-check']],
            ['disabled', ['disabled', 'inactive']],
            ['enabled', ['enabled', 'active']],
            ['shared', ['shared']],
            ['agency only', ['agency only']],
            ['warning', ['warning']],
            ['errors', ['error', 'warning']],
            ['error', ['error']]
        ];

        for (var i = 0; i < mappings.length; i++) {
            if (lower.indexOf(mappings[i][0]) >= 0) return mappings[i][1];
        }

        return [lower.replace(/\s*\([^)]*\)/g, '').trim()];
    }

    function findFilterScope(card) {
        var main = card.closest('main') || document.getElementById('ams-main') || document.body;
        var strip = card.closest('[class*="kpi-strip"], [class*="kpi-grid"]');
        return strip ? (strip.parentElement || main) : main;
    }

    function getFilterTargets(scope) {
        return Array.prototype.slice.call(scope.querySelectorAll([
            '.e-grid .e-row',
            '.e-treegrid .e-row',
            '.ci-msg-row',
            '.mn-notif-card',
            '.mkv-review-card',
            '.pc-cap-card',
            '.pwl-cap-row',
            '.pm-feature-tile',
            '.mke-rule-card',
            '.wf-task-card',
            '.task-card',
            '[data-kpi-filter-row]'
        ].join(','))).filter(function (row) {
            return !row.closest('[class*="kpi-strip"], [class*="kpi-grid"]');
        });
    }

    function clearKpiFilter(scope) {
        scope.querySelectorAll('.ams-kpi-filter-hidden').forEach(function (row) {
            row.classList.remove('ams-kpi-filter-hidden');
        });
        scope.querySelectorAll('.ams-kpi-card-active').forEach(function (card) {
            card.classList.remove('ams-kpi-card-active', 'pc-kpi-card--active');
            card.setAttribute('aria-pressed', 'false');
        });
    }

    function applyKpiFilter(card) {
        if (!card || card.tagName === 'BUTTON' || card.tagName === 'A') return;

        var scope = findFilterScope(card);
        var wasActive = card.classList.contains('ams-kpi-card-active');
        var label = getKpiLabel(card);
        var tokens = getFilterTokens(label);
        var rows = getFilterTargets(scope);

        clearKpiFilter(scope);
        if (wasActive || tokens.length === 0 || rows.length === 0) return;

        card.classList.add('ams-kpi-card-active', 'pc-kpi-card--active');
        card.setAttribute('aria-pressed', 'true');

        rows.forEach(function (row) {
            var text = norm(row.textContent);
            var match = tokens.some(function (token) { return token && text.indexOf(token) >= 0; });
            if (!match) row.classList.add('ams-kpi-filter-hidden');
        });
    }

    function getModalRoot() {
        var root = document.getElementById('ams-modal-root');
        if (!root) {
            root = document.createElement('div');
            root.id = 'ams-modal-root';
            document.body.appendChild(root);
        }

        return root;
    }

    function lockBodyForModals() {
        var root = document.getElementById('ams-modal-root');
        var hasModal = root && root.children.length > 0;
        document.body.classList.toggle('ams-modal-open', !!hasModal);
        document.body.style.overflow = hasModal ? 'hidden' : '';
    }

    document.addEventListener('click', function (event) {
        var card = event.target.closest('[class*="kpi-card"], [class*="kpi-tile"], .pc-kpi-card, .api-kpi-card, .pu-kpi-card, .pr-kpi-card, .pd-kpi-card, .pa-kpi-card');
        if (!card) return;
        applyKpiFilter(card);
    });

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        var card = event.target.closest('[class*="kpi-card"], [class*="kpi-tile"], .pc-kpi-card, .api-kpi-card, .pu-kpi-card, .pr-kpi-card, .pd-kpi-card, .pa-kpi-card');
        if (!card) return;
        if (card.tagName === 'BUTTON' || card.tagName === 'A') return;
        event.preventDefault();
        applyKpiFilter(card);
    });

    return {
        /** Register Ctrl+K / ⌘K keyboard shortcut; pass a DotNetObjectReference. */
        init: function (dotnetRef) {
            _ref = dotnetRef;
            _handler = function (e) {
                if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                    e.preventDefault();
                    if (_ref) _ref.invokeMethodAsync('OpenFromJs');
                }
            };
            document.addEventListener('keydown', _handler);
        },

        /** Remove event listeners and release the .NET reference. */
        dispose: function () {
            if (_handler) {
                document.removeEventListener('keydown', _handler);
                _handler = null;
            }
            _ref = null;
        },

        /** Focus an element by id after a short tick to accommodate Blazor re-render. */
        focus: function (id) {
            var el = document.getElementById(id);
            if (el) { setTimeout(function () { el.focus(); }, 40); }
        },

        /** Toggle the dark-theme attribute on the document root. */
        setTheme: function (dark) {
            document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
        },

        /** Move a Blazor-rendered modal element to the document body so it escapes clipped layouts. */
        portalModal: function (id) {
            var el = document.getElementById(id);
            if (!el) {
                lockBodyForModals();
                return false;
            }

            var root = getModalRoot();
            if (el.parentElement !== root) {
                _portals[id] = { parent: el.parentNode, next: el.nextSibling };
                root.appendChild(el);
            }
            lockBodyForModals();
            return true;
        },

        /** Return a portaled modal to normal Blazor disposal flow when it is still present. */
        releaseModal: function (id) {
            var el = document.getElementById(id);
            if (el && el.parentElement && el.parentElement.id === 'ams-modal-root') {
                var portal = _portals[id];
                if (portal && portal.parent && portal.parent.isConnected) {
                    portal.parent.insertBefore(el, portal.next && portal.next.isConnected ? portal.next : null);
                } else {
                    el.remove();
                }
            }

            delete _portals[id];
            lockBodyForModals();
        },

        /** Trigger a file download from a base-64 encoded string. */
        downloadBase64: function (filename, mimeType, base64) {
            var bytes = atob(base64);
            var buf = new Uint8Array(bytes.length);
            for (var i = 0; i < bytes.length; i++) buf[i] = bytes.charCodeAt(i);
            var blob = new Blob([buf], { type: mimeType });
            var url  = URL.createObjectURL(blob);
            var a    = document.createElement('a');
            a.href     = url;
            a.download = filename;
            a.click();
            URL.revokeObjectURL(url);
        }
    };
}());
