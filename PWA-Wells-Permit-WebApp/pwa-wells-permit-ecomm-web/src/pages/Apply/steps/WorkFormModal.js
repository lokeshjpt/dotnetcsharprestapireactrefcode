import { useRef, useState } from 'react';
import Modal from '../../../components/Modal/Modal';
import Button from '../../../components/UI/Button';
import Step3WorkType from './Step3WorkType';
import Step4WorkInfo from './Step4WorkInfo';
import { validateStepByKey } from '../stepValidators';

// Add/Edit a single work in a modal: work type + work information + well specifications in one place
// (replaces the former separate Work Type / Work Info wizard steps). Edits the shared draft fields;
// on Save the draft is validated and committed to the works list, on Cancel it is discarded.
function WorkFormModal({ mode, app, options, onSaved, onCancel }) {
  const { formData, updateField, setFields } = app;
  const [errors, setErrors] = useState({});
  const bodyRef = useRef(null);

  const shared = { formData, updateField, setFields, options, errors };

  // On a failed save, move to the first invalid field inside the modal instead of leaving the
  // applicant to hunt for it (parity with the wizard's focus-first-error behaviour).
  const scrollToFirstError = () => {
    window.requestAnimationFrame(() => {
      const root = bodyRef.current;
      if (!root) return;
      const invalidControl = root.querySelector('.is-invalid, [aria-invalid="true"]');
      const target = invalidControl || root.querySelector('.field-error');
      if (!target) return;
      target.scrollIntoView({ behavior: 'smooth', block: 'center' });
      const focusable = invalidControl
        && typeof invalidControl.focus === 'function'
        && ['INPUT', 'SELECT', 'TEXTAREA', 'BUTTON'].includes(invalidControl.tagName);
      if (focusable) {
        try { invalidControl.focus({ preventScroll: true }); } catch { invalidControl.focus(); }
      }
    });
  };

  const handleSave = () => {
    const merged = {
      ...validateStepByKey('workType', formData),
      ...validateStepByKey('workInfo', formData),
    };
    if (Object.keys(merged).length > 0) {
      setErrors(merged);
      scrollToFirstError();
      return;
    }
    setErrors({});
    app.saveWork();
    onSaved();
  };

  const footer = (
    <>
      <Button variant="default" onClick={onCancel}>Cancel</Button>
      <Button variant="primary" onClick={handleSave}>
        {mode === 'edit' ? 'Save Work' : 'Add Work'}
      </Button>
    </>
  );

  return (
    <Modal
      title={mode === 'edit' ? 'Edit Work Type' : 'Add Work Type'}
      onClose={onCancel}
      footer={footer}
      wide
    >
      {Object.keys(errors).length > 0 && (
        <div className="alert alert-error" role="alert">
          Please correct the highlighted fields.
        </div>
      )}
      <div ref={bodyRef}>
        <Step3WorkType {...shared} />
        <Step4WorkInfo
          {...shared}
          updateWellSpec={app.updateWellSpec}
          addWellSpec={app.addWellSpec}
          removeWellSpec={app.removeWellSpec}
          seedWellSpecCoords={app.seedWellSpecCoords}
        />
      </div>
    </Modal>
  );
}

export default WorkFormModal;
