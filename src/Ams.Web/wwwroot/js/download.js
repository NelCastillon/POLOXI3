// Shared file-download helper (migrated from the former wwwroot/js/shell.js).
window.amsDownload = {
    /** Trigger a client-side download of a base64-encoded file. */
    downloadBase64: function (filename, mimeType, base64) {
        const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        const blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename || 'download';
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
};
