import './ui.css';

function InputField({ label, id, error, required = false, hint, labelSuffix, ...props }) {
  return (
    <div className="form-row">
      {label && (
        <label className="form-label" htmlFor={id}>
          {label}
          {required && <span className="mandatory">*</span>}
          {labelSuffix}
        </label>
      )}
      <div className="form-col">
        <input
          id={id}
          className={`form-control${error ? ' is-invalid' : ''}`}
          {...props}
        />
        {hint && !error && <span className="text-muted">{hint}</span>}
        {error && <span className="field-error">{error}</span>}
      </div>
    </div>
  );
}

export default InputField;
