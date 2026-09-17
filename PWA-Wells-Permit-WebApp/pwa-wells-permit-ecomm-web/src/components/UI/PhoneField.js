import './ui.css';

function seg(prefix, n) {
  return `${prefix}${n}`;
}

function PhoneField({ label, prefix, formData, updateField, required = false, withExt = false, errors = {}, spanTwo = false, autoComplete = false }) {
  const err = errors[seg(prefix, 1)] || errors[seg(prefix, 2)] || errors[seg(prefix, 3)];
  const labelId = `${prefix}-label`;
  const errorId = err ? `${prefix}-error` : undefined;
  const onChange = (name, max) => (e) => {
    const v = e.target.value.replace(/\D/g, '').slice(0, max);
    updateField(name, v);
  };
  return (
    <div className={`form-row${spanTwo ? ' span-2' : ''}`}>
      <span className="form-label" id={labelId}>
        {label}
        {required && (
          <>
            <span className="mandatory" aria-hidden="true">*</span>
            <span className="sr-only"> (required)</span>
          </>
        )}
      </span>
      <div className="form-col">
        <div
          className="phone-inline"
          role="group"
          aria-labelledby={labelId}
          aria-describedby={errorId}
        >
          <input className="form-control" style={{ width: 60 }} placeholder="000" inputMode="numeric" autoComplete={autoComplete ? 'tel-area-code' : undefined} aria-label={`${label} area code`} aria-invalid={err ? true : undefined} value={formData[seg(prefix, 1)] || ''} onChange={onChange(seg(prefix, 1), 3)} />
          <span aria-hidden="true">-</span>
          <input className="form-control" style={{ width: 60 }} placeholder="000" inputMode="numeric" autoComplete={autoComplete ? 'tel-local-prefix' : undefined} aria-label={`${label} prefix`} aria-invalid={err ? true : undefined} value={formData[seg(prefix, 2)] || ''} onChange={onChange(seg(prefix, 2), 3)} />
          <span aria-hidden="true">-</span>
          <input className="form-control" style={{ width: 80 }} placeholder="0000" inputMode="numeric" autoComplete={autoComplete ? 'tel-local-suffix' : undefined} aria-label={`${label} line number`} aria-invalid={err ? true : undefined} value={formData[seg(prefix, 3)] || ''} onChange={onChange(seg(prefix, 3), 4)} />
          {withExt && (
            <>
              <span aria-hidden="true">ext.</span>
              <input className="form-control" style={{ width: 70 }} placeholder="ext" inputMode="numeric" autoComplete={autoComplete ? 'tel-extension' : undefined} aria-label={`${label} extension`} value={formData[`${prefix}X`] || ''} onChange={onChange(`${prefix}X`, 5)} />
            </>
          )}
        </div>
        {err && <span id={errorId} className="field-error" role="alert">{err}</span>}
      </div>
    </div>
  );
}

export default PhoneField;