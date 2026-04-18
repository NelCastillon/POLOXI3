// AMS Enterprise Shell — global keyboard shortcuts and utilities
window.amsShell = (function () {
    'use strict';

    var _ref     = null;
    var _handler = null;

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
