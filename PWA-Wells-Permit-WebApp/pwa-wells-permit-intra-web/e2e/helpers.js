// Shared helpers for the intra Help-screenshot capture spec.
//
// The intra SPA is Entra-protected and every data call goes through axiosInstance to /api/**. For
// deterministic, PII-free Help screenshots we intercept ALL /api/** traffic with page.route and
// answer with the canned sample data below — so the running .NET API / database is never touched and
// no real applicant data can leak into the committed screenshots. MSAL's own calls go to
// login.microsoftonline.com (never /api/**), so the interactive sign-in is unaffected by the mocks.

// ---------------------------------------------------------------------------------------------
// Canned, obviously-fake sample data (no real people, businesses, or addresses).
// ---------------------------------------------------------------------------------------------
const CITIES = [
  { code: 'OAK', label: 'Oakland' },
  { code: 'FRE', label: 'Fremont' },
  { code: 'HAY', label: 'Hayward' },
  { code: 'LIV', label: 'Livermore' },
  { code: 'PLE', label: 'Pleasanton' },
  { code: 'DUB', label: 'Dublin' },
  { code: 'SNL', label: 'San Leandro' },
];

const INSPECTORS = [
  { code: 'JM', label: 'J. Martinez' },
  { code: 'AK', label: 'A. Kim' },
  { code: 'RT', label: 'R. Thompson' },
];

const WORK_CATEGORIES = [
  { code: 'con', label: 'Construction' },
  { code: 'des', label: 'Destruction' },
  { code: 'mod', label: 'Modification' },
];

const WORK_TYPES = [
  { code: 'con-dom', label: 'Domestic Water Well' },
  { code: 'con-mon', label: 'Monitoring Well' },
  { code: 'con-irr', label: 'Irrigation Well' },
  { code: 'des-std', label: 'Well Destruction' },
];

const QUEUE_COUNTS = {
  counts: { PEND: 14, PENDS: 6, APPRV: 132, PAYFL: 2, CAN: 9 },
};

// Application search rows (used for the work queues and Search results). Deliberately generic.
const SEARCH_ITEMS = [
  { appId: '1748291045000', addDate: '2026-02-18T00:00:00Z', appBusinessName: 'Sample Well Services LLC', appFirstName: 'Sample', appLastName: 'Applicant', siteCityCode: 'FRE', siteCityName: 'Fremont', siteLocation: '100 Example Ave', statusCode: 'PEND' },
  { appId: '1748104600000', addDate: '2026-02-15T00:00:00Z', appBusinessName: 'Demo Drilling Co.', appFirstName: 'Demo', appLastName: 'Contractor', siteCityCode: 'OAK', siteCityName: 'Oakland', siteLocation: '200 Sample Blvd', statusCode: 'PEND' },
  { appId: '1747918200000', addDate: '2026-02-13T00:00:00Z', appBusinessName: 'Test Geotech Inc.', appFirstName: 'Test', appLastName: 'Geologist', siteCityCode: 'LIV', siteCityName: 'Livermore', siteLocation: '55 Placeholder Rd', statusCode: 'PEND' },
  { appId: '1747731800000', addDate: '2026-02-11T00:00:00Z', appBusinessName: 'Placeholder Boring', appFirstName: 'Pat', appLastName: 'Example', siteCityCode: 'HAY', siteCityName: 'Hayward', siteLocation: '9 Demo Court', statusCode: 'PEND' },
  { appId: '1747545400000', addDate: '2026-02-09T00:00:00Z', appBusinessName: 'Anywater Wells', appFirstName: 'Alex', appLastName: 'Sample', siteCityCode: 'PLE', siteCityName: 'Pleasanton', siteLocation: '4 Vineyard Way', statusCode: 'PEND' },
];

// Returns the sample rows stamped with the queue's status code so each work-queue / cancelled screen
// shows the right status pill (Pending Sitemap, Approved, Payment Failed, Cancelled, …).
function searchRowsForStatus(statusCode) {
  const code = String(statusCode || 'PEND').toUpperCase();
  return {
    items: SEARCH_ITEMS.map((row) => ({ ...row, statusCode: code })),
    totalCount: SEARCH_ITEMS.length,
  };
}

