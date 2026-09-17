import { useParams, useLocation } from 'react-router-dom';
import { Link } from 'react-router-dom';
import useApplicationList from '../../hooks/useApplicationList';
import Button from '../../components/UI/Button';
import Loader from '../../components/Loader/Loader';
import Pagination from '../../components/UI/Pagination';
import SortableHeader from '../../components/UI/SortableHeader';
import { formatStatus } from '../../constants/statusCodes';
import { findQueue } from '../../constants/queues';
import '../PendingList/PendingList.css';
import '../Reports/Reports.css';
import '../Search/SearchPage.css';
import './QueueList.css';

// Format an ISO date as MM/DD/YYYY to match the legacy process-list "Date Applied" column.
function formatDate(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleDateString('en-US', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

// Applicant column: business name plus contact name, matching the legacy
// "getAppBusName() - getApplicantName()" concatenation used by the process list JSPs.
function applicantName(item) {
  const person = `${item.appFirstName || ''} ${item.appLastName || ''}`.trim();
  if (item.appBusinessName && person) return `${item.appBusinessName} - ${person}`;
  return item.appBusinessName || person || 'N/A';
}

// Per-queue filter fields, ported field-for-field from the legacy process list JSPs. Pending,
// Pending-Sitemap and Failed-Payments lists share the same five-field set; Approved Permits adds a
// permit number and an approved-date range (filters on APP_PAYMENT_INFO.update_ts).
const BASE_FILTER_FIELDS = [
  { key: 'appBusinessName', label: 'Applicant Business' },
  { key: 'applicantLastName', label: 'Last Name' },
  { key: 'appFirstName', label: 'First Name' },
  { key: 'appId', label: 'Application ID', inputMode: 'numeric' },
  { key: 'email', label: 'Email', type: 'email' },
];

const FILTER_FIELDS = {
  APPRV: [
    ...BASE_FILTER_FIELDS,
    { key: 'permitNum', label: 'Permit #' },
    { key: 'approvedFrom', label: 'Approved Date From', type: 'date' },
    { key: 'approvedTo', label: 'Approved Date To', type: 'date' },
  ],
};

function filterFieldsFor(code) {
  return FILTER_FIELDS[code] || BASE_FILTER_FIELDS;
}

// Deep-link each queue to its matching step in the Help guide's "Work queues" section, so the help
// link opens the write-up for the queue the user is actually on (not just the section top).
const QUEUE_HELP_ANCHORS = {
  PEND: 'queue-pending',
  PENDS: 'queue-pends',
  APPRV: 'queue-approved',
  PAYFL: 'queue-failed',
};

function queueHelpAnchor(code) {
  return QUEUE_HELP_ANCHORS[code] || 'queues';
}

// Generic prefilled-search queue list. The status code comes from the route (/queue/:statusCode),
// applying the same filter the legacy process_menu links applied.
function QueueList() {
  const { statusCode } = useParams();
  const code = String(statusCode || '').toUpperCase();
  // Remount per queue so switching queues from the sidebar (e.g. PEND → PENDS) re-runs the search
  // with fresh state; the list hook only seeds its filters from the status code on mount.
  return <QueueListView key={code} code={code} />;
}

function QueueListView({ code }) {
  const queue = findQueue(code);
  const location = useLocation();
  // Arriving via the left-nav submenu tags the navigation with { fromNav: true }; the persistent
  // sidebar already offers a way back, so the in-page "Back to dashboard" link is redundant there.
  // Dashboard-box clicks carry no such state, so they still get the back link.
  const fromNav = Boolean(location.state?.fromNav);
  const { items, loading, error, filters, setFilters, reload, page, setPage, pageSize, setPageSize, totalCount, sortBy, sortDir, toggleSort } = useApplicationList({ statusCode: code });

  const title = queue?.title || `${formatStatus(code)} applications`;
  const fields = filterFieldsFor(code);

  return (
    <div className="page-shell pending-page">
      {loading && <Loader />}
      <div className="panel">
        <div className="report-toolbar">
          <h1 className="page-title">{title}</h1>
          {!fromNav && <Link className="report-back" to="/">← Back to dashboard</Link>}
        </div>
        {queue?.description && <p className="page-subtitle">{queue.description}</p>}
        <Link className="page-help-link" to={`/help#${queueHelpAnchor(code)}`}>
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> How to work a queue — help
        </Link>
        <form className="queue-filters" onSubmit={(event) => { event.preventDefault(); reload({ ...filters }); }}>
          {fields.map((field) => (
            <div className="search-field" key={field.key}>
              <label htmlFor={`q-${field.key}`}>{field.label}</label>
              <input
                id={`q-${field.key}`}
                className="form-control"
                type={field.type || 'text'}
                inputMode={field.inputMode}
                value={filters[field.key] || ''}
                onChange={(event) => setFilters((current) => ({ ...current, [field.key]: event.target.value }))}
              />
            </div>
          ))}
          <div className="queue-filters__submit">
            <Button type="submit" disabled={loading}>{loading ? 'Refreshing…' : 'Refresh'}</Button>
          </div>
        </form>
        {error && <div className="alert alert-error">{error}</div>}
        <div className="table-scroll" role="region" aria-label={`${title} results`} tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <SortableHeader label="Application ID" sortKey="appId" sortBy={sortBy} sortDir={sortDir} onSort={toggleSort} />
                <SortableHeader label="Date Applied" sortKey="addDate" sortBy={sortBy} sortDir={sortDir} onSort={toggleSort} />
                <SortableHeader label="Applicant Name" sortKey="applicantName" sortBy={sortBy} sortDir={sortDir} onSort={toggleSort} />
                <SortableHeader label="Project Site" sortKey="siteCity" sortBy={sortBy} sortDir={sortDir} onSort={toggleSort} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="4" className="muted-text">{loading ? 'Loading…' : 'No applications matched this queue.'}</td>
                </tr>
              ) : items.map((item) => (
                <tr key={item.appId}>
                  <td><Link to={`/applications/${item.appId}`}>{item.appId}</Link></td>
                  <td>{formatDate(item.addDate)}</td>
                  <td>{applicantName(item)}</td>
                  <td>
                    <div className="search-results__site">
                      <span className="search-results__city">{item.siteCityName || item.siteCityCode || 'N/A'}</span>
                      {item.siteLocation && <span className="search-results__location">{item.siteLocation}</span>}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={totalCount}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          loading={loading}
        />
      </div>
    </div>
  );
}

export default QueueList;
