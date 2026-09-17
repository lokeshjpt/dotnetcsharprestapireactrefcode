import { useEffect, useState } from 'react';
import DetailSection, { DetailField } from '../../../components/DetailSection/DetailSection';
import Button from '../../../components/UI/Button';
import { getPayment } from '../../../api/paymentApi';
import { formatStatus, isApprovedStatus } from '../../../constants/statusCodes';
import { formatPaymentType } from '../../../constants/paymentTypes';
import { recalculatedTotal } from '../../../utils/feeCalc';

function money(n) {
  return `$${Number(n || 0).toFixed(2)}`;
}

function PaymentSection({ appId, payment: paymentProp, application, onEditPayment }) {
  const [fetched, setFetched] = useState(null);
  const payment = paymentProp ?? fetched;

  useEffect(() => {
    if (paymentProp) return undefined;
    let active = true;
    getPayment(appId)
      .then((p) => { if (active) setFetched(p); })
      .catch(() => { if (active) setFetched(null); });
    return () => { active = false; };
  }, [appId, paymentProp]);

  const appStatus = String(application?.statusCode || '').toUpperCase();
  const canEdit = !!application
    && appStatus !== 'CAN'
    && !isApprovedStatus(application.statusCode);

  const isCheckPayment = payment
    && ['CHECK', 'CK'].includes(String(payment.paymentType || '').toUpperCase());
  const isCashPayment = payment
    && ['CASH'].includes(String(payment.paymentType || '').toUpperCase());
  const acctNameLabel = isCashPayment ? 'Name of Payer' : 'Name on Account';

  // "Total Amount Due" mirrors the legacy search_detail.jsp: the live recalculated charge
  // (Σ current non-cancelled works + service charge + fine), not the stored auth_amount, so adding
  // works/wells updates it here too. It is flagged red when an already-recorded paid amount no longer
  // matches, and the included fine is called out when present.
  const works = application?.works || [];
  const serviceCharge = Number(payment?.serviceCharge) || 0;
  const fine = Number(payment?.fineAmount) || 0;
  const isExempt = String(payment?.paymentType || '').toUpperCase() === 'EXMPT';
  const amountDue = works.length
    ? recalculatedTotal(works, serviceCharge, fine)
    : Number(payment?.authAmount || 0);
  const paidAmount = Number(payment?.paidAmount) || 0;
  const dueMismatch = paidAmount > 0 && paidAmount.toFixed(2) !== amountDue.toFixed(2);

  return (
    <DetailSection
      title="Payment Information"
      actions={canEdit ? (
        <Button variant="default" onClick={() => onEditPayment && onEditPayment()}>Update Payment</Button>
      ) : null}
    >
      <div className="detail-col">
        <DetailField label="Payment Type">
          {payment ? formatPaymentType(payment.paymentType) : null}
        </DetailField>
        <DetailField label="Auth">{payment?.authIdEncr}</DetailField>
        <DetailField label="Total Amount Due">
          {payment ? (
            <>
              <span className={dueMismatch ? 'amount-adjusted' : undefined}>
                {isExempt ? 'EXEMPT' : money(amountDue)}
              </span>
              {fine > 0 && <em className="fine-note"> Fine included: {money(fine)}</em>}
            </>
          ) : null}
        </DetailField>
        {isCheckPayment && <DetailField label="Check #">{payment?.checkNum}</DetailField>}
        {(isCheckPayment || isCashPayment) && payment?.acctName
          && <DetailField label={acctNameLabel}>{payment.acctName}</DetailField>}
      </div>
      <div className="detail-col">
        <DetailField label="Amount Paid">
          {payment ? money(payment.paidAmount) : null}
        </DetailField>
        <DetailField label="Fine Amount">
          {payment ? money(payment.fineAmount) : null}
        </DetailField>
        <DetailField label="Payment Status">
          {payment ? formatStatus(payment.statusCode) : null}
        </DetailField>
      </div>
    </DetailSection>
  );
}

export default PaymentSection;
