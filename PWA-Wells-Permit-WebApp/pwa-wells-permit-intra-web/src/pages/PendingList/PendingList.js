import { Link } from 'react-router-dom';
import useApplicationList from '../../hooks/useApplicationList';
import InputField from '../../components/UI/InputField';
import Button from '../../components/UI/Button';
import Loader from '../../components/Loader/Loader';
import './PendingList.css';

// Format an ISO date as MM/DD/YYYY to match the legacy process_pend_list "Date Applied" column.
function formatDate(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleDateString('en-US', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

// Applicant column: business name plus contact name, matching the legacy
// "getAppBusName() - getApplicantName()" concatenation.
function applicantName(item) {
  const person = `${item.appFirstName || ''} ${item.appLastName || ''}`.trim();
  if (item.appBusinessName && person) return `${item.appBusinessName} - ${person}`;
  return item.appBusinessName || person || 'N/A';
}

function PendingList() {
  const { items, loading, error, filters, setFilters, reload } = useApplicationList({ statusCode: 'PEND' });

  return (
    <div className="page-shell pending-page">
      {loading && <Loader />}
      <div className="panel">
        <h1 className="page-title">Pending applications</h1>
        <p className="page-subtitle">Review permit requests awaiting staff action.</p>
        <div className="pending-page__filters">
          <InputField id="filterAppId" label="Application number" value={filters.appId || ''} onChange={(event) => setFilters((current) => ({ ...current, appId: event.target.value }))} />
          <InputField id="filterCity" label="City" value={filters.siteCityName || ''} onChange={(event) => setFilters((current) => ({ ...current, siteCityName: event.target.value }))} />
          <Button onClick={() => reload({ ...filters })} disabled={loading}>{loading ? 'Refreshing...' : 'Refresh'}</Button>
        </div>
        {error && <div className="alert alert-error">{error}</div>}
        <div className="table-scroll" role="region" aria-label="Pending applications" tabIndex={0}>
          <table className="data-table">
            <thead>
              <tr>
                <th>Application ID</th>
                <th>Date Applied</th>
                <th>Applicant Name</th>
                <th>Project Site</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan="4" className="muted-text">No pending applications matched the current filters.</td>
                </tr>
              ) : items.map((item) => (
                <tr key={item.appId}>
                  <td><Link to={`/applications/${item.appId}`}>{item.appId}</Link></td>
                  <td>{formatDate(item.addDate)}</td>
                  <td>{applicantName(item)}</td>
                  <td>
                    {item.siteCityName || item.siteCityCode || 'N/A'}
                    {item.siteLocation ? <><br />{item.siteLocation}</> : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

export default PendingList;
