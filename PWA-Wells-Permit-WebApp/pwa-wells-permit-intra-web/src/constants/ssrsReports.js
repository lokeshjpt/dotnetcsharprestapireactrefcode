import config from '../config';

// External SSRS-hosted reports (ported from legacy intra reports_menu.jsp). The report item
// names and folder path match the legacy PropertiesList.getSsrsServer() links exactly, so these
// resolve against the same SQL Server Reporting Services ReportViewer the JBoss app used.
export const SSRS_REPORTS = [
  {
    key: 'WellsInspectorActivityReport',
    title: 'Inspector Activity Report',
    description: 'Inspector workload and activity summary.',
  },
  {
    key: 'PermitsIssuedInspectedReport',
    title: 'Permits Issued / Inspected Summary Report',
    description: 'Issued and inspected permit counts.',
  },
  {
    key: 'WellsWithoutLatLongs',
    title: 'Wells without Lat/Longs Report',
    description: 'Wells missing geospatial coordinates.',
  },
  {
    key: 'PermitsCollectedByFeeUnit',
    title: 'Permits Collected By Fee Unit Report',
    description: 'Fee collection grouped by fee unit.',
  },
];

export function findSsrsReport(key) {
  return SSRS_REPORTS.find((report) => report.key === key);
}

// Build the ReportViewer URL: base already ends with the item-path prefix
// (…ReportViewer.aspx?%2f<Folder>%2fPWA%2fWells); append the URL-encoded report name. The report is
// always opened in a new browser tab — the SSRS server blocks cross-origin iframe embedding.
export function buildSsrsReportUrl(reportKey) {
  const base = config.ssrsServer;
  if (!base) return '';
  const trimmed = base.endsWith('/') ? base.slice(0, -1) : base;
  return `${trimmed}%2f${reportKey}`;
}

export function isSsrsConfigured() {
  return Boolean(config.ssrsServer);
}
