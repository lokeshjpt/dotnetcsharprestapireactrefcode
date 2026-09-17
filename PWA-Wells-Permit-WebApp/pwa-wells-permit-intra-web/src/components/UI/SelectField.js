import './ui.css';

function SelectField({ label, id, options = [], error, required = false, placeholder, ...props }) {
  return (
    <div className="form-row">
      {label && (
        <label className="form-label" htmlFor={id}>
          {label}
          {required && <span className="mandatory">*</span>}
        </label>
      )}
      <div className="form-col">
        <select
          id={id}
          className={`form-control${error ? ' is-invalid' : ''}`}
          {...props}
        >
          {placeholder !== undefined && <option value="">{placeholder}</option>}
          {options.map((option) => (
            <option key={option.code} value={option.code}>
              {option.label}
            </option>
          ))}
        </select>
        {error && <span className="field-error">{error}</span>}
      </div>
    </div>
  );
}

export default SelectField;
