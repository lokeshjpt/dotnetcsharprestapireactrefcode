import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { getScheduledInspections } from '../../api/inspectionApi';
import Loader from '../../components/Loader/Loader';
import './Inspections.css';
import '../Search/SearchPage.css';

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

const pad = (value) => String(value).padStart(2, '0');
const toKey = (year, month0, day) => `${pad(month0 + 1)}/${pad(day)}/${year}`; // MM/DD/YYYY (matches API)
const toIso = (year, month0, day) => `${year}-${pad(month0 + 1)}-${pad(day)}`;

// Inspections Calendar — parity with inspection_calendar.jsp. Shows a month grid marking days that
// have scheduled inspections; selecting a day lists that day's assignments (inspector, time, status).
function InspectionsCalendar() {
  const today = useMemo(() => new Date(), []);
  const [year, setYear] = useState(today.getFullYear());
  const [month, setMonth] = useState(today.getMonth()); // 0-based
  const [lines, setLines] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [selectedKey, setSelectedKey] = useState(toKey(today.getFullYear(), today.getMonth(), today.getDate()));

  const loadMonth = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      const from = toIso(year, month, 1);
      const to = toIso(year, month, new Date(year, month + 1, 0).getDate());
      const data = await getScheduledInspections(from, to);
      setLines(Array.isArray(data) ? data : []);
    } catch {
      setError('The inspections calendar could not be loaded. Verify the API is available.');
      setLines([]);
    } finally {
      setLoading(false);
    }
  }, [year, month]);

  useEffect(() => { loadMonth(); }, [loadMonth]);

  // Group scheduled inspection lines by their MM/DD/YYYY date key.
  const byDay = useMemo(() => {
    const map = new Map();
    lines.forEach((line) => {
      const key = line.inspectionDate;
      if (!key) return;
      if (!map.has(key)) map.set(key, []);
      map.get(key).push(line);
    });
    return map;
  }, [lines]);

  // Build the calendar grid cells (leading blanks + day numbers).
  const cells = useMemo(() => {
    const firstWeekday = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const result = [];
    for (let i = 0; i < firstWeekday; i += 1) result.push(null);
    for (let day = 1; day <= daysInMonth; day += 1) result.push(day);
    return result;
  }, [year, month]);

  const goPrev = () => {
    const prev = new Date(year, month - 1, 1);
    setYear(prev.getFullYear());
    setMonth(prev.getMonth());
  };
  const goNext = () => {
    const next = new Date(year, month + 1, 1);
    setYear(next.getFullYear());
    setMonth(next.getMonth());
  };

  const selectedEntries = byDay.get(selectedKey) || [];

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel">
        <h1 className="page-title">Inspections Calendar</h1>
        <p className="page-subtitle">Scheduled inspections by day. Select a date to see its assignments.</p>
        <Link className="page-help-link" to="/help#inspections-calendar">
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> How the calendar works — help
        </Link>

        {error && <div className="alert alert-error">{error}</div>}

        <div className="inspcal__head">
          <button type="button" className="btn btn-default" onClick={goPrev} aria-label="Previous month">‹‹</button>
          <h2 className="inspcal__title">{MONTHS[month]} {year}{loading ? ' …' : ''}</h2>
          <button type="button" className="btn btn-default" onClick={goNext} aria-label="Next month">››</button>
        </div>

        <div className="inspcal">
          <table className="inspcal__grid">
            <thead>
              <tr>{WEEKDAYS.map((wd) => <th key={wd} scope="col">{wd}</th>)}</tr>
            </thead>
            <tbody>
              {Array.from({ length: Math.ceil(cells.length / 7) }).map((_, rowIdx) => (
                <tr key={rowIdx}>
                  {cells.slice(rowIdx * 7, rowIdx * 7 + 7).map((day, colIdx) => {
                    if (day === null) {
                      return <td key={`e-${colIdx}`} className="inspcal__cell inspcal__cell--empty" />;
                    }
                    const key = toKey(year, month, day);
                    const count = byDay.get(key)?.length || 0;
                    const isSelected = key === selectedKey;
                    return (
                      <td
                        key={key}
                        className={`inspcal__cell${isSelected ? ' inspcal__cell--selected' : ''}`}
                        onClick={() => setSelectedKey(key)}
                        role="button"
                        tabIndex={0}
                        onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); setSelectedKey(key); } }}
                        aria-label={`${MONTHS[month]} ${day}, ${year}${count ? `, ${count} inspection(s)` : ''}`}
                      >
                        <span className="inspcal__daynum">{day}</span>
                        {count > 0 && <><br /><span className="inspcal__dot">{count}</span></>}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>

          <div className="inspcal__day-panel">
            <h3 className="inspcal__day-title">{selectedKey}</h3>
            {selectedEntries.length === 0 ? (
              <p className="muted-text">No inspections scheduled for this day.</p>
            ) : (
              selectedEntries.map((entry, idx) => (
                <div className="inspcal__entry" key={`${entry.appId}-${idx}`}>
                  <dl>
                    <dt>Inspector</dt><dd>{entry.inspectorName || 'Unassigned'}</dd>
                    <dt>Time</dt><dd>{entry.inspectionTimeDisp || '—'}</dd>
                    <dt>Status</dt><dd>{entry.statusDesc || entry.statusCode}</dd>
                  </dl>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default InspectionsCalendar;
