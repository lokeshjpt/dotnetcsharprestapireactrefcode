// Intra (staff) LIVE journey (PWA_LIVE=1 / --live) — drives the REAL local .NET API in ONE browser
// window, walking a real application through the approval workflow end-to-end. No /api mocks: the
// reviewer actions (apply conditions, record payment, set site-visit type, approve) write to the DB.
//
// Auth: this spec runs against a context restored from the Entra session saved by auth.setup.js
// (e2e/.auth/intra-<env>.json). MSAL replays its cached tokens from that storageState, so the intra
// SPA is signed in and axiosInstance attaches the real bearer to every /api call automatically — the
// same single Microsoft sign-in that unlocked the ecomm live suite.
//
// Target application: the disposable apps the ecomm live suite created (handoff file
// %TEMP%/pwa-e2e-created-apps-<env>.json). We approve the CHECK app (it already has a site map on
// file) and exercise the CC app's "change card" control. Run the ecomm live suite first to create
// them.
import { test, expect } from '@playwright/test';
import fs from 'fs';
import os from 'os';
import path from 'path';
import { resolveEnv, authStatePath } from './environments';

const ENV = resolveEnv();

// The appIds handed off by the ecomm live suite (pwa-permits-ecomm/e2e/live.spec.js).
const CREATED_FILE = path.join(os.tmpdir(), `pwa-e2e-created-apps-${ENV.key}.json`);
function readCreated() {
  try {
    return JSON.parse(fs.readFileSync(CREATED_FILE, 'utf8'));
  } catch {
    return {};
  }
}
const CREATED = readCreated();

// Serial: one long-lived context/page so the reviewer walks the whole approval in a single visible
// window (the "same browser, different transactions" this run demonstrates).
test.describe.configure({ mode: 'serial' });