// Pre-system history permits (legacy HIST_PERMITS, 1987–April 2005). Obviously-fake sample data.
const HISTORY_PERMITS = {
  items: [
    { permitNum: 'W-1992-0345', workType: 'Domestic', wellComplRptNum: 'WCR-88231', bldgNum: '100', addrStreet: 'Example Ave', cityName: 'Fremont', consultant: 'Sample Consulting', drillerName: 'Demo Drilling Co.', startDate: '05/12/1992', permitDate: '05/20/1992' },
    { permitNum: 'W-1997-1180', workType: 'Monitoring', wellComplRptNum: 'WCR-91044', bldgNum: '200', addrStreet: 'Sample Blvd', cityName: 'Oakland', consultant: 'Placeholder Geo', drillerName: 'Test Drilling Inc.', startDate: '08/03/1997', permitDate: '08/11/1997' },
    { permitNum: 'W-2001-0562', workType: 'Irrigation', wellComplRptNum: 'WCR-94518', bldgNum: '55', addrStreet: 'Placeholder Rd', cityName: 'Livermore', consultant: 'Demo Consultants', drillerName: 'Anywater Wells', startDate: '04/19/2001', permitDate: '04/27/2001' },
    { permitNum: 'W-2004-0977', workType: 'Destruction', wellComplRptNum: 'WCR-98720', bldgNum: '9', addrStreet: 'Demo Court', cityName: 'Hayward', consultant: 'Sample Environmental', drillerName: 'Bayview Drilling', startDate: '02/10/2004', permitDate: '02/18/2004' },
  ],
  totalCount: 4,
};

// History well locations (legacy HIST_WELL_LOC). Obviously-fake sample data.
const HISTORY_WELLS = {
  items: [
    { wellKey: 'HW-1', tractNum: 'T-4821', sectNum: 'S-12', permitNum: 'W-1992-0345', addrStreet: '100 Example Ave', addrCity: 'Fremont', ownerName: 'Sample Owner', wellUse: 'Domestic' },
    { wellKey: 'HW-2', tractNum: 'T-5133', sectNum: 'S-07', permitNum: 'W-1997-1180', addrStreet: '200 Sample Blvd', addrCity: 'Oakland', ownerName: 'Placeholder Owner', wellUse: 'Monitoring' },
    { wellKey: 'HW-3', tractNum: 'T-6042', sectNum: 'S-21', permitNum: 'W-2001-0562', addrStreet: '55 Placeholder Rd', addrCity: 'Livermore', ownerName: 'Demo Owner', wellUse: 'Irrigation' },
    { wellKey: 'HW-4', tractNum: 'T-6890', sectNum: 'S-03', permitNum: 'W-2004-0977', addrStreet: '9 Demo Court', addrCity: 'Hayward', ownerName: 'Test Owner', wellUse: 'Domestic' },
  ],
  totalCount: 4,
};

// Rows for the shared "due inspection" lists (Pending WCR / Pending GeoLog). `dueDate` is the WCR /
// GeoLog due date shown in the last column.
function dueInspectionRows() {
  return {
    items: [
      { appId: '1748291045000', inspectorName: 'J. Martinez', permitRange: 'W2026-0412', appBusinessName: 'Sample Well Services LLC', applicantName: 'Sample Applicant', projectSiteLocation: '100 Example Ave', siteCityName: 'Fremont', dueDate: '03/22/2026' },
      { appId: '1748104600000', inspectorName: 'A. Kim', permitRange: 'W2026-0413', appBusinessName: 'Demo Drilling Co.', applicantName: 'Demo Contractor', projectSiteLocation: '200 Sample Blvd', siteCityName: 'Oakland', dueDate: '03/26/2026' },
      { appId: '1747918200000', inspectorName: 'R. Thompson', permitRange: 'W2026-0414', appBusinessName: 'Test Geotech Inc.', applicantName: 'Test Geologist', projectSiteLocation: '55 Placeholder Rd', siteCityName: 'Livermore', dueDate: '03/30/2026' },
    ],
    totalCount: 3,
  };
}

// Permits placed on hold (legacy hold_list.jsp shape).
const HOLD_LIST = {
  items: [
    { appId: '1748104600000', permitRange: 'W2026-0413', appBusinessName: 'Demo Drilling Co.', applicantName: 'Demo Contractor', projectSiteLocation: '200 Sample Blvd', siteCityName: 'Oakland', projectStartDate: '03/14/2026', projectEndDate: '03/28/2026', inspectorName: 'A. Kim' },
    { appId: '1747731800000', permitRange: 'W2026-0415', appBusinessName: 'Placeholder Boring', applicantName: 'Pat Example', projectSiteLocation: '9 Demo Court', siteCityName: 'Hayward', projectStartDate: '03/17/2026', projectEndDate: '03/31/2026', inspectorName: 'J. Martinez' },
  ],
  totalCount: 2,
};

