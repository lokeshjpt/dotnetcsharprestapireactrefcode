import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import Button from '../../components/UI/Button';
import InputField from '../../components/UI/InputField';
import Modal from '../../components/UI/Modal';
import Loader from '../../components/Loader/Loader';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import { findMaintEntity } from '../../constants/maintEntities';
import {
  listMaint,
  createMaint,
  updateMaint,
  deleteMaint,
  getInspectionSlots,
  updateInspectionSlots,
  maintErrorMessage,
} from '../../api/maintenanceApi';
import './Maintenance.css';

// --- date helpers (unavailable-days uses MM/DD/YYYY on the wire, YYYY-MM-DD in the input) ---
function mmddyyyyToIso(value) {
  if (!value) return '';
  const m = String(value).match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
  if (!m) return '';
  const [, mo, da, yr] = m;
  return `${yr}-${mo.padStart(2, '0')}-${da.padStart(2, '0')}`;
}

function isoToMmddyyyy(value) {
  if (!value) return '';
  const m = String(value).match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!m) return value;
  const [, yr, mo, da] = m;
  return `${mo}/${da}/${yr}`;
}

function formatCell(row, column) {
  const raw = row[column.field];
  switch (column.type) {
    case 'active':
      return String(raw || '').toUpperCase() === 'Y' ? 'Active' : 'Inactive';
    case 'yn':
      return String(raw || '').toUpperCase() === 'Y' ? 'Yes' : 'No';
    case 'money':
      return raw == null || raw === '' ? '' : `$${Number(raw).toFixed(2)}`;
    case 'number':
      return raw == null ? '' : String(raw);
    case 'datetime': {
      if (!raw) return '';
      const d = new Date(raw);
      return Number.isNaN(d.getTime()) ? '' : d.toLocaleString();
    }
    default:
      return raw == null ? '' : String(raw);
  }
}

function buildInitialForm(entity, row) {
  const values = {};
  entity.fields.forEach((field) => {
    if (row) {
      const current = row[field.name];
      if (field.type === 'date') {
        values[field.name] = mmddyyyyToIso(current);
      } else if (field.type === 'flag') {
        values[field.name] = String(current || field.default || 'N').toUpperCase() === 'Y' ? 'Y' : 'N';
      } else {
        values[field.name] = current == null ? '' : String(current);
      }
    } else if (field.type === 'flag') {
      values[field.name] = field.default || 'N';
    } else {
      values[field.name] = '';
    }
  });
  return values;
}

function buildRequestBody(entity, form) {
  const body = {};
  entity.fields.forEach((field) => {
    const raw = form[field.name];
    if (field.type === 'number') {
      body[field.name] = raw === '' || raw == null ? null : Number(raw);
    } else if (field.type === 'flag') {
      body[field.name] = String(raw || 'N').toUpperCase() === 'Y' ? 'Y' : 'N';
    } else if (field.type === 'date') {
      body[field.name] = isoToMmddyyyy(raw);
    } else {
      body[field.name] = raw == null ? '' : String(raw).trim();
    }
  });
  return body;
}

