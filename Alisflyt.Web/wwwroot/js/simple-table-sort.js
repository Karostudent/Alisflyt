(function () {
    'use strict';

    const numberPattern = /^[-+]?\d+(?:[.,]\d+)?$/;

    function normalizeNumber(value) {
        let normalized = String(value ?? '').trim().replace(/[\s\u00a0]/g, '');
        if (!normalized) return null;

        const lastComma = normalized.lastIndexOf(',');
        const lastDot = normalized.lastIndexOf('.');
        if (lastComma !== -1 && lastDot !== -1) {
            if (lastComma > lastDot) {
                normalized = normalized.replace(/\./g, '').replace(',', '.');
            } else {
                normalized = normalized.replace(/,/g, '');
            }
        } else if (lastComma !== -1) {
            normalized = normalized.replace(',', '.');
        } else if ((normalized.match(/\./g) || []).length > 1) {
            normalized = normalized.replace(/\./g, '');
        } else if (lastDot !== -1 && normalized.length - lastDot - 1 === 3) {
            normalized = normalized.replace('.', '');
        }

        const number = Number(normalized);
        return Number.isFinite(number) ? number : null;
    }

    function getCellSortValue(td) {
        if (!td) return '';
        const dataValue = td.getAttribute('data-sort-value');
        return dataValue !== null ? dataValue : td.textContent.trim();
    }

    function compareText(a, b) {
        return String(a).localeCompare(String(b), 'nb-NO', {
            numeric: true,
            sensitivity: 'base'
        });
    }

    function isMissingValue(value, type) {
        const normalized = String(value ?? '').trim();
        if (!normalized) return true;
        if (type === 'number') return normalizeNumber(normalized) === null;
        if (type === 'date') return Number.isNaN(Date.parse(normalized));
        return false;
    }

    function compareValues(a, b, type) {
        const left = String(a ?? '').trim();
        const right = String(b ?? '').trim();

        if (isMissingValue(left, type) || isMissingValue(right, type)) return 0;

        if (type === 'number' || (!type && numberPattern.test(left) && numberPattern.test(right))) {
            const leftNumber = normalizeNumber(left);
            const rightNumber = normalizeNumber(right);
            if (leftNumber !== null && rightNumber !== null) return leftNumber - rightNumber;
            if (leftNumber === null && rightNumber !== null) return 1;
            if (leftNumber !== null && rightNumber === null) return -1;
        }

        if (type === 'date') {
            const leftDate = Date.parse(left);
            const rightDate = Date.parse(right);
            if (!Number.isNaN(leftDate) && !Number.isNaN(rightDate)) return leftDate - rightDate;
        } else if (!type && !Number.isNaN(Date.parse(left)) && !Number.isNaN(Date.parse(right))) {
            return Date.parse(left) - Date.parse(right);
        }

        return compareText(left, right);
    }

    function setupTable(table) {
        const thead = table.querySelector('thead');
        const tbody = table.querySelector('tbody');
        if (!thead || !tbody) return;

        const headers = Array.from(thead.querySelectorAll('th'));
        let active = { index: -1, asc: true };

        function clearIndicators() {
            headers.forEach(header => {
                const indicator = header.querySelector('.sort-indicator');
                if (indicator) indicator.textContent = '';
                header.setAttribute('aria-sort', 'none');
            });
        }

        function sortByColumn(index) {
            const rows = Array.from(tbody.querySelectorAll('tr'));
            const sortableRows = rows
                .map((row, originalIndex) => ({
                    row,
                    originalIndex,
                    key: getCellSortValue(row.children[index])
                }))
                .filter(item => item.row.getAttribute('data-sort-ignore') !== 'true');
            const type = headers[index].getAttribute('data-sort-type');

            if (active.index === index) {
                active.asc = !active.asc;
            } else {
                active.index = index;
                active.asc = true;
            }

            sortableRows.sort((left, right) => {
                const leftMissing = isMissingValue(left.key, type);
                const rightMissing = isMissingValue(right.key, type);
                if (leftMissing || rightMissing) {
                    if (leftMissing && rightMissing) return left.originalIndex - right.originalIndex;
                    return leftMissing ? 1 : -1;
                }

                const result = compareValues(left.key, right.key, type);
                if (result !== 0) return active.asc ? result : -result;
                return left.originalIndex - right.originalIndex;
            });

            let sortedIndex = 0;
            const sortedRows = rows.map(row => {
                if (row.getAttribute('data-sort-ignore') === 'true') return row;
                return sortableRows[sortedIndex++].row;
            });

            const fragment = document.createDocumentFragment();
            sortedRows.forEach(row => fragment.appendChild(row));
            tbody.appendChild(fragment);

            clearIndicators();
            const indicator = headers[index].querySelector('.sort-indicator');
            if (indicator) indicator.textContent = active.asc ? ' ▲' : ' ▼';
            headers[index].setAttribute('aria-sort', active.asc ? 'ascending' : 'descending');
        }

        headers.forEach((header, index) => {
            if (header.getAttribute('data-sortable') !== 'true') return;

            const activate = event => {
                event.preventDefault();
                sortByColumn(index);
            };
            header.addEventListener('click', activate);
            header.addEventListener('keydown', event => {
                if (event.key === 'Enter' || event.key === ' ') activate(event);
            });
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('table.sortable-table').forEach(setupTable);
    });
})();