// A single, rich application object for the Application Detail / Approval screenshots.
const DETAIL_APP_ID = '1748291045000';
const DETAIL_APPLICATION = {
  appId: DETAIL_APP_ID,
  addDate: '2026-02-18T09:24:00Z',
  addBy: 'INTERNET',
  statusCode: 'PEND',
  hazard: null,
  siteCityCode: 'FRE',
  siteCityName: 'Fremont',
  projStartDate: '2026-03-10',
  projEndDate: '2026-03-24',
  siteLocation: '100 Example Ave, Fremont, CA 94538',
  siteLat: '37.552300',
  siteLong: '-121.988500',
  ownerFirstName: 'Sample',
  ownerLastName: 'Owner',
  ownerAddrStreet: '77 Placeholder Court',
  ownerAddrCity: 'Pleasanton',
  ownerAddrState: 'CA',
  ownerAddrZip: '94566',
  ownerPhone: '(510) 555-0100',
  ownerEmail: 'owner@example.com',
  clientFirstName: 'Sample',
  clientLastName: 'Owner',
  clientAddrStreet: '77 Placeholder Court',
  clientAddrCity: 'Pleasanton',
  clientAddrState: 'CA',
  clientAddrZip: '94566',
  clientPhone: '(510) 555-0100',
  clientEmail: 'owner@example.com',
  appBusinessName: 'Sample Well Services LLC',
  appFirstName: 'Sample',
  appLastName: 'Applicant',
  appAddrStreet: '1200 Example Avenue',
  appAddrStreet2: '',
  appAddrCity: 'Hayward',
  appAddrState: 'CA',
  appAddrZip: '94542',
  appPhone: '(510) 555-0142',
  appEmailAddr: 'applicant@example.com',
  contactFirstName: 'Sample',
  contactLastName: 'Applicant',
  contactPhone: '(510) 555-0142',
  contactEmail: 'applicant@example.com',
  contactCell: '(510) 555-0143',
  siteVisitType: '',
  workTypes: WORK_TYPES,
  works: [
    {
      workId: 'W1',
      workCategoryDesc: 'Construction',
      workCategory: 'con',
      workTypeDesc: 'Domestic Water Well',
      workType: 'con-dom',
      wellUseDesc: 'Domestic',
      wellUseType: 'DOM',
      drillerName: 'Demo Drilling Co.',
      drillerLicenseNum: 'C57-482913',
      workFeeRate: 379,
      workFeeUnit: 'EA',
      statusCode: 'PEND',
      specs: [
        { workSpecsId: 'S1', ownerWellNum: 'WELL-1', maxDepthFt: 220, holeDiamIn: 10, casingDiamIn: 6, permitNum: '' },
      ],
    },
  ],
};

const DETAIL_PAYMENT = {
  paymentType: 'CC',
  statusCode: 'PEND',
  authAmount: 379,
  paidAmount: 0,
  fineAmount: 0,
  authIdEncr: '\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022',
  checkNum: '',
  acctName: '',
};

const DETAIL_CONDITIONS = {
  available: [
    { code: 'STD1', label: 'Install an approved sanitary well seal per County code.' },
    { code: 'STD2', label: 'Provide 48-hour notice to the inspector before drilling.' },
    { code: 'STD3', label: 'Submit the Well Completion Report within 60 days.' },
    { code: 'STD4', label: 'Maintain required setbacks from septic systems and property lines.' },
  ],
  selected: [
    { conditionType: 'STD1', otherDesc: '' },
    { conditionType: 'STD3', otherDesc: '' },
  ],
};

// Pending-inspections list rows.
const PENDING_INSPECTIONS = {
  items: [
    {
      appId: '1748291045000', permitRange: 'W2026-0412', appBusinessName: 'Sample Well Services LLC', applicantName: 'Sample Applicant',
      projectSiteLocation: '100 Example Ave', siteCityName: 'Fremont', projectStartDate: '03/10/2026', projectEndDate: '03/24/2026',
      schedule: [{ inspectionDate: '03/12/2026', inspectionTimeDisp: '9:00 AM', inspectorName: 'J. Martinez', statusDesc: 'Scheduled' }],
    },
    {
      appId: '1748104600000', permitRange: 'W2026-0413', appBusinessName: 'Demo Drilling Co.', applicantName: 'Demo Contractor',
      projectSiteLocation: '200 Sample Blvd', siteCityName: 'Oakland', projectStartDate: '03/14/2026', projectEndDate: '03/28/2026',
      schedule: [],
    },
    {
      appId: '1747918200000', permitRange: 'W2026-0414', appBusinessName: 'Test Geotech Inc.', applicantName: 'Test Geologist',
      projectSiteLocation: '55 Placeholder Rd', siteCityName: 'Livermore', projectStartDate: '03/16/2026', projectEndDate: '03/30/2026',
      schedule: [{ inspectionDate: '03/18/2026', inspectionTimeDisp: '1:00 PM', inspectorName: 'A. Kim', statusDesc: 'Scheduled' }],
    },
  ],
  totalCount: 3,
};