test.describe('intra live journey — real API, one browser, full approval workflow', () => {
  test.skip(!ENV.isLive, 'Live journey runs only in live mode (--live / PWA_LIVE=1).');
  test.skip(
    !CREATED.CHECK,
    `No created applications found (${CREATED_FILE}). Run the ecomm live suite first ` +
      '(`node e2e/run.js --env=local --live --email=qa@example.com`), then re-run this.'
  );

  const CHECK_ID = CREATED.CHECK;
  const CC_ID = CREATED.CC;

  let context;
  let page;

  // A wizard step chip by its visible label (mirrors the mocked regression suite's helper).
  const wizardStep = (label) =>
    page.locator('.wizard-step').filter({ has: page.getByText(label, { exact: true }) });

  async function openDetail(appId) {
    // Go via about:blank first — Chromium occasionally aborts a direct SPA→SPA navigation while a
    // fetch is in flight.
    await page.goto('about:blank');
    await page.goto(`/applications/${appId}`);
    await expect(page.getByRole('heading', { name: `Application ${appId}` })).toBeVisible();
  }

  test.beforeAll(async ({ browser }) => {
    context = await browser.newContext({
      ignoreHTTPSErrors: true,
      baseURL: ENV.app,
      storageState: authStatePath(ENV.key),
    });
    page = await context.newPage();
    page.setDefaultTimeout(30_000);
    // Auto-accept any window.confirm (e.g. a reviewer confirmation) so actions proceed unattended.
    page.on('dialog', (d) => d.accept().catch(() => {}));
  });

  test.afterAll(async () => {
    await context?.close();
  });

  test('APV-LIVE-01: open the CHECK application and show its approval gates', async () => {
    await openDetail(CHECK_ID);
    await expect(page.locator('.approval-wizard')).toBeVisible();
    const summary = page.locator('.approval-wizard__summary').first();
    await expect(summary).toBeVisible();
    // Sitemap is already on file for this app, so at least the Sitemap gate starts complete.
    await expect(summary).toContainText(/of 5 complete/);
    console.log('[live-intra] CHECK', CHECK_ID, 'initial gates:', (await summary.textContent())?.trim());
  });

  test('CND-LIVE-01: apply conditions to every work — different work types (PENDC → PEND) via real PUT', async () => {
    // Conditions are applied per work from the "Work(s) Requesting Permit" section. This CHECK
    // application was created with THREE different work types (Construction cathodic, Construction
    // water supply, Destruction cathodic), so we drive the per-work-type conditions editor once for
    // each and confirm every work advances past Pending-Conditions.
    const applyButtons = () => page.getByRole('button', { name: 'Apply Conditions' });
    const initial = await applyButtons().count();
    expect(initial, 'the multi-work CHECK app should have several works pending conditions').toBeGreaterThan(1);

    let guard = 0;
    while ((await applyButtons().count()) > 0 && guard < 12) {
      guard += 1;
      const btn = applyButtons().first();
      await btn.scrollIntoViewIfNeeded();
      await btn.click();
      const dlg = page.getByRole('dialog', { name: 'Work Permit Approval Conditions' });
      await expect(dlg).toBeVisible();
      // Some work types expose standard checkbox conditions; others only a free-text "special"
      // condition — handle both so every work type advances.
      const checkboxes = dlg.getByRole('checkbox');
      if ((await checkboxes.count()) > 0) {
        await checkboxes.first().check();
      } else {
        await dlg.locator('textarea').first().fill('E2E live test — verify well seal per county standard.');
      }
      const [resp] = await Promise.all([
        page.waitForResponse(
          (r) =>
            /\/api\/applications\/[^/]+\/works\/[^/]+\/conditions$/.test(new URL(r.url()).pathname) &&
            r.request().method() === 'PUT'
        ),
        dlg.getByRole('button', { name: 'Update Conditions' }).click(),
      ]);
      expect(resp.ok(), `conditions PUT should succeed — got ${resp.status()}`).toBeTruthy();
      await expect(dlg).toBeHidden();
      // Wait for the application refetch (handleConditionsSaved) to drop this work's Apply button.
      await expect(applyButtons()).toHaveCount(initial - guard);
    }
    // Every work has left Pending-Conditions, so the Conditions gate is satisfied.
    await expect(applyButtons()).toHaveCount(0);
    const summary = page.locator('.approval-wizard__summary').first();
    await expect(summary).not.toContainText('1 of 5 complete');
  });

  test('PAY-LIVE-01: record the check payment (PENDP → on file) via real PUT', async () => {
    await wizardStep('Payment').click();
    const wiz = page.locator('.approval-wizard');
    // The CHECK method is already selected for this app; fill the mandatory fields.
    await wiz.locator('#checkNum').fill('10012026');
    const acct = wiz.locator('#acctName');
    if (!(await acct.inputValue())) await acct.fill('Acme Drilling Co');
    // Amount received must equal the amount due — read it from the field hint.
    const hint = await wiz.getByText(/Must equal amount due:/i).first().textContent();
    const due = (hint || '').replace(/[^0-9.]/g, '') || '660';
    await wiz.locator('#paidAmount').fill(due);
    const [resp] = await Promise.all([
      page.waitForResponse(
        (r) =>
          new URL(r.url()).pathname === `/api/payment/${CHECK_ID}` && r.request().method() === 'PUT'
      ),
      wiz.getByRole('button', { name: 'Update Payment' }).click(),
    ]);
    expect(resp.ok(), `payment PUT should succeed — got ${resp.status()}`).toBeTruthy();
  });

  test('APV-LIVE-02: set the site-visit type (Field Technician Review) via real PUT', async () => {
    await wizardStep('Site Visit Type').click();
    const [resp] = await Promise.all([
      page.waitForResponse(
        (r) =>
          new URL(r.url()).pathname === `/api/applications/${CHECK_ID}/approval-details` &&
          r.request().method() === 'PUT'
      ),
      page.getByRole('radio', { name: 'Field Technician Review' }).check(),
    ]);
    expect(resp.ok(), `approval-details PUT should succeed — got ${resp.status()}`).toBeTruthy();
  });

  test('APV-LIVE-03: acknowledge the field-technician review (gate 5)', async () => {
    await wizardStep('Inspections / Review').click();
    await page.getByRole('checkbox', { name: 'Field technician review completed' }).check();
    const summary = page.locator('.approval-wizard__summary').first();
    await expect(summary).toContainText('5 of 5 complete');
    await expect(page.getByRole('button', { name: 'Approve Now' })).toBeEnabled();
  });

  test('APV-LIVE-04: approve the application (real POST /api/approval/{id}/approve)', async () => {
    const [resp] = await Promise.all([
      page.waitForResponse(
        (r) =>
          new URL(r.url()).pathname === `/api/approval/${CHECK_ID}/approve` &&
          r.request().method() === 'POST'
      ),
      page.getByRole('button', { name: 'Approve Now' }).click(),
    ]);
    expect(resp.ok(), `approve POST should succeed — got ${resp.status()}`).toBeTruthy();
    const body = await resp.json().catch(() => ({}));
    console.log('[live-intra] approved', CHECK_ID, '→ permit', body.permitNum || body.receiptNum || '(see DB)');
    // The application is now approved — the read-only permit endpoint should return a permit number.
    await expect(page.getByText(/approved/i).first()).toBeVisible();
  });

  test('PAY-LIVE-02: CC application exposes the "Update and Change Card" control', async () => {
    test.skip(!CC_ID, 'No CC application was created by the ecomm live suite.');
    await openDetail(CC_ID);
    await wizardStep('Payment').click();
    // The CC vault is on file (PEND) but not yet charged, so staff can re-vault a new card. We assert
    // the control is present; completing the 3rd-party IntelliPay vault is out of scope for automation.
    await expect(
      page.locator('.approval-wizard').getByRole('button', { name: 'Update and Change Card' })
    ).toBeVisible();
  });
});
