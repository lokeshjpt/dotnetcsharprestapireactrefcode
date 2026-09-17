import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import Pagination from '../../components/UI/Pagination';
import SortableHeader from '../../components/UI/SortableHeader';
import Loader from '../../components/Loader/Loader';
import useInspectors, { cleanParams } from './useInspectors';
import '../Search/SearchPage.css';
import './Inspections.css';

const DEFAULT_PAGE_SIZE = 25;

function contractor(item) {
  const parts = [item.appBusinessName, item.applicantName].filter((value) => value && value.trim());
  return parts.join(' - ');
}

// Shared list for the "Pending WCR" and "Pending GeoLog" screens (pending_dwr_list.jsp /
// pending_geo_list.jsp): identical layout differing only by title/due-date label and fetch function.
function DueInspectionList({ title, subtitle, dueLabel, fetchFn, itemNoun, ariaLabel, hint }) {
  const inspectors = useInspectors();
  const [filters, setFilters] = useState({ inspectorId: '', appId: '' });
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [searched, setSearched] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('appId');
  const [sortDir, setSortDir] = useState('asc');

  const runSearch = useCallback(async ({ page: targetPage = 1, size = pageSize, sortByKey = sortBy, sortDirVal = sortDir, current = filters } = {}) => {
    setError('');
    setLoading(true);
    setSearched(true);
    try {
      const params = cleanParams({ ...current, page: targetPage, pageSize: size, sortBy: sortByKey, sortDir: sortDirVal });
      const data = await fetchFn(params);
      setItems(data.items || []);
      setTotalCount(data.totalCount ?? (data.items ? data.items.length : 0));
      setPage(targetPage);
      setPageSize(size);
      setSortBy(sortByKey);
      setSortDir(sortDirVal);
    } catch {
      setError('The list could not be loaded. Verify the API is available.');
      setItems([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, [fetchFn, filters, pageSize, sortBy, sortDir]);

  useEffect(() => { runSearch(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSort = (key) => {
    const nextDir = key === sortBy ? (sortDir === 'asc' ? 'desc' : 'asc') : 'asc';
    runSearch({ page: 1, sortByKey: key, sortDirVal: nextDir });
  };

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel search-card">
        <h1 className="page-title">{title}</h1>
        <p className="page-subtitle">{subtitle}</p>

        <form className="insp-filters" onSubmit={(event) => { event.preventDefault(); runSearch({ page: 1 }); }}>
          <div className="search-field">
            <label htmlFor="due-inspector">Inspector</label>
            <select id="due-inspector" className="form-control" value={filters.inspectorId}
              onChange={(event) => setFilters((current) => ({ ...current, inspectorId: event.target.value }))}>
              <option value="">All Inspectors</option>
              {inspectors.map((insp) => <option key={insp.code} value={insp.code}>{insp.label}</option>)}
            </select>
          </div>
          <div className="search-field">
            <label htmlFor="due-appid">Application Id</label>
            <input id="due-appid" type="text" inputMode="numeric" maxLength={13} className="form-control" value={filters.appId}
              onChange={(event) => setFilters((current) => ({ ...current, appId: event.target.value }))} />
          </div>
          <div className="insp-filters__submit">
            <button type="submit" className="btn btn-primary" disabled={loading}>{loading ? 'Refreshing…' : 'Refresh List'}</button>
          </div>
        </form>
        {hint && <p className="muted-text">{hint}</p>}
      </div>

      {error && <div className="alert alert-error search-results__alert">{error}</div>}

      <div className="panel search-results">
        <div className="search-results__head">
          <h2 className="search-results__title">Results</h2>
          {totalCount > 0 && <span className="search-results__count">{totalCount} {itemNoun}(s)</span>}
        </div>
        <div className="table-scroll" role="region" aria-label={ariaLabel} tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <SortableHeader label="Application Id" sortKey="appId" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Inspector" sortKey="inspector" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <th scope="col">Permit#</th>
                <th scope="col">Contractor</th>
                <SortableHeader label="Project Site" sortKey="city" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label={dueLabel} sortKey="dueDate" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="6" className="muted-text">
                    {searched && !loading ? 'No permits due at this time.' : 'Loading…'}
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr key={item.appId}>
                    <td><Link to={`/applications/${item.appId}`}>{item.appId}</Link></td>
                    <td>{item.inspectorName}</td>
                    <td>{item.permitRange || '—'}</td>
                    <td>{contractor(item)}</td>
                    <td>{item.projectSiteLocation}</td>
                    <td>{item.dueDate || '—'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        {searched && (
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={totalCount}
            onPageChange={(next) => runSearch({ page: next })}
            onPageSizeChange={(size) => runSearch({ page: 1, size })}
            loading={loading}
            itemLabel={itemNoun}
          />
        )}
      </div>
    </div>
  );
}

export default DueInspectionList;
