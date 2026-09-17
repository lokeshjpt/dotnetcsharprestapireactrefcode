import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { approveApplication } from '../../api/approvalApi';
import { chargePayment, getPayment } from '../../api/paymentApi';
import InputField from '../../components/UI/InputField';
import Button from '../../components/UI/Button';
import { formatStatus } from '../../constants/statusCodes';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import './ApprovalPage.css';

function ApprovalPage() {
  const { appId } = useParams();
  const navigate = useNavigate();
  const showToast = useToast();
  const [form, setForm] = useState({ approvedBy: '', notes: '' });
  const [payment, setPayment] = useState(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    getPayment(appId).then(setPayment).catch(() => setPayment(null));
  }, [appId]);

  const handleSubmit = async () => {
    setSubmitting(true);
    setError('');
    setMessage('');

    try {
      const result = await approveApplication(appId, form);
      let summary = `Approved ${result.appId}. Permit numbers: ${(result.permitNumbers || []).join(', ') || 'pending assignment'}.`;

      // Business rule: a CC card is only vaulted ($0, status PEND) at public submit.
      // The REAL fee is charged here in the intra app via the API charge path
      // (IIntelliPayGateway.ChargeStoredCustomerAsync over WebApiUrl), moving PEND -> PAID.
      if (payment && payment.paymentType === 'CC' && payment.statusCode === 'PEND') {
        try {
          const charged = await chargePayment({
            appId,
            captureAmount: Number(payment.authAmount || 0),
            approvedBy: form.approvedBy,
          });
          setPayment(charged);
          summary += ` Card charged: $${Number(charged.paidAmount || 0).toFixed(2)} (${formatStatus(charged.statusCode)}).`;
          showToast(`Card charged $${Number(charged.paidAmount || 0).toFixed(2)} — ${formatStatus(charged.statusCode)}.`, 'success');
        } catch {
          // Approval succeeded but the charge failed — surface a clear, non-fatal error.
          setError('Application approved, but the stored card could not be charged. Retry the charge from the Payment page.');
          showToast('Application approved, but the stored card could not be charged.', 'warning');
        }
      }

      setMessage(summary);
      showToast(`Application ${result.appId} approved.`, 'success');
      setTimeout(() => navigate(`/applications/${appId}`), 1500);
    } catch {
      setError('Approval could not be completed.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="page-shell approval-page">
      <div className="panel">
        <h1 className="page-title">Approve application {appId}</h1>
        <p className="page-subtitle">Finalize the review, assign permit numbers, and trigger applicant notification.</p>
        {payment && payment.paymentType === 'CC' && payment.statusCode === 'PEND' && (
          <div className="alert alert-info approval-page__message">
            Approving will charge the vaulted card ${Number(payment.authAmount || 0).toFixed(2)} and move it PEND &rarr; PAID.
          </div>
        )}
        <InputField id="approvedBy" label="Approved by" value={form.approvedBy} onChange={(event) => setForm((current) => ({ ...current, approvedBy: event.target.value }))} />
        <InputField id="notes" label="Approval notes" value={form.notes} onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} />
        <Button onClick={handleSubmit} disabled={submitting || !form.approvedBy.trim()}>{submitting ? 'Approving...' : 'Approve application'}</Button>
        {message && <div className="alert alert-success approval-page__message">{message}</div>}
        {error && <div className="alert alert-error approval-page__message">{error}</div>}
      </div>
    </div>
  );
}

export default ApprovalPage;
