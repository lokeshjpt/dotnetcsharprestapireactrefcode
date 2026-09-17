import { forwardRef, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import Button from '../../components/UI/Button';
import IntelliPayLightbox from '../../components/IntelliPayLightbox/IntelliPayLightbox';
import { approveApplication } from '../../api/approvalApi';
import { chargePayment, getPayment, updatePayment } from '../../api/paymentApi';
import { updateApprovalDetails, uploadSitemap, getApplication } from '../../api/applicationApi';
import { getInspections } from '../../api/inspectionApi';
import { getInspectors } from '../../api/referenceApi';
import InspectionSchedule from './InspectionSchedule';
import { formatStatus, isApprovedStatus, statusPillClass } from '../../constants/statusCodes';
import { formatPaymentType } from '../../constants/paymentTypes';
import { baseFee } from '../../utils/feeCalc';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import { useAuth } from '../../context/AuthContext';
import './ApprovalWizard.css';

// Canonical site_visit_type values shared with the legacy Java intra (Constants.siteVisitInspection /
// Constants.siteVisitReview). Storing these exact strings is what makes the legacy search_detail.jsp
// radios pre-select correctly; any other token shows a checkmark but no selected option.
const VISIT_INSPECTION = 'Inspection';
const VISIT_REVIEW = 'Field Technician Review';

// Site map upload accepts the same document types as the public applicant portal.
const SITEMAP_ACCEPT =
  'application/pdf,image/png,image/jpeg,image/gif,image/tiff,.pdf,.png,.jpg,.jpeg,.gif,.tif,.tiff';

function StatusLine({ done, doneText, pendingText }) {
  return (
    <div className="status-line">
      <span className={`status-line__badge ${done ? 'status-line__badge--ok' : 'status-line__badge--pending'}`}>
        {done ? '\u2713' : '\u26A0'}
      </span>
      <span>{done ? doneText : pendingText}</span>
    </div>
  );
}

function todayIso() {
  return new Date().toISOString().slice(0, 10);
}

const ApprovalWizard = forwardRef(function ApprovalWizard({ application, appId, onUpdated, onPaymentUpdated }, ref) {
  const showToast = useToast();
  const { user } = useAuth();
  const [currentStep, setCurrentStep] = useState(0);
  const [collapsed, setCollapsed] = useState(false);
  const rootRef = useRef(null);

  // ---- Payment ----
  const [payment, setPayment] = useState(null);
  const [paymentType, setPaymentType] = useState('CHECK');
  const [fineAmount, setFineAmount] = useState('0.00');
  // Non-card receipt details (CHECK/CASH), mirroring the legacy intra UpdateAppServlet pay form.
  const [checkNum, setCheckNum] = useState('');
  const [acctName, setAcctName] = useState('');
  const [paidAmount, setPaidAmount] = useState('0.00');
  const [paymentReviewed, setPaymentReviewed] = useState(false);
  const [showLightbox, setShowLightbox] = useState(false);

  // Keep the read-only "Payment Information" section (rendered by the parent, below this wizard) in
  // sync whenever the reviewer changes payment here — the section's "Update Payment" button now
  // routes into this Payment step instead of opening its own modal.
  const propagatePayment = (p) => {
    setPayment(p);
    if (typeof onPaymentUpdated === 'function') onPaymentUpdated(p);
  };

  // ---- Sitemap ----
  const [sitemapReceived, setSitemapReceived] = useState(
    Boolean(application?.sitemapReceivedDate || application?.sitemapFilename),
  );
  const [sitemapDate, setSitemapDate] = useState(
    application?.sitemapReceivedDate ? String(application.sitemapReceivedDate).slice(0, 10) : todayIso(),
  );
  const [sitemapFilename, setSitemapFilename] = useState(application?.sitemapFilename || '');
  const [uploadingSitemap, setUploadingSitemap] = useState(false);
  const sitemapFileRef = useRef(null);

  // ---- Site visit type ----
  const [siteVisitType, setSiteVisitType] = useState(application?.siteVisitType || '');

  // ---- Inspections / review ----
  const [inspectors, setInspectors] = useState([]);
  const [inspections, setInspections] = useState([]);
  const [reviewAck, setReviewAck] = useState(false);

  // ---- Approve ----
  // Mirror the legacy Java intra: the approver is the signed-in staff member (Logon.getUser), not a
  // value typed on the page. Pre-fill from the authenticated user so the Approve button is enabled as
  // soon as the preconditions are met (Java has no "approved by" input and never gates on one).
  const [approvedBy, setApprovedBy] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState(null);

  useEffect(() => {
    const signedInUser = user?.name || user?.username || '';
    if (signedInUser) setApprovedBy((cur) => (cur ? cur : signedInUser));
  }, [user]);

  useEffect(() => {
    getPayment(appId)
      .then((p) => {
        setPayment(p);
        if (p?.paymentType) setPaymentType(p.paymentType);
        if (p?.fineAmount != null) setFineAmount(Number(p.fineAmount).toFixed(2));
        if (p?.checkNum) setCheckNum(p.checkNum);
        if (p?.acctName) setAcctName(p.acctName);
        if (p?.paidAmount != null && Number(p.paidAmount) > 0) setPaidAmount(Number(p.paidAmount).toFixed(2));
      })
      .catch(() => setPayment(null));
    getInspectors().then(setInspectors).catch(() => setInspectors([]));
    getInspections(appId).then((d) => setInspections(d || [])).catch(() => setInspections([]));
  }, [appId]);

  // A payment satisfies the approval checklist once it is either successful (PAID) or vaulted and
  // awaiting the approver's action (PEND = "Pending Approval"). In both cases the payment info is on
  // file, so the step is filled green with a tick. For CC/PEND the real charge happens on approval.
  const paymentStatus = String(payment?.statusCode || '').toUpperCase();
  const paymentOnFile = ['PAID', 'PAYD', 'PEND', 'PENDING', 'EXMPT'].includes(paymentStatus);
  const paymentComplete = paymentReviewed || paymentOnFile;
  const inspectionsComplete = siteVisitType === VISIT_REVIEW ? reviewAck : inspections.length > 0;

  // Derived totals for the final "Application Approval" summary (mirrors the legacy Java intra
  // proc_approval.jsp). "Application Original Total" is the recalculated permit fee (fee × number of
  // wells, Σ drill_count) computed live from the current works — the same math the API charges — so
  // an added well is reflected here without waiting for the stored auth_amount to catch up. The
  // recalculated total then adds the live fine input; when it differs from the base the figure is
  // shown in red and prefixed "Adjusted", exactly like the Java page.
  const computedBase = baseFee(application?.works);
  const originalTotal = computedBase > 0 ? computedBase : Number(payment?.authAmount || 0);
  const fineTotal = Number(fineAmount) || 0;
  const recalcTotal = originalTotal + fineTotal;
  const totalsMatch = fineTotal === 0;
  const receivedTotal = Number(payment?.paidAmount || 0);
  const isCcPayment = (payment?.paymentType || '') === 'CC';
  const isPaidStatus = ['PAID', 'PAYD'].includes(paymentStatus);
  // Once the application is approved (or the card is already charged) the payment can no longer be
  // changed — the vault/charge is final. Mirrors the backend guard in PaymentService.UpdateDetails.
  const applicationApproved = isApprovedStatus(application?.statusCode);
  const paymentLocked = applicationApproved || isPaidStatus;
  const payTotalLabel = `${totalsMatch ? '' : 'Adjusted '}${isPaidStatus ? 'Paid' : 'Pay'} Total`;

  // The per-work list the Conditions gate tracks: every non-cancelled work requesting a permit. A work
  // stays "Pending Conditions" (PENDC) until conditions are applied to it (advancing it to PEND); the
  // application shows "Pending Conditions" while ANY work is still PENDC (retrieveStatusNum uses the
  // least-advanced work). Conditions are applied from the "Work Requesting Permit" section below.
  const conditionWorks = (application?.works || [])
    .filter((w) => (w.statusCode || '').toUpperCase() !== 'CAN');

  // The Conditions gate is complete once every work has moved past "Pending Conditions" (PENDC).
  const conditionsComplete =
    conditionWorks.length > 0 &&
    conditionWorks.every((w) => (w.statusCode || '').toUpperCase() !== 'PENDC');

  const steps = useMemo(() => ([
    { key: 'sitemap', label: 'Sitemap', complete: sitemapReceived },
    { key: 'conditions', label: 'Conditions', complete: conditionsComplete },
    { key: 'payment', label: 'Payment', complete: Boolean(paymentComplete) },
    { key: 'visitType', label: 'Site Visit Type', complete: Boolean(siteVisitType) },
    { key: 'inspections', label: 'Inspections / Review', complete: Boolean(inspectionsComplete) },
  ]), [sitemapReceived, conditionsComplete, paymentComplete, siteVisitType, inspectionsComplete]);

  const allComplete = steps.every((s) => s.complete);
  const completeCount = steps.filter((s) => s.complete).length;
  const currentKey = steps[currentStep].key;

  const goToStep = (i) => {
    setCurrentStep(Math.max(0, Math.min(steps.length - 1, i)));
  };

  // Let a sibling drive this inline wizard to a given gate: expand the panel if collapsed, jump to
  // that step, and scroll into view.
  useImperativeHandle(ref, () => ({
    goToStep: (i) => {
      setCollapsed(false);
      setCurrentStep(Math.max(0, Math.min(steps.length - 1, i)));
      requestAnimationFrame(() => {
        rootRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      });
    },
    goToStepKey: (key) => {
      const i = steps.findIndex((s) => s.key === key);
      if (i < 0) return;
      setCollapsed(false);
      setCurrentStep(i);
      requestAnimationFrame(() => {
        rootRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      });
    },
  }), [steps]);

  // Conditions are applied from the "Work Requesting Permit" section below (per-work "Apply / Edit
  // Conditions" popup). The Conditions gate just scrolls the reviewer down to that section.
  const scrollToWorks = () => {
    document.getElementById('works-section')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  // Persist the approval-workflow columns on APPLICATION_INFO. The wizard owns both values and sends
  // the complete picture each call so the API can set them authoritatively (site_visit_type + sitemap date).
  const persistApprovalDetails = async (next = {}) => {
    const visitType = next.siteVisitType !== undefined ? next.siteVisitType : siteVisitType;
    const received = next.sitemapReceived !== undefined ? next.sitemapReceived : sitemapReceived;
    const receivedDate = next.sitemapDate !== undefined ? next.sitemapDate : sitemapDate;
    try {
      await updateApprovalDetails(appId, {
        siteVisitType: visitType || null,
        sitemapReceivedDate: received ? (receivedDate || todayIso()) : null,
      });
      return true;
    } catch {
      showToast('That change could not be saved. Please try again.', 'error');
      return false;
    }
  };

  // Staff-side site map upload: used when the applicant never provided a map. Scans + delivers the
  // file, records it on APPLICATION_INFO, then refreshes the parent so the filename propagates.
  const handleSitemapUpload = async () => {
    const input = sitemapFileRef.current;
    const file = input?.files?.[0];
    if (!file) {
      showToast('Choose a site map file to upload first.', 'error');
      return;
    }
    setUploadingSitemap(true);
    try {
      const result = await uploadSitemap(appId, file);
      setSitemapFilename(result?.fileName || file.name);
      setSitemapReceived(true);
      setSitemapDate(todayIso());
      if (input) input.value = '';
      try {
        const fresh = await getApplication(appId);
        if (fresh && typeof onUpdated === 'function') onUpdated(fresh);
      } catch {
        /* non-fatal: local state already reflects the upload */
      }
      showToast('Site map uploaded and recorded.', 'success');
    } catch (err) {
      const status = err?.response?.status;
      const detail = err?.response?.data;
      const message =
        status === 502
          ? 'The virus scan service is unavailable. Please try again later.'
          : typeof detail === 'string' && detail
            ? detail
            : 'The site map could not be uploaded. Check the file type and try again.';
      showToast(message, 'error');
    } finally {
      setUploadingSitemap(false);
    }
  };

  const handleApprove = async () => {
    setSubmitting(true);
    const approver = (approvedBy && approvedBy.trim()) || user?.name || user?.username || '';
    try {
      // Send the assessed fine with the approval so the server can stamp it on the payment record
      // before it generates the emailed permit/receipt — otherwise the attached PDF's total would
      // omit a fine that is only persisted later at CC charge time.
      const approval = await approveApplication(appId, { approvedBy: approver, notes: '', fineAmount: fineTotal });
      let charged = null;
      if (payment && payment.paymentType === 'CC' && payment.statusCode === 'PEND') {
        try {
          charged = await chargePayment({
            appId,
            captureAmount: recalcTotal,
            fineAmount: fineTotal,
            approvedBy: approver,
          });
          propagatePayment(charged);
          showToast(`Card charged $${Number(charged.paidAmount || 0).toFixed(2)} — ${formatStatus(charged.statusCode)}.`, 'success');
        } catch {
          showToast('Application approved, but the stored card could not be charged. Retry from the Payment page.', 'warning');
        }
      }
      setResult({ ...approval, charged });
      // Reload the application so the parent header reflects the approved status (status pill,
      // "View Permit/Reprint", Cancel hidden) — mirrors the sitemap-upload refresh pattern.
      try {
        const fresh = await getApplication(appId);
        if (fresh && typeof onUpdated === 'function') onUpdated(fresh);
      } catch {
        /* non-fatal: the result panel already confirms approval */
      }
      showToast(`Application ${approval.appId || appId} approved.`, 'success');
    } catch {
      showToast('Approval could not be completed.', 'error');
    } finally {
      setSubmitting(false);
    }
  };

  if (result) {
    return (
      <div className="panel approval-wizard">
        <div className="approval-result">
          <div className="alert alert-success" style={{ marginBottom: 12 }}>
            <strong>Application {result.appId || appId} approved.</strong>
          </div>
          <p className="muted-text" style={{ marginTop: 0 }}>Receipt / permit number(s)</p>
          <div className="approval-result__receipt">
            {(result.permitNumbers && result.permitNumbers.length)
              ? result.permitNumbers.join(', ')
              : 'Pending assignment'}
          </div>
          {result.charged && (
            <p style={{ marginTop: 12 }}>
              Card charged: <strong>${Number(result.charged.paidAmount || 0).toFixed(2)}</strong>
              {' '}({formatStatus(result.charged.statusCode)})
            </p>
          )}
        </div>
      </div>
    );
  }

  const renderBody = () => {
    switch (currentKey) {
      case 'sitemap':
        return (
          <div className="wizard-body">
            <h3 className="wizard-body__title">Sitemap</h3>
            <p className="wizard-body__hint">Confirm the project site map has been received before approval.</p>
            <StatusLine
              done={sitemapReceived}
              doneText={`Sitemap received${sitemapFilename ? ` — ${sitemapFilename}` : ''}`}
              pendingText="Sitemap not yet received"
            />

            <div className="sitemap-upload">
              <label className="form-label" htmlFor="staffSitemapFile">
                Upload site map {sitemapFilename ? '(replace current file)' : '(if the applicant did not provide one)'}
              </label>
              <div className="sitemap-upload__row">
                <input
                  id="staffSitemapFile"
                  ref={sitemapFileRef}
                  type="file"
                  className="form-control"
                  accept={SITEMAP_ACCEPT}
                  disabled={uploadingSitemap}
                />
                <Button onClick={handleSitemapUpload} disabled={uploadingSitemap}>
                  {uploadingSitemap ? 'Uploading\u2026' : 'Upload site map'}
                </Button>
              </div>
              <p className="wizard-body__hint">
                The file is virus-scanned and delivered to the wells share, then recorded on the
                application (marks the site map as received).
              </p>
            </div>

            <label className="radio-label">
              <input
                type="checkbox"
                checked={sitemapReceived}
                onChange={async (e) => {
                  const checked = e.target.checked;
                  setSitemapReceived(checked);
                  const ok = await persistApprovalDetails({ sitemapReceived: checked });
                  if (ok) showToast(checked ? 'Sitemap marked as received.' : 'Sitemap received cleared.', 'success');
                }}
              />
              Sitemap received
            </label>
            {sitemapReceived && (
              <div className="form-row" style={{ marginTop: 12 }}>
                <label className="form-label" htmlFor="sitemapDate">Date received</label>
                <div className="form-col">
                  <input
                    id="sitemapDate"
                    type="date"
                    className="form-control"
                    style={{ maxWidth: 220 }}
                    value={sitemapDate}
                    onChange={(e) => setSitemapDate(e.target.value)}
                    onBlur={async () => {
                      const ok = await persistApprovalDetails({ sitemapDate });
                      if (ok) showToast('Sitemap date saved.', 'success');
                    }}
                  />
                </div>
              </div>
            )}
          </div>
        );

      case 'conditions':
        return (
          <div className="wizard-body">
            <h3 className="wizard-body__title">Permit conditions</h3>
            {!conditionsComplete && conditionWorks.length > 0 && (
              <div className="wizard-cond-banner">
                This application stays <strong>Pending Conditions</strong> until conditions are applied
                to every work requesting a permit.
              </div>
            )}
            <p className="wizard-body__hint">
              Conditions are applied per work in the <strong>Work Requesting Permit</strong> section
              below. Each work stays <em>Pending Conditions</em> until its conditions are applied.
            </p>
            <StatusLine
              done={conditionsComplete}
              doneText="Conditions applied to every work"
              pendingText="One or more works are still Pending Conditions"
            />

            {conditionWorks.length === 0 ? (
              <p className="muted-text" style={{ marginTop: 12 }}>No works to apply conditions to.</p>
            ) : (
              <ul className="wizard-cond-status">
                {conditionWorks.map((work) => {
                  const isPendc = (work.statusCode || '').toUpperCase() === 'PENDC';
                  const label = [
                    work.workCategoryDesc || work.workCategory,
                    work.workTypeDesc || work.workType,
                  ].filter(Boolean).join(' - ') || `Work #${work.workId}`;
                  return (
                    <li key={work.workId} className="wizard-cond-status__row">
                      <span className="wizard-cond-status__label">{label}</span>
                      <span className={statusPillClass(work.statusCode)}>{formatStatus(work.statusCode)}</span>
                      {isPendc ? (
                        <Button variant="default" onClick={scrollToWorks}>Apply conditions</Button>
                      ) : (
                        <span className="wizard-cond-status__done">{'\u2713'} Conditions applied</span>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        );

      case 'payment':
        return (
          <div className="wizard-body">
            <h3 className="wizard-body__title">Payment</h3>
            <p className="wizard-body__hint">Review the payment on file, update the method, or change the card on the vault.</p>
            {paymentLocked && (
              <div className="alert" style={{ background: '#f0fdf4', color: '#166534', border: '1px solid #bbf7d0', marginBottom: 8 }}>
                This application is approved — the payment can no longer be changed.
              </div>
            )}
            <StatusLine
              done={Boolean(paymentComplete)}
              doneText={
                paymentStatus === 'PAID' || paymentStatus === 'PAYD'
                  ? 'Payment successful'
                  : paymentStatus === 'PEND' || paymentStatus === 'PENDING'
                    ? formatStatus(payment.statusCode)
                    : 'Payment reviewed'
              }
              pendingText={payment ? `Pending approval (${formatStatus(payment.statusCode)})` : 'No payment on file'}
            />
            {payment && (
              <div className="payment-summary">
                <div className="payment-summary__row">
                  <span className="payment-summary__label">Payment Type</span>
                  <span className="payment-summary__value">{formatPaymentType(payment.paymentType)}</span>
                </div>
                <div className="payment-summary__row">
                  <span className="payment-summary__label">Status</span>
                  <span className="payment-summary__value"><span className="status-pill">{formatStatus(payment.statusCode)}</span></span>
                </div>
                <div className="payment-summary__row">
                  <span className="payment-summary__label">Total Amount Due</span>
                  <span className="payment-summary__value">${originalTotal.toFixed(2)}</span>
                </div>
                <div className="payment-summary__row">
                  <span className="payment-summary__label">Customer ID</span>
                  <span className="payment-summary__value">{payment.authIdEncr || 'N/A'}</span>
                </div>
              </div>
            )}
            <div className="form-row">
              <span className="form-label">Payment method</span>
              <div className="radio-group">
                {[['CHECK', 'Check'], ['CASH', 'Cash'], ['EXMPT', 'Exempt'], ['CC', 'Credit Card']].map(([code, label]) => (
                  <label className="radio-label" key={code}>
                    <input type="radio" name="payType" value={code} checked={paymentType === code} disabled={paymentLocked} onChange={() => {
                      setPaymentType(code);
                      // Prefill the amount received with the amount due so staff confirm the full payment
                      // (legacy intra requires Amount Received == Amount Due for check/cash).
                      if ((code === 'CHECK' || code === 'CASH') && (!paidAmount || Number(paidAmount) === 0)) {
                        setPaidAmount((Number(recalcTotal) || 0).toFixed(2));
                      }
                    }} />
                    {label}
                  </label>
                ))}
              </div>
            </div>
            {paymentType === 'CHECK' && (
              <>
                <div className="form-row">
                  <label className="form-label" htmlFor="checkNum">Check number received <span className="mandatory" aria-hidden="true">*</span></label>
                  <div className="form-col">
                    <input id="checkNum" className="form-control" style={{ maxWidth: 200 }} maxLength={20} inputMode="numeric" required aria-required="true" value={checkNum} disabled={paymentLocked} onChange={(e) => setCheckNum(e.target.value.replace(/[^0-9]/g, ''))} />
                  </div>
                </div>
                <div className="form-row">
                  <label className="form-label" htmlFor="acctName">Name on account <span className="mandatory" aria-hidden="true">*</span></label>
                  <div className="form-col">
                    <input id="acctName" className="form-control" style={{ maxWidth: 320 }} maxLength={50} required aria-required="true" value={acctName} disabled={paymentLocked} onChange={(e) => setAcctName(e.target.value)} />
                  </div>
                </div>
                <div className="form-row">
                  <label className="form-label" htmlFor="paidAmount">Check amount received <span className="mandatory" aria-hidden="true">*</span></label>
                  <div className="form-col">
                    <input id="paidAmount" className="form-control" style={{ maxWidth: 200 }} inputMode="decimal" required aria-required="true" value={paidAmount} disabled={paymentLocked} onChange={(e) => setPaidAmount(e.target.value)} />
                    <span className="wizard-body__hint">Must equal amount due: ${recalcTotal.toFixed(2)}</span>
                  </div>
                </div>
              </>
            )}
            {paymentType === 'CASH' && (
              <>
                <div className="form-row">
                  <label className="form-label" htmlFor="acctName">Name of payer <span className="mandatory" aria-hidden="true">*</span></label>
                  <div className="form-col">
                    <input id="acctName" className="form-control" style={{ maxWidth: 320 }} maxLength={50} required aria-required="true" value={acctName} disabled={paymentLocked} onChange={(e) => setAcctName(e.target.value)} />
                  </div>
                </div>
                <div className="form-row">
                  <label className="form-label" htmlFor="paidAmount">Cash amount received <span className="mandatory" aria-hidden="true">*</span></label>
                  <div className="form-col">
                    <input id="paidAmount" className="form-control" style={{ maxWidth: 200 }} inputMode="decimal" required aria-required="true" value={paidAmount} disabled={paymentLocked} onChange={(e) => setPaidAmount(e.target.value)} />
                    <span className="wizard-body__hint">Must equal amount due: ${recalcTotal.toFixed(2)}</span>
                  </div>
                </div>
              </>
            )}
            {paymentType === 'EXMPT' && (
              <p className="wizard-body__hint">No payment is collected for an exempt application; the fee is waived.</p>
            )}
            <div className="form-row">
              <label className="form-label" htmlFor="fineAmount">Fine amount included</label>
              <div className="form-col">
                <input id="fineAmount" className="form-control" style={{ maxWidth: 200 }} value={fineAmount} disabled={paymentLocked} onChange={(e) => setFineAmount(e.target.value)} />
              </div>
            </div>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginTop: 6 }}>
              <Button disabled={paymentLocked} onClick={async () => {
                // Client-side mandatory-field checks mirroring the legacy intra required fields.
                if (paymentType === 'CHECK') {
                  if (!checkNum.trim()) { showToast('Check number received is required.', 'error'); return; }
                  if (!acctName.trim()) { showToast('Name on account is required.', 'error'); return; }
                  if (!paidAmount || Number(paidAmount) <= 0) { showToast('Check amount received is required.', 'error'); return; }
                } else if (paymentType === 'CASH') {
                  if (!acctName.trim()) { showToast('Name of payer is required.', 'error'); return; }
                  if (!paidAmount || Number(paidAmount) <= 0) { showToast('Cash amount received is required.', 'error'); return; }
                }
                try {
                  const payload = { paymentType, fineAmount: Number(fineAmount) || 0 };
                  if (paymentType === 'CHECK') {
                    payload.checkNum = checkNum.trim();
                    payload.acctName = acctName.trim();
                    payload.paidAmount = Number(paidAmount) || 0;
                  } else if (paymentType === 'CASH') {
                    payload.acctName = acctName.trim();
                    payload.paidAmount = Number(paidAmount) || 0;
                  }
                  const saved = await updatePayment(appId, payload);
                  propagatePayment(saved);
                  if (saved?.fineAmount != null) setFineAmount(Number(saved.fineAmount).toFixed(2));
                  setCheckNum(saved?.checkNum || '');
                  setAcctName(saved?.acctName || '');
                  if (saved?.paidAmount != null && Number(saved.paidAmount) > 0) setPaidAmount(Number(saved.paidAmount).toFixed(2));
                  setPaymentReviewed(true);
                  showToast('Payment updated.', 'success');
                } catch (err) {
                  const msg = err?.response?.data?.message || 'Payment could not be updated. Please try again.';
                  showToast(msg, 'error');
                }
              }}
              >
                Update Payment
              </Button>
              {paymentType === 'CC' && !paymentLocked && (
                <Button variant="secondary" onClick={() => setShowLightbox((v) => !v)}>
                  {showLightbox ? 'Cancel card change' : 'Update and Change Card'}
                </Button>
              )}
            </div>
            {showLightbox && paymentType === 'CC' && !paymentLocked && (
              <div className="panel" style={{ marginTop: 16, border: '2px solid var(--btn-primary-bg)' }}>
                <IntelliPayLightbox
                  appId={appId}
                  amount={payment?.authAmount || 0}
                  existingCustomerId={payment?.authIdEncr}
                  autoOpen
                  onSuccess={() => {
                    setPaymentReviewed(true);
                    setShowLightbox(false);
                    getPayment(appId).then(propagatePayment).catch(() => {});
                  }}
                />
              </div>
            )}
          </div>
        );

      case 'visitType':
        return (
          <div className="wizard-body">
            <h3 className="wizard-body__title">Site visit type</h3>
            <p className="wizard-body__hint">Choose how the site will be verified before the permit is issued.</p>
            <StatusLine done={Boolean(siteVisitType)} doneText={`Selected: ${siteVisitType === VISIT_REVIEW ? VISIT_REVIEW : VISIT_INSPECTION}`} pendingText="No site visit type selected" />
            <div className="radio-group" style={{ flexDirection: 'column', gap: 12 }}>
              <label className="radio-label">
                <input type="radio" name="visitType" value={VISIT_INSPECTION} checked={siteVisitType === VISIT_INSPECTION} onChange={async () => { setSiteVisitType(VISIT_INSPECTION); const ok = await persistApprovalDetails({ siteVisitType: VISIT_INSPECTION }); if (ok) showToast('Site visit type saved.', 'success'); }} />
                Inspection
              </label>
              <label className="radio-label">
                <input type="radio" name="visitType" value={VISIT_REVIEW} checked={siteVisitType === VISIT_REVIEW} onChange={async () => { setSiteVisitType(VISIT_REVIEW); const ok = await persistApprovalDetails({ siteVisitType: VISIT_REVIEW }); if (ok) showToast('Site visit type saved.', 'success'); }} />
                Field Technician Review
              </label>
            </div>
          </div>
        );

      case 'inspections':
      default:
        return (
          <div className="wizard-body">
            <h3 className="wizard-body__title">Inspections / Review</h3>
            {siteVisitType === VISIT_REVIEW ? (
              <>
                <p className="wizard-body__hint">Confirm the field technician review has been completed.</p>
                <StatusLine done={reviewAck} doneText="Field technician review completed" pendingText="Review not yet completed" />
                <label className="radio-label">
                  <input type="checkbox" checked={reviewAck} onChange={(e) => setReviewAck(e.target.checked)} />
                  Field technician review completed
                </label>
              </>
            ) : (
              <>
                <p className="wizard-body__hint">Schedule a site visit from the inspection calendar below.</p>
                <StatusLine done={inspections.length > 0} doneText={`${inspections.length} inspection(s) scheduled`} pendingText="No inspection scheduled yet" />
                <InspectionSchedule
                  appId={appId}
                  inspectors={inspectors}
                  inspections={inspections}
                  onChanged={setInspections}
                />
              </>
            )}

            {/* Final "Application Approval" summary — mirrors the legacy Java intra proc_approval.jsp
                shown before pressing APPROVE NOW (ID, Status, original + recalculated totals, payment
                type). The approver is the signed-in staff member, so there is no "approved by" input. */}
            <div className="panel" style={{ marginTop: 24, background: '#f8fbff' }}>
              <h4 style={{ marginTop: 0 }}>Application Approval</h4>
              {!allComplete && (
                <div className="alert" style={{ background: '#fff7ed', color: '#b45309', border: '1px solid #fed7aa' }}>
                  Complete all five preconditions above to enable approval.
                </div>
              )}
              <div className="payment-summary">
                <div className="payment-summary__row">
                  <span className="payment-summary__label">ID</span>
                  <span className="payment-summary__value">{appId}</span>
                </div>
                <div className="payment-summary__row">
                  <span className="payment-summary__label">Status</span>
                  <span className="payment-summary__value">
                    <span className="status-pill">{formatStatus(application?.statusCode)}</span>
                  </span>
                </div>
                <div className="payment-summary__row">
                  <span className="payment-summary__label">Application Original Total</span>
                  <span className="payment-summary__value">${originalTotal.toFixed(2)}</span>
                </div>
                <div className="payment-summary__row">
                  <span className="payment-summary__label">Fine Amount</span>
                  <span className="payment-summary__value" style={fineTotal > 0 ? { color: '#dc2626' } : undefined}>
                    ${fineTotal.toFixed(2)}
                  </span>
                </div>
                <div className="payment-summary__row">
                  <span className="payment-summary__label">Payment Type</span>
                  <span className="payment-summary__value">{formatPaymentType(payment?.paymentType)}</span>
                </div>
                {isCcPayment ? (
                  <div className="payment-summary__row">
                    <span className="payment-summary__label">{payTotalLabel}</span>
                    <span className="payment-summary__value" style={totalsMatch ? undefined : { color: '#dc2626' }}>
                      ${recalcTotal.toFixed(2)}
                    </span>
                  </div>
                ) : (
                  <>
                    <div className="payment-summary__row">
                      <span className="payment-summary__label">Received Total</span>
                      <span className="payment-summary__value">${receivedTotal.toFixed(2)}</span>
                    </div>
                    {!totalsMatch && (
                      <div className="payment-summary__row">
                        <span className="payment-summary__label">{payTotalLabel}</span>
                        <span className="payment-summary__value" style={{ color: '#dc2626' }}>
                          ${recalcTotal.toFixed(2)}
                        </span>
                      </div>
                    )}
                  </>
                )}
              </div>
            </div>
          </div>
        );
    }
  };

  return (
    <div className="panel approval-wizard" ref={rootRef}>
      <button
        type="button"
        className="approval-wizard__toggle"
        aria-expanded={!collapsed}
        onClick={() => setCollapsed((c) => !c)}
      >
        <span className="approval-wizard__toggle-title">
          Approval Gates
          <span className={`approval-wizard__summary${allComplete ? ' approval-wizard__summary--done' : ''}`}>
            {completeCount} of {steps.length} complete
          </span>
        </span>
        <span className="approval-wizard__chevron" aria-hidden="true">{collapsed ? '\u25BC' : '\u25B2'}</span>
      </button>

      {!collapsed && (
      <div className="approval-wizard__content">
      <nav className="wizard-steps" aria-label="Approval progress">
        <div className="wizard-steps-list">
          {steps.map((step, index) => (
            <div
              key={step.key}
              className={`wizard-step${index === currentStep ? ' wizard-step--active' : ''}${step.complete && index !== currentStep ? ' wizard-step--complete' : ''}`}
              role="button"
              tabIndex={0}
              aria-current={index === currentStep ? 'step' : undefined}
              onClick={() => goToStep(index)}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); goToStep(index); } }}
            >
              <span className="step-num" aria-hidden="true">{step.complete ? '\u2713' : index + 1}</span>
              <strong className="step-label">{step.label}</strong>
            </div>
          ))}
        </div>
      </nav>

      {renderBody()}

      <div className="wizard-actions">
        <Button variant="default" onClick={() => goToStep(currentStep - 1)} disabled={currentStep === 0}>Back</Button>
        <div className="wizard-actions__right">
          {currentStep < steps.length - 1 ? (
            <Button onClick={() => goToStep(currentStep + 1)}>Continue</Button>
          ) : (
            <Button onClick={handleApprove} disabled={!allComplete || submitting || applicationApproved}>
              {applicationApproved ? 'Approved' : submitting ? 'Approving…' : 'Approve Now'}
            </Button>
          )}
        </div>
      </div>
      </div>
      )}
    </div>
  );
});

export default ApprovalWizard;
