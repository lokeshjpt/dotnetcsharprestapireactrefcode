// Ecomm (public) regression suite — one traceable test per ref/TEST-PLAN.md case for the public
// Apply / Track / Sitemap surfaces (areas EC, PAY, WRK, SM, TRK, plus the ecomm-side SEC checks).
//
// Environment (PWA_ENV, chosen by `npm run test:regression`):
//   * local            — deterministic: /api/** is mocked, reCAPTCHA is stubbed, the application is
//                        seeded into localStorage (seedApplication only writes on localhost:3000).
//                        Every UI-observable plan case runs here so they are fast and create no real
//                        data. Cases whose expectation is a pure backend / 3rd-party side effect
//                        (IntelliPay vault, ICAP scan, DB rows, server 401/429) are recorded as
//                        `test.fixme` with the reason, so the plan ID stays visible in the report.
//   * dev | test | uat — points at the DEPLOYED SPA. localStorage seeding + API mocking do not apply
//                        cross-origin, so a non-destructive smoke runs (site up + invisible reCAPTCHA
//                        wired) rather than creating throwaway applications on a shared env.
import { test, expect } from '@playwright/test';
import {
  installApiMocks,
  blockGoogleMaps,
  seedApplication,
  stubRecaptcha,
  buildSeed,
  STUB_CAPTCHA_TOKEN,
  RECAPTCHA_IFRAME,
  DISCLOSURE_TEXT,
  PENDING_SITEMAP_APP,
} from './helpers';
import { resolveEnv } from './environments';

const ENV = resolveEnv();
const APP_ID = '1700000000123';
// The applicant email the runner collected; falls back to the seed default when run directly.
const APPLICANT_EMAIL = process.env.PWA_APPLICANT_EMAIL || 'jane.doe@example.com';

// The per-work fields the wizard carries as the "draft"; committed works live in formData.works.
const WORK_FIELDS = [
  'workCat', 'workType', 'workDesc', 'workFeeRate', 'workFeeUnit', 'workSiteMax',
  'wUse', 'wUseDesc', 'drillerName', 'drillerLic', 'dmeth', 'dmethName', 'dmethOth',
  'numbore', 'holediam', 'maxdepth', 'wellSpecs',
];

// Snapshot buildSeed()'s single-work DRAFT fields into a standalone committed work, applying any
// overrides (category/type/wells) the individual test needs.
function workFromSeed(overrides = {}) {
  const base = buildSeed();
  const work = {};
  WORK_FIELDS.forEach((k) => { work[k] = base[k]; });
  work.wellSpecs = (base.wellSpecs || []).map((s) => ({ ...s }));
  return { ...work, ...overrides };
}

function wellSpec(owellnum, extra = {}) {
  return {
    swellid: '', permit: '', dwr: '', owellnum, holediam: '8', casediam: '6',
    sealdepth: '20', maxdepth: '150', latitude: '37.804400', longitude: '-122.271200', ...extra,
  };
}

// A valid seeded application carrying the runner-supplied applicant email and the given committed
// works. The works-review table and the Edit modal render committed works (formData.works), so we
// clear the draft fields to leave exactly the supplied works effective.
function seedWithWorks(works, overrides = {}) {
  const base = buildSeed();
  const clearedDraft = {};
  WORK_FIELDS.forEach((k) => { clearedDraft[k] = ''; });
  clearedDraft.wellSpecs = [];
  return { ...base, ...clearedDraft, works, appEmail: APPLICANT_EMAIL, ...overrides };
}

// The single-work seed the earlier focus/verify regressions relied on.
function seedForApplicant(overrides = {}) {
  return seedWithWorks([workFromSeed()], overrides);
}

// A second, different-category work (Investigation) so multi-work cases exercise mixed categories.
function investigationWork() {
  return workFromSeed({
    workCat: 'inv', workType: 'inv-mon', workDesc: 'Environmental Investigation Well',
    workFeeRate: 195, workFeeUnit: 'EA', wUse: '', wUseDesc: '',
    numbore: '2', holediam: '8', maxdepth: '50',
    wellSpecs: [wellSpec('B-1', { maxdepth: '50' })],
  });
}

