import './DetailSection.css';

/**
 * Legacy "Application Detail" section: a dark-navy full-width header bar with a
 * white bold title on the left and optional action buttons on the right, followed
 * by a clean two-column label:value body (no colored field-name cells).
 */
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
