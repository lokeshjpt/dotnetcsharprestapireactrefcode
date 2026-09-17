import { useMemo, useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { formatStatus, statusPillClass } from '../../../../constants/statusCodes';
import { updateWorkConditions } from '../../../../api/conditionApi';
import '../WorkConditions.css';

// The free-text "special condition" is the CONDITION_TYPES row coded 'other'; it is rendered as the
// special-condition textarea rather than a checkbox (mirrors proc_work_conditions.jsp).
const OTHER_CONDITION_CODE = 'other';

function money(n) {
  return `$${Number(n || 0).toFixed(2)}`;
}

/**
 * Per-work permit-conditions editor (parity with the legacy intra proc_work_conditions.jsp).
 * The work-type-scoped condition master list (entry.available) is rendered as checkboxes — nothing
 * is pre-checked; the reviewer checks the ones to apply. Conditions already applied to this work
 * (entry.selected) are shown checked when editing. A single free-text "special condition" maps to
 * the 'other' condition. Saving replaces the work's conditions via the per-work conditions endpoint.
 */
function WorkConditionsModal({ appId, entry, detail, onClose, onSaved }) {
  const showToast = useToast();
  const available = entry.available || [];
  const checkboxes = available.filter((c) => (c.code || '').toLowerCase() !== OTHER_CONDITION_CODE);

  // Split the persisted selection into checkbox codes and the single free-text 'other' condition.
  const initial = useMemo(() => {
    const selected = entry.selected || [];
    const codes = selected
      .map((s) => s.conditionType)
      .filter((code) => code && code.toLowerCase() !== OTHER_CONDITION_CODE);
    const other = selected.find((s) => (s.conditionType || '').toLowerCase() === OTHER_CONDITION_CODE);
    return { codes, special: other?.otherDesc || '' };
  }, [entry]);

  const [selectedCodes, setSelectedCodes] = useState(initial.codes);
  const [special, setSpecial] = useState(initial.special);
  const [saving, setSaving] = useState(false);

  const isPendc = (entry.statusCode || '').toUpperCase() === 'PENDC';

  const toggle = (code) =>
    setSelectedCodes((cur) => (cur.includes(code) ? cur.filter((c) => c !== code) : [...cur, code]));

  // noSpecials=true is the legacy "No Specials" action: advance a Pending-Conditions work to Pending
  // Approval with zero conditions. Otherwise the checked conditions + special text are saved.
  async function save(noSpecials) {
    setSaving(true);
    try {
      const payload = {
        conditions: noSpecials
          ? []
          : [
              ...selectedCodes.map((code) => ({ conditionType: code, otherDesc: null })),
              ...(special.trim()
                ? [{ conditionType: OTHER_CONDITION_CODE, otherDesc: special.trim() }]
                : []),
            ],
        noSpecials: Boolean(noSpecials),
      };
      const fresh = await updateWorkConditions(appId, entry.workId, payload);
      showToast('Conditions applied.', 'success');
      onSaved(fresh);
    } catch {
      showToast('Conditions could not be saved. Please try again.', 'error');
      setSaving(false);
    }
  }

  const cat = detail?.workCategoryDesc || detail?.workCategory || '';
  const type = detail?.workTypeDesc || detail?.workType || '';
  const feeLine = detail?.workFeeRate != null && detail?.workFeeRate !== ''
    ? `${money(detail.workFeeRate)} per ${detail.workFeeUnit || 'site'}`
    : '';
  const wellUse = detail?.wellUseDesc || detail?.wellUseType || '';
  const headerLabel = [cat, type].filter(Boolean).join(' - ') || entry.workLabel || `Work #${entry.workId}`;

  return (
    <Modal
      open
      size="lg"
      title="Work Permit Approval Conditions"
      onClose={saving ? undefined : onClose}
      footer={(
        <>
          <Button variant="default" onClick={onClose} disabled={saving}>Cancel</Button>
          {isPendc && (
            <Button variant="default" onClick={() => save(true)} disabled={saving}>No Specials</Button>
          )}
          <Button variant="primary" onClick={() => save(false)} disabled={saving}>
            {saving ? 'Saving…' : 'Update Conditions'}
          </Button>
        </>
      )}
    >
      <div className="wc-modal">
        <div className="wc-modal__head">
          <div className="wc-modal__title">
            {headerLabel}{feeLine ? ` : ${feeLine}` : ''}
          </div>
          <span className={statusPillClass(entry.statusCode)}>{formatStatus(entry.statusCode)}</span>
        </div>
        {wellUse && <div className="wc-modal__welluse">Well Use: {wellUse}</div>}
        <p className="wc-modal__hint">
          {checkboxes.length > 0
            ? 'Check the conditions to apply to this work, or type in a special condition to be applied.'
            : 'Type in a special condition to be applied to this work.'}
        </p>

        {checkboxes.length > 0 ? (
          <ul className="wc-cond-list">
            {checkboxes.map((c) => (
              <li key={c.code}>
                <input
                  id={`wc-${entry.workId}-${c.code}`}
                  type="checkbox"
                  checked={selectedCodes.includes(c.code)}
                  onChange={() => toggle(c.code)}
                />
                <label htmlFor={`wc-${entry.workId}-${c.code}`}>{c.label}</label>
              </li>
            ))}
          </ul>
        ) : (
          <p className="wc-modal__empty">
            No standard conditions are defined for this work type. Enter a special condition below
            {isPendc ? ', or use \u201cNo Specials\u201d to move this work to Pending Approval without conditions' : ''}.
          </p>
        )}

        <div className="wc-special">
          <label className="form-label" htmlFor={`wc-special-${entry.workId}`}>Special condition (optional)</label>
          <textarea
            id={`wc-special-${entry.workId}`}
            className="form-control wc-special__area"
            aria-label={`Special condition for ${headerLabel}`}
            placeholder="Special condition (optional)"
            value={special}
            onChange={(e) => setSpecial(e.target.value)}
          />
        </div>
      </div>
    </Modal>
  );
}

export default WorkConditionsModal;
