// Ecomm LIVE journey (PWA_LIVE=1 / --live) — drives the REAL local .NET API in ONE browser window,
// executing several distinct transactions back-to-back (the "same browser, different transactions"
// the run was asked to demonstrate). No /api mocks: applications are really created in the DB.
//
// The public write endpoints are guarded by the server-side captcha filter, which also accepts a
// valid Entra bearer. An automated browser can't silently solve the real invisible reCAPTCHA, so the
// suite (a) stubs window.grecaptcha only to unblock the SPA's client-side gate, and (b) attaches the
// Entra bearer captured by the intra auth.setup to every /api call so the real API accepts the write.
// One Microsoft sign-in (run the intra live suite first) therefore unlocks this whole journey.
import { test, expect } from '@playwright/test';
import { blockGoogleMaps, stubRecaptcha, buildSeed } from './helpers';
import { resolveEnv } from './environments';
import { readBearer, attachBearer, BEARER_FILE } from './liveHelpers';

const ENV = resolveEnv();
const BEARER = readBearer();
const APPLICANT_EMAIL = process.env.PWA_APPLICANT_EMAIL || 'qa@example.com';

// Serial: every test shares ONE long-lived browser context/page, so the transactions chain in a
// single visible window (and later steps reuse the appIds created by earlier ones).
test.describe.configure({ mode: 'serial' });