// -------------------------------------------------------------- Singleton screen (max slots/day)
function InspectionSlotsPanel({ entity }) {
  const showToast = useToast();
  const [value, setValue] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const data = await getInspectionSlots();
        if (active) setValue(data?.maxSlotsPerDay == null ? '' : String(data.maxSlotsPerDay));
      } catch (err) {
        if (active) setError('Unable to load the current setting.');
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => { active = false; };
  }, []);

  const onSave = async () => {
    setSaving(true);
    try {
      await updateInspectionSlots(value === '' ? null : Number(value));
      showToast('Max slots per day updated.', 'success');
    } catch (err) {
      showToast(maintErrorMessage(err, 'Update failed.'), 'error');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel">
        <div className="maint-header">
          <div>
            <h1 className="page-title">{entity.title}</h1>
            <p className="page-subtitle">{entity.subtitle}</p>
          </div>
          <Link className="btn btn-link" to="/maintenance">&larr; Back to menu</Link>
        </div>

        {error && <div className="alert alert-error">{error}</div>}
        {loading ? (
          <p>Loading…</p>
        ) : (
          <div className="maint-slot-form">
            <InputField
              label="Max Slots Per Day"
              id="maxSlotsPerDay"
              type="number"
              min="1"
              required
              value={value}
              onChange={(e) => setValue(e.target.value)}
            />
            <Button variant="primary" onClick={onSave} disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}

// -------------------------------------------------------------- Generic list + CRUD screen
function MaintenanceEntity() {
  const { entityKey } = useParams();
  const entity = findMaintEntity(entityKey);
  const showToast = useToast();

  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [modalOpen, setModalOpen] = useState(false);
  const [editRow, setEditRow] = useState(null); // null => add mode
  const [form, setForm] = useState({});
  const [saving, setSaving] = useState(false);

  const [deleteRow, setDeleteRow] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const canAdd = entity && !entity.singleton;
  const canEdit = entity && !entity.singleton && !entity.noEdit;
  const canDelete = entity && !entity.singleton && !entity.noDelete && !!entity.delete;
  const isEditMode = editRow != null;

  const load = useCallback(async () => {
    if (!entity || entity.singleton) return;
    setLoading(true);
    setError('');
    try {
      const data = await listMaint(entity.endpoint);
      setRows(Array.isArray(data) ? data : []);
    } catch (err) {
      setError('Unable to load records.');
    } finally {
      setLoading(false);
    }
  }, [entity]);

  useEffect(() => {
    load();
  }, [load]);

  const openAdd = () => {
    setEditRow(null);
    setForm(buildInitialForm(entity, null));
    setModalOpen(true);
  };

  const openEdit = (row) => {
    setEditRow(row);
    setForm(buildInitialForm(entity, row));
    setModalOpen(true);
  };

  const closeModal = () => {
    if (saving) return;
    setModalOpen(false);
    setEditRow(null);
  };

  const onFieldChange = (name, value) => {
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const validateForm = () => {
    const missing = entity.fields.filter((f) => {
      if (f.type === 'flag') return false;
      if (isEditMode && f.hideOnAdd) return false;
      if (!isEditMode && f.hideOnAdd) return false; // auto-generated key, not required on add
      if (!f.required) return false;
      const v = form[f.name];
      return v == null || String(v).trim() === '';
    });
    return missing.length === 0;
  };

  const onSubmit = async () => {
    if (!validateForm()) {
      showToast('Please complete all required fields.', 'error');
      return;
    }
    setSaving(true);
    try {
      const body = buildRequestBody(entity, form);
      if (isEditMode) {
        await updateMaint(entity.endpoint, body);
        showToast(`${entity.title} updated.`, 'success');
      } else {
        await createMaint(entity.endpoint, body);
        showToast(`${entity.title} added.`, 'success');
      }
      setModalOpen(false);
      setEditRow(null);
      await load();
    } catch (err) {
      showToast(maintErrorMessage(err), 'error');
    } finally {
      setSaving(false);
    }
  };

  const onConfirmDelete = async () => {
    if (!deleteRow) return;
    setDeleting(true);
    try {
      const cfg = entity.delete;
      if (cfg.type === 'path') {
        await deleteMaint(entity.endpoint, { pathKey: deleteRow[cfg.field] });
      } else {
        const query = {};
        Object.entries(cfg.params).forEach(([param, field]) => {
          query[param] = deleteRow[field];
        });
        await deleteMaint(entity.endpoint, { query });
      }
      showToast(`${entity.title} deleted.`, 'success');
      setDeleteRow(null);
      await load();
    } catch (err) {
      showToast(maintErrorMessage(err), 'error');
    } finally {
      setDeleting(false);
    }
  };

  const visibleFields = useMemo(() => {
    if (!entity || entity.singleton) return [];
    return entity.fields.filter((f) => !(f.hideOnAdd && !isEditMode));
  }, [entity, isEditMode]);

  if (!entity) {
    return (
      <div className="page-shell">
        <div className="panel">
          <h1 className="page-title">Not found</h1>
          <p className="page-subtitle">Unknown maintenance screen.</p>
          <Link className="btn btn-link" to="/maintenance">&larr; Back to menu</Link>
        </div>
      </div>
    );
  }

  if (entity.singleton) {
    return <InspectionSlotsPanel entity={entity} />;
  }

  return (
    <div className="page-shell">
      {loading && <Loader />}
      <div className="panel">
        <div className="maint-header">
          <div>
            <h1 className="page-title">{entity.title}</h1>
            <p className="page-subtitle">{entity.subtitle}</p>
            <Link className="page-help-link" to="/help#maintenance">
              <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> About code maintenance — help
            </Link>
          </div>
          <div className="maint-header__actions">
            <Link className="btn btn-link" to="/maintenance">&larr; Back to menu</Link>
            {canAdd && <Button variant="primary" onClick={openAdd}>Add {entity.title.replace(/s$/, '')}</Button>}
          </div>
        </div>

        {error && <div className="alert alert-error">{error}</div>}

        {loading ? (
          <p>Loading…</p>
        ) : (
          <div className="table-scroll" role="region" aria-label="Records table" tabIndex={0}>
            <table className="data-table">
              <thead>
                <tr>
                  {entity.columns.map((col) => (
                    <th key={col.field} className={col.type === 'money' || col.type === 'number' ? 'report-num' : undefined}>
                      {col.label}
                    </th>
                  ))}
                  {(canEdit || canDelete) && <th className="maint-actions-col">Actions</th>}
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={entity.columns.length + (canEdit || canDelete ? 1 : 0)}>No records found.</td>
                  </tr>
                )}
                {rows.map((row) => (
                  <tr key={entity.rowKey(row)}>
                    {entity.columns.map((col) => (
                      <td key={col.field} className={col.type === 'money' || col.type === 'number' ? 'report-num' : undefined}>
                        {formatCell(row, col)}
                      </td>
                    ))}
                    {(canEdit || canDelete) && (
                      <td className="maint-actions-col">
                        {canEdit && <button type="button" className="btn btn-link maint-action" onClick={() => openEdit(row)}>Edit</button>}
                        {canDelete && <button type="button" className="btn btn-link maint-action maint-action--danger" onClick={() => setDeleteRow(row)}>Delete</button>}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <Modal
        open={modalOpen}
        title={`${isEditMode ? 'Edit' : 'Add'} ${entity.title.replace(/s$/, '')}`}
        onClose={closeModal}
        footer={(
          <>
            <Button variant="default" onClick={closeModal} disabled={saving}>Cancel</Button>
            <Button variant="primary" onClick={onSubmit} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
          </>
        )}
      >
        <div className="maint-form">
          {visibleFields.map((field) => {
            const disabled = isEditMode && (field.keyField || field.readOnly);
            if (field.type === 'flag') {
              return (
                <div className="maint-form__check" key={field.name}>
                  <label htmlFor={`fld-${field.name}`}>
                    <input
                      id={`fld-${field.name}`}
                      type="checkbox"
                      checked={String(form[field.name] || 'N').toUpperCase() === 'Y'}
                      onChange={(e) => onFieldChange(field.name, e.target.checked ? 'Y' : 'N')}
                    />
                    {' '}{field.label}
                  </label>
                </div>
              );
            }
            if (field.type === 'textarea') {
              return (
                <div className="form-row" key={field.name}>
                  <label className="form-label" htmlFor={`fld-${field.name}`}>
                    {field.label}
                    {field.required && <span className="mandatory">*</span>}
                  </label>
                  <div className="form-col">
                    <textarea
                      id={`fld-${field.name}`}
                      className="form-control"
                      rows={3}
                      value={form[field.name] || ''}
                      disabled={disabled}
                      onChange={(e) => onFieldChange(field.name, e.target.value)}
                    />
                  </div>
                </div>
              );
            }
            return (
              <InputField
                key={field.name}
                id={`fld-${field.name}`}
                label={field.label}
                required={field.required}
                type={field.type === 'number' ? 'number' : field.type === 'date' ? 'date' : 'text'}
                step={field.step}
                maxLength={field.maxLength}
                disabled={disabled}
                value={form[field.name] || ''}
                onChange={(e) => onFieldChange(field.name, e.target.value)}
              />
            );
          })}
        </div>
      </Modal>

      <Modal
        open={!!deleteRow}
        title={`Delete ${entity.title.replace(/s$/, '')}`}
        onClose={() => !deleting && setDeleteRow(null)}
        size="sm"
        footer={(
          <>
            <Button variant="default" onClick={() => setDeleteRow(null)} disabled={deleting}>Cancel</Button>
            <Button variant="danger" onClick={onConfirmDelete} disabled={deleting}>{deleting ? 'Deleting…' : 'Delete'}</Button>
          </>
        )}
      >
        <p>Are you sure you want to delete this record? This action cannot be undone.</p>
      </Modal>
    </div>
  );
}

export default MaintenanceEntity;
