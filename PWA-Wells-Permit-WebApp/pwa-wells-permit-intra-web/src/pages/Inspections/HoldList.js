import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { searchHoldList } from '../../api/inspectionApi';
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

// Permits On Hold List — parity with hold_list.jsp.
function HoldList() {
  const inspectors = useInspectors();
  const [filters, setFilters] = useState({ inspectorId: '' });
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
      const data = await searchHoldList(params);
      setItems(data.items || []);
      setTotalCount(data.totalCount ?? (data.items ? data.items.length : 0));
      setPage(targetPage);
      setPageSize(size);
      setSortBy(sortByKey);
      setSortDir(sortDirVal);
    } catch {
      setError('The hold list could not be loaded. Verify the API is available.');
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
        <h1 className="page-title">Permits on Hold Status</h1>
        <p className="page-subtitle">Search permits placed on hold by inspector.</p>

        <form className="insp-filters" onSubmit={(event) => { event.preventDefault(); runSearch({ page: 1 }); }}>
          <div className="search-field">
            <label htmlFor="hold-inspector">Inspector</label>
            <select id="hold-inspector" className="form-control" value={filters.inspectorId}
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
        <div className="table-scroll" role="region" aria-label="Permits on hold" tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Permit#</th>
                <th scope="col">Contractor</th>
                <SortableHeader label="Project Site" sortKey="city" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Project Dates" sortKey="projectStart" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Inspector" sortKey="inspector" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <th scope="col">Action</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="6" className="muted-text">
                    {searched && !loading ? 'No permits on hold.' : 'Loading…'}
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr key={item.appId}>
                    <td>{item.permitRange || '—'}</td>
                    <td>{contractor(item)}</td>
                    <td>{item.projectSiteLocation}</td>
                    <td>{[item.projectStartDate, item.projectEndDate].filter(Boolean).join(' - ')}</td>
                    <td>{item.inspectorName}</td>
                    <td><Link className="btn btn-default btn-sm" to={`/inspections/${item.appId}`}>Update Permits Status</Link></td>
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

export default HoldList;
