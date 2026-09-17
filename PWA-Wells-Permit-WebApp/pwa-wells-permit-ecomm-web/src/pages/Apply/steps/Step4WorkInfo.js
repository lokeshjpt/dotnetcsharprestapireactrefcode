import { useEffect } from 'react';
import FormSection from '../../../components/FormSection/FormSection';
import InputField from '../../../components/UI/InputField';
import SelectField from '../../../components/UI/SelectField';
import Button from '../../../components/UI/Button';
import MapPicker from '../../../components/MapPicker/MapPicker';
import config from '../../../config';
import { DRILL_METHODS, WELL_USE_TYPES, isOtherDrillMethod, isBoreholeCategory } from '../referenceData';

function Step4WorkInfo({ formData, updateField, setFields, options, errors = {}, updateWellSpec, addWellSpec, removeWellSpec, seedWellSpecCoords }) {
  const change = (name) => (e) => updateField(name, e.target.value);
  // Selecting a coded dropdown also captures the option's human-readable label so the verify
  // step and confirmation show the description (e.g. "Domestic") rather than the raw code ("DOM").
  const selectWithDesc = (codeField, descField, opts) => (e) => {
    const code = e.target.value;
    const opt = (opts || []).find((o) => (o.code ?? o.value) === code);
    setFields({ [codeField]: code, [descField]: opt ? opt.label : '' });
  };
  const cat = (formData.workCat || '').toLowerCase();
  const isConstruction = cat.startsWith('con');
  const isInvestigation = isBoreholeCategory(formData.workCat);
  const isDestruction = cat.startsWith('des');
  const hasMapKey = Boolean(config.mapApiKey);

  // Preset each well's coordinates to the project site location before dimensions are entered.
  useEffect(() => {
    if (seedWellSpecCoords) seedWellSpecCoords(formData.siteLat, formData.siteLong);
  }, [formData.siteLat, formData.siteLong, seedWellSpecCoords]);

  const setWellLocation = (i) => (latitude, longitude) => {
    updateWellSpec(i, 'latitude', latitude);
    updateWellSpec(i, 'longitude', longitude);
  };

  const drillMethods = options.drillMethods && options.drillMethods.length ? options.drillMethods : DRILL_METHODS;
  const wellUseTypes = options.wellUseTypes && options.wellUseTypes.length ? options.wellUseTypes : WELL_USE_TYPES;
  const specs = formData.wellSpecs || [];
  const isBlank = (v) => v === undefined || v === null || String(v).trim() === '';

  return (
    <>
      <FormSection title="Work Information" borderColor="m-blue" columns={2}>
        {isConstruction && (
          <SelectField id="wUse" label="Well Use" required placeholder="Select..." options={wellUseTypes} value={formData.wUse} onChange={selectWithDesc('wUse', 'wUseDesc', wellUseTypes)} error={errors.wUse} />
        )}
        <InputField id="drillerName" label="Driller Name" required maxLength={100} value={formData.drillerName} onChange={change('drillerName')} error={errors.drillerName} />
        <InputField
          id="drillerLic"
          label="Driller License #"
          required
          maxLength={50}
          value={formData.drillerLic}
          onChange={change('drillerLic')}
          error={errors.drillerLic}
          labelSuffix={formData.drillerLic && formData.drillerLic.trim() ? (
            <a
              className="driller-verify-link"
              href={`https://www.cslb.ca.gov/OnlineServices/CheckLicenseII/LicenseDetail.aspx?LicNum=${encodeURIComponent(formData.drillerLic.trim())}`}
              target="_blank"
              rel="noopener noreferrer"
            >
              Verify at State Board
            </a>
          ) : null}
        />
        <SelectField id="dmeth" label="Drilling Method" required placeholder="Select..." options={drillMethods} value={formData.dmeth} onChange={selectWithDesc('dmeth', 'dmethName', drillMethods)} error={errors.dmeth} />
        <InputField
          id="dmethOth"
          label="If Other Method, identify"
          required={isOtherDrillMethod(formData.dmeth, formData.dmethName)}
          maxLength={50}
          value={formData.dmethOth}
          onChange={change('dmethOth')}
          error={errors.dmethOth}
        />
        {isInvestigation && (
          <>
            <InputField id="numbore" label="Number of Boreholes" required maxLength={5} value={formData.numbore} onChange={change('numbore')} error={errors.numbore} />
            <InputField id="holediam" label="Hole Diameter (in)" required maxLength={5} value={formData.holediam} onChange={change('holediam')} error={errors.holediam} />
            <InputField id="maxdepth" label="Maximum Depth (ft)" required maxLength={5} value={formData.maxdepth} onChange={change('maxdepth')} error={errors.maxdepth} />
          </>
        )}
      </FormSection>

      {!isInvestigation && (
        <>
      <FormSection
        title="Wells Specifications"
        borderColor="orange"
        headerRight={<Button variant="default" onClick={addWellSpec}>+ Add Well</Button>}
      >
        <p className="text-muted">Enter one row per well.</p>
        <div style={{ overflowX: 'auto' }}>
          <table className="data-table">
            <thead>
              <tr>
                {isDestruction && (
                  <>
                    <th scope="col">State Well #</th>
                    <th scope="col">Permit #</th>
                    <th scope="col">DWR #</th>
                  </>
                )}
                <th scope="col">Owner Well Id <span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span></th>
                <th scope="col">Hole Diam (in) <span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span></th>
                <th scope="col">Casing Diam (in) <span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span></th>
                <th scope="col">Seal Depth (ft) <span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span></th>
                <th scope="col">Max Depth (ft) <span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span></th>
                <th scope="col">Latitude</th>
                <th scope="col">Longitude</th>
                <th scope="col"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {specs.map((row, i) => (
                <tr key={i}>
                  {isDestruction && (
                    <>
                      <td><input className="form-control" aria-label={`Well ${i + 1} State Well #`} maxLength={10} value={row.swellid} onChange={(e) => updateWellSpec(i, 'swellid', e.target.value)} /></td>
                      <td><input className="form-control" aria-label={`Well ${i + 1} Permit #`} maxLength={10} value={row.permit} onChange={(e) => updateWellSpec(i, 'permit', e.target.value)} /></td>
                      <td><input className="form-control" aria-label={`Well ${i + 1} DWR #`} maxLength={10} value={row.dwr} onChange={(e) => updateWellSpec(i, 'dwr', e.target.value)} /></td>
                    </>
                  )}
                  <td>
                    <input className={`form-control${(isBlank(row.owellnum) || errors[`wellSpecs.${i}.owellnum`]) ? ' is-invalid' : ''}`} aria-label={`Well ${i + 1} Owner Well Id`} maxLength={10} value={row.owellnum} onChange={(e) => updateWellSpec(i, 'owellnum', e.target.value)} />
                  </td>
                  <td>
                    <input className={`form-control${(isBlank(row.holediam) || errors[`wellSpecs.${i}.holediam`]) ? ' is-invalid' : ''}`} aria-label={`Well ${i + 1} Hole Diameter (in)`} maxLength={8} value={row.holediam} onChange={(e) => updateWellSpec(i, 'holediam', e.target.value)} />
                  </td>
                  <td>
                    <input className={`form-control${(isBlank(row.casediam) || errors[`wellSpecs.${i}.casediam`]) ? ' is-invalid' : ''}`} aria-label={`Well ${i + 1} Casing Diameter (in)`} maxLength={8} value={row.casediam} onChange={(e) => updateWellSpec(i, 'casediam', e.target.value)} />
                  </td>
                  <td>
                    <input className={`form-control${(isBlank(row.sealdepth) || errors[`wellSpecs.${i}.sealdepth`]) ? ' is-invalid' : ''}`} aria-label={`Well ${i + 1} Seal Depth (ft)`} maxLength={8} value={row.sealdepth} onChange={(e) => updateWellSpec(i, 'sealdepth', e.target.value)} />
                  </td>
                  <td>
                    <input className={`form-control${(isBlank(row.maxdepth) || errors[`wellSpecs.${i}.maxdepth`]) ? ' is-invalid' : ''}`} aria-label={`Well ${i + 1} Max Depth (ft)`} maxLength={8} value={row.maxdepth} onChange={(e) => updateWellSpec(i, 'maxdepth', e.target.value)} />
                  </td>
                  <td><input className="form-control" aria-label={`Well ${i + 1} Latitude`} maxLength={20} value={row.latitude} onChange={(e) => updateWellSpec(i, 'latitude', e.target.value)} /></td>
                  <td><input className="form-control" aria-label={`Well ${i + 1} Longitude`} maxLength={20} value={row.longitude} onChange={(e) => updateWellSpec(i, 'longitude', e.target.value)} /></td>
                  <td>
                    <Button variant="danger" onClick={() => removeWellSpec(i)} disabled={specs.length === 1}>Remove</Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {errors.wellSpecs && <span className="field-error">{errors.wellSpecs}</span>}
      </FormSection>

      {hasMapKey && (
        <FormSection title="Pin Wells on Map" borderColor="l-blue">
          <p className="text-muted">Click the map or drag each marker to set the coordinates for that well.</p>
          {specs.map((row, i) => (
            <div className="form-row" key={i}>
              <label className="form-label">Well {i + 1}{row.owellnum ? ` — ${row.owellnum}` : ''}</label>
              <div className="form-col">
                <MapPicker
                  idPrefix={`well-${i}`}
                  lat={row.latitude}
                  lng={row.longitude}
                  onChange={setWellLocation(i)}
                  showInputs={false}
                  compact
                />
              </div>
            </div>
          ))}
        </FormSection>
      )}
        </>
      )}
    </>
  );
}

export default Step4WorkInfo;