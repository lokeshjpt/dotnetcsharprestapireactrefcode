import { useEffect } from 'react';
import './Toaster.css';

// Leading status glyph per toast type (tick for success, error circle for error, etc.).
function ToastIcon({ type }) {
  const common = {
    viewBox: '0 0 24 24',
    width: 20,
    height: 20,
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 2.2,
    strokeLinecap: 'round',
    strokeLinejoin: 'round',
    'aria-hidden': true,
  };
  switch (type) {
    case 'success':
      return <svg {...common}><path d="M20 6 9 17l-5-5" /></svg>;
    case 'error':
      return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="M15 9l-6 6M9 9l6 6" /></svg>;
    case 'warning':
      return (
        <svg {...common}>
          <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
          <line x1="12" y1="9" x2="12" y2="13" />
          <line x1="12" y1="17" x2="12.01" y2="17" />
        </svg>
      );
    default:
      return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="M12 16v-4M12 8h.01" /></svg>;
  }
}

function Toaster({ message, type = 'info', duration = 3500, onClose }) {
  useEffect(() => {
    if (!message) return undefined;
    const timer = setTimeout(() => onClose && onClose(), duration);
    return () => clearTimeout(timer);
  }, [message, duration, onClose]);

  if (!message) return null;

  return (
    <div className={`toaster toaster-${type}`} role="status">
      <span className="toaster-icon"><ToastIcon type={type} /></span>
      <span className="toaster-message">{message}</span>
      <button type="button" className="toaster-close" onClick={onClose} aria-label="Dismiss">×</button>
    </div>
  );
}

export default Toaster;
