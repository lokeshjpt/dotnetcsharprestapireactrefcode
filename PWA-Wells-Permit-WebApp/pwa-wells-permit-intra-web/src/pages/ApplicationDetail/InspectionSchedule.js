import { useCallback, useEffect, useMemo, useState } from 'react';
import Button from '../../components/UI/Button';
import {
  addInspection,
  deleteInspection,
  getInspectionAvailability,
  getInspections,
  updateInspection,
} from '../../api/inspectionApi';
import { formatStatus, statusPillClass } from '../../constants/statusCodes';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import './InspectionSchedule.css';

// Fixed visit time slots, mirroring the legacy Java inspection_detail.jsp time dropdown.
const TIME_OPTIONS = [
  { value: '05:00:00', label: '05:00 AM (default)' },
  { value: '08:30:00', label: '08:30 AM' },
  { value: '09:00:00', label: '09:00 AM' },
  { value: '09:30:00', label: '09:30 AM' },
  { value: '10:00:00', label: '10:00 AM' },
  { value: '10:30:00', label: '10:30 AM' },
  { value: '11:00:00', label: '11:00 AM' },
  { value: '11:30:00', label: '11:30 AM' },
  { value: '12:00:00', label: '12:00 PM' },
  { value: '12:30:00', label: '12:30 PM' },
  { value: '13:00:00', label: '01:00 PM' },
  { value: '13:30:00', label: '01:30 PM' },
  { value: '14:00:00', label: '02:00 PM' },
  { value: '14:30:00', label: '02:30 PM' },
  { value: '15:00:00', label: '03:00 PM' },
  { value: '15:30:00', label: '03:30 PM' },
];

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

function pad(n) {
  return String(n).padStart(2, '0');
}

