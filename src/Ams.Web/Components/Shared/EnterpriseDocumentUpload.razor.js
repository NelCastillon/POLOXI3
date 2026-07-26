const initializedDropZones = new WeakSet();

export function initialize(dropZone, fileInput) {
    if (!dropZone || !fileInput || initializedDropZones.has(dropZone)) {
        return;
    }

    const preventFileNavigation = event => {
        event.preventDefault();

        if (event.dataTransfer) {
            event.dataTransfer.dropEffect = 'copy';
        }
    };

    dropZone.addEventListener('dragenter', preventFileNavigation);
    dropZone.addEventListener('dragover', preventFileNavigation);
    dropZone.addEventListener('drop', event => {
        event.preventDefault();

        const files = event.dataTransfer?.files;
        if (!files || files.length === 0) {
            return;
        }

        fileInput.files = files;
        fileInput.dispatchEvent(new Event('change', { bubbles: true }));
    });

    initializedDropZones.add(dropZone);
}
