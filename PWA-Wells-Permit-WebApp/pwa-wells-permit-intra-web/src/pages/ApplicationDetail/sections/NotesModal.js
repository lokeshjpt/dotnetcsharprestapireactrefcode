import { useEffect, useState } from 'react';
import Modal from '../../../components/UI/Modal';
import Button from '../../../components/UI/Button';
import { useToast } from '../../../components/UI/Toaster/ToastProvider';
import { getNotes, addNote } from '../../../api/applicationApi';

// Mirrors the legacy intra search_notes.jsp: staff view the free-text notes on an application
// (newest first) and append new ones. Persisted in INSPECTION_NOTES with the acting staff user.
const NOTE_MAX = 4000;

function formatDate(value) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString();
}

function NotesModal({ appId, onClose }) {
  const showToast = useToast();
  const [notes, setNotes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [text, setText] = useState('');
  const [saving, setSaving] = useState(false);

  async function refresh() {
    const list = await getNotes(appId);
    setNotes(list || []);
  }

  useEffect(() => {
    let active = true;
    getNotes(appId)
      .then((list) => { if (active) setNotes(list || []); })
      .catch(() => { if (active) setNotes([]); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [appId]);

  async function handleAdd() {
    const value = text.trim();
    if (!value) { showToast('Enter a note.', 'error'); return; }
    setSaving(true);
    try {
      await addNote(appId, value);
      setText('');
      await refresh();
      showToast('Note added.', 'success');
    } catch (err) {
      const msg = err?.response?.data;
      showToast(typeof msg === 'string' && msg ? msg : 'Failed to add note.', 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      size="lg"
      title="View/Add Notes"
      onClose={saving ? undefined : onClose}
      footer={<Button variant="default" onClick={onClose} disabled={saving}>Close</Button>}
    >
      <div className="modal-subhead">Add Note</div>
      <div className="form-row">
        <div className="form-col" style={{ width: '100%' }}>
          <textarea
            className="form-control"
            rows={5}
            maxLength={NOTE_MAX}
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Enter a note…"
          />
          <span className="text-muted">{text.length}/{NOTE_MAX}</span>
        </div>
      </div>
      <div style={{ margin: '8px 0 16px', textAlign: 'right' }}>
        <Button variant="primary" onClick={handleAdd} disabled={saving}>
          {saving ? 'Adding…' : 'Add'}
        </Button>
      </div>

      <div className="modal-subhead">Notes</div>
      {loading ? (
        <p className="text-muted">Loading notes…</p>
      ) : notes.length === 0 ? (
        <p className="text-muted">No notes have been added.</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Date Added</th>
              <th>Added By</th>
              <th>Note</th>
            </tr>
          </thead>
          <tbody>
            {notes.map((note) => (
              <tr key={note.seqNum}>
                <td style={{ whiteSpace: 'nowrap' }}>{formatDate(note.addTs)}</td>
                <td>{note.addBy || '—'}</td>
                <td style={{ whiteSpace: 'pre-wrap' }}>{note.notesText}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Modal>
  );
}

export default NotesModal;
