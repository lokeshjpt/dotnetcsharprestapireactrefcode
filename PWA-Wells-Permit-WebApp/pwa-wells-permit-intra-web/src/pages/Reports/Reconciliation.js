import { Fragment, useState } from 'react';
import { Link } from 'react-router-dom';
import Button from '../../components/UI/Button';
import Loader from '../../components/Loader/Loader';
import DateRangeFilter from './components/DateRangeFilter';
import { getReconciliationReport } from '../../api/reportApi';
import { resolveDateRange } from '../../utils/reportDates';
import './Reports.css';

const PAY_TYPES = [
  { code: '', label: 'All payment types' },
  { code: 'cc', label: 'Credit Card (MC / VISA)' },
  { code: 'ch', label: 'Check / Cash' },
];

function money(value) {
  const num = Number(value || 0);
  return num.toLocaleString('en-US', { style: 'currency', currency: 'USD' });
}

// Group label for the reconciliation subtotals (legacy rpt_recon groups CASH / CHECK / Credit Card).
function groupOf(paymentType) {
  const t = (paymentType || '').trim().toUpperCase();
  if (t === 'MC' || t === 'VISA') return 'Credit Card';
  if (t === 'CASH') return 'Cash';
  if (t === 'CHECK') return 'Check';
  return t || 'Other';
}

function Reconciliation() {
  const [preset, setPreset] = useState('mtd');
  const [stDate, setStDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [payType, setPayType] = useState('');
  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [ran, setRan] = useState(false);

  const run = async () => {
    const { start, end } = resolveDateRange(preset, stDate, endDate);
    if (!start || !end) {
      setError('Please choose a start and end date.');
      return;
    }
    setError('');
    setLoading(true);
    setRan(true);
    try {
      const data = await getReconciliationReport({ start, end, payType });
      setReport(data);
    } catch {
      setError('Unable to run the reconciliation report.');
      setReport(null);
    } finally {
      setLoading(false);
    }
  };

  const lines = report?.lines || [];

  // Build grouped rows with per-group subtotals and a grand total (parity with rpt_recon.jsp).
  const groups = [];
  let currentKey = null;
  lines.forEach((line) => {
    const key = groupOf(line.paymentType);
    if (key !== currentKey) {
      groups.push({ key, rows: [], subtotal: 0 });
      currentKey = key;
    }
    const g = groups[groups.length - 1];
    g.rows.push(line);
    g.subtotal += Number(line.amount || 0);
  });
  const grandTotal = lines.reduce((sum, l) => sum + Number(l.amount || 0), 0);

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel">
        <div className="report-toolbar">
          <h1 className="page-title">Reconciliation Report</h1>
          <Link className="report-back" to="/reports">← Back to reports</Link>
        </div>
        <p className="page-subtitle">Paid transactions within a date range, grouped by payment method with subtotals.</p>

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
            <label htmlFor="recon-paytype">Payment method</label>
            <select id="recon-paytype" className="form-control" value={payType} onChange={(e) => setPayType(e.target.value)}>
              {PAY_TYPES.map((p) => <option key={p.code} value={p.code}>{p.label}</option>)}
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
                <span className="report-stat__label">Transactions</span>
                <span className="report-stat__value">{lines.length}</span>
              </div>
              <div className="report-stat">
                <span className="report-stat__label">Total collected</span>
                <span className="report-stat__value">{money(grandTotal)}</span>
              </div>
              <div className="report-stat">
                <span className="report-stat__label">Date range</span>
                <span className="report-stat__value" style={{ fontSize: '1rem' }}>{report.startDate} – {report.endDate}</span>
              </div>
            </div>

            <div className="table-scroll" role="region" aria-label="Reconciliation results" tabIndex={0}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Application</th>
                    <th>Business</th>
                    <th>Applicant</th>
                    <th>Payer</th>
                    <th>Method</th>
                    <th>Check #</th>
                    <th>Receipt #</th>
                    <th>Permit(s)</th>
                    <th>Paid date</th>
                    <th>Time</th>
                    <th className="report-num">Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {lines.length === 0 ? (
                    <tr><td colSpan="11" className="muted-text">No paid transactions in this range.</td></tr>
                  ) : groups.map((group) => (
                    <Fragment key={group.key}>
                      {group.rows.map((line, idx) => (
                        <tr key={`${group.key}-${line.appId}-${idx}`}>
                          <td>{line.appId}</td>
                          <td>{line.businessName}</td>
                          <td>{line.applicantName}</td>
                          <td>{line.payer}</td>
                          <td>{(line.paymentDesc || line.paymentType || '').trim()}</td>
                          <td>{line.checkNum}</td>
                          <td>{line.receiptNum}</td>
                          <td>{line.permitRange}</td>
                          <td>{line.paidDate}</td>
                          <td>{line.paidTime}</td>
                          <td className="report-num">{money(line.amount)}</td>
                        </tr>
                      ))}
                      <tr className="report-subtotal">
                        <td colSpan="10">{group.key} subtotal ({group.rows.length})</td>
                        <td className="report-num">{money(group.subtotal)}</td>
                      </tr>
                    </Fragment>
                  ))}
                  {lines.length > 0 && (
                    <tr className="report-grandtotal">
                      <td colSpan="10">Grand total ({lines.length})</td>
                      <td className="report-num">{money(grandTotal)}</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </>
        )}
        {ran && !report && !loading && !error && <p className="muted-text">No results.</p>}
      </div>
    </div>
  );
}

export default Reconciliation;
