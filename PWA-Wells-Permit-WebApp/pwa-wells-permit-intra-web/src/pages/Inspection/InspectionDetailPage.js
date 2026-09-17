import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  getInspectionAvailability,
  getInspections,
  scheduleInspection,
  updateInspection,
} from '../../api/inspectionApi';
import { getInspectors } from '../../api/referenceApi';
import InputField from '../../components/UI/InputField';
import Button from '../../components/UI/Button';
import { formatStatus, statusPillClass } from '../../constants/statusCodes';
import { useToast } from '../../components/UI/Toaster/ToastProvider';

function todayIso() {
  return new Date().toISOString().slice(0, 10);
}

function plusDaysIso(days) {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
}

function InspectionDetailPage() {
  const { appId } = useParams();
  const showToast = useToast();
  const [inspectors, setInspectors] = useState([]);
  const [inspections, setInspections] = useState([]);
  const [availability, setAvailability] = useState([]);
  const [range, setRange] = useState({ from: todayIso(), to: plusDaysIso(14) });
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [loadingSlots, setLoadingSlots] = useState(false);

  const loadInspections = useCallback(() => {
    getInspections(appId).then((data) => setInspections(data || [])).catch(() => setInspections([]));
  }, [appId]);

  useEffect(() => {
    getInspectors().then(setInspectors).catch(() => setInspectors([]));
    loadInspections();
  }, [appId, loadInspections]);

  const loadAvailability = async () => {
    setError('');
    setLoadingSlots(true);
    try {
      const data = await getInspectionAvailability(range.from, range.to);
      setAvailability(data || []);
    } catch {
      setError('Availability could not be loaded for the selected range.');
      setAvailability([]);
    } finally {
      setLoadingSlots(false);
    }
  };

  const handleSchedule = async (slot) => {
    setError('');
    setMessage('');
    try {
      await scheduleInspection({
        applicationId: appId,
        date: slot.date,
        slotId: slot.slotId,
        scheduledBy: 'staff-portal',
      });
      setMessage(`Reserved slot ${slot.slotId} on ${new Date(slot.date).toLocaleDateString()}.`);
      showToast(`Inspection slot ${slot.slotId} reserved for ${new Date(slot.date).toLocaleDateString()}.`, 'success');
      loadInspections();
      loadAvailability();
    } catch {
      setError('That slot could not be scheduled. It may already be booked.');
    }
  };

  const handleMarkPending = async (inspection) => {
    setError('');
    setMessage('');
    try {
      const updated = await updateInspection({
        inspectionDate: inspection.inspectionDate,
        slotId: inspection.slotId,
        inspectorId: inspection.inspectorId,
        inspectionDateTime: inspection.inspectionDateTime,
        statusCode: 'IPEND',
        notes: inspection.notes,
      });
      setInspections((current) => current.map((item) => (
        item.inspectionDate === updated.inspectionDate && item.slotId === updated.slotId ? updated : item
      )));
      setMessage(`Slot ${updated.slotId} moved to ${formatStatus(updated.statusCode)}.`);
      showToast(`Slot ${updated.slotId} updated to ${formatStatus(updated.statusCode)}.`, 'success');
    } catch {
      setError('The inspection status could not be updated.');
    }
  };

  return (
    <div className="page-shell">
      <div className="panel">
        <h1 className="page-title">Inspection scheduling for {appId}</h1>
        <p className="page-subtitle">Reserve inspection slots from availability and move reserved records into the pending state.</p>

        {error && <div className="alert alert-error">{error}</div>}
        {message && <div className="alert alert-success">{message}</div>}

        <div className="grid-two">
          <InputField id="fromDate" label="From" type="date" value={range.from} onChange={(event) => setRange((current) => ({ ...current, from: event.target.value }))} />
          <InputField id="toDate" label="To" type="date" value={range.to} onChange={(event) => setRange((current) => ({ ...current, to: event.target.value }))} />
        </div>
        <Button onClick={loadAvailability} disabled={loadingSlots}>{loadingSlots ? 'Loading...' : 'Check availability'}</Button>

        <table className="data-table" style={{ marginTop: '20px' }}>
          <thead>
            <tr>
              <th>Date</th>
              <th>Slot</th>
              <th>Label</th>
              <th>Available</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {availability.length === 0 ? (
              <tr><td colSpan="5" className="muted-text">No availability loaded. Choose a date range and check availability.</td></tr>
            ) : availability.map((slot) => (
              <tr key={`${slot.date}-${slot.slotId}`}>
                <td>{new Date(slot.date).toLocaleDateString()}</td>
                <td>{slot.slotId}</td>
                <td>{slot.label}</td>
                <td>{slot.available ? 'Yes' : 'No'}</td>
                <td>
                  <Button variant="secondary" disabled={!slot.available} onClick={() => handleSchedule(slot)}>
                    Reserve
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <h2 style={{ marginTop: '28px' }}>Scheduled inspections</h2>
        <table className="data-table" style={{ marginTop: '12px' }}>
          <thead>
            <tr>
              <th>Slot</th>
              <th>Inspector</th>
              <th>Date</th>
              <th>Time</th>
              <th>Status</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {inspections.length === 0 ? (
              <tr><td colSpan="6" className="muted-text">No inspections are scheduled.</td></tr>
            ) : inspections.map((inspection) => (
              <tr key={`${inspection.inspectionDate}-${inspection.slotId}`}>
                <td>{inspection.slotId}</td>
                <td>{(inspectors.find((i) => String(i.code) === String(inspection.inspectorId)) || {}).label || inspection.inspectorId || 'Unassigned'}</td>
                <td>{inspection.inspectionDate ? new Date(inspection.inspectionDate).toLocaleDateString() : 'N/A'}</td>
                <td>{inspection.inspectionDateTime ? new Date(inspection.inspectionDateTime).toLocaleTimeString() : 'N/A'}</td>
                <td><span className={statusPillClass(inspection.statusCode)}>{formatStatus(inspection.statusCode)}</span></td>
                <td>
                  {inspection.statusCode === 'IRSRV' ? (
                    <Button variant="secondary" onClick={() => handleMarkPending(inspection)}>
                      Move to {formatStatus('IPEND')}
                    </Button>
                  ) : (
                    <span className="muted-text">—</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default InspectionDetailPage;
