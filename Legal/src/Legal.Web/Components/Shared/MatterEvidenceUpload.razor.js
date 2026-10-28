// Drag-and-drop interop for MatterEvidenceUpload.razor.
// Forwards files dropped on the zone into the component's hidden <input type="file">
// (the element Blazor's InputFile binds to) and dispatches a change event so the
// existing OnFilesSelectedAsync handler processes them identically to a browse click.
// Also toggles a drag-active flag on the .NET component for styling.

export function init(zone, dotNet) {
    if (!zone) return;

    const input = zone.querySelector('input[type="file"]');
    let depth = 0;

    const setActive = (active) => {
        try { dotNet.invokeMethodAsync('SetDragActive', active); } catch { /* disposed */ }
    };

    const onDragEnter = (e) => {
        e.preventDefault();
        depth++;
        setActive(true);
    };

    const onDragOver = (e) => {
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
    };

    const onDragLeave = (e) => {
        e.preventDefault();
        depth = Math.max(0, depth - 1);
        if (depth === 0) setActive(false);
    };

    const onDrop = (e) => {
        e.preventDefault();
        depth = 0;
        setActive(false);
        if (!input || !e.dataTransfer || !e.dataTransfer.files || e.dataTransfer.files.length === 0) return;
        try {
            input.files = e.dataTransfer.files;
            input.dispatchEvent(new Event('change', { bubbles: true }));
        } catch {
            // Browsers that block assigning FileList fall back to the browse button.
        }
    };

    zone.addEventListener('dragenter', onDragEnter);
    zone.addEventListener('dragover', onDragOver);
    zone.addEventListener('dragleave', onDragLeave);
    zone.addEventListener('drop', onDrop);

    zone._mevCleanup = () => {
        zone.removeEventListener('dragenter', onDragEnter);
        zone.removeEventListener('dragover', onDragOver);
        zone.removeEventListener('dragleave', onDragLeave);
        zone.removeEventListener('drop', onDrop);
    };
}

export function dispose(zone) {
    if (zone && typeof zone._mevCleanup === 'function') {
        zone._mevCleanup();
        delete zone._mevCleanup;
    }
}