// Scheduled inspections for the calendar. Dates are generated for the current month so the calendar
// always shows populated days regardless of when the screenshot is captured.
function scheduledInspectionsForCurrentMonth() {
  const now = new Date();
  const y = now.getFullYear();
  const m = now.getMonth(); // 0-based
  const pad = (v) => String(v).padStart(2, '0');
  const key = (day) => `${pad(m + 1)}/${pad(day)}/${y}`;
  const days = [6, 6, 12, 18, 18, 23];
  const inspectors = ['J. Martinez', 'A. Kim', 'R. Thompson'];
  const times = ['9:00 AM', '10:30 AM', '1:00 PM', '2:30 PM'];
  const daysInMonth = new Date(y, m + 1, 0).getDate();
  return days
    .filter((d) => d <= daysInMonth)
    .map((d, i) => ({
      inspectionDate: key(d),
      appId: SEARCH_ITEMS[i % SEARCH_ITEMS.length].appId,
      inspectorName: inspectors[i % inspectors.length],
      inspectionTimeDisp: times[i % times.length],
      statusDesc: 'Scheduled',
      statusCode: 'SCH',
    }));
}

// ---------------------------------------------------------------------------------------------
// Route interception. Matches by pathname so query strings / order don't matter.
// ---------------------------------------------------------------------------------------------
async function installApiMocks(page) {
  await page.route('**/api/**', async (route) => {
    const req = route.request();
    const method = req.method();
    let pathname;
    try {
      pathname = new URL(req.url()).pathname;
    } catch {
      return route.fallback();
    }

    // Only intercept the app's OWN endpoints; let anything else (incl. MSAL) fall through.
    if (!pathname.startsWith('/api/')) return route.fallback();

    const json = (body, status = 200) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

    // ---- auth allowlist check: succeed so the app renders (no NotAuthorized banner) ----
    if (pathname === '/api/auth/me') return json({ username: 'staff.user@acgov.org', name: 'Staff User' });

    // ---- reference data ----
    if (pathname === '/api/ref/cities') return json(CITIES);
    if (pathname === '/api/ref/inspectors') return json(INSPECTORS);
    if (pathname === '/api/ref/work-categories') return json(WORK_CATEGORIES);
    if (pathname === '/api/ref/work-types') return json(WORK_TYPES);
    if (pathname.startsWith('/api/ref/')) return json([]);

    // ---- dashboard queue counts ----
    if (pathname === '/api/reports/queue-counts') return json(QUEUE_COUNTS);

    // ---- application search (queues + Search page). Status-aware so each queue shows the right
    //      status pill; the Search page (no statusCode) still gets the default Pending rows. ----
    if (pathname === '/api/applications/search') {
      const status = new URL(req.url()).searchParams.get('statusCode');
      return json(searchRowsForStatus(status));
    }

    // ---- pre-system history search (History Permits / History Well Locations) ----
    if (pathname === '/api/history/permits') return json(HISTORY_PERMITS);
    if (pathname === '/api/history/wells') return json(HISTORY_WELLS);
    if (pathname === '/api/history/cities') return json(CITIES);

    // ---- application detail graph ----
    if (/^\/api\/applications\/[^/]+\/conditions$/.test(pathname)) return json(DETAIL_CONDITIONS);
    if (/^\/api\/applications\/[^/]+\/permit$/.test(pathname)) return json({ conditions: [] });
    if (/^\/api\/applications\/[^/]+\/documents$/.test(pathname)) return json([]);
    if (/^\/api\/applications\/[^/]+\/notes$/.test(pathname)) return json([]);
    if (/^\/api\/applications\/[^/]+$/.test(pathname) && method === 'GET') {
      return json({ ...DETAIL_APPLICATION });
    }

    // ---- payment ----
    if (/^\/api\/payment\/[^/]+$/.test(pathname) && method === 'GET') return json(DETAIL_PAYMENT);

    // ---- inspections ----
    if (pathname === '/api/inspections/pending') return json(PENDING_INSPECTIONS);
    if (pathname === '/api/inspections/pending-wcr') return json(dueInspectionRows());
    if (pathname === '/api/inspections/pending-geolog') return json(dueInspectionRows());
    if (pathname === '/api/inspections/hold') return json(HOLD_LIST);
    if (pathname === '/api/inspections/scheduled') return json(scheduledInspectionsForCurrentMonth());
    if (/^\/api\/inspections\/[^/]+$/.test(pathname) && method === 'GET') return json([]);

    // ---- fall-through: empty object keeps the UI from erroring ----
    return json({});
  });
}

