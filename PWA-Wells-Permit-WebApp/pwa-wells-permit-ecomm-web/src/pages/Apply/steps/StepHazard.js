import FormSection from '../../../components/FormSection/FormSection';
import InputField from '../../../components/UI/InputField';
import PhoneField from '../../../components/UI/PhoneField';
import Button from '../../../components/UI/Button';

const HOURS = ['00', '01', '02', '03', '04', '05', '06', '07', '08', '09', '10', '11', '12'];
const MINUTES = Array.from({ length: 60 }, (_, i) => String(i).padStart(2, '0'));

function StepHazard({
  formData,
  updateField,
  errors = {},
  updateHazContamOther,
  addHazContamOther,
  removeHazContamOther,
  updateHazSubstance,
  addHazSubstance,
  removeHazSubstance,
}) {
  const change = (name) => (e) => updateField(name, e.target.value);
  const toggle = (name) => (e) => updateField(name, e.target.checked);
  const others = formData.hazContamOthers || [];
  const substances = formData.hazSubstances || [];

  // Legacy equipment items store a single flag: 'R' (required) or 'A' (available/on-site).
  const EquipRow = ({ name, label, descName }) => {
    const flag = formData[name] || '';
    const setFlag = (value) => () => updateField(name, flag === value ? '' : value);
    return (
      <fieldset className="equip-row">
        <legend className="form-label equip-row__legend">{label}</legend>
        <div className="equip-row__options">
          <label className="radio-label">
            <input type="checkbox" checked={flag === 'R'} onChange={setFlag('R')} aria-label={`${label} required`} /> Required
          </label>
          <label className="radio-label">
            <input type="checkbox" checked={flag === 'A'} onChange={setFlag('A')} aria-label={`${label} available on site`} /> Available
          </label>
        </div>
        {descName && (
          <input
            className="form-control equip-row__desc"
            maxLength={150}
            placeholder="Type / description"
            aria-label={`${label} description`}
            value={formData[descName] || ''}
            onChange={change(descName)}
          />
        )}
      </fieldset>
    );
  };

  return (
    <>
      <FormSection title="Site Consultant" borderColor="m-blue" columns={2}>
        <InputField id="hazConsultantFirstName" label="First Name" required maxLength={50} value={formData.hazConsultantFirstName} onChange={change('hazConsultantFirstName')} error={errors.hazConsultantFirstName} />
        <InputField id="hazConsultantLastName" label="Last Name" required maxLength={50} value={formData.hazConsultantLastName} onChange={change('hazConsultantLastName')} error={errors.hazConsultantLastName} />
        <PhoneField label="Phone" prefix="hazConsultantPhone" withExt formData={formData} updateField={updateField} errors={errors} />
        <PhoneField label="Cell Phone" prefix="hazConsultantCell" formData={formData} updateField={updateField} errors={errors} />
      </FormSection>

      <FormSection title="Site Safety Officer" borderColor="orange" columns={2}>
        <InputField id="hazSafetyFirstName" label="First Name" required maxLength={50} value={formData.hazSafetyFirstName} onChange={change('hazSafetyFirstName')} error={errors.hazSafetyFirstName} />
        <InputField id="hazSafetyLastName" label="Last Name" required maxLength={50} value={formData.hazSafetyLastName} onChange={change('hazSafetyLastName')} error={errors.hazSafetyLastName} />
        <PhoneField label="Phone" prefix="hazSafetyPhone" withExt formData={formData} updateField={updateField} errors={errors} />
        <PhoneField label="Cell Phone" prefix="hazSafetyCell" formData={formData} updateField={updateField} errors={errors} />
      </FormSection>

      <FormSection title="Site Safety Details" borderColor="l-blue" columns={2}>
        <InputField id="hazFacilityType" label="Type of Facility" maxLength={100} value={formData.hazFacilityType} onChange={change('hazFacilityType')} error={errors.hazFacilityType} />
        <InputField id="hazMeetingDate" label="Site Safety Meeting Date" type="date" value={formData.hazMeetingDate} onChange={change('hazMeetingDate')} error={errors.hazMeetingDate} />
        <div className="form-row">
          <label className="form-label" htmlFor="hazMeetingHour">Site Safety Meeting Time</label>
          <div className="form-col">
            <div className="phone-inline">
              <select id="hazMeetingHour" className="form-control" style={{ width: 70 }} value={formData.hazMeetingHour} onChange={change('hazMeetingHour')}>
                {HOURS.map((h) => <option key={h} value={h}>{h}</option>)}
              </select>
              <span>:</span>
              <select className="form-control" style={{ width: 70 }} value={formData.hazMeetingMinute} onChange={change('hazMeetingMinute')} aria-label="Meeting time minutes">
                {MINUTES.map((m) => <option key={m} value={m}>{m}</option>)}
              </select>
              <select className="form-control" style={{ width: 70 }} value={formData.hazMeetingShift} onChange={change('hazMeetingShift')} aria-label="Meeting time AM or PM">
                <option value="AM">AM</option>
                <option value="PM">PM</option>
              </select>
            </div>
          </div>
        </div>
      </FormSection>

      <FormSection title="Level of Personal Protection (PPE)" borderColor="m-blue">
        <div className="form-row">
          <span className="form-label" id="ppe-label">
            Anticipated PPE Level(s)<span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span>
          </span>
          <div
            className="form-col"
            role="group"
            aria-labelledby="ppe-label"
            aria-describedby={errors.hazPpe ? 'hazPpe-error' : undefined}
          >
            <label className="radio-label">
              <input type="checkbox" checked={!!formData.hazPpeA} onChange={toggle('hazPpeA')} /> A (Highest)
            </label>
            <label className="radio-label">
              <input type="checkbox" checked={!!formData.hazPpeB} onChange={toggle('hazPpeB')} /> B (High)
            </label>
            <label className="radio-label">
              <input type="checkbox" checked={!!formData.hazPpeC} onChange={toggle('hazPpeC')} /> C (Medium)
            </label>
            <label className="radio-label">
              <input type="checkbox" checked={!!formData.hazPpeD} onChange={toggle('hazPpeD')} /> D (Low)
            </label>
            {errors.hazPpe && <span id="hazPpe-error" className="field-error" role="alert">{errors.hazPpe}</span>}
          </div>
        </div>
      </FormSection>

      <FormSection title="Safety Equipment" borderColor="orange">
        <p className="text-muted">Mark each item as Required or Available on site.</p>
        <div className="equip-grid">
          <EquipRow name="hazEquipHardHat" label="Hard Hat" />
          <EquipRow name="hazEquipSafetyShoes" label="Safety Shoes" />
          <EquipRow name="hazEquipOrangeVest" label="Orange Traffic Vest" />
          <EquipRow name="hazEquipHearing" label="Hearing Protection" />
          <EquipRow name="hazEquipEyewear" label="Safety Eye Wear" />
          <EquipRow name="hazEquipClothing" label="Clothing" descName="hazEquipClothingDesc" />
          <EquipRow name="hazEquipRespirator" label="Respirator" descName="hazEquipRespiratorDesc" />
          <EquipRow name="hazEquipCartridge" label="Cartridge" descName="hazEquipCartridgeDesc" />
          <EquipRow name="hazEquipGloves" label="Gloves" descName="hazEquipGlovesDesc" />
          <EquipRow name="hazEquipOther" label="Other" descName="hazEquipOtherDesc" />
        </div>
      </FormSection>

      <FormSection
        title="Anticipated Contaminants"
        borderColor="m-blue"
        headerRight={<Button variant="default" onClick={addHazContamOther}>+ Add Other</Button>}
      >
        <p className="text-muted">Select all contaminants anticipated on site.</p>
        <div className="form-row">
          <span className="form-label" id="contaminant-label">
            Contaminant<span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span>
          </span>
          <div
            className="form-col"
            role="group"
            aria-labelledby="contaminant-label"
            aria-describedby={errors.hazContam ? 'hazContam-error' : undefined}
          >
            <label className="radio-label">
              <input type="checkbox" checked={!!formData.hazContamGasoline} onChange={toggle('hazContamGasoline')} /> Gasoline
            </label>
            <label className="radio-label">
              <input type="checkbox" checked={!!formData.hazContamDiesel} onChange={toggle('hazContamDiesel')} /> Diesel
            </label>
            <label className="radio-label">
              <input type="checkbox" checked={!!formData.hazContamWasteOil} onChange={toggle('hazContamWasteOil')} /> Waste Oil
            </label>
            {errors.hazContam && <span id="hazContam-error" className="field-error" role="alert">{errors.hazContam}</span>}
          </div>
        </div>
        {others.map((value, i) => (
          <div className="repeatable-row" key={i}>
            <input
              aria-label={`Other contaminant ${i + 1}`}
              className={`form-control${errors[`hazContamOthers.${i}`] ? ' is-invalid' : ''}`}
              maxLength={100}
              placeholder="Other contaminant"
              value={value}
              onChange={(e) => updateHazContamOther(i, e.target.value)}
            />
            <Button variant="danger" onClick={() => removeHazContamOther(i)} disabled={others.length === 1}>Remove</Button>
            {errors[`hazContamOthers.${i}`] && <span className="field-error">{errors[`hazContamOthers.${i}`]}</span>}
          </div>
        ))}
      </FormSection>

      <FormSection
        title="Anticipated Hazardous Substances"
        borderColor="orange"
        headerRight={<Button variant="default" onClick={addHazSubstance}>+ Add Substance</Button>}
      >
        <p className="text-muted">Please include concentrations. Note if free product historically on site.</p>
        <div style={{ overflowX: 'auto' }}>
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Expected Concentrations (ppm)<br />(list medium - soil, water, air)</th>
                <th scope="col">PEL (ppm)</th>
                <th scope="col">Health Effects</th>
                <th scope="col"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {substances.map((row, i) => (
                <tr key={i}>
                  <td>
                    <input aria-label={`Expected concentration, row ${i + 1}`} className={`form-control${errors[`hazSubstances.${i}.concentration`] ? ' is-invalid' : ''}`} maxLength={100} value={row.concentration} onChange={(e) => updateHazSubstance(i, 'concentration', e.target.value)} />
                  </td>
                  <td>
                    <input aria-label={`PEL ppm, row ${i + 1}`} className={`form-control${errors[`hazSubstances.${i}.pelPpm`] ? ' is-invalid' : ''}`} maxLength={100} value={row.pelPpm} onChange={(e) => updateHazSubstance(i, 'pelPpm', e.target.value)} />
                  </td>
                  <td>
                    <input aria-label={`Health effects, row ${i + 1}`} className={`form-control${errors[`hazSubstances.${i}.healthEffects`] ? ' is-invalid' : ''}`} maxLength={100} value={row.healthEffects} onChange={(e) => updateHazSubstance(i, 'healthEffects', e.target.value)} />
                  </td>
                  <td>
                    <Button variant="danger" onClick={() => removeHazSubstance(i)} disabled={substances.length === 1}>Remove</Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </FormSection>

      <FormSection title="Information Provided By" borderColor="l-blue" columns={2}>
        <InputField id="hazProviderLastName" label="Last Name" required maxLength={50} value={formData.hazProviderLastName} onChange={change('hazProviderLastName')} error={errors.hazProviderLastName} />
        <InputField id="hazProviderFirstName" label="First Name" required maxLength={50} value={formData.hazProviderFirstName} onChange={change('hazProviderFirstName')} error={errors.hazProviderFirstName} />
        <InputField id="hazProviderTitle" label="Title" maxLength={50} value={formData.hazProviderTitle} onChange={change('hazProviderTitle')} error={errors.hazProviderTitle} />
        <PhoneField label="Phone" prefix="hazProviderPhone" withExt formData={formData} updateField={updateField} errors={errors} />
        <div className="form-row span-2">
          <span className="form-label">Acknowledgement<span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span></span>
          <div className="form-col">
            <label className="radio-label">
              <input
                type="checkbox"
                checked={!!formData.hazAcknowledgement}
                onChange={toggle('hazAcknowledgement')}
                aria-invalid={errors.hazAcknowledgement ? true : undefined}
                aria-describedby={errors.hazAcknowledgement ? 'hazAcknowledgement-error' : undefined}
              />{' '}
              I certify that the above hazardous-materials information is accurate to the best of my knowledge.
            </label>
            {errors.hazAcknowledgement && <span id="hazAcknowledgement-error" className="field-error" role="alert">{errors.hazAcknowledgement}</span>}
          </div>
        </div>
      </FormSection>
    </>
  );
}

export default StepHazard;
