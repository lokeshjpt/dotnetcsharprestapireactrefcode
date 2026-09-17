import { useState, useEffect, useRef, useCallback, memo, Fragment } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { searchApplications } from '../../api/applicationApi';
import { getCities } from '../../api/referenceApi';
import InputField from '../../components/UI/InputField';
import SelectField from '../../components/UI/SelectField';
import Button from '../../components/UI/Button';
import Loader from '../../components/Loader/Loader';
import SitemapUpload from '../../components/SitemapUpload/SitemapUpload';
import Pagination from '../../components/UI/Pagination';
import SortableHeader from '../../components/UI/SortableHeader';
import InvisibleCaptcha, { captchaEnabled } from '../../components/Captcha/InvisibleCaptcha';
import { formatStatus, isPendingSitemap, statusPillClass } from '../../constants/statusCodes';
import './TrackPage.css';

const EMPTY_FILTERS = { appId: '', email: '', appBusinessName: '', driller: '', siteCity: '' };
const DEFAULT_PAGE_SIZE = 25;

function normalizeCity(raw) {
  return {
    code: raw.code ?? raw.cityCode ?? raw.city_code ?? '',
    label: raw.label ?? raw.cityName ?? raw.city_name ?? '',
  };
}

// Pure row formatters (module-level so they never change identity between renders).
function cityName(code, cities) {
  if (!code) return '';
  const match = cities.find((c) => c.code === code);
  return match ? match.label : code;
}

function applicantName(item) {
  const business = (item.appBusinessName || '').trim();
  const first = (item.appFirstName || '').trim();
  const last = (item.appLastName || '').trim();
  const person = `${first} ${last}`.trim();
  if (business && person) return `${business} - ${person}`;
  return business || person || 'N/A';
}

function drillerName(item) {
  const works = item.works || [];
  const withDriller = works.find((w) => w.drillerName && w.drillerName.trim());
  return withDriller ? withDriller.drillerName : '';
}

