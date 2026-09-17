import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { searchPendingInspections } from '../../api/inspectionApi';
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

// Permits with Pending Inspections — parity with inspection_pending_list.jsp.
function InspectionsList() {
  const inspectors = useInspectors();
  const [filters, setFilters] = useState({ inspectionDate: '', inspectorId: '' });
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [searched, setSearched] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('projectStart');
  const [sortDir, setSortDir] = useState('asc');

  const runSearch = useCallback(async ({ page: targetPage = 1, size = pageSize, sortByKey = sortBy, sortDirVal = sortDir, current = filters } = {}) => {
    setError('');
    setLoading(true);
    setSearched(true);
    try {
      const params = cleanParams({ ...current, page: targetPage, pageSize: size, sortBy: sortByKey, sortDir: sortDirVal });
      const data = await searchPendingInspections(params);
      setItems(data.items || []);
      setTotalCount(data.totalCount ?? (data.items ? data.items.length : 0));
      setPage(targetPage);
      setPageSize(size);
      setSortBy(sortByKey);
      setSortDir(sortDirVal);
    } catch {
      setError('The inspections list could not be loaded. Verify the API is available.');
      setItems([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, [filters, pageSize, sortBy, sortDir]);

  useEffect(() => { runSearch(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSort = (key) => {
    const nextDir = key === sortBy ? (sortDir === 'asc' ? 'desc' : 'asc') : 'asc';
    runSearch({ page: 1, sortByKey: key, sortDirVal: nextDir });
  };

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel search-card">
        <h1 className="page-title">Permits with Pending Inspections</h1>
        <p className="page-subtitle">Search pending inspections by date or inspector.</p>
        <Link className="page-help-link" to="/help#inspections">
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> How inspections work — help
        </Link>

        <form className="insp-filters" onSubmit={(event) => { event.preventDefault(); runSearch({ page: 1 }); }}>
          <div className="search-field">
            <label htmlFor="ins-date">Inspection Date</label>
            <input id="ins-date" type="date" className="form-control" value={filters.inspectionDate}
              onChange={(event) => setFilters((current) => ({ ...current, inspectionDate: event.target.value }))} />
          </div>
          <div className="search-field">
            <label htmlFor="ins-inspector">Inspector</label>
            <select id="ins-inspector" className="form-control" value={filters.inspectorId}
              onChange={(event) => setFilters((current) => ({ ...current, inspectorId: event.target.value }))}>
              <option value="">All Inspectors</option>
              {inspectors.map((insp) => <option key={insp.code} value={insp.code}>{insp.label}</option>)}
            </select>
          </div>
          <div className="insp-filters__submit">
            <button type="submit" className="btn btn-primary" disabled={loading}>{loading ? 'Refreshing…' : 'Refresh List'}</button>
          </div>
        </form>
      </div>

      {error && <div className="alert alert-error search-results__alert">{error}</div>}

      <div className="panel search-results">
        <div className="search-results__head">
          <h2 className="search-results__title">Results</h2>
          {totalCount > 0 && <span className="search-results__count">{totalCount} permit(s)</span>}
        </div>
        <div className="table-scroll" role="region" aria-label="Pending inspections" tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Permit#</th>
                <SortableHeader label="Contractor" sortKey="contractor" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Project Site" sortKey="city" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Project Dates" sortKey="projectStart" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <th scope="col">Inspection Schedule</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="6" className="muted-text">
                    {searched && !loading ? 'No permits pending for inspection.' : 'Loading…'}
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr key={item.appId}>
                    <td>{item.permitRange || '—'}</td>
                    <td>{contractor(item)}</td>
                    <td>{item.projectSiteLocation}</td>
                    <td>{[item.projectStartDate, item.projectEndDate].filter(Boolean).join(' - ')}</td>
                    <td>
                      {item.schedule && item.schedule.length > 0 ? (
                        <ul className="insp-sched-list">
                          {item.schedule.map((line, idx) => (
                            <li key={`${item.appId}-${idx}`}>
                              {line.inspectionDate} {line.inspectionTimeDisp || ''} — {line.inspectorName || 'Unassigned'}
                            </li>
                          ))}
                        </ul>
                      ) : '—'}
                    </td>
                    <td>
                      <div className="insp-actions">
                        <Link className="btn btn-default btn-sm" to={`/inspections/${item.appId}`}>Update/Add</Link>
                        <Link className="btn btn-default btn-sm" to={`/permit/${item.appId}`} target="_blank" rel="noreferrer">Print Permit</Link>
                        <Link className="btn btn-default btn-sm" to={`/applications/${item.appId}`}>Open</Link>
                      </div>
                    </td>
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
            itemLabel="permit"
          />
        )}
      </div>
    </div>
  );
}

export default InspectionsList;
