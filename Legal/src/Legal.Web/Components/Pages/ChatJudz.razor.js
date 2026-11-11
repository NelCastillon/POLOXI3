// Chat Judz — minimal browser-only interop (CSS isolation sibling module).
// Only genuine browser behaviors live here; all chat logic/state stays in C#/Blazor.

// Smoothly scrolls a thread element to its latest message.
export function scrollToLatest(element) {
    if (!element) return;
    element.scrollTo({ top: element.scrollHeight, behavior: 'smooth' });
}

// Grows a textarea to fit its content (up to a max height), then enables scrolling.
export function autoGrow(element, maxHeightPx) {
    if (!element) return;
    const max = typeof maxHeightPx === 'number' && maxHeightPx > 0 ? maxHeightPx : 200;
    element.style.height = 'auto';
    const next = Math.min(element.scrollHeight, max);
    element.style.height = next + 'px';
    element.style.overflowY = element.scrollHeight > max ? 'auto' : 'hidden';
}

// Copies text to the clipboard; returns true on success. Falls back to a temporary textarea
// when the async Clipboard API is unavailable (older browsers / insecure contexts).
export async function copyText(text) {
    try {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return true;
        }
    } catch { /* fall through to legacy path */ }

    try {
        const area = document.createElement('textarea');
        area.value = text;
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.focus();
        area.select();
        const ok = document.execCommand('copy');
        document.body.removeChild(area);
        return ok;
    } catch {
        return false;
    }
}
