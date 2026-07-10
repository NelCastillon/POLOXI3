// Collocated JS module for AppQuickSearch (migrated from the former wwwroot/js/shell.js).
// Registers the global Ctrl+K / Cmd+K shortcut and provides focus + cleanup helpers.

let _ref = null;
let _handler = null;

/** Register the Ctrl+K / Cmd+K shortcut that opens the quick-search overlay. */
export function init(dotnetRef) {
    dispose();
    _ref = dotnetRef;
    _handler = (e) => {
        if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
            e.preventDefault();
            _ref?.invokeMethodAsync('OpenFromJs');
        }
    };
    document.addEventListener('keydown', _handler);
}

/** Focus an element by id (used to focus the search input when the overlay opens). */
export function focus(id) {
    requestAnimationFrame(() => document.getElementById(id)?.focus());
}

/** Remove the shortcut listener and release the .NET reference. */
export function dispose() {
    if (_handler) {
        document.removeEventListener('keydown', _handler);
        _handler = null;
    }
    _ref = null;
}
