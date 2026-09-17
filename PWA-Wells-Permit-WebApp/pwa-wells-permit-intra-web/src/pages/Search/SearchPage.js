import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Search as SearchIcon } from 'lucide-react';
import { getApplication, searchApplications } from '../../api/applicationApi';
import { getCities, getWorkCategories, getWorkTypes } from '../../api/referenceApi';
import Pagination from '../../components/UI/Pagination';
import SortableHeader from '../../components/UI/SortableHeader';
import Loader from '../../components/Loader/Loader';
import { formatStatus, statusPillClass } from '../../constants/statusCodes';
import './SearchPage.css';

const DEFAULT_PAGE_SIZE = 25;

// "Year Applied" / "Permit Issued Year" selects run from the current year back to 2005 (the year the
// online system launched), matching search_form.jsp.
const CURRENT_YEAR = new Date().getFullYear();
const YEARS = [];
for (let year = CURRENT_YEAR; year >= 2005; year -= 1) YEARS.push(year);

const emptyFilters = {
  // Application info
  permitNum: '',
  appId: '',
  appAddedOn: '',
  addedAfter: '',
  addedBefore: '',
  permitIssuedFrom: '',
  permitIssuedTo: '',
  appYear: '',
  permitYear: '',
  // Project info
  projectLocation: '',
  siteCityName: '',
  // Applicant info
  appBusinessName: '',
  email: '',
  appFirstName: '',
  applicantLastName: '',
  // Wells info
  workCategory: '',
  workType: '',
  // Driller info
  drillerName: '',
  drillerLicense: '',
};

const APPLICATION_FIELDS = [
  { key: 'permitNum', label: 'Permit #', placeholder: 'Contains…' },
  { key: 'appId', label: 'Application ID', placeholder: '13-digit application number', inputMode: 'numeric' },
  { key: 'appAddedOn', label: 'Date Applied', type: 'date' },
  { key: 'addedAfter', label: 'Applied Date From', type: 'date' },
  { key: 'addedBefore', label: 'Applied Date To', type: 'date' },
  { key: 'permitIssuedFrom', label: 'Permit Issued From', type: 'date' },
  { key: 'permitIssuedTo', label: 'Permit Issued To', type: 'date' },
];

const APPLICANT_FIELDS = [
  { key: 'appBusinessName', label: 'Applicant Business', placeholder: 'Business name' },
  { key: 'email', label: 'Email', placeholder: 'Applicant email', type: 'email' },
  { key: 'appFirstName', label: 'First Name', placeholder: 'First name' },
  { key: 'applicantLastName', label: 'Last Name', placeholder: 'Last name' },
];

