// Click-to-sort for any table header marked data-sort="text" or data-sort="num".
// Numeric cells sort by data-value when present, so formatted money like "-$1,200" sorts correctly.

function cellValue(row: HTMLTableRowElement, index: number, numeric: boolean): string | number {
  const cell = row.cells[index];
  const raw = cell?.dataset.value ?? cell?.textContent?.trim() ?? "";
  if (!numeric) return raw.toLowerCase();
  const n = Number(raw.replace(/[$,%\s]/g, ""));
  return Number.isNaN(n) ? Number.NEGATIVE_INFINITY : n;
}

function sortBy(table: HTMLTableElement, header: HTMLTableCellElement): void {
  const body = table.tBodies[0];
  if (!body) return;
  const index = header.cellIndex;
  const numeric = header.dataset.sort === "num";
  const ascending = !header.classList.contains("sorted-asc");

  table.querySelectorAll("th[data-sort]").forEach((th) => th.classList.remove("sorted-asc", "sorted-desc"));
  header.classList.add(ascending ? "sorted-asc" : "sorted-desc");

  const rows = Array.from(body.rows);
  rows.sort((a, b) => {
    const x = cellValue(a, index, numeric);
    const y = cellValue(b, index, numeric);
    const order = x < y ? -1 : x > y ? 1 : 0;
    return ascending ? order : -order;
  });
  body.append(...rows);
}

document.querySelectorAll<HTMLTableCellElement>("table th[data-sort]").forEach((header) => {
  header.addEventListener("click", () => sortBy(header.closest("table")!, header));
});
