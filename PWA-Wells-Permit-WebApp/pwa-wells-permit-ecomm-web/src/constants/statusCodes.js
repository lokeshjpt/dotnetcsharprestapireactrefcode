// Authoritative status descriptions from EEAOWN.status_codes (source of truth for the
// legacy JBoss app). Extra aliases cover codes the rewrite emits (e.g. PENDING, APPR).
const statusCodes = {
  APPRV: 'Approved',
  CAN: 'Cancelled',
  COMPL: 'Completed Work',
  EXMPT: 'Exempt',
  HOLD: 'Hold',
  ICOMP: 'Inspection Completed',
  IPEND: 'Inspection Pending',
  IRSRV: 'Inspection Date Reserved',
  IWAIV: 'Inspection Waived',
  PAID: 'Payment Successful',
  PAYFL: 'Payment Failed',
  PCLSD: 'Permit Closed',
  PDWR: 'Pending WCR',
  PEND: 'Pending Approval',
  PENDC: 'Pending Conditions',
  PENDP: 'Pending Payment',
  PENDS: 'Pending Sitemap',
  PEXPD: 'Permit Expired',
  PGEO: 'Pending Geolog',
  POPEN: 'Permit Open',
  // Aliases used across the rewrite:
  APRVD: 'Approved',
  APPR: 'Approved',
  APPROVED: 'Approved',
  PENDING: 'Pending Approval',
  PAYD: 'Payment Successful',
  ISSUED: 'Permit Issued',
  DECL: 'Declined',
};

// Resolve a status code to its human description; falls back to the raw code.
export function formatStatus(code) {
  if (!code) return '';
  return statusCodes[code] || statusCodes[String(code).toUpperCase()] || code;
}

// Status codes that mean the permit has been approved (green pill).
const APPROVED_STATUSES = new Set(['APPRV', 'APRVD', 'APPR', 'APPROVED']);
export function isApprovedStatus(code) {
  return APPROVED_STATUSES.has(String(code || '').toUpperCase());
}

// True when the application is still awaiting its required site map (offer an upload option).
export function isPendingSitemap(code) {
  return String(code || '').toUpperCase() === 'PENDS';
}

// CSS modifier for the status pill so approved shows green.
export function statusPillClass(code) {
  return `status-pill${isApprovedStatus(code) ? ' status-pill--approved' : ''}`;
}

export default statusCodes;
