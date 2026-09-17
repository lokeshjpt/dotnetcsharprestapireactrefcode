import { useParams } from 'react-router-dom';
import { Link } from 'react-router-dom';
import { useEffect, useRef, useState } from 'react';
import Loader from '../../components/Loader/Loader';
import Button from '../../components/UI/Button';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import useApplication from '../../hooks/useApplication';
import { getPayment } from '../../api/paymentApi';
import { cancelApplication } from '../../api/applicationApi';
import ApprovalWizard from './ApprovalWizard';
import ProjectInfo from './sections/ProjectInfo';
import PaymentSection from './sections/PaymentSection';
import WorksSection from './sections/WorksSection';
import DocumentsModal from './sections/DocumentsModal';
import NotesModal from './sections/NotesModal';
import ExtendDateModal from './sections/edit/ExtendDateModal';
import { formatStatus, statusPillClass, isApprovedStatus } from '../../constants/statusCodes';
import { authTotal, recalculatedTotal } from '../../utils/feeCalc';
import './ApplicationDetail.css';

function money(n) {
  return `$${Number(n || 0).toFixed(2)}`;
}


function ApplicationDetail() {
  const { appId } = useParams();
  const { application, loading, error, setApplication } = useApplication(appId);
  const [payment, setPayment] = useState(null);
  const [cancelling, setCancelling] = useState(false);
  const [showDocuments, setShowDocuments] = useState(false);
  const [showNotes, setShowNotes] = useState(false);
  const [showExtension, setShowExtension] = useState(false);
  const showToast = useToast();
  const wizardRef = useRef(null);

  useEffect(() => {
    let active = true;
    getPayment(appId)
      .then((p) => { if (active) setPayment(p); })
      .catch(() => { if (active) setPayment(null); });
    return () => { active = false; };
  }, [appId]);

  const handleCancel = async () => {
    if (!window.confirm('Cancel this application? This cannot be undone.')) return;
    setCancelling(true);
    try {
      const updated = await cancelApplication(appId);
      if (updated) setApplication(updated);
      showToast('Application cancelled.', 'success');
    } catch (err) {
      const msg = err?.response?.status === 409
        ? 'This application can no longer be cancelled.'
        : 'Unable to cancel the application.';
      showToast(msg, 'error');
    } finally {
      setCancelling(false);
    }
  };

  const openPermit = () => {
    window.open(`${window.location.pathname}#/permit/${appId}`, '_blank', 'noopener');
  };

  const openHazard = () => {
    window.open(`${window.location.pathname}#/hazard/${appId}`, '_blank', 'noopener');
  };

  if (loading) {
    return <div className="page-shell"><Loader message="Loading application detail..." /></div>;
  }

  if (error) {
    return <div className="page-shell"><div className="alert alert-error">{error}</div></div>;
  }

  if (!application) {
    return <div className="page-shell"><div className="alert alert-error">Application not found.</div></div>;
  }

  // Header money figures mirror the legacy search_detail.jsp summary card exactly — both are computed
  // live from the works, never from the stored auth_amount:
  //   Total    = ApplicationBean.getAuthAmt()          — every work (incl. cancelled) + service charge + fine
  //   Pay Total= ApplicationBean.getRecalculatedAppAmt — non-cancelled works only + service charge + fine
  // They diverge only once a work is cancelled; then the Pay Total is flagged "Adjusted" and shown red.
  const serviceCharge = Number(payment?.serviceCharge) || 0;
  const fineAmount = Number(payment?.fineAmount) || 0;
  const totalDue = authTotal(application?.works, serviceCharge, fineAmount);
  const recalcTotal = recalculatedTotal(application?.works, serviceCharge, fineAmount);
  const isExemptPay = String(payment?.paymentType || '').toUpperCase() === 'EXMPT';
  const isPaidPay = ['PAID', 'PAYD'].includes(String(payment?.statusCode || '').toUpperCase());
  const totalsMatch = totalDue.toFixed(2) === recalcTotal.toFixed(2);
  const payTotalAdjusted = !totalsMatch || isExemptPay;
  const payTotalLabel = `${totalsMatch ? '' : 'Adjusted '}${isPaidPay ? 'Paid' : 'Pay'} Total`;
  const statusLabel = formatStatus(application.statusCode);
  const submittedOn = application.addDate ? new Date(application.addDate).toLocaleString() : '—';

  const appStatus = String(application.statusCode || '').toUpperCase();
  const payStatus = String(payment?.statusCode || '').toUpperCase();
  const approved = isApprovedStatus(application.statusCode);
  // Mirror legacy search_detail.jsp: hide Cancel once the app is already cancelled/approved or
  // the payment has settled/failed.
  const canCancel = appStatus !== 'CAN' && !approved && payStatus !== 'PAID' && payStatus !== 'PAYFL';

  return (
    <div className="page-shell application-detail-page">
      <h1 className="page-title">Application {application.appId}</h1>
      <Link className="page-help-link" to="/help#detail">
        <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> How to review &amp; approve — help
      </Link>
      <div className="detail-header panel">
        <div className="detail-header__actions">
          <Button variant="default" onClick={() => setShowDocuments(true)}>Upload Documents</Button>
          <Button variant="default" onClick={() => setShowNotes(true)}>View/Add Notes</Button>
          <Button variant="default" onClick={openPermit}>
            {approved ? 'View Permit/Reprint' : 'Preview Permit'}
          </Button>
          {approved && (
            <Button variant="default" onClick={() => setShowExtension(true)}>New Extension</Button>
          )}
          {application.hazard && (
            <Button variant="default" onClick={openHazard}>Print Site Hazard</Button>
          )}
          {canCancel && (
            <Button variant="danger" onClick={handleCancel} disabled={cancelling}>
              {cancelling ? 'Cancelling…' : 'Cancel Application'}
            </Button>
          )}
        </div>
        <div className="detail-header__grid">
          <div className="detail-header__col">
            <div className="detail-header__row">
              <span className="detail-header__label">Application ID:</span>
              <span className="detail-header__value">{application.appId}</span>
            </div>
            <div className="detail-header__row">
              <span className="detail-header__label">Submitted On:</span>
              <span className="detail-header__value">{submittedOn} By: {application.addBy || 'INTERNET'}</span>
            </div>
          </div>
          <div className="detail-header__col">
            <div className="detail-header__row">
              <span className="detail-header__label">Status:</span>
              <span className="detail-header__value"><span className={statusPillClass(application.statusCode)}>{statusLabel}</span></span>
            </div>
          </div>
          <div className="detail-header__col detail-header__col--right">
            <div className="detail-header__row">
              <span className="detail-header__label">Total:</span>
              <span className="detail-header__value">{money(totalDue)}</span>
            </div>
            <div className="detail-header__row">
              <span className="detail-header__label">{payTotalLabel}:</span>
              <span className={`detail-header__value${payTotalAdjusted ? ' detail-header__value--adjusted' : ''}`}>
                {isExemptPay ? 'EXEMPT' : money(recalcTotal)}
              </span>
            </div>
          </div>
        </div>
      </div>

      <h2 className="detail-section-heading">Approval Status</h2>
      <ApprovalWizard ref={wizardRef} application={application} appId={application.appId} onUpdated={setApplication} onPaymentUpdated={setPayment} />

      <ProjectInfo application={application} onUpdated={setApplication} />
      <PaymentSection
        appId={application.appId}
        payment={payment}
        application={application}
        onEditPayment={() => wizardRef.current?.goToStepKey('payment')}
      />
      <div id="works-section">
        <WorksSection
          application={application}
          onUpdated={setApplication}
        />
      </div>

      {showDocuments && <DocumentsModal appId={application.appId} onClose={() => setShowDocuments(false)} />}
      {showNotes && <NotesModal appId={application.appId} onClose={() => setShowNotes(false)} />}
      {showExtension && (
        <ExtendDateModal
          application={application}
          onClose={() => setShowExtension(false)}
          onSaved={setApplication}
        />
      )}
    </div>
  );
}

export default ApplicationDetail;
