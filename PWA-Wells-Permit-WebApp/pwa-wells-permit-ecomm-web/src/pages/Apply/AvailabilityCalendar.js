import { useEffect, useMemo, useState } from 'react';
import { getInspectionCalendar } from '../../api/inspectionApi';
import {
  isWeekend,
  isCountyHoliday,
  parseLocalDate,
  addDays,
  startOfToday,
  toIsoDate,
  formatUsDate,
  VALID_START_MIN_DAYS,
  VALID_START_MAX_DAYS,
} from './countyHolidays';
import './AvailabilityCalendar.css';

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];
const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

// Client-side fallback reason when the API is unreachable (weekends/holidays only).
function fallbackReason(date) {
  return isWeekend(date) || isCountyHoliday(date) ? 'H' : '';
}

function AvailabilityCalendar({ mode = 'start', value, startDate, onSelect, onClose }) {
  const today = startOfToday();
  const clientValidStart = addDays(today, VALID_START_MIN_DAYS);
  const clientValidEnd = addDays(today, VALID_START_MAX_DAYS);

  // For the completion-date calendar, the earliest selectable day is 10 days out AND on/after the
  // chosen start date (mirrors the legacy end-date calendar min plus a sensible start<=end rule).
  const endMin = useMemo(() => {
    const s = parseLocalDate(startDate);
    return s && s > clientValidStart ? s : clientValidStart;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [startDate]);

  const initial = parseLocalDate(value) || (mode === 'end' ? endMin : clientValidStart);
  const [viewYear, setViewYear] = useState(initial.getFullYear());
  const [viewMonth, setViewMonth] = useState(initial.getMonth());
  const [dayInfo, setDayInfo] = useState({}); // iso -> { available, reason }
  const [validStart, setValidStart] = useState(clientValidStart);
  const [validEnd, setValidEnd] = useState(clientValidEnd);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const first = new Date(viewYear, viewMonth, 1);
    const last = new Date(viewYear, viewMonth + 1, 0);
    setLoading(true);
    getInspectionCalendar(toIsoDate(first), toIsoDate(last))
      .then((data) => {
        if (cancelled) return;
        const map = {};
        (data.days || []).forEach((d) => {
          map[toIsoDate(parseLocalDate(d.date))] = { available: d.available, reason: d.reason || '' };
        });
        setDayInfo(map);
        if (data.validRangeStart) setValidStart(parseLocalDate(data.validRangeStart));
        if (data.validRangeEnd) setValidEnd(parseLocalDate(data.validRangeEnd));
      })
      .catch(() => {
        if (cancelled) return;
        // Offline fallback: compute weekends/holidays locally, leave U/M unknown.
        const map = {};
        for (let d = 1; d <= last.getDate(); d += 1) {
          const dt = new Date(viewYear, viewMonth, d);
          const reason = fallbackReason(dt);
          map[toIsoDate(dt)] = { available: reason === '', reason };
        }
        setDayInfo(map);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [viewYear, viewMonth]);

  const weeks = useMemo(() => {
    const first = new Date(viewYear, viewMonth, 1);
    const daysInMonth = new Date(viewYear, viewMonth + 1, 0).getDate();
    const cells = [];
    for (let i = 0; i < first.getDay(); i += 1) cells.push(null);
    for (let d = 1; d <= daysInMonth; d += 1) cells.push(new Date(viewYear, viewMonth, d));
    while (cells.length % 7 !== 0) cells.push(null);
    const rows = [];
    for (let i = 0; i < cells.length; i += 7) rows.push(cells.slice(i, i + 7));
    return rows;
  }, [viewYear, viewMonth]);

  const withinRange = (date) => {
    if (mode === 'end') return date >= endMin;
    return date >= validStart && date <= validEnd;
  };

  const classify = (date) => {
    const iso = toIsoDate(date);
    const info = dayInfo[iso] || { available: fallbackReason(date) === '', reason: fallbackReason(date) };
    const inRange = withinRange(date);
    if (!info.available) {
      if (info.reason === 'U') return { cls: 'ac-day--blocked', selectable: false };
      if (info.reason === 'M') return { cls: 'ac-day--max', selectable: false };
      return { cls: 'ac-day--holiday', selectable: false }; // H / weekend
    }
    if (!inRange) return { cls: 'ac-day--outrange', selectable: false };
    return { cls: 'ac-day--available', selectable: true };
  };

  const goMonth = (delta) => {
    const d = new Date(viewYear, viewMonth + delta, 1);
    setViewYear(d.getFullYear());
    setViewMonth(d.getMonth());
  };

  const pick = (date) => {
    const { selectable } = classify(date);
    if (selectable) onSelect(toIsoDate(date));
  };

  const rangeLabel = mode === 'end'
    ? `Completion date must be on or after ${formatUsDate(endMin)} and available for inspection.`
    : `Valid Project Start Dates are between ${formatUsDate(validStart)} and ${formatUsDate(validEnd)}.`;

  return (
    <div className="ac-overlay" role="dialog" aria-modal="true" aria-label="Inspection Availability Calendar" onClick={onClose}>
      <div className="ac-modal" onClick={(e) => e.stopPropagation()}>
        <div className="ac-header">
          <h3 className="ac-title">Inspection Availability Calendar</h3>
          <button type="button" className="ac-close" aria-label="Close" onClick={onClose}>&times;</button>
        </div>

        <p className="ac-help">
          Project {mode === 'end' ? 'Completion' : 'Start'} Date must be at least 10 days, but no more than
          90 days, from the current date, and on a date available for inspection. County holidays and
          weekends are not available.
        </p>
        <p className="ac-range">{rangeLabel}</p>

        <div className="ac-body">
          <ul className="ac-legend">
            <li><span className="ac-swatch ac-day--available" /> Available</li>
            <li><span className="ac-swatch ac-day--blocked" /> Date blocked by Public Works Agency</li>
            <li><span className="ac-swatch ac-day--max" /> Maximum Inspections reached</li>
            <li><span className="ac-swatch ac-day--holiday" /> Weekends &amp; County Holidays</li>
            <li><span className="ac-swatch ac-day--outrange" /> Date not within current valid range</li>
          </ul>

          <div className="ac-cal">
            <div className="ac-nav">
              <button type="button" className="ac-nav-btn" onClick={() => goMonth(-1)} aria-label="Previous month">&laquo;</button>
              <span className="ac-month">{MONTH_NAMES[viewMonth]} {viewYear}</span>
              <button type="button" className="ac-nav-btn" onClick={() => goMonth(1)} aria-label="Next month">&raquo;</button>
            </div>
            <table className="ac-grid">
              <thead>
                <tr>{DAY_NAMES.map((d) => <th key={d}>{d}</th>)}</tr>
              </thead>
              <tbody>
                {weeks.map((row, ri) => (
                  // eslint-disable-next-line react/no-array-index-key
                  <tr key={ri}>
                    {row.map((date, ci) => {
                      if (!date) return <td key={ci} className="ac-day ac-day--empty" />;
                      const { cls, selectable } = classify(date);
                      const isSelected = value && toIsoDate(date) === toIsoDate(parseLocalDate(value));
                      return (
                        <td
                          key={ci}
                          className={`ac-day ${cls}${selectable ? ' ac-day--pick' : ''}${isSelected ? ' ac-day--selected' : ''}`}
                          onClick={() => pick(date)}
                          role={selectable ? 'button' : undefined}
                          tabIndex={selectable ? 0 : undefined}
                          onKeyDown={(e) => { if (selectable && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); pick(date); } }}
                        >
                          {date.getDate()}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
            {loading && <div className="ac-loading">Loading availability…</div>}
          </div>
        </div>
      </div>
    </div>
  );
}

export default AvailabilityCalendar;
