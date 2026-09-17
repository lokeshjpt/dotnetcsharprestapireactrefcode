import FormSection from '../../../components/FormSection/FormSection';
import InputField from '../../../components/UI/InputField';
import { formatDollar } from '../referenceData';

const PAYMENT_OPTIONS = [
  { code: 'CC', label: 'Credit Card (via IntelliPay)' },
  { code: 'CHECK', label: 'Check' },
  { code: 'EXMPT', label: 'Fee Exempt' },
];

function Step5Payment({ formData, updateField, options, amountDue, errors = {} }) {
  const change = (name) => (e) => updateField(name, e.target.value);
  // Always offer the supported public methods in a stable order (Cash is intra-only and never shown
  // to applicants); prefer the API's label for a type when one is provided.
  const apiByCode = new Map((options.paymentTypes || []).map((p) => [p.code, p]));
  const paymentOptions = PAYMENT_OPTIONS.map((o) => ({ code: o.code, label: apiByCode.get(o.code)?.label || o.label }));

  return (
    <FormSection title="Payment Information" borderColor="m-blue">
      <div className="form-row">
        <label className="form-label">Amount Due</label>
        <div className="form-col">
          <strong style={{ fontSize: '1.2rem' }}>{formatDollar(amountDue)}</strong>
        </div>
      </div>

      <div className="form-row">
        <span className="form-label" id="paymentType-label">
          Payment Type<span className="mandatory" aria-hidden="true">*</span><span className="sr-only"> (required)</span>
        </span>
        <div className="form-col">
          <div
            className="radio-group"
            role="radiogroup"
            aria-labelledby="paymentType-label"
            aria-required="true"
            aria-invalid={errors.paymentType ? true : undefined}
            aria-describedby={errors.paymentType ? 'paymentType-error' : undefined}
          >
            {paymentOptions.map((option) => (
              <label className="radio-label" key={option.code}>
                <input
                  type="radio"
                  name="paymentType"
                  value={option.code}
                  checked={formData.paymentType === option.code}
                  onChange={change('paymentType')}
                />
                {option.label}
              </label>
            ))}
          </div>
          {errors.paymentType && <span id="paymentType-error" className="field-error" role="alert">{errors.paymentType}</span>}
        </div>
      </div>

      {formData.paymentType === 'CC' && (
        <div className="alert alert-success">
          Your card information will be entered at the end of the order process via a secure
          IntelliPay lightbox. The card is stored securely and is not charged now.
        </div>
      )}

      {formData.paymentType === 'CHECK' && (
        <>
          <InputField id="acctName" label="Name on Account" required maxLength={50} value={formData.acctName} onChange={change('acctName')} error={errors.acctName} />
          <div className="alert alert-success">
            Please mail your check to the Alameda County Public Works Agency. Your application
            will be processed once the check is received.
          </div>
        </>
      )}

      {formData.paymentType === 'EXMPT' && (
        <div className="alert alert-success">
          Fee-exempt applications require no payment. Eligibility is verified by PWA staff.
        </div>
      )}
    </FormSection>
  );
}

export default Step5Payment;