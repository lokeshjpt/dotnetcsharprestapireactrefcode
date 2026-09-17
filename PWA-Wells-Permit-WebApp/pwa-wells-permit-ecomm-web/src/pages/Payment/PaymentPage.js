import { useLocation, useNavigate, useParams } from 'react-router-dom';
import IntelliPayLightbox from './IntelliPayLightbox';
import { formatPaymentType } from '../../constants/paymentTypes';
import './PaymentPage.css';

function PaymentPage() {
  const { appId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const formData = location.state?.formData || {};
  const amount = Number(formData.estimatedFee || 0);

  return (
    <div className="page-shell payment-page">
      <div className="panel">
        <h1 className="page-title">Authorize payment</h1>
        <p className="page-subtitle">
          Use IntelliPay to store a customer payment token for application <strong>{appId}</strong>. The card will be charged when staff approves the permit.
        </p>
        <div className="payment-page__summary">
          <div>
            <span>Amount to authorize</span>
            <strong>${amount.toFixed(2)}</strong>
          </div>
          <div>
            <span>Payment type</span>
            <strong>{formatPaymentType(formData.paymentType || 'CC')}</strong>
          </div>
        </div>
        <IntelliPayLightbox
          appId={appId}
          amount={amount}
          existingCustomerId={formData.customerId}
          onSuccess={() => navigate(`/confirmation/${appId}`, { state: { application: { appId }, paymentType: formData.paymentType || 'CC' } })}
        />
      </div>
    </div>
  );
}

export default PaymentPage;
