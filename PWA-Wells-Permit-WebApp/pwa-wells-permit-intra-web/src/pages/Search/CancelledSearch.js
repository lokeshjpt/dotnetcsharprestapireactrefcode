import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Search as SearchIcon } from 'lucide-react';
import { searchApplications } from '../../api/applicationApi';
import Pagination from '../../components/UI/Pagination';
import SortableHeader from '../../components/UI/SortableHeader';
import Loader from '../../components/Loader/Loader';
import './SearchPage.css';

const DEFAULT_PAGE_SIZE = 25;

// Legacy search_cancelled.jsp field set (s=CL, status_code = 'CAN').
const emptyFilters = {
  appBusinessName: '',
  applicantLastName: '',
  appFirstName: '',
  appId: '',
  email: '',
  appAddedOn: '',
};

const FIELDS = [
  { key: 'appBusinessName', label: 'Applicant Business', placeholder: 'Business name' },
  { key: 'applicantLastName', label: 'Last Name', placeholder: 'Last name' },
  { key: 'appFirstName', label: 'First Name', placeholder: 'First name' },
  { key: 'appId', label: 'Application ID', placeholder: '13-digit application number', inputMode: 'numeric' },
  { key: 'email', label: 'Email', placeholder: 'Applicant email', type: 'email' },
  { key: 'appAddedOn', label: 'Date Applied', type: 'date' },
];

function cleanParams(obj) {
  const out = {};
  Object.entries(obj).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) out[key] = value;
  });
  return out;
}

function formatDate(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleDateString('en-US', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

function applicantName(item) {
  const person = `${item.appFirstName || ''} ${item.appLastName || ''}`.trim();
  if (item.appBusinessName && person) return `${item.appBusinessName} — ${person}`;
  return item.appBusinessName || person || 'N/A';
}

function CancelledSearch() {
  const [filters, setFilters] = useState(emptyFilters);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [searched, setSearched] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('addDate');
  const [sortDir, setSortDir] = useState('desc');

  const update = (key) => (event) =>
    setFilters((current) => ({ ...current, [key]: event.target.value }));

  const runSearch = async ({ page: targetPage = 1, size = pageSize, sortByKey = sortBy, sortDirVal = sortDir } = {}) => {
    setError('');
    setLoading(true);
    setSearched(true);
    try {
      const params = cleanParams({ ...filters, statusCode: 'CAN', page: targetPage, pageSize: size, sortBy: sortByKey, sortDir: sortDirVal });
      const data = await searchApplications(params);
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

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel search-card">
        <h1 className="page-title">Cancelled Applications</h1>
        <p className="page-subtitle">Search applications cancelled before approval (status CAN).</p>

        <form className="search-form" onSubmit={handleSearch}>
          <div className="search-form__grid">
            {FIELDS.map((field) => (
              <div className="search-field" key={field.key}>
                <label htmlFor={`can-${field.key}`}>{field.label}</label>
                <input
                  id={`can-${field.key}`}
                  className="form-control"
                  type={field.type || 'text'}
                  inputMode={field.inputMode}
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
          <h2 className="search-results__title">Results</h2>
          {totalCount > 0 && <span className="search-results__count">{totalCount} application(s)</span>}
        </div>
        <div className="table-scroll" role="region" aria-label="Cancelled application results" tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <SortableHeader label="Application ID" sortKey="appId" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Applicant Name" sortKey="applicantName" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Date Applied" sortKey="addDate" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Project Site" sortKey="siteCity" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="4" className="muted-text">
                    {searched && !loading ? 'No cancelled applications matched the criteria.' : 'Enter criteria above and select Search.'}
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr key={item.appId}>
                    <td>
                      <Link className="search-results__link" to={`/applications/${item.appId}`}>{item.appId}</Link>
                    </td>
                    <td>{applicantName(item)}</td>
                    <td>{formatDate(item.addDate)}</td>
                    <td>
                      <div className="search-results__site">
                        <span className="search-results__city">{item.siteCityName || item.siteCityCode || 'N/A'}</span>
                        {item.siteLocation && <span className="search-results__location">{item.siteLocation}</span>}
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
          />
        )}
      </div>
    </div>
  );
}

export default CancelledSearch;
