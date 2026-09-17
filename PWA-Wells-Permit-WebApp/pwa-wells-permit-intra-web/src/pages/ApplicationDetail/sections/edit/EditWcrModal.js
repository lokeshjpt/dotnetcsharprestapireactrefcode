import { useRef, useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { updateWcr, uploadWcrImage } from '../../../../api/applicationApi';

// Mirrors the legacy upd_work_specs_dwr.jsp "Enter WCR" grid: staff enter the State Well # and WCR #
// for each well spec (plus Construction Permit #/WCR # for destruction-category work), can copy the
// WCR number down from row 1, and upload a scanned WCR image per row (only after the row is saved).
function fileName(path) {
  if (!path) return '';
  const parts = String(path).split(/[\\/]/);
  return parts[parts.length - 1] || String(path);
}

function EditWcrModal({ application, work, onClose, onSaved }) {
  const showToast = useToast();
  const [saving, setSaving] = useState(false);
  const [uploadingId, setUploadingId] = useState(null);
  const fileInputs = useRef({});
  const isDestruction = (work.workCategory || '').toLowerCase().startsWith('des');

  const [dwrNumShared, setDwrNumShared] = useState(
    (work.specs || []).some((s) => (s.dwrNumShared || '').toUpperCase() === 'Y'),
  );
  const [rows, setRows] = useState(() => (work.specs || []).map((s) => ({
    workSpecsId: s.workSpecsId,
    ownerWellNum: s.ownerWellNum || '',
    stateWellId: s.stateWellId || '',
    complWellDwrNum: s.complWellDwrNum || '',
    permitNum: s.permitNum || '',
    dwrNum: s.dwrNum || '',
    dwrImage: s.dwrImage || '',
    // Row can accept an image only once its State Well # has been persisted (legacy guard).
    savedStateWellId: !!(s.stateWellId && String(s.stateWellId).trim()),
  })));

  const setRow = (idx, key) => (e) =>
    setRows((list) => list.map((r, i) => (i === idx ? { ...r, [key]: e.target.value } : r)));

  function handleCopyToggle(e) {
    const checked = e.target.checked;
    setDwrNumShared(checked);
    if (!checked || rows.length === 0) return;
    const first = rows[0];
    if (!first.stateWellId.trim() || !first.complWellDwrNum.trim()) {
      showToast('Enter State Well # and WCR # for Row 1 first.', 'error');
      setDwrNumShared(false);
      return;
    }
    setRows((list) => list.map((r, i) => (i === 0 ? r : {
      ...r,
      stateWellId: r.stateWellId.trim() ? r.stateWellId : first.stateWellId,
      complWellDwrNum: r.complWellDwrNum.trim() ? r.complWellDwrNum : first.complWellDwrNum,
    })));
  }

  async function handleSave() {
    setSaving(true);
    try {
      const payload = {
        dwrNumShared,
        specs: rows.map((r) => ({
          workSpecsId: r.workSpecsId,
          stateWellId: r.stateWellId,
          complWellDwrNum: r.complWellDwrNum,
          permitNum: isDestruction ? r.permitNum : null,
          dwrNum: isDestruction ? r.dwrNum : null,
        })),
      };
      const updated = await updateWcr(application.appId, work.workId, payload);
      onSaved(updated);
      showToast('WCR details saved.', 'success');
      onClose();
    } catch (err) {
      const msg = err?.response?.data || 'Failed to save WCR details.';
      showToast(typeof msg === 'string' ? msg : 'Failed to save WCR details.', 'error');
    } finally {
      setSaving(false);
    }
  }

  async function handleImagePick(row, e) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    setUploadingId(row.workSpecsId);
    try {
      const result = await uploadWcrImage(application.appId, work.workId, row.workSpecsId, file);
      setRows((list) => list.map((r) => (
        r.workSpecsId === row.workSpecsId ? { ...r, dwrImage: result.fileName || file.name } : r
      )));
      showToast('WCR image uploaded.', 'success');
    } catch (err) {
      const msg = err?.response?.data || 'Failed to upload WCR image.';
      showToast(typeof msg === 'string' ? msg : 'Failed to upload WCR image.', 'error');
    } finally {
      setUploadingId(null);
    }
  }

  return (
    <Modal
      open
      size="lg"
      title="Enter WCR"
      onClose={saving ? undefined : onClose}
      footer={(
        <>
          <Button variant="default" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>{saving ? 'Saving…' : 'Save Changes'}</Button>
        </>
      )}
    >
      <div className="modal-subhead">
        {[work.workCategoryDesc || work.workCategory, work.workTypeDesc || work.workType].filter(Boolean).join(' - ')}
      </div>
      <p className="text-muted" style={{ marginTop: 4 }}>
        Enter one well per row. <span className="mandatory">*</span> required fields.
      </p>

      {rows.length > 1 && (
        <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, margin: '4px 0 10px' }}>
          <input type="checkbox" checked={dwrNumShared} onChange={handleCopyToggle} />
          Copy WCR Number for all Well Works from Row 1.
        </label>
      )}

      <table className="modal-spec-table">
        <thead>
          <tr>
            <th>Row #</th>
            {isDestruction && <th>Construction Permit #</th>}
            {isDestruction && <th>Construction WCR #</th>}
            <th>State Well #<span className="mandatory">*</span></th>
            <th>WCR #<span className="mandatory">*</span></th>
            <th>Owner Well Id</th>
            <th>WCR Image</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, idx) => (
            <tr key={row.workSpecsId}>
              <td>{idx + 1}</td>
              {isDestruction && (
                <td><input className="form-control" maxLength={10} aria-label={`Construction permit ${idx + 1}`} value={row.permitNum} onChange={setRow(idx, 'permitNum')} /></td>
              )}
              {isDestruction && (
                <td><input className="form-control" maxLength={10} aria-label={`Construction WCR ${idx + 1}`} value={row.dwrNum} onChange={setRow(idx, 'dwrNum')} /></td>
              )}
              <td><input className="form-control" maxLength={10} aria-label={`State well number ${idx + 1}`} value={row.stateWellId} onChange={setRow(idx, 'stateWellId')} /></td>
              <td><input className="form-control" maxLength={50} aria-label={`WCR number ${idx + 1}`} value={row.complWellDwrNum} onChange={setRow(idx, 'complWellDwrNum')} /></td>
              <td>{row.ownerWellNum || '—'}</td>
              <td>
                <input
                  ref={(el) => { fileInputs.current[row.workSpecsId] = el; }}
                  type="file"
                  accept=".pdf,.png,.jpg,.jpeg,.tif,.tiff"
                  style={{ display: 'none' }}
                  onChange={(e) => handleImagePick(row, e)}
                />
                <Button
                  variant="default"
                  onClick={() => {
                    if (!row.savedStateWellId) {
                      showToast('Save the State Well # and WCR # first, then upload the image.', 'error');
                      return;
                    }
                    fileInputs.current[row.workSpecsId]?.click();
                  }}
                  disabled={uploadingId === row.workSpecsId || (dwrNumShared && idx > 0)}
                >
                  {uploadingId === row.workSpecsId ? 'Uploading…' : 'Upload WCR Image'}
                </Button>
                {row.dwrImage && (
                  <div className="text-muted" style={{ marginTop: 4, wordBreak: 'break-all' }}>{fileName(row.dwrImage)}</div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Modal>
  );
}

export default EditWcrModal;
