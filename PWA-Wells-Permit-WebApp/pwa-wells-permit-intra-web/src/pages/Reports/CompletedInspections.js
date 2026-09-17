import { Fragment, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import Button from '../../components/UI/Button';
import Loader from '../../components/Loader/Loader';
import { getCompletedInspectionsReport } from '../../api/reportApi';
import { getInspectors } from '../../api/referenceApi';
import './Reports.css';

function CompletedInspections() {
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [inspectorId, setInspectorId] = useState('');
  const [inspectors, setInspectors] = useState([]);
  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    getInspectors().then((data) => setInspectors(data || [])).catch(() => {});
  }, []);

  const run = async () => {
    setError('');
    setLoading(true);
    try {
      const data = await getCompletedInspectionsReport({ fromDate, toDate, inspectorId });
      setReport(data);
    } catch {
      setError('Unable to run the completed inspections report.');
      setReport(null);
    } finally {
      setLoading(false);
    }
  };

  const lines = report?.lines || [];

  // Group by inspector with per-inspector totals (parity with print_inspection_by_inspector.jsp).
  const groups = [];
  let currentKey = null;
  lines.forEach((line) => {
    const key = line.inspectorId || '—';
    if (key !== currentKey) {
      groups.push({ key, name: (line.inspectorName || '').trim(), rows: [] });
      currentKey = key;
    }
    groups[groups.length - 1].rows.push(line);
  });

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel">
        <div className="report-toolbar">
          <h1 className="page-title">Completed Inspections by Inspector</h1>
          <Link className="report-back" to="/reports">← Back to reports</Link>
        </div>
        <p className="page-subtitle">Applications with completed inspections (ICOMP), grouped by inspector. Optionally filter by approval date range.</p>

        <div className="report-filter">
          <div className="report-field">
            <label htmlFor="ci-inspector">Inspector</label>
            <select id="ci-inspector" className="form-control" value={inspectorId} onChange={(e) => setInspectorId(e.target.value)}>
              <option value="">All inspectors</option>
              {inspectors.map((i) => <option key={i.code} value={i.code}>{(i.label || '').trim()}</option>)}
            </select>
          </div>
          <div className="report-field">
            <label htmlFor="ci-from">Approved from</label>
            <input id="ci-from" type="date" className="form-control" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </div>
          <div className="report-field">
            <label htmlFor="ci-to">Approved to</label>
            <input id="ci-to" type="date" className="form-control" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </div>
          <div className="report-field report-field--actions">
            <Button onClick={run} disabled={loading}>{loading ? 'Running…' : 'Run report'}</Button>
          </div>
        </div>

        {error && <div className="alert alert-error">{error}</div>}

        {report && (
          <>
            <div className="report-summary">
              <div className="report-stat">
                <span className="report-stat__label">Inspections</span>
                <span className="report-stat__value">{lines.length}</span>
              </div>
            </div>

            <div className="table-scroll" role="region" aria-label="Completed inspections" tabIndex={0}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Application</th>
                    <th>Permit(s)</th>
                    <th>Business</th>
                    <th>Applicant</th>
                    <th>Project site</th>
                    <th>City</th>
                    <th>Start</th>
                    <th>End</th>
                  </tr>
                </thead>
                <tbody>
                  {lines.length === 0 ? (
                    <tr><td colSpan="8" className="muted-text">No completed inspections matched the criteria.</td></tr>
                  ) : groups.map((group) => (
                    <Fragment key={group.key}>
                      <tr className="report-group-header">
                        <td colSpan="8">Inspector: {group.name || group.key} (ID {group.key})</td>
                      </tr>
                      {group.rows.map((line, idx) => (
                        <tr key={`${group.key}-${line.appId}-${idx}`}>
                          <td>{line.appId}</td>
                          <td>{line.permitRange}</td>
                          <td>{line.businessName}</td>
                          <td>{line.applicantName}</td>
                          <td>{line.projectSiteLocation}</td>
                          <td>{(line.cityName || '').trim()}</td>
                          <td>{line.projectStartDate}</td>
                          <td>{line.projectEndDate}</td>
                        </tr>
                      ))}
                      <tr className="report-subtotal">
                        <td colSpan="8">{(group.name || group.key)} subtotal — {group.rows.length} inspection(s)</td>
                      </tr>
                    </Fragment>
                  ))}
                  {lines.length > 0 && (
                    <tr className="report-grandtotal">
                      <td colSpan="8">Grand total — {lines.length} inspection(s)</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

export default CompletedInspections;
