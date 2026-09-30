(function () {
    'use strict';

    const isNumber = (v) => /^-?\d+(?:[\.,]\d+)?$/.test(v.trim());
    const toNumber = (v) => Number(v.replace(',', '.'));
    const isISODate = (v) => !isNaN(Date.parse(v)); // used only for data-sort-value which we set to ISO

    function getCellSortValue(td) {
        if (!td) return '';
        const dv = td.getAttribute('data-sort-value');
        if (dv !== null) return dv;
        return td.textContent.trim();
    }

    function stableSort(arr, compare) {
        return arr
            .map((v, i) => ({ v, i }))
            .sort((a, b) => {
                const r = compare(a.v, b.v);
                return r !== 0 ? r : a.i - b.i;
            })
            .map(x => x.v);
    }

    function compareValues(a, b) {
        // both null/undefined
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        // numeric?
        if (isNumber(a) && isNumber(b)) return toNumber(a) - toNumber(b);

        // ISO date?
        if (isISODate(a) && isISODate(b)) {
            return new Date(a) - new Date(b);
        }

        // fallback to localeCompare
        return a.localeCompare(b, navigator.language || 'nb-NO', { numeric: true });
    }

    function setupTable(table) {
        const thead = table.querySelector('thead');
        const tbody = table.querySelector('tbody');
        if (!thead || !tbody) return;

        const headers = Array.from(thead.querySelectorAll('th'));
        let active = { index: -1, asc: true };

        function clearIndicators() {
            headers.forEach(h => {
                const ind = h.querySelector('.sort-indicator');
                if (ind) ind.textContent = '';
                h.setAttribute('aria-sort', 'none');
            });
        }

        function sortByColumn(idx) {
            const rows = Array.from(tbody.querySelectorAll('tr'));
            const mapped = rows.map(r => ({ row: r, key: getCellSortValue(r.children[idx]) }));
            const compare = (a, b) => compareValues(a.key, b.key);
            let sorted = stableSort(mapped, compare);

            if (active.index === idx) {
                // toggle
                active.asc = !active.asc;
            } else {
                active.index = idx;
                active.asc = true;
            }

            if (!active.asc) sorted = sorted.reverse();

            // reattach
            const frag = document.createDocumentFragment();
            sorted.forEach(s => frag.appendChild(s.row));
            tbody.appendChild(frag);

            // update indicators
            clearIndicators();
            const indicator = headers[idx].querySelector('.sort-indicator');
            if (indicator) indicator.textContent = active.asc ? ' ▲' : ' ▼';
            headers[idx].setAttribute('aria-sort', active.asc ? 'ascending' : 'descending');
        }

        headers.forEach((th, idx) => {
            if (th.getAttribute('data-sortable') === 'true') {
                const activate = (e) => {
                    e.preventDefault();
                    sortByColumn(idx);
                };
                th.addEventListener('click', activate);
                th.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                        activate(e);
                    }
                });
            }
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('table.sortable-table').forEach(setupTable);
    });
})();
