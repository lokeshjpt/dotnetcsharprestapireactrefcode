import './FormSection.css';

const COLOR_MAP = {
  'm-blue': 'var(--border-m-blue)',
  'orange': 'var(--border-orange)',
  'l-blue': 'var(--border-l-blue)',
  'none': 'var(--border-default)',
};

function FormSection({ title, borderColor = 'none', headerRight, children, flat = false, columns = 1 }) {
  const color = COLOR_MAP[borderColor] ?? COLOR_MAP['none'];
  return (
    <div
      className={`form-section${flat ? ' form-section--flat' : ''}`}
      style={{ '--section-border-color': color }}
    >
      <div className="form-section__header">
        <h2 className="form-section__title">{title}</h2>
        {headerRight && <div className="form-section__header-right">{headerRight}</div>}
      </div>
      <div className={`form-section__body${columns === 2 ? ' form-grid-2' : ''}`}>{children}</div>
    </div>
  );
}

export default FormSection;