// Memoized results table. Isolated from the filter inputs so typing in a search field (which only
// updates `filters` on the parent) does NOT re-render the potentially large results table — the
// callbacks it receives are referentially stable, so React.memo skips the re-render entirely.
const ResultsTable = memo(function ResultsTable({
  results,
  searched,
  sortBy,
  sortDir,
  cities,
  sitemapOpenId,
  onSort,
  onToggleSitemap,
  onUploaded,
  onCloseSitemap,
}) {
  return (
    <div className="track-page__results" role="region" aria-label="Application results" tabIndex={0}>
      <table className="simple-table">
        <thead>
          <tr>
            <SortableHeader label="Date Applied" sortKey="addDate" sortBy={sortBy} sortDir={sortDir} onSort={onSort} />
            <SortableHeader label="Application ID" sortKey="appId" sortBy={sortBy} sortDir={sortDir} onSort={onSort} />
            <SortableHeader label="Applicant Name" sortKey="applicantName" sortBy={sortBy} sortDir={sortDir} onSort={onSort} />
            <SortableHeader label="Project Site" sortKey="siteCity" sortBy={sortBy} sortDir={sortDir} onSort={onSort} />
            <SortableHeader label="Driller Name" sortKey="drillerName" sortBy={sortBy} sortDir={sortDir} onSort={onSort} />
            <SortableHeader label="Application Status" sortKey="statusCode" sortBy={sortBy} sortDir={sortDir} onSort={onSort} />
          </tr>
        </thead>
        <tbody>
          {results.length === 0 ? (
            <tr>
              <td colSpan="6" className="muted-text">
                {searched ? 'No applications match your search.' : 'Enter search criteria and run a lookup.'}
              </td>
            </tr>
          ) : results.map((item) => (
            <Fragment key={item.appId}>
              <tr>
                <td>{item.addDate ? new Date(item.addDate).toLocaleDateString() : ''}</td>
                <td>{item.appId}</td>
                <td>{applicantName(item)}</td>
                <td>
                  {cityName(item.siteCityCode, cities) || 'N/A'}
                  {item.siteLocation && item.siteLocation.trim() ? <><br />{item.siteLocation}</> : null}
                </td>
                <td>{drillerName(item)}</td>
                <td>
                  <div className="track-page__status-cell">
                    <span className={statusPillClass(item.statusCode)}>{formatStatus(item.statusCode)}</span>
                    {isPendingSitemap(item.statusCode) && (
                      <Button
                        variant="primary"
                        onClick={() => onToggleSitemap(item.appId)}
                      >
                        Upload Sitemap
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
              {isPendingSitemap(item.statusCode) && sitemapOpenId === item.appId && (
                <tr className="track-page__sitemap-row">
                  <td colSpan="6">
                    <p className="muted-text track-page__sitemap-hint">
                      A site map is required before this application can be approved. Upload it
                      below (Adobe PDF preferred).
                    </p>
                    <SitemapUpload
                      appId={item.appId}
                      onUploaded={() => onUploaded(item.appId)}
                      onClose={onCloseSitemap}
                    />
                  </td>
                </tr>
              )}
            </Fragment>
          ))}
        </tbody>
      </table>
    </div>
  );
});

function TrackPage() {
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [results, setResults] = useState([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [searched, setSearched] = useState(false);
  const [cities, setCities] = useState([]);
  const [sitemapOpenId, setSitemapOpenId] = useState(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('addDate');
  const [sortDir, setSortDir] = useState('desc');
  const captchaRef = useRef(null);
  const [searchParams] = useSearchParams();

  useEffect(() => {
    let active = true;
    getCities()
      .then((data) => { if (active) setCities((data || []).map(normalizeCity)); })
      .catch(() => { /* dropdown falls back to codes */ });
    return () => { active = false; };
  }, []);

  const setField = (name) => (event) =>
    setFilters((current) => ({ ...current, [name]: event.target.value }));

  // Snapshot the latest search inputs so the search/sort callbacks can stay referentially stable
  // (useCallback with no deps). Without this, every keystroke in a filter field would create new
  // callbacks and force the memoized results table to re-render — the source of the typing lag.
  const latest = useRef({});
  latest.current = { filters, pageSize, sortBy, sortDir };

  // On a successful sitemap upload the backend advances the application out of PENDS into PEND
  // ("Pending Approval"). Reflect that immediately in the results so the status pill updates and
  // the upload row collapses without needing another search.
  const handleSitemapUploaded = useCallback((appId) => {
    setResults((current) =>
      current.map((item) =>
        item.appId === appId && isPendingSitemap(item.statusCode)
          ? { ...item, statusCode: 'PEND' }
          : item
      )
    );
    setSitemapOpenId(null);
  }, []);

  const toggleSitemap = useCallback(
    (appId) => setSitemapOpenId((id) => (id === appId ? null : appId)),
    []
  );
  const closeSitemap = useCallback(() => setSitemapOpenId(null), []);

  const handleSearch = useCallback(async ({ page: targetPage = 1, size, sortByKey, sortDirVal, overrideFilters } = {}) => {
    const { filters: curFilters, pageSize: curSize, sortBy: curSortBy, sortDir: curSortDir } = latest.current;
    const f = overrideFilters ?? curFilters;
    const nextSize = size ?? curSize;
    const nextSortBy = sortByKey ?? curSortBy;
    const nextSortDir = sortDirVal ?? curSortDir;
    // Require at least two search fields so a lookup is meaningfully scoped (e.g. Application ID +
    // Email, or Business + City) instead of returning the entire application set.
    const providedCount = [f.appId, f.email, f.appBusinessName, f.driller, f.siteCity]
      .filter((v) => (v || '').trim() !== '').length;
    if (providedCount < 2) {
      setError('Please provide at least two search fields to search.');
      return;
    }
    setLoading(true);
    setError('');
    setSitemapOpenId(null);
    // Invisible captcha gate: humans pass silently; bots are challenged before any lookup runs. A
    // fresh single-use token is minted for every search (initial lookup, pagination and sorting) and
    // forwarded to the API, which verifies it server-side — so the exposed public search endpoint is
    // bot-protected end to end, not just gated in the UI.
    const token = await captchaRef.current?.execute();
    if (captchaEnabled && !token) {
      setError('Security verification could not be completed. Please try again in a moment.');
      setLoading(false);
      return;
    }
    try {
      const params = {
        appId: f.appId || undefined,
        email: f.email || undefined,
        appBusinessName: f.appBusinessName || undefined,
        drillerName: f.driller || undefined,
        siteCityName: f.siteCity || undefined,
        page: targetPage,
        pageSize: nextSize,
        sortBy: nextSortBy,
        sortDir: nextSortDir,
      };
      const data = await searchApplications(params, token);
      setResults(data.items || []);
      setTotalCount(data.totalCount ?? (data.items ? data.items.length : 0));
      setPage(targetPage);
      setPageSize(nextSize);
      setSortBy(nextSortBy);
      setSortDir(nextSortDir);
      setSearched(true);
    } catch {
      setError('Unable to retrieve application results right now.');
    } finally {
      setLoading(false);
    }
  }, []);

  // Clicking a column header toggles direction when it is already the active sort, otherwise sorts
  // that column ascending. Sorting always returns to the first page.
  const handleSort = useCallback((key) => {
    const { sortBy: curSortBy, sortDir: curSortDir } = latest.current;
    const nextDir = key === curSortBy ? (curSortDir === 'asc' ? 'desc' : 'asc') : 'asc';
    handleSearch({ page: 1, sortByKey: key, sortDirVal: nextDir });
  }, [handleSearch]);

  // Prefill and auto-run the search from an emailed tracking link (…/#/track?appid=..&email=..).
  // appId + email satisfies the two-field minimum, so a one-click link shows the applicant their
  // status without re-entering anything. Runs once.
  const didPrefill = useRef(false);
  useEffect(() => {
    if (didPrefill.current) return;
    const linkAppId = (searchParams.get('appid') || '').trim();
    const linkEmail = (searchParams.get('email') || '').trim();
    if (!linkAppId && !linkEmail) return;
    didPrefill.current = true;
    const prefilled = { ...EMPTY_FILTERS, appId: linkAppId, email: linkEmail };
    setFilters(prefilled);
    if (linkAppId && linkEmail) {
      handleSearch({ overrideFilters: prefilled });
    }
  }, [searchParams, handleSearch]);

  return (
    <div className="page-shell track-page">
      {loading && <Loader />}
      <div className="panel">
        <h1 className="page-title">Track an application</h1>
        <p className="page-subtitle">
          To view your application status, provide at least two search fields &mdash; for example your Application ID and the email address used in the application.
          You can also search by Applicant Business, Driller&apos;s Name, or Project Site City.
        </p>
        <Link className="page-help-link" to="/help#track">
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> How to track &amp; upload a site map — help
        </Link>
        <div className="grid-two">
          <InputField id="trackAppId" label="Application ID" maxLength={13} value={filters.appId} onChange={setField('appId')} />
          <InputField id="trackEmail" label="Email Address" type="email" maxLength={30} autoComplete="email" value={filters.email} onChange={setField('email')} />
          <InputField id="trackBusiness" label="Applicant Business" maxLength={200} value={filters.appBusinessName} onChange={setField('appBusinessName')} />
          <InputField id="trackDriller" label="Driller's Name" maxLength={100} value={filters.driller} onChange={setField('driller')} />
          <SelectField id="trackSiteCity" label="Project Site City" placeholder="Select..." options={cities} value={filters.siteCity} onChange={setField('siteCity')} />
        </div>
        <div className="track-page__actions">
          <Button onClick={() => handleSearch({ page: 1 })} disabled={loading}>{loading ? 'Searching...' : 'Search'}</Button>
        </div>
        <InvisibleCaptcha ref={captchaRef} />
        {captchaEnabled && (
          <p className="captcha-notice">
            This site is protected by reCAPTCHA and the Google{' '}
            <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Privacy Policy</a>{' '}
            and{' '}
            <a href="https://policies.google.com/terms" target="_blank" rel="noopener noreferrer">Terms of Service</a>{' '}
            apply.
          </p>
        )}
        {error && <div className="alert alert-error track-page__alert">{error}</div>}
        {searched && (
          <p className="page-subtitle track-page__count">
            {totalCount} application{totalCount === 1 ? '' : 's'} found
          </p>
        )}
        <ResultsTable
          results={results}
          searched={searched}
          sortBy={sortBy}
          sortDir={sortDir}
          cities={cities}
          sitemapOpenId={sitemapOpenId}
          onSort={handleSort}
          onToggleSitemap={toggleSitemap}
          onUploaded={handleSitemapUploaded}
          onCloseSitemap={closeSitemap}
        />
        {searched && (
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={totalCount}
            onPageChange={(next) => handleSearch({ page: next })}
            onPageSizeChange={(size) => handleSearch({ page: 1, size })}
            loading={loading}
          />
        )}
      </div>
    </div>
  );
}

export default TrackPage;
