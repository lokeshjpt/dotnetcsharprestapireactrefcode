# React — Cards / Sections

Two card primitives structure every screen. Use them instead of ad-hoc `<div>`s.

1. **`FormSection`** — a titled white panel with a colored top accent border. Wraps **form** content
   (inputs, grids). Used across the Apply wizard and staff edit screens.
2. **`DetailSection`** — a dark-navy header bar over a two-column **label : value** body. Read-only
   "Application Detail" style cards, with optional right-aligned action buttons (e.g. **Edit**).

---

## FormSection

### Component — `components/FormSection/FormSection.js`

```jsx
import './FormSection.css';

const COLOR_MAP = {
  'm-blue': 'var(--border-m-blue)',   // #337ab7 (default)
  'orange': 'var(--border-orange)',   // #e67e22
  'l-blue': 'var(--border-l-blue)',   // #5bc0de
  'none':   'var(--border-default)',  // #dddddd
};

function FormSection({ title, subtitle, borderColor = 'm-blue', headerRight, children, flat = false }) {
  const color = COLOR_MAP[borderColor] ?? COLOR_MAP['none'];
  return (
    <section className={`form-section${flat ? ' form-section--flat' : ''}`}
             style={{ '--section-border-color': color }}>
      <div className="form-section__header">
        <div className="form-section__heading">
          <h2 className="form-section__title">{title}</h2>
          {subtitle && <p className="form-section__subtitle">{subtitle}</p>}
        </div>
        {headerRight && <div className="form-section__header-right">{headerRight}</div>}
      </div>
      <div className="form-section__body">{children}</div>
    </section>
  );
}
export default FormSection;
```

### Styles — `components/FormSection/FormSection.css`

```css
.form-section { margin-bottom: var(--spacing-lg); background: #fff;
  box-shadow: 3px 3px 3px rgba(0,0,0,0.05); border-radius: 2px 2px 0 0; }
.form-section__header { padding: 8px var(--spacing-md);
  border-top: 2px solid var(--section-border-color, var(--border-default));  /* colored accent */
  border-bottom: 1px solid rgb(234,233,233);
  display: flex; align-items: center; justify-content: space-between;
  min-height: 40px; border-radius: 2px 2px 0 0; }
.form-section__heading { display: flex; flex-direction: column; gap: 2px; }
.form-section__title { font-size: 15px; font-weight: 700; margin: 0; color: var(--text-primary); }
.form-section__subtitle { margin: 0; font-size: 12px; color: var(--text-muted); }
.form-section__header-right { display: flex; align-items: center; gap: var(--spacing-sm); }
.form-section__body { padding: var(--spacing-md); }
.form-section--flat { background: transparent; box-shadow: none; }
.form-section--flat .form-section__body { padding-left: 0; padding-right: 0; }
```

### Usage

```jsx
<FormSection title="Applicant Information" subtitle="Person submitting the application"
             borderColor="m-blue" headerRight={<Button variant="link">Clear</Button>}>
  <div className="grid-two">
    <InputField label="First Name" required />
    <InputField label="Last Name" required />
  </div>
</FormSection>
```

- `borderColor` selects the top accent (`m-blue` default, `orange`, `l-blue`, `none`).
- `headerRight` holds inline actions. `flat` removes the panel chrome for embedding.
- Multi-column layouts: use `.grid-two` / `.grid-three` (from `global.css`) inside the body; they
  collapse to one column ≤900px.

---

## DetailSection (read-only detail cards)

### Component — `components/DetailSection/DetailSection.js`

```jsx
import './DetailSection.css';

function DetailSection({ title, actions, children }) {
  return (
    <section className="detail-section">
      <div className="detail-section__header">
        <span className="detail-section__title">{title}</span>
        {actions && <div className="detail-section__actions">{actions}</div>}
      </div>
      <div className="detail-section__body">{children}</div>
    </section>
  );
}

export function DetailField({ label, children, wrap = false }) {
  return (
    <div className={`detail-field${wrap ? ' detail-field--wrap' : ''}`}>
      <span className="detail-field__label">{label}</span>
      <span className="detail-field__value">{children ?? '—'}</span>
    </div>
  );
}
export default DetailSection;
```

### Styles — `components/DetailSection/DetailSection.css`

```css
.detail-section { margin-top: 16px; border: 1px solid var(--color-border);
  border-radius: var(--border-radius); overflow: hidden; background: #fff; }
.detail-section__header { display: flex; align-items: center; justify-content: space-between; gap: 12px;
  padding: 7px 14px; background: #22507a; color: var(--table-header-color); }   /* navy bar */
.detail-section__title { font-weight: 700; font-size: 0.95rem; letter-spacing: 0.01em; }
.detail-section__actions { display: flex; gap: 8px; flex-shrink: 0; }
.detail-section__actions .btn { padding: 3px 12px; font-size: 0.8rem; line-height: 1.4; }
.detail-section__body { display: grid; grid-template-columns: 1fr 1fr;
  column-gap: 48px; row-gap: 10px; padding: 16px 20px; }
.detail-section__body--single { grid-template-columns: 1fr; }

.detail-field { display: flex; gap: 10px; align-items: baseline; font-size: 0.9rem; line-height: 1.4; }
.detail-field__label { flex: 0 0 auto; width: 150px; text-align: right; font-weight: 700; color: var(--text-primary); }
.detail-field__value { flex: 1 1 auto; min-width: 0; color: var(--text-primary); word-break: break-word; }
.detail-field--wrap .detail-field__value { white-space: pre-line; }

@media (max-width: 760px) {
  .detail-section__body { grid-template-columns: 1fr; column-gap: 0; }
  .detail-field__label { width: 130px; }
}
```

### Usage

```jsx
<DetailSection title="Project Information" actions={<Button onClick={openEdit}>Edit</Button>}>
  <DetailField label="Application #">{app.appId}</DetailField>
  <DetailField label="Status">{statusLabel(app.statusCode)}</DetailField>
  <DetailField label="Location" wrap>{app.siteLocation}</DetailField>
</DetailSection>
```

- Right-aligned bold labels (150px) : left-aligned values, two columns collapsing to one ≤760px.
- `DetailField` renders an em-dash (`—`) for empty values; `wrap` preserves line breaks.
- Header `actions` typically open a `Modal` edit form (see `react-fields.md` / `react-design-patterns.md`).

## Choosing between them

| Use | When |
|-----|------|
| `FormSection` | User is **entering/editing** data (inputs, selects, grids). |
| `DetailSection` + `DetailField` | Displaying **read-only** record details, optionally with an Edit action. |
| `.panel` (global) | Generic white card that isn't a form or a detail record. |
