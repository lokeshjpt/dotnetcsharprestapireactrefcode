import { useState } from 'react';
import FormSection from '../../../components/FormSection/FormSection';
import InputField from '../../../components/UI/InputField';
import SelectField from '../../../components/UI/SelectField';
import PhoneField from '../../../components/UI/PhoneField';
import Button from '../../../components/UI/Button';
import AvailabilityCalendar from '../AvailabilityCalendar';
import { parseLocalDate, formatUsDate } from '../countyHolidays';

// Project Start/Completion date field. Dates can only be chosen through the Inspection
// Availability Calendar (which enforces the 10-90 day / weekend / holiday / max-reached rules),
// so the field itself is read-only and simply displays the selected date.
function DateWithCalendar({ id, label, value, error, onOpenCalendar }) {
  const parsed = parseLocalDate(value);
  const display = parsed ? formatUsDate(parsed) : '';
  return (
    <div className="form-row">
      <label className="form-label" htmlFor={id}>
        {label}
        <span className="mandatory" aria-hidden="true">*</span>
        <span className="sr-only"> (required)</span>
      </label>
      <div className="form-col">
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <input
            id={id}
            type="text"
            className={`form-control${error ? ' is-invalid' : ''}`}
            readOnly
            required
            aria-required
            aria-invalid={error ? true : undefined}
            value={display}
            placeholder="Select a date…"
            onClick={onOpenCalendar}
            onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onOpenCalendar(); } }}
            style={{ maxWidth: 190, cursor: 'pointer', backgroundColor: '#fff' }}
          />
          <Button variant="default" type="button" onClick={onOpenCalendar}>
            📅 Availability Calendar
          </Button>
        </div>
        {error && <span className="field-error" role="alert">{error}</span>}
      </div>
    </div>
  );
}

