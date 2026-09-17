import { useEffect, useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import InputField from '../../../../components/UI/InputField';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { updateExtension, getPermitInfo } from '../../../../api/applicationApi';

function toDateInput(value) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  return d.toISOString().slice(0, 10);
}

function displayDate(value) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString();
}

// Staff "New/Edit Post Approval Extension" (legacy app_post_approval_extension.jsp). Records a new
// permit extension window; on a new extension the count is incremented server-side.
function ExtendDateModal({ application, isEdit = false, onClose, onSaved }) {
  const showToast = useToast();
  const [saving, setSaving] = useState(false);
  const [permitExpireDate, setPermitExpireDate] = useState(null);
  const [error, setError] = useState('');
  const [form, setForm] = useState(() => ({
    extensionStartDate: isEdit ? toDateInput(application.extendStartDate) : '',
    extensionEndDate: isEdit ? toDateInput(application.extendEndDate) : '',
  }));

  useEffect(() => {
    let active = true;
    getPermitInfo(application.appId)
      .then((info) => {
        if (!active) return;
        const permits = info?.permits || [];
        if (permits.length) setPermitExpireDate(permits[0].expireDate);
      })
      .catch(() => {});
    return () => { active = false; };
  }, [application.appId]);

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));

  async function handleSave() {
    setError('');
    if (!form.extensionStartDate) { setError('Extension Start Date is required.'); return; }
    if (!form.extensionEndDate) { setError('Extension End Date is required.'); return; }
    if (form.extensionEndDate < form.extensionStartDate) {
      setError('Extension End Date must be greater than or same as Extension Start Date.');
      return;
    }
    setSaving(true);
    try {
      const updated = await updateExtension(application.appId, {
        extensionStartDate: form.extensionStartDate,
        extensionEndDate: form.extensionEndDate,
        isEdit,
      });
      onSaved(updated);
      showToast('Permit extension recorded.', 'success');
      onClose();
    } catch (err) {
      const msg = err?.response?.data?.detail || err?.response?.data || 'Failed to record the extension.';
      setError(typeof msg === 'string' ? msg : 'Failed to record the extension.');
      showToast('Failed to record the extension.', 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      size="md"
      title={`${isEdit ? 'Edit' : 'New'} Post Approval Extension`}
      onClose={saving ? undefined : onClose}
      footer={(
        <>
          <Button variant="default" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>{saving ? 'Saving…' : 'Update'}</Button>
        </>
      )}
    >
      {error && <div className="alert alert-error" role="alert">{error}</div>}
      <div className="ext-readonly-grid">
        <div><span className="ext-label">Permit Expire Date:</span> {displayDate(permitExpireDate)}</div>
        <div><span className="ext-label">Original Project Start Date:</span> {displayDate(application.projStartDate)}</div>
        <div><span className="ext-label">Project End Date:</span> {displayDate(application.projEndDate)}</div>
        <div><span className="ext-label">Extension Count:</span> {application.extendCount ?? 0}</div>
        <div><span className="ext-label">Extended By:</span> {application.extendBy || '—'}</div>
      </div>
      <div className="modal-form-grid">
        <InputField label="Extension Start Date" id="extensionStartDate" type="date" required value={form.extensionStartDate} onChange={set('extensionStartDate')} />
        <InputField label="Extension End Date" id="extensionEndDate" type="date" required value={form.extensionEndDate} onChange={set('extensionEndDate')} />
      </div>
    </Modal>
  );
}

export default ExtendDateModal;
