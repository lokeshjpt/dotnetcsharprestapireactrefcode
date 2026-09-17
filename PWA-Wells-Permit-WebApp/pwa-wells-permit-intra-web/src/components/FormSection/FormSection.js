import './FormSection.css';

const COLOR_MAP = {
  'm-blue': 'var(--border-m-blue)',
  'orange': 'var(--border-orange)',
  'l-blue': 'var(--border-l-blue)',
  'none': 'var(--border-default)',
};

function FormSection({ title, subtitle, borderColor = 'm-blue', headerRight, children, flat = false }) {
  const color = COLOR_MAP[borderColor] ?? COLOR_MAP['none'];
  return (
    <section
      className={`form-section${flat ? ' form-section--flat' : ''}`}
      style={{ '--section-border-color': color }}
    >
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
