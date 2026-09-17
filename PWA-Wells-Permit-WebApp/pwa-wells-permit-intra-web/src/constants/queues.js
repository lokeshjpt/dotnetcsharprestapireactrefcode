// Prefilled-search queues ported from the legacy intra process_menu.jsp. Each maps to an
// APPLICATION_INFO.status_code the dashboard boxes filter by (via /api/applications/search).
export const QUEUES = [
  { statusCode: 'PEND', title: 'Pending Applications', description: 'Awaiting staff review, sitemap upload, and permit conditions.', accent: 'accent' },
  { statusCode: 'PENDS', title: 'Pending Sitemaps', description: 'Approved applications waiting on a site map upload.', accent: 'accent' },
  { statusCode: 'APPRV', title: 'Approved Permits', description: 'Approved & paid — reprint permits from this list.', accent: 'approved' },
  { statusCode: 'PAYFL', title: 'Failed Payments', description: 'Credit-card payment failures to update and retry.', accent: 'danger' },
  { statusCode: 'CAN', title: 'Cancelled Applications', description: 'Applications cancelled before approval.', accent: '' },
];

export function findQueue(statusCode) {
  const code = String(statusCode || '').toUpperCase();
  return QUEUES.find((q) => q.statusCode === code);
}
