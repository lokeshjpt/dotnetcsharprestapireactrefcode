import { useEffect, useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import InputField from '../../../../components/UI/InputField';
import SelectField from '../../../../components/UI/SelectField';
import MapPicker from '../../../../components/MapPicker/MapPicker';
import config from '../../../../config';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { updateWork } from '../../../../api/applicationApi';
import { getWellUseTypes, getDrillMethods } from '../../../../api/referenceApi';
import { isBoreholeCategory } from '../../../../constants/workCategories';

function toNum(value) {
  if (value === '' || value === null || value === undefined) return null;
  const n = Number(value);
  return Number.isNaN(n) ? null : n;
}

// A required value is "filled" when it is a non-empty string / number.
function isFilled(value) {
  return value !== '' && value !== null && value !== undefined;
}

function EditWorkModal({ application, work, onClose, onSaved }) {
  const showToast = useToast();
  const hasMapKey = Boolean(config.mapApiKey);
  const [saving, setSaving] = useState(false);
  const [wellUseTypes, setWellUseTypes] = useState([]);
  const [drillMethods, setDrillMethods] = useState([]);
  const [form, setForm] = useState(() => ({
    wellUseType: work.wellUseType || '',
    drillerName: work.drillerName || '',
    drillerLicenseNum: work.drillerLicenseNum || '',
    drillMethodType: work.drillMethodType || '',
    drillMethodOtherDesc: work.drillMethodOtherDesc || '',
    workFeeRate: work.workFeeRate ?? '',
    workFeeUnit: work.workFeeUnit || '',
    workSiteMax: work.workSiteMax ?? '',
  }));
  const [specs, setSpecs] = useState(() => (work.specs || []).map((s) => ({
    workSpecsId: s.workSpecsId,
    ownerWellNum: s.ownerWellNum || '',
    maxDepthFt: s.maxDepthFt ?? '',
    holeDiamIn: s.holeDiamIn ?? '',
    casingDiamIn: s.casingDiamIn ?? '',
    sealDepthFt: s.sealDepthFt ?? '',
    drillCount: s.drillCount ?? '',
    latitude: s.latitude || '',
    longitude: s.longitude || '',
  })));

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  const setSpec = (idx, key) => (e) =>
    setSpecs((rows) => rows.map((r, i) => (i === idx ? { ...r, [key]: e.target.value } : r)));
  // Setter used by the per-well MapPicker (writes both coordinates in one update).
  const setWellLocation = (idx) => (latitude, longitude) =>
    setSpecs((rows) => rows.map((r, i) => (i === idx ? { ...r, latitude, longitude } : r)));
  const addSpec = () => setSpecs((rows) => [...rows, {
    workSpecsId: 0,
    ownerWellNum: '',
    maxDepthFt: '',
    holeDiamIn: '',
    casingDiamIn: '',
    sealDepthFt: '',
    drillCount: '',
    // Seed a new well at the project site so its map marker starts on the parcel.
    latitude: application?.siteLat || '',
    longitude: application?.siteLong || '',
  }]);
  const removeSpec = (idx) => setSpecs((rows) => rows.filter((_, i) => i !== idx));
  const [attempted, setAttempted] = useState(false);

  // Borehole/investigation categories (inv, invprb) carry a single set of borehole specifications
  // (number of boreholes, hole diameter, max depth) instead of a per-well specifications table.
  const isBorehole = isBoreholeCategory(work.workCategory);
  const boreSpec = specs[0] || {};

  useEffect(() => {
    let active = true;
    getWellUseTypes(work.workCategory, work.workType)
      .then((r) => { if (active) setWellUseTypes(r || []); })
      .catch(() => {});
    getDrillMethods()
      .then((r) => { if (active) setDrillMethods(r || []); })
      .catch(() => {});
    return () => { active = false; };
  }, [work.workCategory, work.workType]);

  const wellUseRequired = wellUseTypes.length > 0;
  const headerInvalid = !form.drillerName.trim()
    || !form.drillerLicenseNum.trim()
    || !form.drillMethodType
    || (wellUseRequired && !form.wellUseType);
  const specsInvalid = isBorehole
    ? (!isFilled(boreSpec.drillCount) || !isFilled(boreSpec.holeDiamIn) || !isFilled(boreSpec.maxDepthFt))
    : (specs.length === 0 || specs.some((s) => (
      !isFilled(s.ownerWellNum) || !isFilled(s.holeDiamIn) || !isFilled(s.casingDiamIn)
        || !isFilled(s.sealDepthFt) || !isFilled(s.maxDepthFt)
    )));

  async function handleSave() {
    setAttempted(true);
    if (headerInvalid || specsInvalid) {
      showToast('Please complete the required fields.', 'error');
      return;
    }
    const payloadSpecs = isBorehole
      ? [{
        workSpecsId: boreSpec.workSpecsId,
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
        workSpecsId: s.workSpecsId,
        ownerWellNum: s.ownerWellNum,
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
      const payload = {
        wellUseType: form.wellUseType,
        drillerName: form.drillerName,
        drillerLicenseNum: form.drillerLicenseNum,
        drillMethodType: form.drillMethodType,
        drillMethodOtherDesc: form.drillMethodOtherDesc,
        workFeeRate: toNum(form.workFeeRate),
        workFeeUnit: form.workFeeUnit,
        workSiteMax: toNum(form.workSiteMax),
        specs: payloadSpecs,
      };
      const updated = await updateWork(application.appId, work.workId, payload);
      onSaved(updated);
      showToast('Work information updated.', 'success');
      onClose();
    } catch (err) {
      const msg = err?.response?.data?.detail || err?.response?.data;
      showToast(typeof msg === 'string' && msg.trim() ? msg : 'Failed to update work information.', 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      size="xl"
      title="Edit Work Requesting Permit"
      onClose={saving ? undefined : onClose}
      footer={(
        <>
          <Button variant="default" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
        </>
      )}
    >
      <div className="modal-form-grid">
        <div className="modal-subhead">Work Category / Type</div>
        <InputField
          label="Work Category"
          id="editWorkCategory"
          value={work.workCategoryDesc || work.workCategory || ''}
          disabled
          readOnly
        />
        <InputField
          label="Work Type"
          id="editWorkType"
          value={work.workTypeDesc || work.workType || ''}
          disabled
          readOnly
        />
        <div className="modal-span-2 add-work-fee">
          {(work.workCategoryDesc || work.workCategory)} - {(work.workTypeDesc || work.workType)}
          {form.workFeeRate !== '' && form.workFeeRate !== null
            ? ` : $${Number(form.workFeeRate).toFixed(2)} per ${form.workFeeUnit || 'site'}`
            : ''}
        </div>
        <SelectField
          label="Well Use"
          id="wellUseType"
          placeholder="— Select —"
          options={wellUseTypes}
          value={form.wellUseType}
          onChange={set('wellUseType')}
          required={wellUseRequired}
          error={attempted && wellUseRequired && !form.wellUseType ? 'Required' : undefined}
        />
        <div className="modal-subhead">Driller Information</div>
        <SelectField
          label="Drilling Method"
          id="drillMethodType"
          placeholder="— Select —"
          options={drillMethods}
          value={form.drillMethodType}
          onChange={set('drillMethodType')}
          required
          error={attempted && !form.drillMethodType ? 'Required' : undefined}
        />
        <InputField label="If Other Method, identify" id="drillMethodOtherDesc" value={form.drillMethodOtherDesc} onChange={set('drillMethodOtherDesc')} />
        <InputField
          label="Driller Name"
          id="drillerName"
          value={form.drillerName}
          onChange={set('drillerName')}
          required
          error={attempted && !form.drillerName.trim() ? 'Required' : undefined}
        />
        <InputField
          label="Driller License #"
          id="drillerLicenseNum"
          value={form.drillerLicenseNum}
          onChange={set('drillerLicenseNum')}
          required
          error={attempted && !form.drillerLicenseNum.trim() ? 'Required' : undefined}
          labelSuffix={form.drillerLicenseNum ? (
            <a
              className="add-work-verify-link"
              href={`https://www.cslb.ca.gov/OnlineServices/CheckLicenseII/LicenseDetail.aspx?LicNum=${encodeURIComponent(form.drillerLicenseNum)}`}
              target="_blank"
              rel="noopener noreferrer"
            >
              Verify at State Board
            </a>
          ) : null}
        />
      </div>

      <div className="modal-subhead" style={{ marginTop: 14 }}>
        {isBorehole ? 'Borehole Specifications' : 'Well Specifications'}
      </div>

      {isBorehole ? (
        <div className="modal-form-grid">
          <InputField
            label="Number of Boreholes"
            id="editBoreCount"
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
            id="editBoreHoleDiam"
            required
            type="number"
            step="0.01"
            value={boreSpec.holeDiamIn}
            onChange={setSpec(0, 'holeDiamIn')}
            error={attempted && !isFilled(boreSpec.holeDiamIn) ? 'Required' : undefined}
          />
          <InputField
            label="Maximum Depth (ft)"
            id="editBoreMaxDepth"
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
                  <tr key={spec.workSpecsId || `new-${idx}`}>
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
                <div className="work-map-well" key={spec.workSpecsId || `map-${idx}`}>
                  <div className="work-map-well__label">
                    Well {idx + 1}{spec.ownerWellNum ? ` — ${spec.ownerWellNum}` : ''}
                  </div>
                  <MapPicker
                    idPrefix={`work-well-${idx}`}
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

export default EditWorkModal;
