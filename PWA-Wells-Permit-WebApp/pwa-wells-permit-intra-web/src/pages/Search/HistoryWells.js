import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Search as SearchIcon } from 'lucide-react';
import { searchHistoryWells } from '../../api/historyApi';
import Pagination from '../../components/UI/Pagination';
import SortableHeader from '../../components/UI/SortableHeader';
import Loader from '../../components/Loader/Loader';
import './SearchPage.css';

const DEFAULT_PAGE_SIZE = 25;

// Legacy search_hist_well_results.jsp (s=HW) over HIST_WELL_LOC.
const emptyFilters = {
  permitNum: '',
  tractNum: '',
  sectNum: '',
  addrStreet: '',
  addrCity: '',
  ownerName: '',
  wellUse: '',
};

const FIELDS = [
  { key: 'permitNum', label: 'Permit #', placeholder: 'Contains…' },
  { key: 'tractNum', label: 'Tract #', placeholder: 'Contains…' },
  { key: 'sectNum', label: 'Section #', placeholder: 'Contains…' },
  { key: 'addrStreet', label: 'Street Address', placeholder: 'Contains…' },
  { key: 'addrCity', label: 'City Name', placeholder: 'Contains…' },
  { key: 'ownerName', label: 'Owner Name', placeholder: 'Contains…' },
  { key: 'wellUse', label: 'Well Use', placeholder: 'Contains…' },
];

function cleanParams(obj) {
  const out = {};
  Object.entries(obj).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) out[key] = value;
  });
  return out;
}

function HistoryWells() {
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
      const data = await searchHistoryWells(params);
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

  const tractSection = (item) => [item.tractNum, item.sectNum].filter(Boolean).join(' - ') || 'N/A';

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel search-card">
        <h1 className="page-title">History Well Locations</h1>
        <p className="page-subtitle">Search historical well locations by tract, section, address, or owner.</p>

        <form className="search-form" onSubmit={handleSearch}>
          <div className="search-form__grid">
            {FIELDS.map((field) => (
              <div className="search-field" key={field.key}>
                <label htmlFor={`hw-${field.key}`}>{field.label}</label>
                <input
                  id={`hw-${field.key}`}
                  className="form-control"
                  type="text"
                  placeholder={field.placeholder}
                  value={filters[field.key]}
                  onChange={update(field.key)}
                />
              </div>
            ))}
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
            {totalCount > 0 && <span className="search-results__count">{totalCount} well(s)</span>}
          </div>
          <Link className="btn btn-primary" to="/search/history-wells/new">Add History Well Location</Link>
        </div>
        <div className="table-scroll" role="region" aria-label="History well location results" tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <SortableHeader label="Tract - Section Number" sortKey="tractNum" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Permit Number" sortKey="permitNum" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Street Address" sortKey="addrStreet" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="City Name" sortKey="addrCity" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Owner Name" sortKey="ownerName" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Well Use" sortKey="wellUse" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="6" className="muted-text">
                    {searched && !loading ? 'No history well locations matched the criteria.' : 'Enter criteria above and select Search.'}
                  </td>
                </tr>
              ) : (
                items.map((item, idx) => (
                  <tr key={`${item.permitNum || 'w'}-${idx}`}>
                    <td>
                      {item.wellKey
                        ? <Link to={`/search/history-wells/${item.wellKey}`}>{tractSection(item)}</Link>
                        : tractSection(item)}
                    </td>
                    <td>{item.permitNum}</td>
                    <td>{item.addrStreet}</td>
                    <td>{item.addrCity}</td>
                    <td>{item.ownerName}</td>
                    <td>{item.wellUse}</td>
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
            itemLabel="well"
          />
        )}
      </div>
    </div>
  );
}

export default HistoryWells;
