import { useEffect, useRef } from 'react';
import { Link, useParams } from 'react-router-dom';
import Button from '../../components/UI/Button';
import { buildSsrsReportUrl, findSsrsReport, isSsrsConfigured } from '../../constants/ssrsReports';
import './Reports.css';

// SSRS ReportViewer launcher. The SQL Server Reporting Services server blocks cross-origin framing
// (X-Frame-Options), so the report cannot be embedded in an iframe here — the browser just shows a
// bare "refused to connect". Instead the report opens in a new browser tab (parity with the legacy
// target="_rpt" links). We best-effort auto-open on mount and always show a prominent launch button
// in case the auto-open was blocked by the browser's popup blocker.
function SsrsReport() {
  const { reportKey } = useParams();
  const report = findSsrsReport(reportKey);
  const configured = isSsrsConfigured();
  const externalUrl = buildSsrsReportUrl(reportKey);
  const autoOpened = useRef(false);

  useEffect(() => {
    if (configured && report && externalUrl && !autoOpened.current) {
      autoOpened.current = true;
      window.open(externalUrl, '_blank', 'noopener,noreferrer');
    }
  }, [configured, report, externalUrl]);

  return (
    <div className="page-shell">
      <div className="panel">
        <div className="report-toolbar">
          <h1 className="page-title">{report?.title || 'SSRS Report'}</h1>
          <Link className="report-back" to="/reports">← Back to reports</Link>
        </div>
        {report?.description && <p className="page-subtitle">{report.description}</p>}

        {!configured ? (
          <div className="alert alert-error">
            No SSRS report server is configured for this environment. Set <code>ssrsServer</code> in the environment config to enable this report.
          </div>
        ) : !report ? (
          <div className="alert alert-error">Unknown report.</div>
        ) : (
          <div className="ssrs-launch">
            <p className="ssrs-launch__text">
              This SSRS report opens in a new browser tab — the report server does not allow it to be
              embedded inside the app. If a new tab did not open automatically, it may have been blocked
              by your browser; use the button below to open the report.
            </p>
            <a href={externalUrl} target="_blank" rel="noreferrer">
              <Button variant="primary">Open report ↗</Button>
            </a>
          </div>
        )}
      </div>
    </div>
  );
}

export default SsrsReport;
