import { useEffect, useMemo, useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import InputField from '../../../../components/UI/InputField';
import SelectField from '../../../../components/UI/SelectField';
import MapPicker from '../../../../components/MapPicker/MapPicker';
import config from '../../../../config';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { addWork } from '../../../../api/applicationApi';
import { getWorkCategories, getWorkTypes, getWellUseTypes, getDrillMethods } from '../../../../api/referenceApi';
import { isBoreholeCategory } from '../../../../constants/workCategories';

function money(n) {
  return `$${Number(n || 0).toFixed(2)}`;
}

function toNum(value) {
  if (value === '' || value === null || value === undefined) return null;
  const n = Number(value);
  return Number.isNaN(n) ? null : n;
}

// A required spec value is "filled" when it is a non-empty string / number.
function isFilled(value) {
  return value !== '' && value !== null && value !== undefined;
}

// Staff "Add Work": reproduces the legacy upd_work_type.jsp → upd_work_info.jsp flow in one modal.
// A work category dropdown drives a work-type dropdown; once a type is chosen the fee line is shown
// and the driller / drilling-method / well-use fields are collected. The chosen type seeds the new
// work's fee columns server-side; the row is created at status PENDC.
function AddWorkModal({ application, onClose, onSaved }) {
  const showToast = useToast();
  const hasMapKey = Boolean(config.mapApiKey);
  const [saving, setSaving] = useState(false);
  const [categories, setCategories] = useState([]);
  const [workTypes, setWorkTypes] = useState([]);
  const [wellUseTypes, setWellUseTypes] = useState([]);
  const [drillMethods, setDrillMethods] = useState([]);
  const [form, setForm] = useState({
    workCategory: '',
    workType: '',
    wellUseType: '',
    drillerName: '',
    drillerLicenseNum: '',
    drillMethodType: '',
    drillMethodOtherDesc: '',
  });
  // A new work is created with at least one well spec; the reviewer captures each well's specifications
  // here (parity with the applicant flow, where work info is followed by well count + specs).
  const newSpec = () => ({
    ownerWellNum: '',
    maxDepthFt: '',
    holeDiamIn: '',
    casingDiamIn: '',
    sealDepthFt: '',
    drillCount: '',
    latitude: application?.siteLat || '',
    longitude: application?.siteLong || '',
  });
  const [specs, setSpecs] = useState(() => [newSpec()]);
  const [attempted, setAttempted] = useState(false);

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  const setSpec = (idx, key) => (e) =>
    setSpecs((rows) => rows.map((r, i) => (i === idx ? { ...r, [key]: e.target.value } : r)));
  const setWellLocation = (idx) => (latitude, longitude) =>
    setSpecs((rows) => rows.map((r, i) => (i === idx ? { ...r, latitude, longitude } : r)));
  const addSpec = () => setSpecs((rows) => [...rows, newSpec()]);
  const removeSpec = (idx) => setSpecs((rows) => rows.filter((_, i) => i !== idx));

  useEffect(() => {
    let active = true;
    getWorkCategories().then((c) => { if (active) setCategories(c || []); }).catch(() => {});
    getDrillMethods().then((d) => { if (active) setDrillMethods(d || []); }).catch(() => {});
    return () => { active = false; };
  }, []);

  // Reload the work-type list whenever the category changes (and reset the downstream selections).
  useEffect(() => {
    let active = true;
    setForm((f) => ({ ...f, workType: '', wellUseType: '' }));
    setWorkTypes([]);
    setWellUseTypes([]);
    if (!form.workCategory) return undefined;
    getWorkTypes(form.workCategory).then((t) => { if (active) setWorkTypes(t || []); }).catch(() => {});
    return () => { active = false; };
  }, [form.workCategory]);

  // Well-use options depend on both the category and the chosen type (legacy work_use_types keys).
  useEffect(() => {
    let active = true;
    setForm((f) => ({ ...f, wellUseType: '' }));
    setWellUseTypes([]);
    if (!form.workCategory || !form.workType) return undefined;
    getWellUseTypes(form.workCategory, form.workType).then((u) => { if (active) setWellUseTypes(u || []); }).catch(() => {});
    return () => { active = false; };
  }, [form.workCategory, form.workType]);

  const selectedType = useMemo(
    () => workTypes.find((t) => t.code === form.workType) || null,
    [workTypes, form.workType],
  );
  const selectedCategory = useMemo(
    () => categories.find((c) => c.code === form.workCategory) || null,
    [categories, form.workCategory],
  );

  const feeLine = selectedType
    ? `${[selectedCategory?.label, selectedType.label].filter(Boolean).join(' - ')} : ${money(selectedType.feeRate)} per ${selectedType.feeUnit || 'well'}`
    : null;

  // Borehole/investigation categories (inv, invprb) collect a single set of borehole specifications
  // (number of boreholes, hole diameter, max depth) instead of a per-well specifications table —
  // parity with the applicant flow and the legacy app_work_info.jsp borehole branch.
  const isBorehole = isBoreholeCategory(form.workCategory);
  const boreSpec = specs[0] || {};
  const specsInvalid = isBorehole
    ? (!isFilled(boreSpec.drillCount) || !isFilled(boreSpec.holeDiamIn) || !isFilled(boreSpec.maxDepthFt))
    : (specs.length === 0 || specs.some((s) => (
      !isFilled(s.ownerWellNum) || !isFilled(s.holeDiamIn) || !isFilled(s.casingDiamIn)
        || !isFilled(s.sealDepthFt) || !isFilled(s.maxDepthFt)
    )));

  // Mirror the legacy required-field rules: driller name, license and drilling method are always
  // required; well use is required only when the work type actually offers well-use options.
  const wellUseRequired = wellUseTypes.length > 0;
  const canSave = Boolean(
    form.workCategory
    && form.workType
    && form.drillerName.trim()
    && form.drillerLicenseNum.trim()
    && form.drillMethodType
    && (!wellUseRequired || form.wellUseType),
  );

  async function handleSave() {
    if (!canSave) return;
    setAttempted(true);
    if (specsInvalid) {
      showToast('Please complete the required specification fields.', 'error');
      return;
    }
    const payloadSpecs = isBorehole
      ? [{
        ownerWellNum: null,
        drillCount: toNum(boreSpec.drillCount),
        holeDiamIn: toNum(boreSpec.holeDiamIn),
        casingDiamIn: null,
        sealDepthFt: null,
        maxDepthFt: toNum(boreSpec.maxDepthFt),
        latitude: null,
        longitude: null,
      }]
      : specs.map((s) => ({
        ownerWellNum: s.ownerWellNum || null,
        drillCount: toNum(s.drillCount),
        holeDiamIn: toNum(s.holeDiamIn),
        casingDiamIn: toNum(s.casingDiamIn),
        sealDepthFt: toNum(s.sealDepthFt),
        maxDepthFt: toNum(s.maxDepthFt),
        latitude: s.latitude ? String(s.latitude).trim() : null,
        longitude: s.longitude ? String(s.longitude).trim() : null,
      }));
    setSaving(true);
    try {
      const updated = await addWork(application.appId, {
        workCategory: form.workCategory,
        workType: form.workType,
        wellUseType: form.wellUseType || null,
        drillerName: form.drillerName,
        drillerLicenseNum: form.drillerLicenseNum,
        drillMethodType: form.drillMethodType,
        drillMethodOtherDesc: form.drillMethodOtherDesc || null,
        specs: payloadSpecs,
      });
      onSaved(updated);
      showToast('Work added.', 'success');
      onClose();
    } catch (err) {
      const msg = err?.response?.data?.detail || err?.response?.data;
      showToast(typeof msg === 'string' && msg.trim() ? msg : 'Failed to add work.', 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      size="xl"
      title="Add Work"
      onClose={saving ? undefined : onClose}
      footer={(
        <>
          <Button variant="default" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button variant="primary" onClick={handleSave} disabled={saving || !canSave}>{saving ? 'Adding…' : 'Add Work'}</Button>
        </>
      )}
    >
      <div className="modal-form-grid">
        <div className="modal-subhead">Work Category / Type</div>
        <SelectField
          label="Work Category"
          id="addWorkCategory"
          required
          placeholder="— Select —"
          options={categories}
          value={form.workCategory}
          onChange={set('workCategory')}
        />
        <SelectField
          label="Work Type"
          id="addWorkType"
          required
          placeholder={form.workCategory ? '— Select —' : 'Select a category first'}
          options={workTypes}
          value={form.workType}
          onChange={set('workType')}
        />
        {feeLine && (
          <div className="modal-span-2 add-work-fee">{feeLine}</div>
        )}

        {wellUseTypes.length > 0 && (
          <SelectField
            label="Well Use"
            id="addWellUseType"
            required
            placeholder="— Select —"
            options={wellUseTypes}
            value={form.wellUseType}
            onChange={set('wellUseType')}
          />
        )}

        <div className="modal-subhead">Driller Information</div>
        <InputField label="Name" id="addDrillerName" required value={form.drillerName} onChange={set('drillerName')} />
        <InputField
          label="Driller License #"
          id="addDrillerLicenseNum"
          required
          value={form.drillerLicenseNum}
          onChange={set('drillerLicenseNum')}
          labelSuffix={form.drillerLicenseNum.trim() ? (
            <a
              className="add-work-verify-link"
              href={`https://cslb.ca.gov/OnlineServices/CheckLicenseII/LicenseDetail.aspx?LicNum=${encodeURIComponent(form.drillerLicenseNum.trim())}`}
              target="_blank"
              rel="noopener noreferrer"
            >
              Verify at State Board
            </a>
          ) : null}
        />
        <SelectField
          label="Drilling Method"
          id="addDrillMethodType"
          required
          placeholder="— Select —"
          options={drillMethods}
          value={form.drillMethodType}
          onChange={set('drillMethodType')}
        />
        <InputField label="If Other Method, identify" id="addDrillMethodOtherDesc" value={form.drillMethodOtherDesc} onChange={set('drillMethodOtherDesc')} />
      </div>

      <div className="modal-subhead" style={{ marginTop: 14 }}>
        {isBorehole ? 'Borehole Specifications' : 'Well Specifications'}
      </div>

      {isBorehole ? (
        <div className="modal-form-grid">
          <InputField
            label="Number of Boreholes"
            id="addBoreCount"
            required
            type="number"
            min="1"
            step="1"
            value={boreSpec.drillCount}
            onChange={setSpec(0, 'drillCount')}
            error={attempted && !isFilled(boreSpec.drillCount) ? 'Required' : undefined}
          />
          <InputField
            label="Hole Diameter (in)"
            id="addBoreHoleDiam"
            required
            type="number"
            step="0.01"
            value={boreSpec.holeDiamIn}
            onChange={setSpec(0, 'holeDiamIn')}
            error={attempted && !isFilled(boreSpec.holeDiamIn) ? 'Required' : undefined}
          />
          <InputField
            label="Maximum Depth (ft)"
            id="addBoreMaxDepth"
            required
            type="number"
            step="0.01"
            value={boreSpec.maxDepthFt}
            onChange={setSpec(0, 'maxDepthFt')}
            error={attempted && !isFilled(boreSpec.maxDepthFt) ? 'Required' : undefined}
          />
        </div>
      ) : (
        <>
          {specs.length > 0 ? (
            <table className="modal-spec-table">
              <thead>
                <tr>
                  <th>Well #<span className="mandatory">*</span></th>
                  <th>Max depth (ft)<span className="mandatory">*</span></th>
                  <th>Hole diam (in)<span className="mandatory">*</span></th>
                  <th>Casing diam (in)<span className="mandatory">*</span></th>
                  <th>Seal depth (ft)<span className="mandatory">*</span></th>
                  <th>Latitude</th>
                  <th>Longitude</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {specs.map((spec, idx) => (
                  <tr key={`new-${idx}`}>
                    <td><input className={`form-control${!isFilled(spec.ownerWellNum) ? ' is-invalid' : ''}`} aria-label={`Well number ${idx + 1}`} value={spec.ownerWellNum} onChange={setSpec(idx, 'ownerWellNum')} /></td>
                    <td><input className={`form-control${!isFilled(spec.maxDepthFt) ? ' is-invalid' : ''}`} type="number" step="0.01" aria-label={`Max depth ${idx + 1}`} value={spec.maxDepthFt} onChange={setSpec(idx, 'maxDepthFt')} /></td>
                    <td><input className={`form-control${!isFilled(spec.holeDiamIn) ? ' is-invalid' : ''}`} type="number" step="0.01" aria-label={`Hole diameter ${idx + 1}`} value={spec.holeDiamIn} onChange={setSpec(idx, 'holeDiamIn')} /></td>
                    <td><input className={`form-control${!isFilled(spec.casingDiamIn) ? ' is-invalid' : ''}`} type="number" step="0.01" aria-label={`Casing diameter ${idx + 1}`} value={spec.casingDiamIn} onChange={setSpec(idx, 'casingDiamIn')} /></td>
                    <td><input className={`form-control${!isFilled(spec.sealDepthFt) ? ' is-invalid' : ''}`} type="number" step="0.01" aria-label={`Seal depth ${idx + 1}`} value={spec.sealDepthFt} onChange={setSpec(idx, 'sealDepthFt')} /></td>
                    <td><input className="form-control" inputMode="decimal" aria-label={`Latitude ${idx + 1}`} value={spec.latitude} onChange={setSpec(idx, 'latitude')} /></td>
                    <td><input className="form-control" inputMode="decimal" aria-label={`Longitude ${idx + 1}`} value={spec.longitude} onChange={setSpec(idx, 'longitude')} /></td>
                    <td>{specs.length > 1 ? <Button variant="default" onClick={() => removeSpec(idx)}>Remove</Button> : null}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="text-muted" style={{ margin: '4px 0' }}>No wells yet. Use “Add Well” to enter well specifications.</p>
          )}
          <div style={{ marginTop: 8 }}>
            <Button variant="default" onClick={addSpec}>Add Well</Button>
          </div>

          {hasMapKey && specs.length > 0 && (
            <>
              <div className="modal-subhead" style={{ marginTop: 14 }}>Pin Wells on Map</div>
              <p className="text-muted" style={{ marginTop: 0 }}>
                Click the map or drag each marker to set the coordinates for that well.
              </p>
              {specs.map((spec, idx) => (
                <div className="work-map-well" key={`map-${idx}`}>
                  <div className="work-map-well__label">
                    Well {idx + 1}{spec.ownerWellNum ? ` — ${spec.ownerWellNum}` : ''}
                  </div>
                  <MapPicker
                    idPrefix={`add-work-well-${idx}`}
                    lat={spec.latitude}
                    lng={spec.longitude}
                    onChange={setWellLocation(idx)}
                    showInputs={false}
                    compact
                  />
                </div>
              ))}
            </>
          )}
        </>
      )}
    </Modal>
  );
}

export default AddWorkModal;
