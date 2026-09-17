# React — Form Fields & Buttons

Reusable field primitives live in `components/UI/` and all render the same `.form-row` /
`.form-control` markup from `global.css`, so every form looks identical. Compose forms from these —
don't write raw `<input>`s.

Each field takes `label`, `id`, `error`, `required`, and spreads the rest (`...props`) onto the
native control, so `value`, `onChange`, `type`, `placeholder`, `maxLength`, `disabled`, etc. all
work.

## InputField — `components/UI/InputField.js`

```jsx
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
        <input id={id} className={`form-control${error ? ' is-invalid' : ''}`} {...props} />
        {hint && !error && <span className="text-muted">{hint}</span>}
        {error && <span className="field-error">{error}</span>}
      </div>
    </div>
  );
}
export default InputField;
```

## SelectField — `components/UI/SelectField.js`

Options are `{ code, label }` objects (matching the API's reference-data shape).

```jsx
import './ui.css';

function SelectField({ label, id, options = [], error, required = false, placeholder, ...props }) {
  return (
    <div className="form-row">
      {label && (
        <label className="form-label" htmlFor={id}>
          {label}{required && <span className="mandatory">*</span>}
        </label>
      )}
      <div className="form-col">
        <select id={id} className={`form-control${error ? ' is-invalid' : ''}`} {...props}>
          {placeholder !== undefined && <option value="">{placeholder}</option>}
          {options.map((o) => <option key={o.code} value={o.code}>{o.label}</option>)}
        </select>
        {error && <span className="field-error">{error}</span>}
      </div>
    </div>
  );
}
export default SelectField;
```

## PhoneField — `components/UI/PhoneField.js`

Three segmented inputs (area / prefix / line = 3/3/4 digits) that the app normalizes to digits-only
before sending; the API's `PhoneNormalizer.DigitsOnly` mirrors this server-side. Validation is
per-segment (see `react-validation.md`).

## Button — `components/UI/Button.js`

```jsx
import './ui.css';

const VARIANT_CLASS = {
  primary: 'btn-primary', secondary: 'btn-default', default: 'btn-default',
  ghost: 'btn-link', link: 'btn-link', danger: 'btn-danger',
};

function Button({ children, variant = 'primary', type = 'button', className = '', ...props }) {
  const variantClass = VARIANT_CLASS[variant] || 'btn-primary';
  return (
    <button type={type} className={`btn ${variantClass} ${className}`.trim()} {...props}>
      {children}
    </button>
  );
}
export default Button;
```

| Variant | Class | Look |
|---------|-------|------|
| `primary` (default) | `.btn-primary` | Navy `#22507a`, white text |
| `secondary` / `default` | `.btn-default` | Grey `#f5f5f5` |
| `danger` | `.btn-danger` | Red `#a11f1c` |
| `ghost` / `link` | `.btn-link` | Text-only link button |

Buttons default to `type="button"` (avoids accidental form submits); use `type="submit"` explicitly.

## The row/grid contract

- One field = one `.form-row` (label ≥160px + flexible `.form-col`).
- Put multiple `.form-row`s inside `.grid-two` / `.grid-three` for multi-column forms (collapse to 1
  column ≤900px).
- Error state: pass `error` (a string). It adds `.is-invalid` (red border) to the control and renders
  a `.field-error` message. `hint` shows muted helper text only when there's no error.
- `ui.css` adds a few helpers: `.field-inline`, `.phone-inline`, `.repeatable-row` (for add/remove
  lists like CC emails). It otherwise inherits everything from `global.css`.

## Accessibility

- Always pass a unique `id`; the `<label htmlFor={id}>` binds to it.
- `required` renders the visible red `*`; back it with real validation, not just the asterisk.
- Errors are rendered inline and referenced next to the control so screen readers announce them.

## Usage

```jsx
<FormSection title="Contact">
  <div className="grid-two">
    <InputField id="firstName" label="First Name" required
                value={fd.firstName} onChange={onChange('firstName')}
                error={errors.firstName} />
    <SelectField id="state" label="State" required placeholder="Select…"
                 options={states} value={fd.state} onChange={onChange('state')}
                 error={errors.state} />
  </div>
  <div className="form-actions">
    <Button variant="secondary" onClick={onBack}>Back</Button>
    <Button type="submit">Continue</Button>
  </div>
</FormSection>
```
