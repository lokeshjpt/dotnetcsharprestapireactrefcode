import { useEffect, useRef, useState } from 'react';
import Modal from '../../../components/UI/Modal';
import Button from '../../../components/UI/Button';
import InputField from '../../../components/UI/InputField';
import SelectField from '../../../components/UI/SelectField';
import { useToast } from '../../../components/UI/Toaster/ToastProvider';
import { getDocuments, uploadDocument, deleteDocument } from '../../../api/applicationApi';
import { listMaint } from '../../../api/maintenanceApi';

// Mirrors the legacy intra upload_documents.jsp: staff attach supporting documents (type + optional
// description) to an application and can remove them. Metadata is stored in APP_DOCUMENT_LINKS; the
// file itself is virus-scanned and delivered to the file share by the API.
const DOC_ACCEPT =
  'application/pdf,image/png,image/jpeg,image/tiff,.pdf,.png,.jpg,.jpeg,.tif,.tiff';

function baseName(path) {
  if (!path) return '';
  const parts = String(path).split(/[\\/]/);
  return parts[parts.length - 1] || String(path);
}

function formatDate(value) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString();
}

function DocumentsModal({ appId, onClose }) {
  const showToast = useToast();
  const fileInput = useRef(null);
  const [documents, setDocuments] = useState([]);
  const [docTypes, setDocTypes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [deletingSeq, setDeletingSeq] = useState(null);
  const [documentType, setDocumentType] = useState('');
  const [description, setDescription] = useState('');
  const [file, setFile] = useState(null);

  async function refresh() {
    const list = await getDocuments(appId);
    setDocuments(list || []);
  }

  useEffect(() => {
    let active = true;
    Promise.all([
      getDocuments(appId).catch(() => []),
      listMaint('document-types').catch(() => []),
    ])
      .then(([docs, types]) => {
        if (!active) return;
        setDocuments(docs || []);
        setDocTypes((types || []).map((t) => ({ code: t.documentType, label: t.documentDesc || t.documentType })));
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [appId]);

  async function handleUpload() {
    if (!documentType) { showToast('Select a document type.', 'error'); return; }
    if (!file) { showToast('Choose a file to upload.', 'error'); return; }
    setUploading(true);
    try {
      await uploadDocument(appId, file, documentType, description);
      setDocumentType('');
      setDescription('');
      setFile(null);
      if (fileInput.current) fileInput.current.value = '';
      await refresh();
      showToast('Document uploaded.', 'success');
    } catch (err) {
      const msg = err?.response?.data;
      showToast(typeof msg === 'string' && msg ? msg : 'Failed to upload document.', 'error');
    } finally {
      setUploading(false);
    }
  }

  async function handleDelete(seqNum) {
    if (!window.confirm('Remove this document?')) return;
    setDeletingSeq(seqNum);
    try {
      await deleteDocument(appId, seqNum);
      await refresh();
      showToast('Document removed.', 'success');
    } catch {
      showToast('Failed to remove document.', 'error');
    } finally {
      setDeletingSeq(null);
    }
  }

  return (
    <Modal
      open
      size="lg"
      title="Upload Documents"
      onClose={uploading ? undefined : onClose}
      footer={<Button variant="default" onClick={onClose} disabled={uploading}>Return to Detail</Button>}
    >
      <div className="modal-subhead">Add Document</div>
      <div className="modal-form-grid">
        <SelectField
          label="Document Type"
          id="documentType"
          placeholder="— Select —"
          required
          options={docTypes}
          value={documentType}
          onChange={(e) => setDocumentType(e.target.value)}
        />
        <InputField
          label="Description"
          id="documentDesc"
          maxLength={50}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
        <div className="form-row">
          <label className="form-label" htmlFor="documentFile">File</label>
          <div className="form-col">
            <input
              id="documentFile"
              ref={fileInput}
              type="file"
              className="form-control"
              accept={DOC_ACCEPT}
              onChange={(e) => setFile(e.target.files?.[0] || null)}
            />
          </div>
        </div>
      </div>
      <div style={{ margin: '8px 0 16px', textAlign: 'right' }}>
        <Button variant="primary" onClick={handleUpload} disabled={uploading}>
          {uploading ? 'Uploading…' : 'Upload'}
        </Button>
      </div>

      <div className="modal-subhead">Documents on File</div>
      {loading ? (
        <p className="text-muted">Loading documents…</p>
      ) : documents.length === 0 ? (
        <p className="text-muted">No documents have been uploaded.</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Type</th>
              <th>Description</th>
              <th>File</th>
              <th>Added By</th>
              <th>Date</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {documents.map((doc) => (
              <tr key={doc.seqNum}>
                <td>{doc.documentDesc || doc.documentType || '—'}</td>
                <td>{doc.otherTypeDesc || '—'}</td>
                <td style={{ wordBreak: 'break-all' }}>{baseName(doc.fileName) || '—'}</td>
                <td>{doc.addBy || '—'}</td>
                <td>{formatDate(doc.addTs)}</td>
                <td>
                  <Button
                    variant="danger"
                    onClick={() => handleDelete(doc.seqNum)}
                    disabled={deletingSeq === doc.seqNum}
                  >
                    {deletingSeq === doc.seqNum ? 'Removing…' : 'Delete'}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Modal>
  );
}

export default DocumentsModal;
