import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getQueueCounts } from '../../api/reportApi';
import { QUEUES } from '../../constants/queues';
import config from '../../config';
import '../Reports/Reports.css';

function Home() {
  const [counts, setCounts] = useState({});
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    let active = true;
    getQueueCounts()
      .then((data) => {
        if (!active) return;
        setCounts(data?.counts || {});
      })
      .catch(() => {})
      .finally(() => {
        if (active) setLoaded(true);
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <div className="page-shell">
      <div className="panel">
        <h1 className="page-title">Permit processing dashboard</h1>
        <p className="page-subtitle">
          Review incoming applications, verify payment readiness, assign inspections, and complete approvals from one workspace.
        </p>
        <Link className="page-help-link" to="/help#overview">
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> New here? Read the Help &amp; how-to guide
        </Link>

        <h2>Work queues</h2>
        <p className="muted-text">Click a queue to open its prefiltered application list.</p>
        <div className="queue-boxes">
          {QUEUES.map((queue) => (
            <Link
              className={`queue-box${queue.accent ? ` queue-box--${queue.accent}` : ''}`}
              to={`/queue/${queue.statusCode}`}
              key={queue.statusCode}
            >
              <span className="queue-box__count">{loaded ? (counts[queue.statusCode] ?? 0) : '…'}</span>
              <span className="queue-box__label">{queue.title}</span>
              <span className="queue-box__desc">{queue.description}</span>
            </Link>
          ))}
        </div>

        <div className="grid-three" style={{ marginTop: '24px' }}>
          <div className="panel">
            <h2>Create Application</h2>
            <p className="muted-text">Start a new public well permit application in the applicant portal. Opens in a new tab.</p>
            <a
              className="btn btn-default"
              href={`${config.ecommBaseUrl}#/apply`}
              target="_blank"
              rel="noopener noreferrer"
            >
              Create Application
              <svg
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
                style={{ marginLeft: '6px', verticalAlign: 'text-bottom' }}
              >
                <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
                <polyline points="15 3 21 3 21 9" />
                <line x1="10" y1="14" x2="21" y2="3" />
              </svg>
            </a>
          </div>
          <div className="panel">
            <h2>Search applications</h2>
            <p className="muted-text">Lookup historical applications by number, city, or applicant last name.</p>
            <Link className="btn btn-default" to="/search">Search</Link>
          </div>
          <div className="panel">
            <h2>Reports</h2>
            <p className="muted-text">Access operational summaries and reference data.</p>
            <Link className="btn btn-default" to="/reports">Reports</Link>
          </div>
          <div className="panel">
            <h2>Inspections</h2>
            <p className="muted-text">Schedule and track inspections, WCR/GeoLog reviews, and on-hold permits.</p>
            <Link className="btn btn-default" to="/inspections/list">Inspections</Link>
          </div>
          <div className="panel">
            <h2>Code Maintenance</h2>
            <p className="muted-text">Manage reference code tables used across the permitting workflow.</p>
            <Link className="btn btn-default" to="/maintenance">Code Maintenance</Link>
          </div>
        </div>
      </div>
    </div>
  );
}

export default Home;