function Step2ProjectInfo({ formData, updateField, options, errors = {}, onChangeLocation }) {
  const stateOptions = options.states && options.states.length ? options.states : [];
  const change = (name) => (e) => updateField(name, e.target.value);
  const [calOpen, setCalOpen] = useState(null); // 'start' | 'end' | null

  const hasCoords = formData.siteLat && formData.siteLong;

  return (
    <>
      <FormSection title="Location / Dates" borderColor="m-blue" columns={2}>
        <div className="span-2 form-row">
          <span className="form-label" id="proj-loc-label">Project Location</span>
          <div className="form-col" aria-labelledby="proj-loc-label">
            <div className="location-readback">
              <div><strong>{formData.siteLoc || 'No location selected'}</strong></div>
              {formData.siteCityName && <div>{formData.siteCityName}</div>}
              <div className="text-muted">
                {hasCoords ? `Lat/Long: (${formData.siteLat}, ${formData.siteLong})` : 'Lat/Long not set'}
              </div>
              {onChangeLocation && (
                <Button variant="default" onClick={onChangeLocation} style={{ marginTop: 6 }}>
                  Change location
                </Button>
              )}
            </div>
            {errors.siteLoc && <span className="field-error" role="alert">{errors.siteLoc}</span>}
          </div>
        </div>

        <DateWithCalendar id="startDate" label="Project Start Date" value={formData.startDate} error={errors.startDate} onOpenCalendar={() => setCalOpen('start')} />
        <DateWithCalendar id="endDate" label="Project Completion Date" value={formData.endDate} error={errors.endDate} onOpenCalendar={() => setCalOpen('end')} />

        <div className="span-2 form-row">
          <span className="form-label" id="sitehazard-label">
            Are there any Site Hazards?<span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span>
          </span>
          <div className="form-col">
            <div
              className="radio-group"
              role="radiogroup"
              aria-labelledby="sitehazard-label"
              aria-required="true"
              aria-invalid={errors.sitehazardrequired ? true : undefined}
              aria-describedby={errors.sitehazardrequired ? 'sitehazard-error' : undefined}
            >
              <label className="radio-label">
                <input type="radio" name="sitehazardrequired" value="Y" checked={formData.sitehazardrequired === 'Y'} onChange={change('sitehazardrequired')} />
                Yes
              </label>
              <label className="radio-label">
                <input type="radio" name="sitehazardrequired" value="N" checked={formData.sitehazardrequired === 'N'} onChange={change('sitehazardrequired')} />
                No
              </label>
            </div>
            {errors.sitehazardrequired && <span id="sitehazard-error" className="field-error" role="alert">{errors.sitehazardrequired}</span>}
          </div>
        </div>
      </FormSection>

      <FormSection title="Property Owner" borderColor="orange" columns={2}>
        <InputField id="ownLastName" label="Owner Last Name" required maxLength={50} value={formData.ownLastName} onChange={change('ownLastName')} error={errors.ownLastName} />
        <InputField id="ownFirstName" label="Owner First Name" required maxLength={50} value={formData.ownFirstName} onChange={change('ownFirstName')} error={errors.ownFirstName} />
        <InputField id="ownAddr" label="Owner Mail Address" required maxLength={50} value={formData.ownAddr} onChange={change('ownAddr')} error={errors.ownAddr} />
        <InputField id="ownCity" label="City" required maxLength={50} value={formData.ownCity} onChange={change('ownCity')} error={errors.ownCity} />
        <SelectField id="ownState" label="State" required placeholder="Select..." options={stateOptions} value={formData.ownState} onChange={change('ownState')} error={errors.ownState} />
        <InputField id="ownZip" label="Zip Code" required maxLength={5} value={formData.ownZip} onChange={change('ownZip')} error={errors.ownZip} />
        <PhoneField label="Phone" prefix="ownPhone" withExt formData={formData} updateField={updateField} errors={errors} />
        <InputField id="ownEmail" label="Email Address" type="email" maxLength={50} value={formData.ownEmail} onChange={change('ownEmail')} error={errors.ownEmail} />
      </FormSection>

      <FormSection title={<>Client Information <span style={{ fontWeight: 400, fontSize: '0.85em', color: 'var(--text-muted, #666)' }}>(Required if different from Property Owner)</span></>} borderColor="l-blue" columns={2}>
        <InputField id="cliLastName" label="Client Last Name" maxLength={50} value={formData.cliLastName} onChange={change('cliLastName')} error={errors.cliLastName} />
        <InputField id="cliFirstName" label="Client First Name" maxLength={50} value={formData.cliFirstName} onChange={change('cliFirstName')} error={errors.cliFirstName} />
        <InputField id="cliAddr" label="Client Mail Address" maxLength={50} value={formData.cliAddr} onChange={change('cliAddr')} error={errors.cliAddr} />
        <InputField id="cliCity" label="City" maxLength={50} value={formData.cliCity} onChange={change('cliCity')} error={errors.cliCity} />
        <SelectField id="cliState" label="State" placeholder="Select..." options={stateOptions} value={formData.cliState} onChange={change('cliState')} error={errors.cliState} />
        <InputField id="cliZip" label="Zip Code" maxLength={5} value={formData.cliZip} onChange={change('cliZip')} error={errors.cliZip} />
        <PhoneField label="Phone" prefix="cliPhone" withExt formData={formData} updateField={updateField} errors={errors} />
        <InputField id="cliEmail" label="Email Address" type="email" maxLength={50} value={formData.cliEmail} onChange={change('cliEmail')} error={errors.cliEmail} />
      </FormSection>

      {calOpen && (
        <AvailabilityCalendar
          mode={calOpen}
          value={calOpen === 'start' ? formData.startDate : formData.endDate}
          startDate={formData.startDate}
          onSelect={(iso) => {
            updateField(calOpen === 'start' ? 'startDate' : 'endDate', iso);
            setCalOpen(null);
          }}
          onClose={() => setCalOpen(null)}
        />
      )}
    </>
  );
}

export default Step2ProjectInfo;
