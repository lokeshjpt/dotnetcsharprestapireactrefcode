import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Search as SearchIcon } from 'lucide-react';
import { searchHistoryPermits } from '../../api/historyApi';
import Pagination from '../../components/UI/Pagination';
import SortableHeader from '../../components/UI/SortableHeader';
import Loader from '../../components/Loader/Loader';
import './SearchPage.css';

const DEFAULT_PAGE_SIZE = 25;

// Legacy search_hist_results.jsp (s=HL) year selects run 2005 → 1987.
const YEARS = [];
for (let year = 2005; year >= 1987; year -= 1) YEARS.push(year);

const emptyFilters = {
  permitNum: '',
  workType: '',
  wellComplRptNum: '',
  permitDate: '',
  permitYear: '',
  startDate: '',
  startYear: '',
  addrStreet: '',
  cityName: '',
  consultant: '',
  drillerName: '',
};

const TEXT_FIELDS = [
  { key: 'permitNum', label: 'Permit #', placeholder: 'Contains…' },
  { key: 'workType', label: 'Work Type', placeholder: 'Contains…' },
  { key: 'wellComplRptNum', label: 'Well Completion Report #', placeholder: 'Contains…' },
  { key: 'addrStreet', label: 'Address', placeholder: 'Street / building #' },
  { key: 'cityName', label: 'City Name', placeholder: 'Contains…' },
  { key: 'consultant', label: 'Consultant', placeholder: 'Contains…' },
  { key: 'drillerName', label: 'Driller Name', placeholder: 'Name or license #' },
];

function cleanParams(obj) {
  const out = {};
  Object.entries(obj).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) out[key] = value;
  });
  return out;
}

function HistoryPermits() {
  const [filters, setFilters] = useState(emptyFilters);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [searched, setSearched] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('permitNum');
  const [sortDir, setSortDir] = useState('asc');

  const update = (key) => (event) =>
    setFilters((current) => ({ ...current, [key]: event.target.value }));

  const runSearch = async ({ page: targetPage = 1, size = pageSize, sortByKey = sortBy, sortDirVal = sortDir } = {}) => {
    setError('');
    setLoading(true);
    setSearched(true);
    try {
      const params = cleanParams({ ...filters, page: targetPage, pageSize: size, sortBy: sortByKey, sortDir: sortDirVal });
      const data = await searchHistoryPermits(params);
      setItems(data.items || []);
      setTotalCount(data.totalCount ?? (data.items ? data.items.length : 0));
      setPage(targetPage);
      setPageSize(size);
      setSortBy(sortByKey);
      setSortDir(sortDirVal);
    } catch {
      setError('Search could not be completed. Verify the criteria and API availability.');
      setItems([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  };

  const handleSort = (key) => {
    const nextDir = key === sortBy ? (sortDir === 'asc' ? 'desc' : 'asc') : 'asc';
    runSearch({ page: 1, sortByKey: key, sortDirVal: nextDir });
  };

  const handleSearch = (event) => {
    if (event) event.preventDefault();
    runSearch({ page: 1 });
  };

  const handleClear = () => {
    setFilters(emptyFilters);
    setItems([]);
    setError('');
    setSearched(false);
    setPage(1);
    setTotalCount(0);
  };

  const address = (item) => `${item.bldgNum ? `${item.bldgNum} ` : ''}${item.addrStreet || ''}`.trim();

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel search-card">
        <h1 className="page-title">Pre-System History Permits (1987–April 2005)</h1>
        <p className="page-subtitle">Search the historical permits issued before the online system.</p>

        <form className="search-form" onSubmit={handleSearch}>
          <div className="search-form__grid">
            {TEXT_FIELDS.map((field) => (
              <div className="search-field" key={field.key}>
                <label htmlFor={`hp-${field.key}`}>{field.label}</label>
                <input
                  id={`hp-${field.key}`}
                  className="form-control"
                  type="text"
                  placeholder={field.placeholder}
                  value={filters[field.key]}
                  onChange={update(field.key)}
                />
              </div>
            ))}
            <div className="search-field">
              <label htmlFor="hp-permitDate">Permit Date</label>
              <input id="hp-permitDate" type="date" className="form-control" value={filters.permitDate} onChange={update('permitDate')} />
            </div>
            <div className="search-field">
              <label htmlFor="hp-permitYear">Permit Year</label>
              <select id="hp-permitYear" className="form-control" value={filters.permitYear} onChange={update('permitYear')}>
                <option value="">All years</option>
                {YEARS.map((year) => <option key={year} value={year}>{year}</option>)}
              </select>
            </div>
            <div className="search-field">
              <label htmlFor="hp-startDate">Start Date</label>
              <input id="hp-startDate" type="date" className="form-control" value={filters.startDate} onChange={update('startDate')} />
            </div>
            <div className="search-field">
              <label htmlFor="hp-startYear">Start Year</label>
              <select id="hp-startYear" className="form-control" value={filters.startYear} onChange={update('startYear')}>
                <option value="">All years</option>
                {YEARS.map((year) => <option key={year} value={year}>{year}</option>)}
              </select>
            </div>
          </div>
          <div className="search-form__actions">
            <button type="submit" className="btn btn-primary" disabled={loading}>
              <SearchIcon size={16} />
              {loading ? 'Searching…' : 'Search'}
            </button>
            <button type="button" className="btn btn-default" onClick={handleClear} disabled={loading}>
              Clear
            </button>
          </div>
        </form>
      </div>

      {error && <div className="alert alert-error search-results__alert">{error}</div>}

      <div className="panel search-results">
        <div className="search-results__head">
          <div className="search-results__head-left">
            <h2 className="search-results__title">Results</h2>
            {totalCount > 0 && <span className="search-results__count">{totalCount} permit(s)</span>}
          </div>
          <Link className="btn btn-primary" to="/search/history-permits/new">Add History</Link>
        </div>
        <div className="table-scroll" role="region" aria-label="History permit results" tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <SortableHeader label="Permit#" sortKey="permitNum" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Work Type" sortKey="workType" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <th scope="col">Well Compl Rpt</th>
                <SortableHeader label="Bldg# / Address" sortKey="address" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="City Name" sortKey="city" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Consultant" sortKey="consultant" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Driller Name" sortKey="driller" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Start Date" sortKey="startDt" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Permit Date" sortKey="permitDt" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="9" className="muted-text">
                    {searched && !loading ? 'No history permits matched the criteria.' : 'Enter criteria above and select Search.'}
                  </td>
                </tr>
              ) : (
                items.map((item, idx) => (
                  <tr key={`${item.permitNum || 'p'}-${idx}`}>
                    <td>
                      {item.permitNum
                        ? <Link to={`/search/history-permits/${encodeURIComponent(item.permitNum.trim())}`}>{item.permitNum}</Link>
                        : item.permitNum}
                    </td>
                    <td>{item.workType}</td>
                    <td>{item.wellComplRptNum}</td>
                    <td>{address(item)}</td>
                    <td>{item.cityName}</td>
                    <td>{item.consultant}</td>
                    <td>{item.drillerName}</td>
                    <td>{item.startDate}</td>
                    <td>{item.permitDate}</td>
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

export default HistoryPermits;
