import './ui.css';

function InputField({ label, id, error, required = false, hint, spanTwo = false, labelSuffix, ...props }) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [errorId, hintId].filter(Boolean).join(' ') || undefined;
  return (
    <div className={`form-row${spanTwo ? ' span-2' : ''}`}>
      {label && (
        <label className="form-label" htmlFor={id}>
          {label}
          {required && (
            <>
              <span className="mandatory" aria-hidden="true">*</span>
              <span className="sr-only"> (required)</span>
            </>
          )}
          {labelSuffix}
        </label>
      )}
      <div className="form-col">
        <input
          id={id}
          className={`form-control${error ? ' is-invalid' : ''}`}
          required={required}
          aria-required={required || undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          {...props}
        />
        {hint && !error && <span id={hintId} className="text-muted">{hint}</span>}
        {error && <span id={errorId} className="field-error" role="alert">{error}</span>}
      </div>
    </div>
  );
}

export default InputField;