// Sets the local-only capture-bypass flag in localStorage BEFORE the app boots, so index.js renders
// without the Entra redirect and AuthContext skips token acquisition. Only effective in the local
// build (REACT_APP_ENV=local); see src/captureBypass.js.
async function enableCaptureBypass(page) {
  await page.addInitScript(() => {
    try {
      if (window.location.origin.includes('localhost:3003')) {
        window.localStorage.setItem('pwaCaptureBypass', '1');
      }
    } catch {
      /* opaque origin (e.g. about:blank) — ignore */
    }
  });
}

// Capture-only cosmetics injected on every document load: hide the dev environment badge so it
// doesn't appear in the committed Help screenshots.
async function injectCaptureStyles(page) {
  await page.addInitScript(() => {
    const css = `
      .env-switcher { display: none !important; }
    `;
    const inject = () => {
      const style = document.createElement('style');
      style.setAttribute('data-capture', '1');
      style.textContent = css;
      (document.head || document.documentElement).appendChild(style);
    };
    if (document.head) inject();
    else document.addEventListener('DOMContentLoaded', inject);
  });
}

// Waits for the intra app shell (staff sidebar) to render.
async function waitForAppReady(page, timeout = 60_000) {
  await page.locator('.sidebar').first().waitFor({ state: 'visible', timeout });
}

// ---------------------------------------------------------------------------------------------
// Test-state builders (used by regression.spec.js to craft per-test variants without a backend).
// deepClone keeps each test's mutations isolated from the shared canned objects.
// ---------------------------------------------------------------------------------------------
const deepClone = (o) => JSON.parse(JSON.stringify(o));

// The base detail application/payment as the mocks serve them, plus the per-work conditions payload
// shape the WorksSection / PermitPage consume (GET /conditions -> { works: [...] }).
function detailApplication(overrides = {}) {
  return { ...deepClone(DETAIL_APPLICATION), ...overrides };
}
function detailPayment(overrides = {}) {
  return { ...deepClone(DETAIL_PAYMENT), ...overrides };
}

// Two-work variant so the "Delete" (hard) button appears (only rendered when works.length > 1).
function twoWorkApplication(overrides = {}) {
  const base = deepClone(DETAIL_APPLICATION);
  const second = deepClone(base.works[0]);
  second.workId = 'W2';
  second.workTypeDesc = 'Monitoring Well';
  second.workType = 'con-mon';
  second.specs = [{ workSpecsId: 'S2', ownerWellNum: 'WELL-2', maxDepthFt: 180, holeDiamIn: 8, casingDiamIn: 5, permitNum: '' }];
  base.works.push(second);
  return { ...base, ...overrides };
}

// Per-work conditions payload (GET /api/applications/{id}/conditions). `pendc` controls whether the
// work is still Pending Conditions (drives the "Apply Conditions" vs "Edit Conditions" button and the
// approval Conditions gate).
function conditionsPayload({ pendc = true, workId = 'W1' } = {}) {
  return {
    works: [
      {
        workId,
        statusCode: pendc ? 'PENDC' : 'PEND',
        available: deepClone(DETAIL_CONDITIONS.available),
        selected: pendc ? [] : deepClone(DETAIL_CONDITIONS.selected),
      },
    ],
  };
}

module.exports = {
  installApiMocks,
  enableCaptureBypass,
  injectCaptureStyles,
  waitForAppReady,
  DETAIL_APP_ID,
  DETAIL_APPLICATION,
  DETAIL_PAYMENT,
  DETAIL_CONDITIONS,
  WORK_CATEGORIES,
  WORK_TYPES,
  CITIES,
  SEARCH_ITEMS,
  detailApplication,
  detailPayment,
  twoWorkApplication,
  conditionsPayload,
};
