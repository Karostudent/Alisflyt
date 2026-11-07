(() => {
    const form = document.querySelector("[data-guidance-correction-form]");
    const rows = document.getElementById("guidance-session-rows");
    const template = document.getElementById("guidance-session-template");
    const addButton = document.getElementById("add-guidance-session");

    if (!form || !rows || !template || !addButton) return;

    const reindexRows = () => {
        rows.querySelectorAll(".guidance-session-row").forEach((row, index) => {
            row.querySelectorAll("[name]").forEach(input => {
                input.name = input.name.replace(/Sessions\[\d+\]/, `Sessions[${index}]`);
            });

            row.querySelectorAll("[id]").forEach(input => {
                input.id = input.id.replace(/Sessions_\d+__/, `Sessions_${index}__`);
            });

            row.querySelectorAll("label[for]").forEach(label => {
                label.htmlFor = label.htmlFor.replace(/Sessions_\d+__/, `Sessions_${index}__`);
            });
        });
    };

    const addRow = focus => {
        const fragment = template.content.cloneNode(true);
        rows.appendChild(fragment);
        reindexRows();
        if (focus) rows.lastElementChild?.querySelector("input[type=date]")?.focus();
    };

    addButton.addEventListener("click", () => addRow(true));

    rows.addEventListener("click", event => {
        const removeButton = event.target.closest("[data-remove-guidance-session]");
        if (!removeButton) return;

        removeButton.closest(".guidance-session-row")?.remove();
        reindexRows();
        addButton.focus();
    });

    form.addEventListener("submit", reindexRows);
})();
