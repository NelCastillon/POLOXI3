// Collocated JS module for MainLayout - theme handling and global shell behaviors
// (migrated from the former wwwroot/js/shell.js).

const kpiAllLabels = ['all', 'total', 'total users', 'total documents', 'total events', 'total invoices', 'total payments', 'total portal users'];

let _initialized = false;
let _modalObserver = null;

function norm(value) {
    return (value || '').toString().trim().toLowerCase();
}

function getKpiLabel(card) {
    const label = card.querySelector('[class*="kpi-label"], [class*="kpi-lbl"], [class$="-kl"], [class*="-kl"], .pc-kl, .api-kl, .pu-kl, .pr-kl, .pd-kl, .pa-kl, .fin-kpi-label');
    if (label && label.textContent) return label.textContent.trim();

    const text = (card.textContent || '').replace(/\s+/g, ' ').trim();
    return text.replace(/^[\$€£]?[\d,\.]+\s*(%|hrs?|days?|d)?\s*/i, '').trim();
}

function getFilterTokens(label) {
    const lower = norm(label);
    if (!lower || kpiAllLabels.some(x => lower === x || lower.indexOf(x + ' ') === 0)) return [];

    const mappings = [
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

    for (const [key, tokens] of mappings) {
        if (lower.indexOf(key) >= 0) return tokens;
    }

    return [lower.replace(/\s*\([^)]*\)/g, '').trim()];
}

function findFilterScope(card) {
    const main = card.closest('main') || document.getElementById('ams-main') || document.body;
    const strip = card.closest('[class*="kpi-strip"], [class*="kpi-grid"]');
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
    ].join(','))).filter(row => !row.closest('[class*="kpi-strip"], [class*="kpi-grid"]'));
}

function clearKpiFilter(scope) {
    scope.querySelectorAll('.ams-kpi-filter-hidden').forEach(row => {
        row.classList.remove('ams-kpi-filter-hidden');
    });
    scope.querySelectorAll('.ams-kpi-card-active').forEach(card => {
        card.classList.remove('ams-kpi-card-active', 'pc-kpi-card--active');
        card.setAttribute('aria-pressed', 'false');
    });
}

function applyKpiFilter(card) {
    if (!card || card.tagName === 'BUTTON' || card.tagName === 'A') return;

    const scope = findFilterScope(card);
    const wasActive = card.classList.contains('ams-kpi-card-active');
    const label = getKpiLabel(card);
    const tokens = getFilterTokens(label);
    const rows = getFilterTargets(scope);

    clearKpiFilter(scope);
    if (wasActive || tokens.length === 0 || rows.length === 0) return;

    card.classList.add('ams-kpi-card-active', 'pc-kpi-card--active');
    card.setAttribute('aria-pressed', 'true');

    rows.forEach(row => {
        const text = norm(row.textContent);
        const match = tokens.some(token => token && text.indexOf(token) >= 0);
        if (!match) row.classList.add('ams-kpi-filter-hidden');
    });
}

function lockBodyForModals() {
    const hasModal = !!document.querySelector('.ld-modal-backdrop, .um-modal-backdrop, .e-dlg-container');
    document.body.classList.toggle('ams-modal-open', !!hasModal);
    document.body.style.overflow = hasModal ? 'hidden' : '';
}

function onDocumentClick(event) {
    const card = event.target.closest('[class*="kpi-card"], [class*="kpi-tile"], .pc-kpi-card, .api-kpi-card, .pu-kpi-card, .pr-kpi-card, .pd-kpi-card, .pa-kpi-card');
    if (!card) return;
    applyKpiFilter(card);
}

function onDocumentKeydown(event) {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    const card = event.target.closest('[class*="kpi-card"], [class*="kpi-tile"], .pc-kpi-card, .api-kpi-card, .pu-kpi-card, .pr-kpi-card, .pd-kpi-card, .pa-kpi-card');
    if (!card) return;
    if (card.tagName === 'BUTTON' || card.tagName === 'A') return;
    event.preventDefault();
    applyKpiFilter(card);
}

/** One-time registration of global shell behaviors (KPI click-to-filter, modal scroll lock). */
export function init() {
    if (_initialized) return;
    _initialized = true;

    document.addEventListener('click', onDocumentClick);
    document.addEventListener('keydown', onDocumentKeydown);

    // Keep body scrolling locked while any modal backdrop / Syncfusion dialog is visible.
    _modalObserver = new MutationObserver(lockBodyForModals);
    _modalObserver.observe(document.body, { childList: true, subtree: true });
    lockBodyForModals();
}

/** Apply the selected theme to the document root and persist it in a cookie. */
export function applyTheme(dark) {
    const theme = dark ? 'dark' : 'light';
    document.documentElement.setAttribute('data-theme', theme);
    try {
        document.cookie = 'ams-theme=' + theme + ';path=/;max-age=31536000;samesite=lax';
    } catch (e) { /* cookies unavailable */ }
}
