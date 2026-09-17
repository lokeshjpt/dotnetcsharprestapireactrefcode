import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { chargePayment, getPayment } from '../../api/paymentApi';
import InputField from '../../components/UI/InputField';
import Button from '../../components/UI/Button';
import { formatStatus } from '../../constants/statusCodes';
import { formatPaymentType } from '../../constants/paymentTypes';
import { useToast } from '../../components/UI/Toaster/ToastProvider';

function PaymentInfoPage() {
  const { appId } = useParams();
  const showToast = useToast();
  const [payment, setPayment] = useState(null);
  const [approvedBy, setApprovedBy] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    getPayment(appId).then(setPayment).catch(() => setPayment(null));
  }, [appId]);

  const handleCharge = async () => {
    const updated = await chargePayment({ appId, captureAmount: Number(payment?.authAmount || 0), approvedBy });
    setPayment(updated);
    setMessage(`Payment status updated to ${formatStatus(updated.statusCode)}.`);
    showToast(`Payment updated to ${formatStatus(updated.statusCode)}.`, 'success');
  };

  return (
    <div className="page-shell">
      <div className="panel">
        <h1 className="page-title">Payment details</h1>
        <p className="page-subtitle">Review stored payment data and charge the card when approval conditions are met.</p>
        {payment ? (
          <table className="data-table">
            <tbody>
              <tr><th>Type</th><td>{formatPaymentType(payment.paymentType)}</td></tr>
              <tr><th>Status</th><td><span className="status-pill">{formatStatus(payment.statusCode)}</span></td></tr>
              <tr><th>Customer id</th><td>{payment.authIdEncr || 'N/A'}</td></tr>
              <tr><th>Authorized amount</th><td>${Number(payment.authAmount || 0).toFixed(2)}</td></tr>
              <tr><th>Paid amount</th><td>${Number(payment.paidAmount || 0).toFixed(2)}</td></tr>
            </tbody>
          </table>
        ) : (
          <div className="alert alert-error">No payment record was found for this application.</div>
        )}
        <div style={{ maxWidth: '360px', marginTop: '18px' }}>
          <InputField id="approvedByPayment" label="Processed by" value={approvedBy} onChange={(event) => setApprovedBy(event.target.value)} />
          <Button onClick={handleCharge} disabled={!payment}>Charge stored payment</Button>
        </div>
        {message && <div className="alert alert-success" style={{ marginTop: '16px' }}>{message}</div>}
      </div>
    </div>
  );
}

export default PaymentInfoPage;
