import './SortableHeader.css';

// Accessible sortable column header (ARIA table sort pattern). The whole label is a button; the
// parent <th> carries aria-sort so assistive tech announces the current sort state. Clicking calls
// onSort(sortKey) — the parent decides the next direction and refetches (server-side).
function SortableHeader({ label, sortKey, sortBy, sortDir, onSort, scope = 'col' }) {
  const active = sortBy === sortKey;
  const ariaSort = active ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none';
  const icon = active ? (sortDir === 'asc' ? '\u25B2' : '\u25BC') : '\u21C5';

  return (
    <th scope={scope} aria-sort={ariaSort} className="sortable-th">
      <button
        type="button"
        className="sortable-th__btn"
        onClick={() => onSort(sortKey)}
        aria-label={`Sort by ${label}`}
      >
        <span className="sortable-th__label">{label}</span>
        <span className="sortable-th__icon" aria-hidden="true">{icon}</span>
      </button>
    </th>
  );
}

export default SortableHeader;
