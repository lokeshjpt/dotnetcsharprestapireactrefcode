import './ui.css';

function SelectField({ label, id, options = [], error, required = false, placeholder, spanTwo = false, ...props }) {
  const errorId = error ? `${id}-error` : undefined;
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
        </label>
      )}
      <div className="form-col">
        <select
          id={id}
          className={`form-control${error ? ' is-invalid' : ''}`}
          required={required}
          aria-required={required || undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={errorId}
          {...props}
        >
          {placeholder !== undefined && <option value="">{placeholder}</option>}
          {options.map((option, index) => (
            <option key={`${option.code}-${index}`} value={option.code}>
              {option.label}
            </option>
          ))}
        </select>
        {error && <span id={errorId} className="field-error" role="alert">{error}</span>}
      </div>
    </div>
  );
}

export default SelectField;