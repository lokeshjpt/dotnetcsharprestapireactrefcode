const paymentTypes = [
  { code: 'CC', label: 'Credit Card' },
  { code: 'CHECK', label: 'Check' },
  { code: 'EXMPT', label: 'Exempt' },
];

// Authoritative descriptions from EEAOWN.PAYMENT_TYPES, plus the 'CK' alias the intra app uses.
// Cash is intra-only and intentionally omitted here so it can never surface in the public app.
const paymentTypeLabels = {
  CC: 'Credit Card',
  CHECK: 'Check',
  CK: 'Check',
  EXMPT: 'Exempt',
  MC: 'Master Card',
  VISA: 'VISA',
};

// Resolve a payment-type code to its human description; falls back to the raw code.
export function formatPaymentType(code) {
  if (!code) return '';
  return paymentTypeLabels[code] || paymentTypeLabels[String(code).toUpperCase()] || code;
}

export default paymentTypes;
