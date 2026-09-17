import { Link } from 'react-router-dom';
import { SSRS_REPORTS, buildSsrsReportUrl, isSsrsConfigured } from '../../constants/ssrsReports';
import './Reports.css';

// Java-built operational reports ported from the legacy intra DisplayReportServlet.
const APP_REPORTS = [
  { to: '/reports/reconciliation', title: 'Reconciliation Report', description: 'Paid transactions in a date range, grouped by payment method with subtotals and totals.' },
  { to: '/reports/completed-works', title: 'Completed Work Report', description: 'Closed permits with completed inspections, grouped by inspector with drill/permit totals.' },
  { to: '/reports/completed-inspections', title: 'Completed Inspections by Inspector', description: 'Applications with completed inspections, grouped by inspector.' },
  { to: '/reports/extract', title: 'Extract to Excel', description: 'Flat permit/well extract across current + historical records with CSV export.' },
];

function ReportsPage() {
  const ssrsConfigured = isSsrsConfigured();

  return (
    <div className="page-shell">
      <div className="panel">
        <h1 className="page-title">Reports</h1>
        <p className="page-subtitle">Operational reports for reconciliation, completed work, inspections, and data extracts.</p>
        <Link className="page-help-link" to="/help#reports">
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> About these reports — help
        </Link>

        <h2>Operational reports</h2>
        <div className="grid-two">
          {APP_REPORTS.map((report) => (
            <Link className="queue-box" to={report.to} key={report.to}>
              <span className="queue-box__label">{report.title}</span>
              <span className="queue-box__desc">{report.description}</span>
            </Link>
          ))}
        </div>

        <h2 style={{ marginTop: '28px' }}>SSRS reports</h2>
        {!ssrsConfigured && (
          <div className="alert alert-error">
            No SSRS report server is configured for this environment. Set <code>ssrsServer</code> in the environment config to enable these reports.
          </div>
        )}
        {ssrsConfigured && (
          <p className="page-subtitle">SSRS reports open in a new browser tab (the report server does not allow embedding in the app).</p>
        )}
        <div className="grid-two">
          {SSRS_REPORTS.map((report) => (
            <a
              className="queue-box"
              href={buildSsrsReportUrl(report.key)}
              target="_blank"
              rel="noreferrer"
              key={report.key}
            >
              <span className="queue-box__label">{report.title} ↗</span>
              <span className="queue-box__desc">{report.description}</span>
            </a>
          ))}
        </div>
      </div>
    </div>
  );
}

export default ReportsPage;
