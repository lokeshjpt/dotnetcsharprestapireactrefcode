# React — Validation

Validation is **plain functions**, not a form library. Two layers:

1. **`validation.js`** — small, pure, reusable validators. Each returns an **error message string or
   `null`**. Signature: `validateX(value, { label, required, maxLen, ... }) => string | null`.
2. **`stepValidators.js`** — composes those validators into an `errors` object keyed by field name for
   a whole step/form. The page stores `errors` in state, passes `errors[field]` to each field, and
   blocks navigation/submit while any error exists.

This mirrors the legacy `Validator` rules and keeps validation testable (see `*.test.js` beside each).

## Reusable validators — `pages/**/validation.js`

```js
const QUOTE_RE = /['"]/;
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const NUMERIC_RE = /^\d+$/;

export function isEmpty(v) { return v === undefined || v === null || String(v).trim() === ''; }
export function hasQuotes(v) { return QUOTE_RE.test(String(v || '')); }
export function isEmail(v) { return EMAIL_RE.test(String(v || '').trim()); }
export function isNumeric(v) { return NUMERIC_RE.test(String(v || '').trim()); }
export function isDecimal(v) { return /^\d+(\.\d+)?$/.test(String(v || '').trim()); }

export function validateText(value, { label, required, maxLen }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (hasQuotes(value)) return `${label} cannot contain quotes.`;            // legacy anti-injection rule
  if (maxLen && String(value).length > maxLen) return `${label} must be ${maxLen} characters or fewer.`;
  return null;
}

export function validateEmail(value, { label, required }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (!isEmail(value)) return `${label} must be a valid email address.`;
  return null;
}

export function validateZip(value, { label, required }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (!/^\d{5}$/.test(String(value).trim())) return `${label} must be 5 digits.`;
  return null;
}

export function validateNumeric(value, { label, required, maxLen }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (!isNumeric(value)) return `${label} must be numeric.`;
  if (maxLen && String(value).length > maxLen) return `${label} must be ${maxLen} digits or fewer.`;
  return null;
}
```

Also provided: `validateDecimal`, `validateDate`, `validateFutureDate`, `validateAfter(value, other, …)`,
and domain date rules `validateStartDate` / `validateEndDate` (reject weekends/County holidays and
enforce a min/max lead-time window).

## Composing a step — `pages/**/stepValidators.js`

Build an errors object; each key is a field name, each value is a message or `null`. Segmented phones
validate area/prefix/line independently:

```js
function validatePhone(fd, prefix, label, required, errors) {
  const [p1, p2, p3] = [fd[`${prefix}1`], fd[`${prefix}2`], fd[`${prefix}3`]];
  const anyEntered = !isEmpty(p1) || !isEmpty(p2) || !isEmpty(p3);
  if (!anyEntered) { if (required) errors[`${prefix}1`] = `${label} is required.`; return; }
  if (isEmpty(p1) || !/^\d{3}$/.test(String(p1))) errors[`${prefix}1`] = `${label} area code must be 3 digits.`;
  else if (isEmpty(p2) || !/^\d{3}$/.test(String(p2))) errors[`${prefix}2`] = `${label} prefix must be 3 digits.`;
  else if (isEmpty(p3) || !/^\d{4}$/.test(String(p3))) errors[`${prefix}3`] = `${label} line number must be 4 digits.`;
}

function applicantErrors(fd) {
  const e = {};
  e.appBusinessName = validateText(fd.appBusinessName, { label: 'Applicant Business Name', required: true, maxLen: 100 });
  e.appLastName     = validateText(fd.appLastName,     { label: 'Applicant Last Name', required: true, maxLen: 50 });
  e.appZip          = validateZip(fd.appZip,           { label: 'Zip Code', required: true });
  validatePhone(fd, 'appPhone', 'Phone', true, e);
  e.appEmail        = validateEmail(fd.appEmail,       { label: 'Email Address', required: true });
  return e;   // keys with null = valid
}
```

## Wiring into a page

```js
const [errors, setErrors] = useState({});

function handleNext() {
  const e = applicantErrors(formData);
  const hasErrors = Object.values(e).some(Boolean);   // any non-null message
  setErrors(e);
  if (hasErrors) return;                               // block until clean
  goToNextStep();
}
```

- Pass `error={errors.appEmail}` to each `InputField`/`SelectField`; the field shows the red border +
  message automatically.
- `Object.values(errors).some(Boolean)` is the "is this step valid?" check (null = valid).
- Optionally scroll to / focus the first invalid field for accessibility.

## Conventions

1. **Validators are pure and return messages** (not booleans) so the message lives with the rule and
   is unit-testable. Keep a `*.test.js` next to each validator/step file.
2. **Reject quotes** in free-text fields (`hasQuotes`) — legacy anti-injection parity.
3. **Enforce `maxLen`** to match DB column widths; keep the same limits the API/DB expects.
4. **Client validation is a UX layer only.** The API re-validates and guards independently — never
   rely on the client for security.
5. Normalize before sending (phones → digits only) to match server normalization.
