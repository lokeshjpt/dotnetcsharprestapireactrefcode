import { useId } from 'react';
import './Pagination.css';

const DEFAULT_PAGE_SIZES = [10, 25, 50, 100];

// Reusable server-side pagination control. Drives one page at a time against a backend that
// returns { items, totalCount }. `page` is 1-based. Renders nothing when there are no results.
function Pagination({
  page,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = DEFAULT_PAGE_SIZES,
  itemLabel = 'application',
  loading = false,
}) {
  const selectId = useId();

  if (!totalCount || totalCount <= 0) return null;

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const currentPage = Math.min(Math.max(1, page), totalPages);
  const firstItem = (currentPage - 1) * pageSize + 1;
  const lastItem = Math.min(currentPage * pageSize, totalCount);
  const pluralLabel = totalCount === 1 ? itemLabel : `${itemLabel}s`;

  const goTo = (next) => {
    const clamped = Math.min(Math.max(1, next), totalPages);
    if (clamped !== currentPage) onPageChange(clamped);
  };

  return (
    <nav className="pagination" aria-label="Pagination">
      <p className="pagination__status" aria-live="polite">
        Showing <strong>{firstItem}</strong>&ndash;<strong>{lastItem}</strong> of{' '}
        <strong>{totalCount}</strong> {pluralLabel}
      </p>
      <div className="pagination__controls">
        {onPageSizeChange && (
          <div className="pagination__page-size">
            <label htmlFor={selectId}>Per page</label>
            <select
              id={selectId}
              className="form-control pagination__select"
              value={pageSize}
              disabled={loading}
              onChange={(event) => onPageSizeChange(Number(event.target.value))}
            >
              {pageSizeOptions.map((size) => (
                <option key={size} value={size}>{size}</option>
              ))}
            </select>
          </div>
        )}
        <div className="pagination__nav">
          <button
            type="button"
            className="btn btn-default pagination__btn"
            onClick={() => goTo(currentPage - 1)}
            disabled={loading || currentPage <= 1}
            aria-label="Previous page"
          >
            &lsaquo; Prev
          </button>
          <span className="pagination__page">Page {currentPage} of {totalPages}</span>
          <button
            type="button"
            className="btn btn-default pagination__btn"
            onClick={() => goTo(currentPage + 1)}
            disabled={loading || currentPage >= totalPages}
            aria-label="Next page"
          >
            Next &rsaquo;
          </button>
        </div>
      </div>
    </nav>
  );
}

export default Pagination;