// ---------------------------------------------------------------------------------------------
// LOCAL — deterministic UI regressions (mocked API + stubbed captcha + seeded application).
// ---------------------------------------------------------------------------------------------
test.describe('ecomm regression (local)', () => {
  test.skip(!ENV.isLocal, 'Deterministic seed/mocked specs only run against the local dev server.');

  test.beforeEach(async ({ page }) => {
    await blockGoogleMaps(page);
    await installApiMocks(page, { appId: APP_ID });
  });

  // Seeds the app and opens the Apply wizard on the named step (Location, Applicant, Project,
  // Work(s), Payment, Verify).
  async function openApplyOnStep(page, stepLabel, seed = seedForApplicant()) {
    await seedApplication(page, seed);
    await page.goto('/apply');
    await page.locator('.wizard-step__button', { hasText: stepLabel }).click();
  }

  // Opens the Edit Work modal on the first committed work (guaranteed to have one well-spec row).
  async function openEditWorkModal(page, seed = seedForApplicant()) {
    await openApplyOnStep(page, 'Work(s)', seed);
    await page.getByRole('button', { name: 'Update' }).first().click();
    await expect(page.getByText('Edit Work Type')).toBeVisible();
  }

  // Opens the Add Work modal (blank draft) from the Work(s) review step.
  async function openAddWorkModal(page, seed = seedForApplicant()) {
    await openApplyOnStep(page, 'Work(s)', seed);
    await page.getByRole('button', { name: '+ Add Work Type' }).click();
    await expect(page.getByText('Add Work Type', { exact: true })).toBeVisible();
  }

  // ---- EC: end-to-end application ------------------------------------------------------------
  test('EC-001: single-work Check application submits and reaches confirmation with captcha token', async ({ page }) => {
    await stubRecaptcha(page);
    await seedApplication(page, seedForApplicant());
    await page.goto('/apply');

    await page.locator('.wizard-step__button', { hasText: 'Verify' }).click();
    await expect(page.getByText('Review your application below')).toBeVisible();

    const submitReq = page.waitForRequest(
      (r) => r.method() === 'POST' && new URL(r.url()).pathname === '/api/applications',
    );
    await page.getByRole('button', { name: 'Submit Application' }).click();

    const req = await submitReq;
    expect(
      req.headers()['x-captcha-token'],
      'X-Captcha-Token header should ride along on POST /api/applications',
    ).toBe(STUB_CAPTCHA_TOKEN);
    expect(req.postData() || '').toContain(APPLICANT_EMAIL);

    await page.waitForURL('**/confirmation/**');
  });

  test('EC-002: required-field validation blocks Save and marks the field aria-invalid', async ({ page }) => {
    await openEditWorkModal(page);
    await page.locator('#drillerName').fill('');
    await page.getByRole('button', { name: 'Save Work' }).click();

    // The modal must stay open with the summary error and the field flagged for assistive tech.
    await expect(page.getByText('Please correct the highlighted fields.')).toBeVisible();
    await expect(page.getByText('Edit Work Type')).toBeVisible();
    await expect(page.locator('#drillerName')).toHaveAttribute('aria-invalid', 'true');
  });

  test('EC-003: multi-work application lists every work with a running Total', async ({ page }) => {
    await openApplyOnStep(page, 'Work(s)', seedWithWorks([workFromSeed(), investigationWork()]));

    const table = page.locator('.works-review-table');
    await expect(table).toBeVisible();
    await expect(table.getByText('Domestic Water Well')).toBeVisible();
    await expect(table.getByText('Environmental Investigation Well')).toBeVisible();
    // Header Total row present (Σ of both works).
    await expect(page.locator('.works-review-total')).toBeVisible();
  });

  test('EC-004: well-spec fields accept multi-character input (Modal focus-steal fix)', async ({ page }) => {
    await openEditWorkModal(page);

    // Type character-by-character to reproduce the original bug: if the modal stole focus on each
    // keystroke only the FIRST character would survive.
    const ownerId = page.getByLabel('Well 1 Owner Well Id');
    await ownerId.click();
    await ownerId.fill('');
    await ownerId.pressSequentially('WELL-98765');
    await expect(ownerId).toHaveValue('WELL-98765');

    const holeDiam = page.getByLabel('Well 1 Hole Diameter (in)');
    await holeDiam.click();
    await holeDiam.fill('');
    await holeDiam.pressSequentially('12.75');
    await expect(holeDiam).toHaveValue('12.75');

    const maxDepth = page.getByLabel('Well 1 Max Depth (ft)');
    await maxDepth.click();
    await maxDepth.fill('');
    await maxDepth.pressSequentially('220');
    await expect(maxDepth).toHaveValue('220');
  });

  test('EC-005: fee recalculates with the number of wells (rate × well count)', async ({ page }) => {
    // A construction work (rate $379) with two wells ⇒ Work Total $758.00.
    const twoWell = workFromSeed({ wellSpecs: [wellSpec('W-1'), wellSpec('W-2')] });
    await openApplyOnStep(page, 'Work(s)', seedWithWorks([twoWell]));
    const row = page.locator('.works-review-table tbody tr').first();
    await expect(row).toContainText('$758.00');
  });

  test('EC-006: session inactivity warning before timeout', async ({ page }) => {
    test.fixme(true, 'Time-based inactivity warning (REACT_APP_SESSION_TIMEOUT_MIN); not covered by the deterministic UI harness.');
  });

  test('EC-007: wells preset to the project site coordinates', async ({ page }) => {
    await openEditWorkModal(page);
    // seedWellSpecCoords presets each well to the project site lat/long before dimensions are entered.
    await expect(page.getByLabel('Well 1 Latitude')).toHaveValue('37.804400');
    await expect(page.getByLabel('Well 1 Longitude')).toHaveValue('-122.271200');
  });

  // ---- PAY: payment types --------------------------------------------------------------------
  test('PAY-001: Credit Card vault ($0 pre-auth)', async ({ page }) => {
    test.fixme(true, 'IntelliPay card vault/pre-authorization is a 3rd-party lightbox + backend side effect (custid encryption, status PEND) — not reproducible in the mocked harness.');
  });

  test('PAY-002: CC declined inside the IntelliPay lightbox (runOnDecline)', async ({ page }) => {
    test.fixme(true, 'Decline path lives inside the IntelliPay 3rd-party popup; cannot be driven from Playwright without the sandbox terminal.');
  });

  test('PAY-003: only PCI-safe fields captured (no nonce/hmac/methodhint…)', async ({ page }) => {
    test.fixme(true, 'PCI-field exclusion is enforced server-side in logging/DB; verified in API tests, not the SPA.');
  });

  test('PAY-004: Check payment submits with paymentType CHECK', async ({ page }) => {
    await stubRecaptcha(page);
    await openApplyOnStep(page, 'Verify', seedForApplicant({ paymentType: 'CHECK', acctName: 'Acme Drilling Co' }));
    const payRow = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: 'Payment Type' }) });
    await expect(payRow).toContainText('Check');
    await expect(page.getByRole('rowheader', { name: 'Name on Account' })).toBeVisible();

    const submitReq = page.waitForRequest((r) => r.method() === 'POST' && new URL(r.url()).pathname === '/api/applications');
    await page.getByRole('button', { name: 'Submit Application' }).click();
    const req = await submitReq;
    expect(req.postData() || '').toContain('"paymentType":"CHECK"');
  });

  test('PAY-005: Cash is NOT offered in the public portal (intra-only, covered by intra PAY-101)', async ({ page }) => {
    await stubRecaptcha(page);
    // The public portal offers Credit Card, Check, and Fee Exempt only. Cash is recorded by County
    // staff in the intra app (intra regression PAY-101), so no Cash radio is ever rendered here.
    await openApplyOnStep(page, 'Payment', seedForApplicant());
    await expect(page.getByRole('radio', { name: 'Cash', exact: true })).toHaveCount(0);
    await expect(page.getByRole('radio', { name: /Credit Card/i })).toBeVisible();
    await expect(page.getByRole('radio', { name: 'Check', exact: true })).toBeVisible();
    await expect(page.getByRole('radio', { name: 'Fee Exempt', exact: true })).toBeVisible();
  });

  test('PAY-006: Fee-exempt ($0) submits with paymentType EXMPT', async ({ page }) => {
    await stubRecaptcha(page);
    await openApplyOnStep(page, 'Verify', seedForApplicant({ paymentType: 'EXMPT' }));
    const payRow = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: 'Payment Type' }) });
    await expect(payRow).toContainText('Exempt');

    const submitReq = page.waitForRequest((r) => r.method() === 'POST' && new URL(r.url()).pathname === '/api/applications');
    await page.getByRole('button', { name: 'Submit Application' }).click();
    const req = await submitReq;
    expect(req.postData() || '').toContain('"paymentType":"EXMPT"');
  });

  test('PAY-007: payment-type description shown, never the raw code', async ({ page }) => {
    await openApplyOnStep(page, 'Verify', seedForApplicant({ paymentType: 'CC' }));
    const payRow = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: 'Payment Type' }) });
    await expect(payRow).toContainText('Credit Card');
    await expect(payRow).not.toContainText(/\bCC\b/);
  });

  // ---- WRK: per-category well fields ---------------------------------------------------------
  test('WRK-001: Construction shows Well Use + well specifications table', async ({ page }) => {
    await openEditWorkModal(page);
    await expect(page.getByRole('combobox', { name: /Well Use/ })).toBeVisible();
    await expect(page.getByLabel('Well 1 Owner Well Id')).toBeVisible();
  });

  test('WRK-002: Investigation shows borehole / hole-diameter / max-depth fields', async ({ page }) => {
    await openAddWorkModal(page);
    await page.getByLabel('Work Category').selectOption({ label: 'Investigation / Monitoring' });
    await expect(page.locator('#numbore')).toBeVisible();
    await expect(page.locator('#holediam')).toBeVisible();
    await expect(page.locator('#maxdepth')).toBeVisible();
  });

  test('WRK-003: Destruction shows State Well # / Permit # / DWR # columns', async ({ page }) => {
    await openAddWorkModal(page);
    await page.getByLabel('Work Category').selectOption({ label: 'Destruction' });
    await expect(page.getByRole('columnheader', { name: 'State Well #' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Permit #' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'DWR #' })).toBeVisible();
  });

  test('WRK-004: "If Other Method, identify" is always visible', async ({ page }) => {
    await openEditWorkModal(page);
    // The seed uses a non-Other drill method, yet the field must still render (required only when
    // the method IS Other).
    await expect(page.getByRole('textbox', { name: /If Other Method, identify/ })).toBeVisible();
  });

  test('WRK-005: drilling-method dropdown falls back to the static list including "Other"', async ({ page }) => {
    await openEditWorkModal(page);
    const method = page.locator('#dmeth');
    await expect(method).toBeVisible();
    await expect(method.locator('option', { hasText: 'Other' })).toHaveCount(1);
    // Fallback static list has 7 methods (+ the placeholder option).
    expect(await method.locator('option').count()).toBeGreaterThanOrEqual(7);
  });

  test('WRK-006: the same work type can be added twice; totals sum without a crash', async ({ page }) => {
    await openApplyOnStep(page, 'Work(s)', seedWithWorks([workFromSeed(), workFromSeed()]));
    const rows = page.locator('.works-review-table tbody tr.works-review-total');
    await expect(rows).toHaveCount(1);
    // Two identical Domestic Water Well rows render.
    await expect(page.locator('.works-review-table').getByText('Domestic Water Well')).toHaveCount(2);
  });

  // ---- SM: site-map upload -------------------------------------------------------------------
  test('SM-001: a valid site map uploads with the captcha token attached', async ({ page }) => {
    await stubRecaptcha(page);
    await installApiMocks(page, {
      search: {
        items: [{
          appId: PENDING_SITEMAP_APP, addDate: '2024-05-02T00:00:00Z', appBusinessName: 'Pending Sitemap LLC',
          appFirstName: 'Sam', appLastName: 'Smith', works: [{ drillerName: 'Drill Co' }],
          siteCityCode: 'FRE', siteLocation: '9 Sitemap Way', statusCode: 'PENDS',
        }],
        totalCount: 1,
      },
    });
    await page.goto('/track');
    await page.locator('#trackAppId').fill(PENDING_SITEMAP_APP);
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    await page.getByRole('button', { name: 'Upload Sitemap', exact: true }).click();

    await page.getByLabel(`Site map file for application ${PENDING_SITEMAP_APP}`).setInputFiles({
      name: 'sitemap.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 test sitemap'),
    });

    const uploadReq = page.waitForRequest(
      (r) => r.method() === 'POST' && /\/api\/applications\/[^/]+\/sitemap$/.test(new URL(r.url()).pathname),
    );
    await page.getByRole('button', { name: 'Upload Sitemap File', exact: true }).click();
    const req = await uploadReq;
    expect(req.headers()['x-captcha-token']).toBe(STUB_CAPTCHA_TOKEN);
    // On success the backend advances the application out of PENDS, so the row's "Upload Sitemap"
    // entry point disappears (the upload panel unmounts) — proof the happy path completed.
    await expect(page.getByRole('button', { name: 'Upload Sitemap', exact: true })).toHaveCount(0);
  });

  test('SM-002: infected file rejected (ICAP)', async ({ page }) => {
    test.fixme(true, 'ICAP virus-scan rejection is a backend gate (EICAR); no client-observable behavior in the mocked harness.');
  });

  test('SM-003: scan service unavailable → 502', async ({ page }) => {
    test.fixme(true, 'ICAP-unavailable 502 is a backend condition; asserted in API tests, not the SPA.');
  });

  test('SM-004: the file picker restricts to the allowed site-map types', async ({ page }) => {
    await stubRecaptcha(page);
    await installApiMocks(page, {
      search: {
        items: [{
          appId: PENDING_SITEMAP_APP, addDate: '2024-05-02T00:00:00Z', appBusinessName: 'Pending Sitemap LLC',
          appFirstName: 'Sam', appLastName: 'Smith', works: [{ drillerName: 'Drill Co' }],
          siteCityCode: 'FRE', siteLocation: '9 Sitemap Way', statusCode: 'PENDS',
        }],
        totalCount: 1,
      },
    });
    await page.goto('/track');
    await page.locator('#trackAppId').fill(PENDING_SITEMAP_APP);
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    await page.getByRole('button', { name: 'Upload Sitemap', exact: true }).click();

    const accept = await page.getByLabel(`Site map file for application ${PENDING_SITEMAP_APP}`).getAttribute('accept');
    expect(accept).toContain('application/pdf');
    expect(accept).toContain('image/png');
    expect(accept).not.toContain('.exe');
  });

  test('SM-005: supporting (non-sitemap) document upload', async ({ page }) => {
    test.fixme(true, 'The public ecomm portal exposes only the site-map upload; general document upload is a staff/intra feature (see intra Upload Documents).');
  });

  // ---- TRK: track an application -------------------------------------------------------------
  test('TRK-001: track by App ID + secondary key returns the status summary', async ({ page }) => {
    await stubRecaptcha(page);
    await installApiMocks(page, {
      search: {
        items: [{
          appId: APP_ID, addDate: '2024-05-02T00:00:00Z', appBusinessName: 'Acme Drilling Co',
          appFirstName: 'Jane', appLastName: 'Doe', works: [{ drillerName: 'Bob the Driller' }],
          siteCityCode: 'OAK', siteLocation: '123 Well Site Rd', statusCode: 'PEND',
        }],
        totalCount: 1,
      },
    });
    await page.goto('/track');
    await page.locator('#trackAppId').fill(APP_ID);
    await page.locator('#trackEmail').fill(APPLICANT_EMAIL);
    await page.getByRole('button', { name: 'Search', exact: true }).click();

    const results = page.getByRole('region', { name: 'Application results' });
    await expect(results.getByText(APP_ID)).toBeVisible();
    await expect(page.getByText(/1 application found/)).toBeVisible();
  });

  test('TRK-002: an unknown ID shows a friendly not-found message', async ({ page }) => {
    await stubRecaptcha(page);
    await installApiMocks(page, { search: { items: [], totalCount: 0 } });
    await page.goto('/track');
    await page.locator('#trackAppId').fill('9999999999999');
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    await expect(page.getByText('No applications match your search.')).toBeVisible();
  });

  test('TRK-003: the search forwards an X-Captcha-Token (server enforces 401 without it — SEC-003)', async ({ page }) => {
    await stubRecaptcha(page);
    await installApiMocks(page, { search: { items: [], totalCount: 0 } });
    await page.goto('/track');
    await page.locator('#trackAppId').fill(APP_ID);

    const searchReq = page.waitForRequest(
      (r) => r.method() === 'GET' && new URL(r.url()).pathname === '/api/applications/search',
    );
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    const req = await searchReq;
    expect(req.headers()['x-captcha-token']).toBe(STUB_CAPTCHA_TOKEN);
  });

  test('TRK-004: results table sticky header / mobile scroll box', async ({ page }) => {
    test.fixme(true, 'Sticky-header / responsive-scroll is a CSS/visual concern; validated by design review, not asserted in the functional harness.');
  });

  // ---- SEC (ecomm-side) ----------------------------------------------------------------------
  test('SEC-012: ecomm reCAPTCHA gate is wired on the Apply flow', async ({ page }) => {
    // With no grecaptcha stub the REAL invisible widget mounts; its iframe + required disclosure are
    // the client half of the bot-challenge protection (a bot gets an image challenge; humans pass).
    await page.goto('/apply');
    await expect(page.locator(RECAPTCHA_IFRAME).first()).toBeAttached();
    await expect(page.getByText(DISCLOSURE_TEXT)).toBeVisible();
  });
});

// ---------------------------------------------------------------------------------------------
// REMOTE (dev/test/uat) — non-destructive smoke against the deployed SPA. Proves the public site
// loads and the invisible reCAPTCHA gate is wired without creating throwaway applications.
// ---------------------------------------------------------------------------------------------
test.describe('ecomm smoke (remote)', () => {
  test.skip(ENV.isLocal, 'Remote smoke only runs against dev/test/uat.');

  test(`Apply page loads with reCAPTCHA wired on ${ENV.key}`, async ({ page }) => {
    await page.goto('/apply');
    await expect(page.locator(RECAPTCHA_IFRAME).first()).toBeAttached();
    await expect(page.getByText(DISCLOSURE_TEXT)).toBeVisible();
  });
});