const DRILLER_FIELDS = [
  { key: 'drillerName', label: 'Driller Name', placeholder: 'Driller name' },
  { key: 'drillerLicense', label: 'Driller License #', placeholder: 'License number' },
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

function SearchPage() {
  const navigate = useNavigate();
  const [filters, setFilters] = useState(emptyFilters);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [searched, setSearched] = useState(false);
  const [cityNames, setCityNames] = useState({});
  const [cities, setCities] = useState([]);
  const [categories, setCategories] = useState([]);
  const [workTypes, setWorkTypes] = useState([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('addDate');
  const [sortDir, setSortDir] = useState('desc');

  // Load the dropdown reference data (project-site cities, work categories, work types). The city
  // list also builds a code→name map so the results table can label the site city (parity with the
  // legacy list).
  useEffect(() => {
    let active = true;
    getCities()
      .then((data) => {
        if (!active) return;
        setCities(data || []);
        const map = {};
        (data || []).forEach((city) => {
          if (city.code) map[city.code] = city.label;
        });
        setCityNames(map);
      })
      .catch(() => {});
    getWorkCategories().then((data) => { if (active) setCategories(data || []); }).catch(() => {});
    getWorkTypes().then((data) => { if (active) setWorkTypes(data || []); }).catch(() => {});
    return () => {
      active = false;
    };
  }, []);

  const update = (key) => (event) =>
    setFilters((current) => ({ ...current, [key]: event.target.value }));

  const renderInput = (field) => (
    <div className="search-field" key={field.key}>
      <label htmlFor={`s-${field.key}`}>{field.label}</label>
      <input
        id={`s-${field.key}`}
        className="form-control"
        type={field.type || 'text'}
        inputMode={field.inputMode}
        placeholder={field.placeholder}
        value={filters[field.key]}
        onChange={update(field.key)}
      />
    </div>
  );

  const runSearch = async ({ page: targetPage = 1, size = pageSize, sortByKey = sortBy, sortDirVal = sortDir } = {}) => {
    setError('');
    setLoading(true);
    setSearched(true);
    try {
      const params = cleanParams({ ...filters, page: targetPage, pageSize: size, sortBy: sortByKey, sortDir: sortDirVal });
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

  // Clicking a column header toggles direction when it is already active, otherwise sorts that
  // column ascending. Sorting always returns to the first page.
  const handleSort = (key) => {
    const nextDir = key === sortBy ? (sortDir === 'asc' ? 'desc' : 'asc') : 'asc';
    runSearch({ page: 1, sortByKey: key, sortDirVal: nextDir });
  };

  const handleSearch = async (event) => {
    if (event) event.preventDefault();
    // Legacy deep-link parity: a 13-digit application id opens its detail page directly.
    const idNum = filters.appId.trim();
    if (/^\d{13}$/.test(idNum)) {
      setError('');
      setLoading(true);
      setSearched(true);
      try {
        const application = await getApplication(idNum);
        if (application) {
          navigate(`/applications/${application.appId}`);
          return;
        }
        setError(`No application found for ${idNum}.`);
        setItems([]);
        setTotalCount(0);
      } catch {
        setError('Search could not be completed. Verify the criteria and API availability.');
        setItems([]);
        setTotalCount(0);
      } finally {
        setLoading(false);
      }
      return;
    }

    await runSearch({ page: 1 });
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
        <h1 className="page-title">Search Applications</h1>
        <p className="page-subtitle">Type in one or more fields to search the applications.</p>
        <Link className="page-help-link" to="/help#search">
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> How to search — help
        </Link>

        <form className="search-form" onSubmit={handleSearch}>
          <div className="search-section">
            <h2 className="search-section__title">Application Info</h2>
            <div className="search-form__grid">
              {APPLICATION_FIELDS.map(renderInput)}
              <div className="search-field">
                <label htmlFor="s-appYear">Year Applied</label>
                <select id="s-appYear" className="form-control" value={filters.appYear} onChange={update('appYear')}>
                  <option value="">Any year</option>
                  {YEARS.map((year) => <option key={year} value={year}>{year}</option>)}
                </select>
              </div>
              <div className="search-field">
                <label htmlFor="s-permitYear">Permit Issued Year</label>
                <select id="s-permitYear" className="form-control" value={filters.permitYear} onChange={update('permitYear')}>
                  <option value="">Any year</option>
                  {YEARS.map((year) => <option key={year} value={year}>{year}</option>)}
                </select>
              </div>
            </div>
          </div>

          <div className="search-section">
            <h2 className="search-section__title">Project Info</h2>
            <div className="search-form__grid">
              <div className="search-field">
                <label htmlFor="s-projectLocation">Project Location</label>
                <input id="s-projectLocation" className="form-control" type="text" placeholder="Site location contains…" value={filters.projectLocation} onChange={update('projectLocation')} />
              </div>
              <div className="search-field">
                <label htmlFor="s-siteCityName">Project City</label>
                <select id="s-siteCityName" className="form-control" value={filters.siteCityName} onChange={update('siteCityName')}>
                  <option value="">All cities</option>
                  {cities.map((city) => <option key={city.code} value={(city.code || '').trim()}>{(city.label || city.code || '').trim()}</option>)}
                </select>
              </div>
            </div>
          </div>

          <div className="search-section">
            <h2 className="search-section__title">Applicant Info</h2>
            <div className="search-form__grid">
              {APPLICANT_FIELDS.map(renderInput)}
            </div>
          </div>

          <div className="search-section">
            <h2 className="search-section__title">Wells Info</h2>
            <div className="search-form__grid">
              <div className="search-field">
                <label htmlFor="s-workCategory">Work Category</label>
                <select id="s-workCategory" className="form-control" value={filters.workCategory} onChange={update('workCategory')}>
                  <option value="">All categories</option>
                  {categories.map((category) => <option key={category.code} value={(category.code || '').trim()}>{(category.label || category.code || '').trim()}</option>)}
                </select>
              </div>
              <div className="search-field">
                <label htmlFor="s-workType">Work Type</label>
                <select id="s-workType" className="form-control" value={filters.workType} onChange={update('workType')}>
                  <option value="">All types</option>
                  {workTypes.map((type) => <option key={type.code} value={(type.code || '').trim()}>{(type.label || type.code || '').trim()}</option>)}
                </select>
              </div>
            </div>
          </div>

          <div className="search-section">
            <h2 className="search-section__title">Driller Info</h2>
            <div className="search-form__grid">
              {DRILLER_FIELDS.map(renderInput)}
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
          <h2 className="search-results__title">Results</h2>
          {totalCount > 0 && <span className="search-results__count">{totalCount} application(s)</span>}
        </div>
        <div className="table-scroll" role="region" aria-label="Search results" tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <SortableHeader label="Application ID" sortKey="appId" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Applicant Name" sortKey="applicantName" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Date Applied" sortKey="addDate" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Status" sortKey="statusCode" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                <SortableHeader label="Project Site" sortKey="siteCity" sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="5" className="muted-text">
                    {searched && !loading
                      ? 'No applications matched the current criteria.'
                      : 'Enter criteria above and select Search.'}
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr key={item.appId}>
                    <td>
                      <Link className="search-results__link" to={`/applications/${item.appId}`}>
                        {item.appId}
                      </Link>
                    </td>
                    <td>{applicantName(item)}</td>
                    <td>{formatDate(item.addDate)}</td>
                    <td>
                      <span className={statusPillClass(item.statusCode)}>{formatStatus(item.statusCode)}</span>
                    </td>
                    <td>
                      <div className="search-results__site">
                        <span className="search-results__city">{cityNames[item.siteCityCode] || item.siteCityName || item.siteCityCode || 'N/A'}</span>
                        {item.siteLocation && (
                          <span className="search-results__location">{item.siteLocation}</span>
                        )}
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

export default SearchPage;
