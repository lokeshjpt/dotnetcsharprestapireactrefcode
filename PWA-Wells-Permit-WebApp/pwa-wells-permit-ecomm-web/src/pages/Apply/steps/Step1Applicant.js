import FormSection from '../../../components/FormSection/FormSection';
import InputField from '../../../components/UI/InputField';
import SelectField from '../../../components/UI/SelectField';
import PhoneField from '../../../components/UI/PhoneField';
import Button from '../../../components/UI/Button';

function Step1Applicant({ formData, updateField, options, errors = {}, updateCcEmail, addCcEmail, removeCcEmail }) {
  const stateOptions = options.states && options.states.length ? options.states : [];
  const change = (name) => (e) => updateField(name, e.target.value);

  return (
    <>
      <FormSection title="Applicant Information" borderColor="m-blue" columns={2}>
        <InputField id="appBusinessName" label="Applicant Business Name" required maxLength={100} autoComplete="organization" value={formData.appBusinessName} onChange={change('appBusinessName')} error={errors.appBusinessName} />
        <InputField id="appLastName" label="Last Name" required maxLength={50} autoComplete="family-name" value={formData.appLastName} onChange={change('appLastName')} error={errors.appLastName} />
        <InputField id="appFirstName" label="First Name" required maxLength={50} autoComplete="given-name" value={formData.appFirstName} onChange={change('appFirstName')} error={errors.appFirstName} />
        <InputField id="appAddr" label="Mailing Address" required maxLength={50} autoComplete="address-line1" value={formData.appAddr} onChange={change('appAddr')} error={errors.appAddr} />
        <InputField id="appAddr2" label="Address Line 2" maxLength={50} autoComplete="address-line2" value={formData.appAddr2} onChange={change('appAddr2')} error={errors.appAddr2} />
        <InputField id="appCity" label="City" required maxLength={50} autoComplete="address-level2" value={formData.appCity} onChange={change('appCity')} error={errors.appCity} />
        <SelectField id="appState" label="State" required placeholder="Select..." autoComplete="address-level1" options={stateOptions} value={formData.appState} onChange={change('appState')} error={errors.appState} />
        <InputField id="appZip" label="Zip Code" required maxLength={5} autoComplete="postal-code" value={formData.appZip} onChange={change('appZip')} error={errors.appZip} />
        <InputField id="appEmail" label="Email Address" required type="email" maxLength={50} autoComplete="email" value={formData.appEmail} onChange={change('appEmail')} error={errors.appEmail} />
        <PhoneField label="Phone" prefix="appPhone" withExt required autoComplete formData={formData} updateField={updateField} errors={errors} />
        <PhoneField label="Fax" prefix="appFax" formData={formData} updateField={updateField} errors={errors} />
      </FormSection>

      <FormSection title="Contact Information" borderColor="l-blue" columns={2}>
        <InputField id="conLastName" label="Contact Last Name" maxLength={50} value={formData.conLastName} onChange={change('conLastName')} error={errors.conLastName} />
        <InputField id="conFirstName" label="Contact First Name" maxLength={50} value={formData.conFirstName} onChange={change('conFirstName')} error={errors.conFirstName} />
        <PhoneField label="Contact Phone" prefix="conPhone" withExt formData={formData} updateField={updateField} errors={errors} />
        <PhoneField label="Contact Cell" prefix="conCell" formData={formData} updateField={updateField} errors={errors} />
        <InputField id="conEmail" label="Contact Email" type="email" maxLength={50} value={formData.conEmail} onChange={change('conEmail')} error={errors.conEmail} />
      </FormSection>

      <FormSection title="Other Emails to CC" borderColor="none">
        {(formData.ccEmails || []).map((email, index) => (
          <div key={index} className="repeatable-row">
            <input
              className={`form-control${errors[`ccEmails.${index}`] ? ' is-invalid' : ''}`}
              type="email"
              aria-label={`CC email ${index + 1}`}
              maxLength={50}
              placeholder="name@example.com"
              value={email}
              onChange={(e) => updateCcEmail(index, e.target.value)}
            />
            <Button variant="danger" onClick={() => removeCcEmail(index)}>Remove</Button>
            {errors[`ccEmails.${index}`] && <span className="field-error">{errors[`ccEmails.${index}`]}</span>}
          </div>
        ))}
        <Button variant="default" onClick={addCcEmail}>+ Add CC Email</Button>
      </FormSection>
    </>
  );
}

export default Step1Applicant;