(() => {
    const rowSelector = ".e-grid .e-row, .e-treegrid .e-row, table tbody tr";
    const interactiveSelector = "a, button, input, select, textarea, label, summary, [role='button'], [contenteditable='true']";

    const isEditAction = (element) => {
        const text = (element.textContent || "").trim();
        const title = element.getAttribute("title") || "";
        const ariaLabel = element.getAttribute("aria-label") || "";
        const href = element.getAttribute("href") || "";

        return /\bedit\b/i.test(`${text} ${title} ${ariaLabel} ${href}`)
            || element.querySelector(".bi-pencil, .bi-pencil-fill, .fa-pencil, .fa-edit, .oi-pencil") !== null;
    };

    document.addEventListener("dblclick", (event) => {
        if (event.defaultPrevented || event.target.closest(interactiveSelector)) {
            return;
        }

        const row = event.target.closest(rowSelector);
        if (!row) {
            return;
        }

        const editAction = Array.from(row.querySelectorAll("button, a"))
            .find(isEditAction);

        if (!editAction || editAction.disabled || editAction.getAttribute("aria-disabled") === "true") {
            return;
        }

        event.preventDefault();
        editAction.click();
    }, true);
})();
