import { useRef, useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { uploadGeolog } from '../../../../api/applicationApi';

// Mirrors the legacy proc_geolog_upload.jsp "Enter GeoLog" screen: staff attach a Geotechnical log
// (PDF < 100 MB) per well spec. The file name is recorded on APP_WORK_SPECS.geolog_file.
function fileName(path) {
  if (!path) return '';
  const parts = String(path).split(/[\\/]/);
  return parts[parts.length - 1] || String(path);
}

function EditGeoLogModal({ application, work, onClose, onSaved }) {
  const showToast = useToast();
  const [uploadingId, setUploadingId] = useState(null);
  const [touched, setTouched] = useState(false);
  const fileInputs = useRef({});
  const [rows, setRows] = useState(() => (work.specs || []).map((s) => ({
    workSpecsId: s.workSpecsId,
    ownerWellNum: s.ownerWellNum || '',
    geologFile: s.geologFile || '',
  })));

  async function handlePick(row, e) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    setUploadingId(row.workSpecsId);
    try {
      const result = await uploadGeolog(application.appId, work.workId, row.workSpecsId, file);
      setRows((list) => list.map((r) => (
        r.workSpecsId === row.workSpecsId ? { ...r, geologFile: result.fileName || file.name } : r
      )));
      setTouched(true);
      showToast('Geotechnical log uploaded.', 'success');
    } catch (err) {
      const msg = err?.response?.data || 'Failed to upload Geotechnical log.';
      showToast(typeof msg === 'string' ? msg : 'Failed to upload Geotechnical log.', 'error');
    } finally {
      setUploadingId(null);
    }
  }

  function handleClose() {
    // Refresh the parent so the newly stored file names are reflected in the detail view.
    if (touched && onSaved) onSaved(null);
    onClose();
  }

  return (
    <Modal
      open
      size="lg"
      title="Enter GeoLog"
      onClose={handleClose}
      footer={<Button variant="primary" onClick={handleClose}>Done</Button>}
    >
      <div className="modal-subhead">
        {[work.workCategoryDesc || work.workCategory, work.workTypeDesc || work.workType].filter(Boolean).join(' - ')}
      </div>
      <p className="text-muted" style={{ marginTop: 4 }}>
        Attach a Geotechnical log file (Adobe PDF, under 100 MB) for each well.
      </p>

      <table className="modal-spec-table">
        <thead>
          <tr>
            <th>Row #</th>
            <th>Owner Well Id</th>
            <th>Geotechnical Log File</th>
            <th>Upload</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, idx) => (
            <tr key={row.workSpecsId}>
              <td>{idx + 1}</td>
              <td>{row.ownerWellNum || '—'}</td>
              <td style={{ wordBreak: 'break-all' }}>
                {row.geologFile ? fileName(row.geologFile) : <span className="text-muted">Not yet received</span>}
              </td>
              <td>
                <input
                  ref={(el) => { fileInputs.current[row.workSpecsId] = el; }}
                  type="file"
                  accept=".pdf"
                  style={{ display: 'none' }}
                  onChange={(e) => handlePick(row, e)}
                />
                <Button
                  variant="default"
                  onClick={() => fileInputs.current[row.workSpecsId]?.click()}
                  disabled={uploadingId === row.workSpecsId}
                >
                  {uploadingId === row.workSpecsId ? 'Uploading…' : 'Upload GeoLog File'}
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Modal>
  );
}

export default EditGeoLogModal;