test.describe('ecomm live journey — real API, one browser, multiple transactions', () => {
  test.skip(!ENV.isLive, 'Live journey runs only in live mode (--live / PWA_LIVE=1).');
  test.skip(
    !BEARER,
    `No Entra bearer captured (${BEARER_FILE}). Sign in once via the intra live suite ` +
      '(`node e2e/run.js --env=local --live`) first, then re-run.'
  );

  let context;
  let page;
  const created = {}; // paymentType -> 13-digit appId

  // Real reference values (fetched from /api/ref/*) that satisfy the composite FK
  // (work_category, work_type, well_use_type) and the drill-method FK on APP_WORKS. buildSeed()'s
  // mock codes (con-dom / DOM / MUD) don't exist in the real DB, so override the work fields here.
  const LIVE_WORK = {
    workCat: 'con', // Well Construction
    workType: 'cathodic', // valid for cat=con
    workDesc: 'Cathodic Protection',
    workFeeRate: 660,
    workFeeUnit: 'well',
    workSiteMax: 0,
    wUse: 'cath', // valid well use for con/cathodic
    wUseDesc: 'Cathodic Protection',
    dmeth: 'mud', // valid drill method
    dmethName: 'Mud Rotary',
  };

  // A single well specification row for a committed work.
  const spec = (owellnum, extra = {}) => ({
    swellid: '', permit: '', dwr: '', owellnum,
    holediam: '8', casediam: '6', sealdepth: '20', maxdepth: '150',
    latitude: '37.804400', longitude: '-122.271200', ...extra,
  });

  // THREE DIFFERENT work types spanning two categories — each is a valid composite
  // (work_category, work_type, well_use_type) + drill-method FK in the real DB. Used to create a
  // single multi-work application so the intra live suite can apply conditions to each different
  // work type. (Investigation types are omitted: they carry no well_use_type row and would trip the
  // composite FK.)
  const LIVE_WORKS = [
    { workCat: 'con', workType: 'cathodic', workDesc: 'Cathodic Protection', workFeeRate: 660, workFeeUnit: 'well', workSiteMax: 0,
      wUse: 'cath', wUseDesc: 'Cathodic Protection', drillerName: 'Bob the Driller', drillerLic: 'C57-123456',
      dmeth: 'mud', dmethName: 'Mud Rotary', dmethOth: '', numbore: '', holediam: '', maxdepth: '', wellSpecs: [spec('W-1')] },
    { workCat: 'con', workType: 'supply', workDesc: 'Water Supply', workFeeRate: 660, workFeeUnit: 'well', workSiteMax: 0,
      wUse: 'dom', wUseDesc: 'Domestic', drillerName: 'Bob the Driller', drillerLic: 'C57-123456',
      dmeth: 'mud', dmethName: 'Mud Rotary', dmethOth: '', numbore: '', holediam: '', maxdepth: '', wellSpecs: [spec('W-2')] },
    { workCat: 'des', workType: 'cathodic', workDesc: 'Cathodic Protection (Destruction)', workFeeRate: 660, workFeeUnit: 'well', workSiteMax: 0,
      wUse: 'catd', wUseDesc: 'Cathodic Protection', drillerName: 'Bob the Driller', drillerLic: 'C57-123456',
      dmeth: 'mud', dmethName: 'Mud Rotary', dmethOth: '', numbore: '', holediam: '', maxdepth: '',
      wellSpecs: [spec('W-3', { swellid: '37A0999', permit: 'P-123', dwr: 'DWR-9' })] },
  ];

  test.beforeAll(async ({ browser }) => {
    context = await browser.newContext({ ignoreHTTPSErrors: true });
    page = await context.newPage();
    page.setDefaultTimeout(30_000);
    await blockGoogleMaps(page); // Location step degrades to manual lat/long; no Google network
    await stubRecaptcha(page); // resolve a client-side token so the SPA fires the guarded call
    await attachBearer(page, BEARER); // real API accepts the write via the Entra bearer
  });

  test.afterAll(async () => {
    await context?.close();
  });

  // Seed a complete, valid application into localStorage, then jump straight to the Verify step.
  // Pass `works` (an array) to build a multi-work application; otherwise the single LIVE_WORK is used.
  async function seedAndOpenVerify(paymentType, works) {
    const seed = { ...buildSeed(), ...LIVE_WORK, paymentType, appEmail: APPLICANT_EMAIL };
    if (paymentType !== 'CHECK') delete seed.acctName; // acctName is only required/validated for CHECK
    if (works) {
      // Multi-work: commit the works array and clear the flat single-work draft so effectiveWorks()
      // doesn't append a duplicate work.
      Object.assign(seed, {
        works,
        workCat: '', workType: '', workDesc: '', workFeeRate: 0, workFeeUnit: 'EA', workSiteMax: 0,
        wUse: '', wUseDesc: '', dmeth: '', dmethName: '', wellSpecs: [], workEditIndex: null,
      });
    }
    await page.goto('/');
    await page.evaluate((data) => {
      window.localStorage.setItem('pwa-permits-ecomm', JSON.stringify(data));
    }, seed);
    await page.goto('/apply');
    await page.locator('.wizard-step__button', { hasText: 'Verify' }).click();
    await expect(page.getByText('Review your application below')).toBeVisible();
    if (works && works.length > 1) {
      // The Verify page lists every distinct work type before submit (pluralised section title).
      await expect(page.getByText('Works Requesting Permit')).toBeVisible();
      await expect(page.locator('.verify-work')).toHaveCount(works.length);
      await expect(page.getByText('Water Supply', { exact: false }).first()).toBeVisible();
    }
  }

  // Non-CC submit ("Submit Application") -> real POST -> confirmation. Returns the created appId.
  async function submitNonCc(paymentType, works) {
    await seedAndOpenVerify(paymentType, works);
    const [resp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/api/applications') && r.request().method() === 'POST'
      ),
      page.getByRole('button', { name: 'Submit Application', exact: true }).click(),
    ]);
    expect(resp.ok(), `POST /api/applications (${paymentType}) should succeed — got ${resp.status()}`).toBeTruthy();
    const body = await resp.json();
    const appId = String(body.appId || body.applicationId || '');
    expect(appId).toMatch(/^\d{13}$/);
    await expect(page).toHaveURL(new RegExp(`/confirmation/${appId}`));
    await expect(page.locator('.confirmation-card__number')).toHaveText(appId);
    created[paymentType] = appId;
    return appId;
  }

  test('PAY-002 create a multi-work CHECK application (3 different work types) against the real API', async () => {
    const appId = await submitNonCc('CHECK', LIVE_WORKS);
    console.log('[live] CHECK appId =', appId, '(works:', LIVE_WORKS.map((w) => `${w.workCat}/${w.workType}`).join(', '), ')');
  });

  test('SM-001 upload a site map for the created application (real multipart POST)', async () => {
    const appId = created.CHECK;
    expect(appId, 'CHECK application must have been created first').toBeTruthy();
    // We are on /confirmation/{CHECK}; the page renders the shared SitemapUpload control.
    await page
      .getByLabel(`Site map file for application ${appId}`)
      .setInputFiles({ name: 'sitemap.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 e2e sitemap') });
    const [resp] = await Promise.all([
      page.waitForResponse(
        (r) =>
          /\/api\/applications\/[^/]+\/sitemap$/.test(new URL(r.url()).pathname) &&
          r.request().method() === 'POST'
      ),
      page.getByRole('button', { name: 'Upload Sitemap File', exact: true }).click(),
    ]);
    expect(resp.ok(), `sitemap POST should succeed — got ${resp.status()}`).toBeTruthy();
    await expect(page.getByText(/Sitemap file successfully uploaded/i)).toBeVisible();
  });

  test('PAY-004 create a Fee-Exempt application', async () => {
    const appId = await submitNonCc('EXMPT');
    console.log('[live] EXMPT appId =', appId);
  });

  test('PAY-001 create a Credit-Card application (record created; IntelliPay vault left pending)', async () => {
    await seedAndOpenVerify('CC');
    const [resp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/api/applications') && r.request().method() === 'POST'
      ),
      page.getByRole('button', { name: 'Pay Now and Submit', exact: true }).click(),
    ]);
    expect(resp.ok(), `CC POST /api/applications should succeed — got ${resp.status()}`).toBeTruthy();
    const body = await resp.json();
    const appId = String(body.appId || body.applicationId || '');
    expect(appId).toMatch(/^\d{13}$/);
    // The record is created and the inline IntelliPay lightbox appears; we stop before the
    // 3rd-party card vault (secure.cpteller.com) which is out of scope for an automated run.
    await expect(
      page.getByText(new RegExp(`Your application\\s+${appId}\\s+has been recorded`, 'i'))
    ).toBeVisible();
    created.CC = appId;
    console.log('[live] CC appId =', appId);
  });

  test('TRK-001 track the created CHECK application by ID (real search)', async () => {
    const appId = created.CHECK;
    await page.goto('/track');
    await page.locator('#trackAppId').fill(appId);
    const [resp] = await Promise.all([
      page.waitForResponse(
        (r) => new URL(r.url()).pathname === '/api/applications/search' && r.request().method() === 'GET'
      ),
      page.getByRole('button', { name: 'Search', exact: true }).click(),
    ]);
    expect(resp.ok(), `search should succeed — got ${resp.status()}`).toBeTruthy();
    await expect(page.getByText(appId, { exact: false }).first()).toBeVisible();
  });

  test('handoff publish the created appIds for the intra live suite', async () => {
    const fs = require('fs');
    const os = require('os');
    const path = require('path');
    const outFile = path.join(os.tmpdir(), `pwa-e2e-created-apps-${ENV.key}.json`);
    fs.writeFileSync(outFile, JSON.stringify(created, null, 2));
    console.log('[live] published created appIds to', outFile, created);
    expect(Object.keys(created).length).toBeGreaterThan(0);
  });
});
