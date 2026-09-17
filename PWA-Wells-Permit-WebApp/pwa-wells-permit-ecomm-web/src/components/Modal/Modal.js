import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import './Modal.css';

// Accessible modal dialog: Escape to close, backdrop click to close, background scroll locked,
// focus moved into the dialog on open and restored on close. Footer/children are caller-provided.
function Modal({ title, onClose, children, footer, wide = false, labelledBy }) {
  const dialogRef = useRef(null);
  const lastFocusedRef = useRef(null);
  // Keep the latest onClose in a ref so the mount effect can run exactly once. If this effect
  // depended on onClose (which callers usually recreate every render), it would re-run on every
  // parent render and call dialogRef.focus(), stealing focus from inputs after one keystroke.
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    lastFocusedRef.current = document.activeElement;
    const { body } = document;
    const prevOverflow = body.style.overflow;
    body.style.overflow = 'hidden';
    dialogRef.current?.focus();

    const onKeyDown = (e) => {
      if (e.key === 'Escape') {
        e.stopPropagation();
        onCloseRef.current();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      body.style.overflow = prevOverflow;
      if (lastFocusedRef.current && lastFocusedRef.current.focus) lastFocusedRef.current.focus();
    };
  }, []);

  const titleId = labelledBy || 'modal-title';

  return createPortal(
    <div className="modal-overlay" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div
        className={`modal-dialog${wide ? ' modal-dialog--wide' : ''}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        ref={dialogRef}
      >
        <div className="modal-header">
          <h2 className="modal-title" id={titleId}>{title}</h2>
          <button type="button" className="modal-close" aria-label="Close" onClick={onClose}>×</button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}

export default Modal;
