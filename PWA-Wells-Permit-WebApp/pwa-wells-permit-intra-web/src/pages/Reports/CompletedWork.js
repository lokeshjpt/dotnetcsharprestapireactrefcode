import { Fragment, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import Button from '../../components/UI/Button';
import Loader from '../../components/Loader/Loader';
import DateRangeFilter from './components/DateRangeFilter';
import { getCompletedWorksReport } from '../../api/reportApi';
import { getInspectors, getCities } from '../../api/referenceApi';
import { resolveDateRange } from '../../utils/reportDates';
import './Reports.css';

function CompletedWork() {
  const [preset, setPreset] = useState('mtd');
  const [stDate, setStDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [inspectorId, setInspectorId] = useState('');
  const [cityCode, setCityCode] = useState('');
  const [inspectors, setInspectors] = useState([]);
  const [cities, setCities] = useState([]);
  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    getInspectors().then((data) => setInspectors(data || [])).catch(() => {});
    getCities().then((data) => setCities(data || [])).catch(() => {});
  }, []);

  const run = async () => {
    const { start, end } = resolveDateRange(preset, stDate, endDate);
    if (!start || !end) {
      setError('Please choose a start and end date.');
      return;
    }
    setError('');
    setLoading(true);
    try {
      const data = await getCompletedWorksReport({ start, end, inspectorId, cityCode });
      setReport(data);
    } catch {
      setError('Unable to run the completed work report.');
      setReport(null);
    } finally {
      setLoading(false);
    }
  };

  const lines = report?.lines || [];

  // Group by inspector with running subtotals (permit count + drill count) — parity with print_completed.jsp.
  const groups = [];
  let currentKey = null;
  lines.forEach((line) => {
    const key = line.inspectorId || '—';
    if (key !== currentKey) {
      groups.push({ key, name: (line.inspectorName || '').trim(), rows: [], drills: 0 });
      currentKey = key;
    }
    const g = groups[groups.length - 1];
    g.rows.push(line);
    g.drills += Number(line.drillCount || 0);
  });
  const totalDrills = lines.reduce((sum, l) => sum + Number(l.drillCount || 0), 0);

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel">
        <div className="report-toolbar">
          <h1 className="page-title">Completed Work Report</h1>
          <Link className="report-back" to="/reports">← Back to reports</Link>
        </div>
        <p className="page-subtitle">Closed permits (PCLSD) whose inspection was completed in the date range, grouped by inspector.</p>

        <div className="report-filter">
          <DateRangeFilter
            preset={preset}
            onPresetChange={setPreset}
            stDate={stDate}
            endDate={endDate}
            onStDateChange={setStDate}
            onEndDateChange={setEndDate}
          />
          <div className="report-field">
            <label htmlFor="cw-inspector">Inspector</label>
            <select id="cw-inspector" className="form-control" value={inspectorId} onChange={(e) => setInspectorId(e.target.value)}>
              <option value="">All inspectors</option>
              {inspectors.map((i) => <option key={i.code} value={i.code}>{(i.label || '').trim()}</option>)}
            </select>
          </div>
          <div className="report-field">
            <label htmlFor="cw-city">City</label>
            <select id="cw-city" className="form-control" value={cityCode} onChange={(e) => setCityCode(e.target.value)}>
              <option value="">All cities</option>
              {cities.map((c) => <option key={c.code} value={c.code}>{(c.label || '').trim()}</option>)}
            </select>
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
                <span className="report-stat__label">Permits</span>
                <span className="report-stat__value">{lines.length}</span>
              </div>
              <div className="report-stat">
                <span className="report-stat__label">Total drills</span>
                <span className="report-stat__value">{totalDrills}</span>
              </div>
            </div>

            <div className="table-scroll" role="region" aria-label="Completed work" tabIndex={0}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Work category</th>
                    <th>Work type</th>
                    <th>Well use</th>
                    <th>Application</th>
                    <th>City</th>
                    <th>Permit #</th>
                    <th>Permit status</th>
                    <th>Completed</th>
                    <th className="report-num">Drills</th>
                  </tr>
                </thead>
                <tbody>
                  {lines.length === 0 ? (
                    <tr><td colSpan="9" className="muted-text">No completed work in this range.</td></tr>
                  ) : groups.map((group) => (
                    <Fragment key={group.key}>
                      <tr className="report-group-header">
                        <td colSpan="9">Inspector: {group.name || group.key} (ID {group.key})</td>
                      </tr>
                      {group.rows.map((line, idx) => (
                        <tr key={`${group.key}-${line.appId}-${line.permitNumber}-${idx}`}>
                          <td>{(line.workCatDesc || line.workCategory || '').trim()}</td>
                          <td>{(line.workDesc || line.workType || '').trim()}</td>
                          <td>{(line.wellUseDesc || line.wellUseType || '').trim()}</td>
                          <td>{line.appId}</td>
                          <td>{(line.cityName || line.cityCode || '').trim()}</td>
                          <td>{(line.permitNumber || '').trim()}</td>
                          <td>{line.permitStatus}</td>
                          <td>{line.inspectionCompleteDate}</td>
                          <td className="report-num">{line.drillCount}</td>
                        </tr>
                      ))}
                      <tr className="report-subtotal">
                        <td colSpan="8">{(group.name || group.key)} subtotal — {group.rows.length} permit(s)</td>
                        <td className="report-num">{group.drills}</td>
                      </tr>
                    </Fragment>
                  ))}
                  {lines.length > 0 && (
                    <tr className="report-grandtotal">
                      <td colSpan="8">Grand total — {lines.length} permit(s)</td>
                      <td className="report-num">{totalDrills}</td>
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

export default CompletedWork;