// Local yyyy-MM-dd (avoids the UTC shift that toISOString would introduce).
function dateKey(d) {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function todayKey() {
  return dateKey(new Date());
}

function firstOfMonth(d) {
  return new Date(d.getFullYear(), d.getMonth(), 1);
}

// Extracts the HH:mm:ss time portion from an inspectionDateTime value.
function timeOf(value) {
  if (!value) return '';
  const str = String(value);
  if (str.includes('T')) return str.split('T')[1].slice(0, 8);
  if (/^\d{2}:\d{2}/.test(str)) return str.slice(0, 8);
  return '';
}

function InspectionSchedule({ appId, inspectors, inspections, onChanged }) {
  const showToast = useToast();

  const [calMonth, setCalMonth] = useState(() => firstOfMonth(new Date()));
  const [availByDay, setAvailByDay] = useState({});
  const [loadingCal, setLoadingCal] = useState(false);

  // "Add a Site Visit" form state.
  const [addInspector, setAddInspector] = useState('');
  const [addDate, setAddDate] = useState('');
  const [addSlot, setAddSlot] = useState(null);
  const [addTime, setAddTime] = useState('05:00:00');
  const [adding, setAdding] = useState(false);

  // Per-row editable copy of the scheduled assignments (inspector + time).
  const [rows, setRows] = useState([]);

  useEffect(() => {
    setRows((inspections || []).map((ins) => ({
      ...ins,
      inspectorId: ins.inspectorId != null ? String(ins.inspectorId) : '',
      time: timeOf(ins.inspectionDateTime),
    })));
  }, [inspections]);

  const loadMonth = useCallback(async (monthStart) => {
    const start = firstOfMonth(monthStart);
    const end = new Date(start.getFullYear(), start.getMonth() + 1, 0);
    setLoadingCal(true);
    try {
      const data = await getInspectionAvailability(dateKey(start), dateKey(end));
      const map = {};
      (data || []).forEach((slot) => {
        const key = String(slot.date).slice(0, 10);
        if (!map[key]) map[key] = { total: 0, open: 0, slots: [] };
        map[key].total += 1;
        map[key].slots.push({ slotId: slot.slotId, available: slot.available });
        if (slot.available) map[key].open += 1;
      });
      setAvailByDay(map);
    } catch {
      setAvailByDay({});
      showToast('Inspection availability could not be loaded.', 'error');
    } finally {
      setLoadingCal(false);
    }
  }, [showToast]);

  useEffect(() => {
    loadMonth(calMonth);
  }, [calMonth, loadMonth]);

  const refresh = async () => {
    try {
      const data = await getInspections(appId);
      if (onChanged) onChanged(data || []);
    } catch {
      /* parent keeps its current list */
    }
    loadMonth(calMonth);
  };

  const firstOpenSlot = (key) => {
    const day = availByDay[key];
    if (!day) return null;
    const open = day.slots.find((s) => s.available);
    return open ? open.slotId : null;
  };

  const selectDay = (key) => {
    const slot = firstOpenSlot(key);
    if (slot == null) return;
    setAddDate(key);
    setAddSlot(slot);
  };

  const handleAdd = async () => {
    if (!addDate || addSlot == null) {
      showToast('Pick an available date from the calendar first.', 'error');
      return;
    }
    setAdding(true);
    try {
      await addInspection({
        appId,
        inspectionDate: addDate,
        slotId: addSlot,
        inspectorId: addInspector ? Number(addInspector) : null,
        inspectionDateTime: `${addDate}T${addTime || '05:00:00'}`,
        addedBy: 'staff-portal',
        statusCode: 'IPEND',
        notes: null,
      });
      showToast(`Site visit added for ${new Date(`${addDate}T00:00:00`).toLocaleDateString()}.`, 'success');
      setAddInspector('');
      setAddDate('');
      setAddSlot(null);
      setAddTime('05:00:00');
      await refresh();
    } catch {
      showToast('That site visit could not be added. The slot may already be booked.', 'error');
    } finally {
      setAdding(false);
    }
  };

  const rowKey = (r) => `${String(r.inspectionDate).slice(0, 10)}-${r.slotId}`;

  const patchRow = (key, patch) => {
    setRows((current) => current.map((r) => (rowKey(r) === key ? { ...r, ...patch } : r)));
  };

  const handleUpdate = async (row) => {
    try {
      await updateInspection({
        inspectionDate: String(row.inspectionDate).slice(0, 10),
        slotId: row.slotId,
        inspectorId: row.inspectorId ? Number(row.inspectorId) : null,
        inspectionDateTime: `${String(row.inspectionDate).slice(0, 10)}T${row.time || '05:00:00'}`,
        statusCode: 'IPEND',
        notes: null,
      });
      showToast('Site visit updated.', 'success');
      await refresh();
    } catch {
      showToast('That site visit could not be updated.', 'error');
    }
  };

  const handleRemove = async (row) => {
    // eslint-disable-next-line no-alert
    if (!window.confirm('Remove this site visit?')) return;
    const dateKey = String(row.inspectionDate).slice(0, 10);
    try {
      await deleteInspection(dateKey, row.slotId);
      // Optimistically drop it from the parent list so the row disappears immediately, even if the
      // follow-up reload is slow or fails.
      if (onChanged) {
        onChanged((inspections || []).filter((i) => !(String(i.inspectionDate).slice(0, 10) === dateKey && i.slotId === row.slotId)));
      }
      showToast('Site visit removed.', 'success');
      loadMonth(calMonth);
    } catch {
      showToast('That site visit could not be removed.', 'error');
    }
  };

  const inspectorName = (id) => {
    const found = (inspectors || []).find((i) => String(i.code) === String(id));
    return found ? found.label : (id || 'Unassigned');
  };

  // Build the calendar grid (weeks of 7 days, leading/trailing blanks).
  const weeks = useMemo(() => {
    const start = firstOfMonth(calMonth);
    const daysInMonth = new Date(start.getFullYear(), start.getMonth() + 1, 0).getDate();
    const cells = [];
    for (let i = 0; i < start.getDay(); i += 1) cells.push(null);
    for (let d = 1; d <= daysInMonth; d += 1) cells.push(new Date(start.getFullYear(), start.getMonth(), d));
    while (cells.length % 7 !== 0) cells.push(null);
    const out = [];
    for (let i = 0; i < cells.length; i += 7) out.push(cells.slice(i, i + 7));
    return out;
  }, [calMonth]);

  const tKey = todayKey();

  return (
    <div className="insp-schedule">
      <h4 className="insp-schedule__title">INSPECTION / REVIEW SCHEDULE</h4>

      {/* --- Inspection availability calendar --- */}
      <div className="insp-cal">
        <div className="insp-cal__nav">
          <Button variant="secondary" onClick={() => setCalMonth((m) => new Date(m.getFullYear(), m.getMonth() - 1, 1))}>&#8249; Prev</Button>
          <span className="insp-cal__month">{MONTHS[calMonth.getMonth()]} {calMonth.getFullYear()}{loadingCal ? ' · loading…' : ''}</span>
          <Button variant="secondary" onClick={() => setCalMonth((m) => new Date(m.getFullYear(), m.getMonth() + 1, 1))}>Next &#8250;</Button>
        </div>
        <table className="insp-cal__grid">
          <thead>
            <tr>{WEEKDAYS.map((w) => <th key={w}>{w}</th>)}</tr>
          </thead>
          <tbody>
            {weeks.map((week, wi) => (
              <tr key={wi}>
                {week.map((day, di) => {
                  if (!day) return <td key={di} className="insp-cal__cell insp-cal__cell--blank" />;
                  const key = dateKey(day);
                  const info = availByDay[key];
                  const past = key < tKey;
                  const open = info ? info.open : 0;
                  const isSelected = key === addDate;
                  const bookable = !past && open > 0;
                  let cls = 'insp-cal__cell';
                  if (past) cls += ' insp-cal__cell--past';
                  else if (open === 0 && info) cls += ' insp-cal__cell--full';
                  else if (bookable) cls += ' insp-cal__cell--open';
                  if (isSelected) cls += ' insp-cal__cell--selected';
                  return (
                    <td
                      key={di}
                      className={cls}
                      title={past ? 'Past date' : info ? (open > 0 ? `${open} of ${info.total} slots open` : 'Fully reserved / unavailable') : ''}
                    >
                      {bookable ? (
                        <button
                          type="button"
                          className="insp-cal__cellbtn"
                          aria-pressed={isSelected}
                          onClick={() => selectDay(key)}
                        >
                          <span className="insp-cal__day">{day.getDate()}</span>
                          <span className="insp-cal__slots">{open > 0 ? `${open} open` : 'Reserved'}</span>
                        </button>
                      ) : (
                        <>
                          <span className="insp-cal__day">{day.getDate()}</span>
                          {!past && info && (
                            <span className="insp-cal__slots">{open > 0 ? `${open} open` : 'Reserved'}</span>
                          )}
                        </>
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
        <div className="insp-cal__legend">
          <span><i className="swatch swatch--open" /> Open</span>
          <span><i className="swatch swatch--full" /> Fully reserved</span>
          <span><i className="swatch swatch--past" /> Past</span>
        </div>
      </div>

      {/* --- Add a Site Visit --- */}
      <div className="insp-add">
        <div className="insp-add__label">Add a Site Visit</div>
        <div className="insp-add__row">
          <label className="insp-field">
            <span>Inspector / Field Technician</span>
            <select className="form-control" value={addInspector} onChange={(e) => setAddInspector(e.target.value)}>
              <option value="">(none)</option>
              {(inspectors || []).map((i) => (
                <option key={i.code} value={i.code}>{i.label}</option>
              ))}
            </select>
          </label>
          <label className="insp-field">
            <span>Date</span>
            <input className="form-control" readOnly value={addDate ? new Date(`${addDate}T00:00:00`).toLocaleDateString() : ''} placeholder="Select from calendar" />
          </label>
          <label className="insp-field">
            <span>Time</span>
            <select className="form-control" value={addTime} onChange={(e) => setAddTime(e.target.value)}>
              {TIME_OPTIONS.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
          </label>
          <div className="insp-field insp-field--action">
            <Button onClick={handleAdd} disabled={adding || !addDate}>{adding ? 'Adding…' : 'Add'}</Button>
          </div>
        </div>
      </div>

      {/* --- Scheduled visits --- */}
      <table className="data-table insp-list">
        <thead>
          <tr>
            <th>Inspector / Field Technician</th>
            <th>Date / Time</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr><td colSpan="4" className="muted-text">No site visits scheduled.</td></tr>
          ) : rows.map((row) => {
            const editable = row.statusCode === 'IRSRV' || row.statusCode === 'IPEND';
            const dateDisp = new Date(`${String(row.inspectionDate).slice(0, 10)}T00:00:00`).toLocaleDateString();
            return (
              <tr key={rowKey(row)}>
                <td>
                  {editable ? (
                    <select className="form-control" aria-label={`Inspector for the ${dateDisp} site visit`} value={row.inspectorId} onChange={(e) => patchRow(rowKey(row), { inspectorId: e.target.value })}>
                      <option value="">(none)</option>
                      {(inspectors || []).map((i) => (
                        <option key={i.code} value={i.code}>{i.label}</option>
                      ))}
                    </select>
                  ) : inspectorName(row.inspectorId)}
                </td>
                <td>
                  <div className="insp-list__datetime">
                    <span className="insp-list__date">{dateDisp}</span>
                    {editable ? (
                      <select className="form-control insp-list__time" aria-label={`Time for the ${dateDisp} site visit`} value={row.time} onChange={(e) => patchRow(rowKey(row), { time: e.target.value })}>
                        <option value="" />
                        {TIME_OPTIONS.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                      </select>
                    ) : (
                      <span>{(TIME_OPTIONS.find((t) => t.value === row.time) || {}).label || row.time}</span>
                    )}
                  </div>
                </td>
                <td><span className={statusPillClass(row.statusCode)}>{formatStatus(row.statusCode)}</span></td>
                <td>
                  <div className="insp-list__actions">
                    {editable && <Button variant="secondary" onClick={() => handleUpdate(row)}>Update</Button>}
                    <Button variant="secondary" onClick={() => handleRemove(row)}>Remove</Button>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export default InspectionSchedule;
