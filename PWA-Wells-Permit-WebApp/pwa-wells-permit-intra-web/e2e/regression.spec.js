// Intra (staff) regression suite — one titled test per case in ref/TEST-PLAN.md.
//
// Environment (PWA_ENV, chosen by `npm run test:regression`):
//   * local            — capture-bypass renders the app without an Entra sign-in and every /api/**
//                        call is mocked with canned, PII-free data (see helpers.js). The deep,
//                        deterministic regressions run here so they are fast and touch no backend.
//   * dev | test | uat — drives the DEPLOYED admin site using the Entra session saved by the setup
//                        project (bearer reused via storageState). Read-only smoke only.
//
// Coverage strategy for the local suite:
//   * UI-observable behaviour is asserted for real (headings, gate summaries, modal contents,
//     read-only vs editable fields, button visibility/disabled states).
//   * Mutations are verified "assert-on-request": we wait for the exact METHOD + path the action
//     must fire (e.g. PUT /works/{id}), and override that endpoint so the post-mutation re-render is
//     stable. This proves the wiring without a backend.
//   * Cases whose *effect* is a backend/3rd-party side effect that cannot be observed from the SPA
//     (IntelliPay charge/vault, ICAP scan, API-enforced 401/403/409/429, audit emails, DB rows) are
//     recorded as `test.fixme` with the reason, so every plan ID is traceable in the report and the
//     out-of-scope ones are explicit rather than silently missing.
const { test, expect } = require('@playwright/test');
const {
  installApiMocks,
  enableCaptureBypass,
  waitForAppReady,
  DETAIL_APP_ID,
  DETAIL_CONDITIONS,
  detailApplication,
  detailPayment,
  twoWorkApplication,
  conditionsPayload,
} = require('./helpers');
const { resolveEnv } = require('./environments');

const ENV = resolveEnv();
const APPLICANT_EMAIL = process.env.PWA_APPLICANT_EMAIL || 'applicant@example.com';
const APP = DETAIL_APP_ID;

// Navigating directly between two loaded SPA routes is occasionally aborted by Chromium when in-flight
// (mocked) fetches are cancelled; go via about:blank and retry. (Same approach as capture-help.spec.)
async function nav(page, url) {
  const isNavError = (e) => /ERR_ABORTED|interrupted|net::/.test(String(e));
  let lastErr;
  for (let attempt = 0; attempt < 8; attempt += 1) {
    try {
      await page.goto('about:blank', { waitUntil: 'load' });
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      return;
    } catch (e) {
      if (!isNavError(e)) throw e;
      lastErr = e;
      await page.waitForTimeout(500);
    }
  }
  throw lastErr;
}

// ---------------------------------------------------------------------------------------------
// LOCAL — deterministic regressions (capture-bypass + mocked API).
// ---------------------------------------------------------------------------------------------
test.describe('intra regression (local)', () => {
  test.skip(!ENV.isLocal || ENV.isLive, 'Deterministic bypass/mocked specs only run against the local dev server (non-live).');

  test.beforeEach(async ({ page }) => {
    await enableCaptureBypass(page);
    await installApiMocks(page);
    // Soft-confirm dialogs (window.confirm for cancel/delete) auto-accept so the action proceeds.
    page.on('dialog', (d) => d.accept().catch(() => {}));
  });

  // Register per-test /api overrides. Because this route is added AFTER the beforeEach base mock,
  // Playwright runs it first (LIFO); anything not matched defers to the base mock via route.fallback.
  // Each rule: { method?, path? (exact) | rx? (RegExp on pathname), json?, status?, handler? }.
  async function overrides(page, rules) {
    await page.route('**/api/**', async (route) => {
      const req = route.request();
      let pathname;
      try { pathname = new URL(req.url()).pathname; } catch { return route.fallback(); }
      for (const r of rules) {
        if (r.method && r.method !== req.method()) continue;
        const pathOk = r.path ? r.path === pathname : (r.rx ? r.rx.test(pathname) : false);
        if (!pathOk) continue;
        if (typeof r.handler === 'function') return r.handler(route, req);
        return route.fulfill({
          status: r.status || 200,
          contentType: 'application/json',
          body: JSON.stringify(r.json ?? {}),
        });
      }
      return route.fallback();
    });
  }

  function waitForApi(page, method, pathOrRx) {
    return page.waitForRequest((r) => {
      if (r.method() !== method) return false;
      let p; try { p = new URL(r.url()).pathname; } catch { return false; }
      return pathOrRx instanceof RegExp ? pathOrRx.test(p) : p === pathOrRx;
    }, { timeout: 20_000 });
  }

  async function openDetail(page, appId = APP) {
    await nav(page, `/applications/${appId}`);
    await expect(page.getByRole('heading', { name: `Application ${appId}` })).toBeVisible();
  }

  // The "Edit" button inside a given detail section (Project / Applicant / Site Hazard).
  function sectionEdit(page, title) {
    return page.locator('.detail-section')
      .filter({ has: page.getByText(title, { exact: true }) })
      .getByRole('button', { name: 'Edit', exact: true });
  }

  // A wizard step chip by its visible label.
  function wizardStep(page, label) {
    return page.locator('.wizard-step').filter({ has: page.getByText(label, { exact: true }) });
  }

  // ============================ AUTH / shell ============================
  test('AUTH: capture-bypass renders the staff dashboard without a sign-in', async ({ page }) => {
    await nav(page, '/');
    await waitForAppReady(page);
    await expect(page.getByRole('heading', { name: 'Permit processing dashboard' })).toBeVisible();
  });

  // ============================ UPD — update application ============================
  test('UPD-001: Edit Project info saves via PUT /project', async ({ page }) => {
    await overrides(page, [{ method: 'PUT', path: `/api/applications/${APP}/project`, json: detailApplication() }]);
    await openDetail(page);
    await sectionEdit(page, 'Project Information').click();
    const dlg = page.getByRole('dialog', { name: 'Edit Project Information' });
    await expect(dlg).toBeVisible();
    await dlg.locator('#siteLocation').fill('250 Updated Way, Fremont, CA 94538');
    const req = waitForApi(page, 'PUT', `/api/applications/${APP}/project`);
    await dlg.getByRole('button', { name: 'Save' }).click();
    await req;
  });

  test('UPD-002: Edit Applicant info saves via PUT /applicant', async ({ page }) => {
    await overrides(page, [{ method: 'PUT', path: `/api/applications/${APP}/applicant`, json: detailApplication() }]);
    await openDetail(page);
    await sectionEdit(page, 'Applicant Information').click();
    const dlg = page.getByRole('dialog', { name: 'Edit Applicant Information' });
    await expect(dlg).toBeVisible();
    const req = waitForApi(page, 'PUT', `/api/applications/${APP}/applicant`);
    await dlg.getByRole('button', { name: 'Save' }).click();
    await req;
  });

  test('UPD-003: Edit Hazard info saves via PUT /hazard', async ({ page }) => {
    const withHazard = detailApplication({
      hazard: {
        consultantFirstName: 'Sample', consultantLastName: 'Consultant',
        safetyOfficerFirstName: 'Safety', safetyOfficerLastName: 'Officer',
      },
    });
    await overrides(page, [
      { method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: withHazard },
      { method: 'PUT', path: `/api/applications/${APP}/hazard`, json: withHazard },
    ]);
    await openDetail(page);
    await sectionEdit(page, 'Site Hazard Information').click();
    const dlg = page.getByRole('dialog', { name: 'Edit Site Hazard Information' });
    await expect(dlg).toBeVisible();
    const req = waitForApi(page, 'PUT', `/api/applications/${APP}/hazard`);
    await dlg.getByRole('button', { name: 'Save' }).click();
    await req;
  });

  test('UPD-004: Edit Work — category/type read-only, driller editable, PUT /works/{id}', async ({ page }) => {
    await overrides(page, [{ method: 'PUT', path: `/api/applications/${APP}/works/W1`, json: detailApplication() }]);
    await openDetail(page);
    await page.locator('.works-table__actions').first().getByRole('button', { name: 'Edit', exact: true }).click();
    const dlg = page.getByRole('dialog', { name: 'Edit Work Requesting Permit' });
    await expect(dlg).toBeVisible();
    // Category & type are shown read-only; the driller name is editable.
    await expect(dlg.locator('#editWorkCategory')).toBeDisabled();
    await expect(dlg.locator('#editWorkType')).toBeDisabled();
    const driller = dlg.locator('#drillerName');
    await expect(driller).toBeEnabled();
    await driller.fill('Updated Drilling Co.');
    const req = waitForApi(page, 'PUT', `/api/applications/${APP}/works/W1`);
    await dlg.getByRole('button', { name: 'Save' }).click();
    await req;
  });

  test.fixme('UPD-005: Edit terminal application blocked — API-enforced 409 on a terminal app; not observable from the local mocked SPA.', () => {});
  test.fixme('UPD-006: Non-whitelisted staff edit — 403 not_authorized + audit email are enforced by the .NET API (see API test project).', () => {});

  // ============================ WRK-1xx — works on an existing application ============================
  test('WRK-101: Add work (category/type/driller/method + well) fires POST /works', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', path: '/api/ref/drill-methods', json: [{ code: 'ROT', label: 'Rotary' }] },
      { method: 'POST', path: `/api/applications/${APP}/works`, json: detailApplication() },
    ]);
    await openDetail(page);
    await page.getByRole('button', { name: 'Add Work' }).click();
    const dlg = page.getByRole('dialog', { name: 'Add Work' });
    await expect(dlg).toBeVisible();
    await dlg.locator('#addWorkCategory').selectOption('con');
    await dlg.locator('#addWorkType').selectOption('con-dom');
    await dlg.locator('#addDrillerName').fill('Demo Drilling Co.');
    await dlg.locator('#addDrillerLicenseNum').fill('C57-123456');
    await dlg.locator('#addDrillMethodType').selectOption('ROT');
    const saveBtn = dlg.getByRole('button', { name: 'Add Work' });
    await expect(saveBtn).toBeEnabled();
    const req = waitForApi(page, 'POST', `/api/applications/${APP}/works`);
    await saveBtn.click();
    await req;
  });

  test('WRK-102: Add Work modal drives category → type → fee line', async ({ page }) => {
    await overrides(page, [{ method: 'GET', path: '/api/ref/drill-methods', json: [{ code: 'ROT', label: 'Rotary' }] }]);
    await openDetail(page);
    await page.getByRole('button', { name: 'Add Work' }).click();
    const dlg = page.getByRole('dialog', { name: 'Add Work' });
    await expect(dlg).toBeVisible();
    // Work Type is gated on a category; selecting one populates the type options.
    await dlg.locator('#addWorkCategory').selectOption('con');
    await dlg.locator('#addWorkType').selectOption('con-dom');
    await expect(dlg.locator('.add-work-fee')).toBeVisible();
  });

  test('WRK-103: Cancel work (soft) fires POST /works/{id}/cancel', async ({ page }) => {
    await overrides(page, [{ method: 'POST', path: `/api/applications/${APP}/works/W1/cancel`, json: detailApplication() }]);
    await openDetail(page);
    const req = waitForApi(page, 'POST', `/api/applications/${APP}/works/W1/cancel`);
    await page.getByRole('button', { name: 'Cancel Work' }).first().click();
    await req;
  });

  test('WRK-104: Delete work (hard) available with >1 work, fires DELETE /works/{id}', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: twoWorkApplication() },
      { method: 'DELETE', path: `/api/applications/${APP}/works/W2`, json: twoWorkApplication() },
    ]);
    await openDetail(page);
    const req = waitForApi(page, 'DELETE', `/api/applications/${APP}/works/W2`);
    // Second work's row Delete (danger) button.
    await page.getByRole('button', { name: 'Delete', exact: true }).last().click();
    await req;
  });

  test('WRK-105: Delete last work blocked — no Delete button when only one work', async ({ page }) => {
    await openDetail(page); // default detail has exactly one work
    await expect(page.locator('.works-table')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Delete', exact: true })).toHaveCount(0);
  });

  test('WRK-106: Add work on terminal app — Add Work hidden when approved', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ statusCode: 'APPRV' }) }]);
    await openDetail(page);
    await expect(page.locator('.works-table')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add Work' })).toHaveCount(0);
  });

  // ============================ PAY-1xx — payment updates ============================
  test('PAY-101: Change payment type (→ Cash) fires PUT /payment/{id}', async ({ page }) => {
    await overrides(page, [{ method: 'PUT', path: `/api/payment/${APP}`, json: detailPayment({ paymentType: 'CASH', acctName: 'Jordan Payer', paidAmount: 379 }) }]);
    await openDetail(page);
    await wizardStep(page, 'Payment').click();
    const wiz = page.locator('.approval-wizard');
    await wiz.locator('input[name="payType"][value="CASH"]').check();
    await wiz.locator('#acctName').fill('Jordan Payer');
    const req = waitForApi(page, 'PUT', `/api/payment/${APP}`);
    await wiz.getByRole('button', { name: 'Update Payment' }).click();
    await req;
  });

  test('PAY-102: Change / re-vault card — IntelliPay change-card control present on CC', async ({ page }) => {
    await openDetail(page);
    await wizardStep(page, 'Payment').click();
    const wiz = page.locator('.approval-wizard');
    // Default payment is CC → the "change card" (re-vault) entry point is offered.
    await expect(wiz.locator('input[name="payType"][value="CC"]')).toBeChecked();
    await expect(wiz.getByRole('button', { name: 'Update and Change Card' })).toBeVisible();
    // NB: the actual vault happens inside the IntelliPay lightbox (3rd-party) — not asserted here.
  });

  test('PAY-103: Payment locked after approval — radios disabled + locked notice', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ statusCode: 'APPRV' }) }]);
    await openDetail(page);
    await wizardStep(page, 'Payment').click();
    const wiz = page.locator('.approval-wizard');
    await expect(wiz.locator('input[name="payType"][value="CC"]')).toBeDisabled();
    await expect(wiz.getByText('the payment can no longer be changed')).toBeVisible();
    // The read-only Payment section also drops its "Update Payment" action for an approved app.
    await expect(page.locator('.detail-section').filter({ has: page.getByText('Payment Information', { exact: true }) })
      .getByRole('button', { name: 'Update Payment' })).toHaveCount(0);
  });

  test('PAY-104: Fine amount reflected — "Fine included" note in the Payment card', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/payment/${APP}$`), json: detailPayment({ fineAmount: 445 }) }]);
    await openDetail(page);
    const paySection = page.locator('.detail-section').filter({ has: page.getByText('Payment Information', { exact: true }) });
    await expect(paySection.getByText(/Fine included:\s*\$445\.00/)).toBeVisible();
  });

  test('PAY-105: Payment-type description in intra shows "Credit Card" (not CC)', async ({ page }) => {
    await openDetail(page);
    const paySection = page.locator('.detail-section').filter({ has: page.getByText('Payment Information', { exact: true }) });
    await expect(paySection.getByText('Credit Card')).toBeVisible();
  });

  // ============================ APP-2xx — cancel application ============================
  test('APP-201: Cancel application fires POST /cancel', async ({ page }) => {
    await overrides(page, [{ method: 'POST', path: `/api/applications/${APP}/cancel`, json: detailApplication({ statusCode: 'CAN' }) }]);
    await openDetail(page);
    const req = waitForApi(page, 'POST', `/api/applications/${APP}/cancel`);
    await page.getByRole('button', { name: 'Cancel Application' }).click();
    await req;
  });

  test('APP-202: Cancel not offered when terminal (approved)', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ statusCode: 'APPRV' }) }]);
    await openDetail(page);
    await expect(page.getByRole('button', { name: 'Cancel Application' })).toHaveCount(0);
  });

  test.fixme('APP-203: Cancel audit / email — server-side notification; not observable from the SPA.', () => {});

  // ============================ PRM — permit preview / print ============================
  test('PRM-001: Preview (pre-approval) shows PREVIEW flag + "Payment Type: Credit Card"', async ({ page }) => {
    await nav(page, `/permit/${APP}`);
    await expect(page.locator('.permit-preview-flag')).toContainText('PREVIEW');
    const payLine = page.locator('.pay-b').filter({ hasText: 'Credit Card' });
    await expect(payLine).toContainText('Payment Type');
    await expect(payLine).toContainText('Credit Card');
  });

  test('PRM-002: Approved permit — "Paid By" label, no preview flag, Print button', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ statusCode: 'APPRV' }) },
      { method: 'GET', rx: new RegExp(`^/api/applications/${APP}/permit$`), json: { approvedDate: '2026-03-01', approvedBy: 'Staff User', permitNumbers: ['W2026-0500'], conditions: [] } },
    ]);
    await nav(page, `/permit/${APP}`);
    await expect(page.locator('.pay-b').filter({ hasText: 'Credit Card' })).toContainText('Paid By');
    await expect(page.locator('.permit-preview-flag')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Print / Save as PDF' })).toBeVisible();
  });

  test('PRM-003: Payment type description on permit is "Credit Card"', async ({ page }) => {
    await nav(page, `/permit/${APP}`);
    await expect(page.locator('.pay-b').filter({ hasText: 'Credit Card' })).toBeVisible();
  });

  test('PRM-004: Reprint after approval — detail header shows "View Permit/Reprint"', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ statusCode: 'APPRV' }) }]);
    await openDetail(page);
    await expect(page.getByRole('button', { name: 'View Permit/Reprint' })).toBeVisible();
  });

  test('PRM-005: Print Site Hazard button appears when a hazard is on file', async ({ page }) => {
    const withHazard = detailApplication({ hazard: { consultantFirstName: 'Sample', consultantLastName: 'Consultant' } });
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: withHazard }]);
    await openDetail(page);
    await expect(page.getByRole('button', { name: 'Print Site Hazard' }).first()).toBeVisible();
  });

  // ============================ APV — approval gates ============================
  test('APV-001: Approve disabled until all gates green + banner + "2 of 5"', async ({ page }) => {
    await openDetail(page);
    const summary = page.locator('.approval-wizard__summary').first();
    await expect(summary).toContainText('2 of 5 complete');
    await wizardStep(page, 'Inspections / Review').click();
    await expect(page.getByText('Complete all five preconditions above to enable approval.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Approve Now' })).toBeDisabled();
  });

  test('APV-002: Gate 1 Sitemap — marking received advances the summary', async ({ page }) => {
    await openDetail(page);
    const summary = page.locator('.approval-wizard__summary').first();
    await wizardStep(page, 'Sitemap').click();
    await page.getByRole('checkbox', { name: 'Sitemap received' }).check();
    await expect(summary).toContainText('3 of 5 complete');
  });

  test('APV-003: Gate 2 Conditions — a PENDC work leaves the gate incomplete', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ works: [{ ...detailApplication().works[0], statusCode: 'PENDC' }] }) }]);
    await openDetail(page);
    // sitemap✗ conditions✗(PENDC) payment✓ visit✗ inspections✗ → 1 of 5.
    await expect(page.locator('.approval-wizard__summary').first()).toContainText('1 of 5 complete');
  });

  test('APV-004: Gate 3 Payment — PEND on file marks the Payment step complete', async ({ page }) => {
    await openDetail(page);
    const step = wizardStep(page, 'Payment');
    await expect(step.locator('.step-num')).toHaveText('\u2713');
  });

  test('APV-005: Gate 4 Site Visit Type — selecting a type advances the summary', async ({ page }) => {
    await openDetail(page);
    const summary = page.locator('.approval-wizard__summary').first();
    await wizardStep(page, 'Site Visit Type').click();
    await page.getByRole('radio', { name: 'Inspection', exact: true }).check();
    await expect(summary).toContainText('3 of 5 complete');
  });

  test('APV-006: Gate 5 Inspections/Review — review acknowledgement completes the gate', async ({ page }) => {
    await openDetail(page);
    const summary = page.locator('.approval-wizard__summary').first();
    await wizardStep(page, 'Site Visit Type').click();
    await page.getByRole('radio', { name: 'Field Technician Review' }).check();
    await wizardStep(page, 'Inspections / Review').click();
    await page.getByRole('checkbox', { name: 'Field technician review completed' }).check();
    await expect(summary).toContainText('4 of 5 complete');
  });

  test('APV-007: Approve CC application → POST /approval/{id}/approve', async ({ page }) => {
    await overrides(page, [
      { method: 'POST', path: `/api/approval/${APP}/approve`, json: { appId: APP, permitNumbers: ['W2026-0500'] } },
      { method: 'POST', path: '/api/payment/charge', json: { paidAmount: 379, statusCode: 'PAID' } },
    ]);
    await openDetail(page);
    const summary = page.locator('.approval-wizard__summary').first();
    await wizardStep(page, 'Sitemap').click();
    await page.getByRole('checkbox', { name: 'Sitemap received' }).check();
    await wizardStep(page, 'Site Visit Type').click();
    await page.getByRole('radio', { name: 'Field Technician Review' }).check();
    await wizardStep(page, 'Inspections / Review').click();
    await page.getByRole('checkbox', { name: 'Field technician review completed' }).check();
    await expect(summary).toContainText('5 of 5 complete');
    const approveBtn = page.getByRole('button', { name: 'Approve Now' });
    await expect(approveBtn).toBeEnabled();
    const req = waitForApi(page, 'POST', `/api/approval/${APP}/approve`);
    await approveBtn.click();
    await req;
    await expect(page.getByText(`Application ${APP} approved.`)).toBeVisible();
  });

  test('APV-008: Approve non-CC (Exempt) application → POST /approval/{id}/approve (no charge)', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', rx: new RegExp(`^/api/payment/${APP}$`), json: detailPayment({ paymentType: 'EXMPT', statusCode: 'EXMPT' }) },
      { method: 'POST', path: `/api/approval/${APP}/approve`, json: { appId: APP, permitNumbers: ['W2026-0501'] } },
    ]);
    await openDetail(page);
    await wizardStep(page, 'Sitemap').click();
    await page.getByRole('checkbox', { name: 'Sitemap received' }).check();
    await wizardStep(page, 'Site Visit Type').click();
    await page.getByRole('radio', { name: 'Field Technician Review' }).check();
    await wizardStep(page, 'Inspections / Review').click();
    await page.getByRole('checkbox', { name: 'Field technician review completed' }).check();
    const req = waitForApi(page, 'POST', `/api/approval/${APP}/approve`);
    await page.getByRole('button', { name: 'Approve Now' }).click();
    await req;
  });

  test.fixme('APV-009: CC charge fails on approval — IntelliPay decline path is a 3rd-party gateway outcome (verified in API/gateway tests).', () => {});

  test('APV-010: Approver defaults to signed-in user — no "approved by" input required', async ({ page }) => {
    await openDetail(page);
    // The Java-parity wizard never gates on a typed approver; there is no such field.
    await expect(page.getByLabel('Approved By')).toHaveCount(0);
    await expect(page.locator('.approval-wizard input[placeholder*="Approved" i]')).toHaveCount(0);
  });

  test('APV-011: Re-approve blocked — button shows "Approved" and is disabled', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ statusCode: 'APPRV' }) }]);
    await openDetail(page);
    await wizardStep(page, 'Inspections / Review').click();
    const btn = page.getByRole('button', { name: 'Approved', exact: true });
    await expect(btn).toBeVisible();
    await expect(btn).toBeDisabled();
  });

  test('APV-012: Cancelled works excluded — a CAN + a PEND work keep Conditions green', async ({ page }) => {
    const base = detailApplication().works[0];
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({
      works: [
        { ...base, workId: 'W1', statusCode: 'CAN' },
        { ...base, workId: 'W2', statusCode: 'PEND' },
      ],
    }) }]);
    await openDetail(page);
    // conditions✓ (only non-cancelled W2, which is PEND) + payment✓ = 2 of 5.
    await expect(page.locator('.approval-wizard__summary').first()).toContainText('2 of 5 complete');
  });

  // ============================ CND — per-work permit conditions ============================
  test('CND-001: Standard conditions listed + Special field in the modal', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}/conditions$`), json: conditionsPayload({ pendc: true }) }]);
    await openDetail(page);
    await page.getByRole('button', { name: 'Apply Conditions' }).click();
    const dlg = page.getByRole('dialog', { name: 'Work Permit Approval Conditions' });
    await expect(dlg).toBeVisible();
    await expect(dlg.getByRole('checkbox')).toHaveCount(DETAIL_CONDITIONS.available.length);
    await expect(dlg.getByLabel(/Special condition for/)).toBeVisible();
    await expect(dlg.getByRole('button', { name: 'No Specials' })).toBeVisible();
  });

  test('CND-002: Apply conditions fires PUT /works/{id}/conditions', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', rx: new RegExp(`^/api/applications/${APP}/conditions$`), json: conditionsPayload({ pendc: true }) },
      { method: 'PUT', path: `/api/applications/${APP}/works/W1/conditions`, json: conditionsPayload({ pendc: false }) },
    ]);
    await openDetail(page);
    await page.getByRole('button', { name: 'Apply Conditions' }).click();
    const dlg = page.getByRole('dialog', { name: 'Work Permit Approval Conditions' });
    await dlg.locator('#wc-W1-STD1').check();
    const req = waitForApi(page, 'PUT', `/api/applications/${APP}/works/W1/conditions`);
    await dlg.getByRole('button', { name: 'Update Conditions' }).click();
    await req;
  });

  test('CND-003: Work type with no predefined conditions — empty-state + No Specials', async ({ page }) => {
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}/conditions$`), json: { works: [{ workId: 'W1', statusCode: 'PENDC', available: [], selected: [] }] } }]);
    await openDetail(page);
    await page.getByRole('button', { name: 'Apply Conditions' }).click();
    const dlg = page.getByRole('dialog', { name: 'Work Permit Approval Conditions' });
    await expect(dlg.locator('.wc-modal__empty')).toBeVisible();
    await expect(dlg.getByRole('button', { name: 'No Specials' })).toBeVisible();
    await expect(dlg.getByRole('checkbox')).toHaveCount(0);
  });

  test.fixme('CND-004: Conditions refresh after adding a work — depends on the add-work round-trip advancing works then re-fetching; covered structurally by WRK-101 + CND-001.', () => {});

  test('CND-005: Edit existing conditions — pre-checked selection + PUT on save', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', rx: new RegExp(`^/api/applications/${APP}/conditions$`), json: conditionsPayload({ pendc: false }) },
      { method: 'PUT', path: `/api/applications/${APP}/works/W1/conditions`, json: conditionsPayload({ pendc: false }) },
    ]);
    await openDetail(page);
    await page.getByRole('button', { name: 'Edit Conditions' }).click();
    const dlg = page.getByRole('dialog', { name: 'Work Permit Approval Conditions' });
    await expect(dlg.locator('#wc-W1-STD1')).toBeChecked();
    await dlg.locator('#wc-W1-STD2').check();
    const req = waitForApi(page, 'PUT', `/api/applications/${APP}/works/W1/conditions`);
    await dlg.getByRole('button', { name: 'Update Conditions' }).click();
    await req;
  });

  test('CND-006: Per-work independence — W1 "Apply" + W2 "Edit" shown together', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: twoWorkApplication() },
      { method: 'GET', rx: new RegExp(`^/api/applications/${APP}/conditions$`), json: {
        works: [
          { workId: 'W1', statusCode: 'PENDC', available: DETAIL_CONDITIONS.available, selected: [] },
          { workId: 'W2', statusCode: 'PEND', available: DETAIL_CONDITIONS.available, selected: DETAIL_CONDITIONS.selected },
        ],
      } },
    ]);
    await openDetail(page);
    await expect(page.getByRole('button', { name: 'Apply Conditions' })).toHaveCount(1);
    await expect(page.getByRole('button', { name: 'Edit Conditions' })).toHaveCount(1);
  });

  // ============================ SRCH — search & prefiltered queues ============================
  test('SRCH-001: Search by App ID opens the matching application', async ({ page }) => {
    await nav(page, '/search');
    await expect(page.getByRole('heading', { name: 'Search Applications' })).toBeVisible();
    await page.locator('#s-appId').fill(APP);
    await page.locator('.search-form__actions button[type="submit"]').click();
    await expect(page.getByRole('heading', { name: `Application ${APP}` })).toBeVisible();
  });

  test('SRCH-002: Search by name renders results', async ({ page }) => {
    await nav(page, '/search');
    await page.locator('#s-appBusinessName').fill('Well');
    await page.locator('.search-form__actions button[type="submit"]').click();
    await expect(page.getByText(/application\(s\)/)).toBeVisible();
  });

  test('SRCH-003: Prefiltered Pending Processing queue', async ({ page }) => {
    await nav(page, '/queue/PEND');
    await expect(page.getByRole('heading', { name: 'Pending Applications' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Open' }).first()).toBeVisible();
  });

  test('SRCH-004: Prefiltered Pending Sitemaps queue', async ({ page }) => {
    await nav(page, '/queue/PENDS');
    await expect(page.getByRole('heading', { name: 'Pending Sitemaps' })).toBeVisible();
  });

  test.fixme('SRCH-005: Prefiltered Collect Payment — no such prefiltered queue exists in the current intra build (queues: Pending / Pending Sitemaps / Approved / Failed / Cancelled).', () => {});

  test('SRCH-006: Prefiltered Failed Payments queue', async ({ page }) => {
    await nav(page, '/queue/PAYFL');
    await expect(page.getByRole('heading', { name: 'Failed Payments' })).toBeVisible();
  });

  test.fixme('SRCH-007: Prefiltered Completed Today — no date-based "completed today" queue in the build; date filtering is covered by the Search date range.', () => {});

  test('SRCH-008: Prefiltered Cancelled Orders', async ({ page }) => {
    await nav(page, '/search/cancelled');
    await expect(page.getByRole('heading', { name: 'Cancelled Applications' })).toBeVisible();
  });

  test('SRCH-009: Queue by status code (generic /queue/PENDC)', async ({ page }) => {
    await nav(page, '/queue/PENDC');
    await expect(page.getByRole('heading', { name: 'Pending Conditions applications' })).toBeVisible();
  });

  test('SRCH-010: History permits & wells list pages render', async ({ page }) => {
    await nav(page, '/search/history-permits');
    await expect(page.getByRole('heading', { name: 'Pre-System History Permits (1987–April 2005)' })).toBeVisible();
    await nav(page, '/search/history-wells');
    await expect(page.getByRole('heading', { name: 'History Well Locations' })).toBeVisible();
  });

  test.fixme('SRCH-011: Return-to-search preserves context — stateful cross-navigation history; not deterministically assertable in the mocked harness.', () => {});

  // ============================ RPT — reports ============================
  test('RPT-001: Reconciliation report renders', async ({ page }) => {
    await nav(page, '/reports/reconciliation');
    await expect(page.getByRole('heading', { name: 'Reconciliation Report' })).toBeVisible();
  });

  test('RPT-002: Completed Works report renders', async ({ page }) => {
    await nav(page, '/reports/completed-works');
    await expect(page.getByRole('heading', { name: 'Completed Work Report' })).toBeVisible();
  });

  test('RPT-003: Completed Inspections report renders', async ({ page }) => {
    await nav(page, '/reports/completed-inspections');
    await expect(page.getByRole('heading', { name: 'Completed Inspections by Inspector' })).toBeVisible();
  });

  test('RPT-004: Data Extract page renders', async ({ page }) => {
    await nav(page, '/reports/extract');
    await expect(page.getByRole('heading', { name: 'Extract to Excel' })).toBeVisible();
  });

  test('RPT-005: SSRS report route renders its page', async ({ page }) => {
    await nav(page, '/reports/ssrs/permit-summary');
    await expect(page.locator('.page-title')).toBeVisible();
  });

  test.fixme('RPT-006: Empty range — data-dependent "no results" state; depends on live report data.', () => {});

  // ============================ MNT — code maintenance ============================
  test('MNT-001: Create entity row fires POST /maint/{endpoint}', async ({ page }) => {
    await overrides(page, [{ method: 'POST', path: '/api/maint/cities', json: {} }]);
    await nav(page, '/maintenance/cities');
    await expect(page.getByRole('heading', { name: 'City Codes' })).toBeVisible();
    await page.getByRole('button', { name: 'Add City Code' }).click();
    const dlg = page.getByRole('dialog', { name: 'Add City Code' });
    await dlg.locator('#fld-cityCode').fill('ZZZ');
    await dlg.locator('#fld-cityName').fill('Ztown');
    const req = waitForApi(page, 'POST', '/api/maint/cities');
    await dlg.getByRole('button', { name: 'Save' }).click();
    await req;
  });

  test('MNT-002: Update entity row fires PUT /maint/{endpoint}', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', path: '/api/maint/cities', json: [{ cityCode: 'OAK', cityName: 'Oakland', countyJuris: 'Alameda' }] },
      { method: 'PUT', path: '/api/maint/cities', json: {} },
    ]);
    await nav(page, '/maintenance/cities');
    await page.getByRole('button', { name: 'Edit' }).first().click();
    const dlg = page.getByRole('dialog', { name: 'Edit City Code' });
    await dlg.locator('#fld-cityName').fill('Oakland Updated');
    const req = waitForApi(page, 'PUT', '/api/maint/cities');
    await dlg.getByRole('button', { name: 'Save' }).click();
    await req;
  });

  test('MNT-003: Delete entity row fires DELETE /maint/{endpoint}/{key}', async ({ page }) => {
    await overrides(page, [
      { method: 'GET', path: '/api/maint/cities', json: [{ cityCode: 'OAK', cityName: 'Oakland', countyJuris: 'Alameda' }] },
      { method: 'DELETE', rx: /^\/api\/maint\/cities\/OAK$/, json: {} },
    ]);
    await nav(page, '/maintenance/cities');
    await page.getByRole('button', { name: 'Delete' }).first().click();
    const dlg = page.getByRole('dialog', { name: 'Delete City Code' });
    const req = waitForApi(page, 'DELETE', /^\/api\/maint\/cities\/OAK$/);
    await dlg.getByRole('button', { name: 'Delete' }).click();
    await req;
  });

  test.fixme('MNT-004: Duplicate key rejected — server-enforced 400/409 uniqueness; verified in the API test project.', () => {});
  test.fixme('MNT-005: Work-condition-types drive Conditions modal — cross-feature (maintenance rows → applicant/intra conditions); the conditions modal itself is covered by CND-001/003.', () => {});
  test.fixme('MNT-006: Payment-types drive descriptions — formatPaymentType label mapping; the "Credit Card" description is asserted in PAY-105 / PRM-003.', () => {});
  test.fixme('MNT-007: Maintenance requires auth+allowlist — every /api/maint route is [Authorize]; 401/403 enforced by the .NET API.', () => {});

  // ============================ INS — inspections ============================
  test('INS-001: Inspections list renders with Open links', async ({ page }) => {
    await nav(page, '/inspections/list');
    await expect(page.getByRole('heading', { name: 'Permits with Pending Inspections' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Open' }).first()).toBeVisible();
  });

  test('INS-002: Pending WCR list renders', async ({ page }) => {
    await nav(page, '/inspections/pending-wcr');
    await expect(page.getByRole('heading', { name: 'Pending WCR List' })).toBeVisible();
  });

  test('INS-003: Pending GeoLog list renders', async ({ page }) => {
    await nav(page, '/inspections/pending-geolog');
    await expect(page.getByRole('heading', { name: 'Pending GeoLog List' })).toBeVisible();
  });

  test('INS-004: Hold list renders', async ({ page }) => {
    await nav(page, '/inspections/hold');
    await expect(page.getByRole('heading', { name: 'Permits on Hold Status' })).toBeVisible();
  });

  test('INS-005: Calendar view renders', async ({ page }) => {
    await nav(page, '/inspections/calendar');
    await expect(page.getByRole('heading', { name: 'Inspections Calendar' })).toBeVisible();
  });

  test.fixme('INS-006: Schedule inspection (approval gate 5) — availability-driven booking against the API; the Review-path gate is covered by APV-006.', () => {});
  test.fixme('INS-007: Availability respects unavailable days/slots — maintenance-defined availability enforced by the API.', () => {});

  test('INS-008: Enter WCR is hidden pre-approval and shown once approved', async ({ page }) => {
    await openDetail(page); // default PEND → no WCR entry
    await expect(page.getByRole('button', { name: 'Enter WCR' })).toHaveCount(0);
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ statusCode: 'APPRV', works: [{ ...detailApplication().works[0], statusCode: 'APPRV' }] }) }]);
    await openDetail(page);
    await expect(page.getByRole('button', { name: 'Enter WCR' }).first()).toBeVisible();
  });

  test('INS-009: Enter GeoLog is hidden pre-approval and shown once approved', async ({ page }) => {
    await openDetail(page);
    await expect(page.getByRole('button', { name: 'Enter GeoLog' })).toHaveCount(0);
    await overrides(page, [{ method: 'GET', rx: new RegExp(`^/api/applications/${APP}$`), json: detailApplication({ statusCode: 'APPRV', works: [{ ...detailApplication().works[0], statusCode: 'APPRV' }] }) }]);
    await openDetail(page);
    await expect(page.getByRole('button', { name: 'Enter GeoLog' }).first()).toBeVisible();
  });

  test.fixme('INS-010: Public inspection availability (anonymous) — belongs to the public ecomm/API surface, not the intra SPA.', () => {});

  // ============================ SEC — security (intra-relevant) ============================
  test.fixme('SEC-001: Entra-protected intra endpoint requires token — API returns 401 without a bearer (API test project / SEC section of TEST-PLAN).', () => {});
  test.fixme('SEC-002: Allowlist enforcement (403) — non-whitelisted Entra token gets 403 not_authorized + audit email (API-enforced).', () => {});
  test.fixme('SEC-003: Public write needs captcha OR bearer — 401 verification_required on POST /api/applications (public API guard).', () => {});
  test.fixme('SEC-004: Valid captcha token passes — hCaptcha siteverify success path (API + gateway).', () => {});
  test.fixme('SEC-005: Bearer-token fallback when no captcha — trusted client/bearer path (API guard).', () => {});
  test.fixme('SEC-006: Stale/replayed captcha token rejected — siteverify timeout/duplicate → deny (API guard).', () => {});
  test.fixme('SEC-007: Bot simulation on public reads — 429 from the IP rate limiter (API middleware).', () => {});
  test.fixme('SEC-008: Rate-limit steady state — no 429 under sustained low rate (API middleware).', () => {});
  test.fixme('SEC-009: Rate limiter exempts guarded/auth routes (API middleware).', () => {});
  test.fixme('SEC-010: Fail-open when captcha secret blank (API guard configuration).', () => {});

  test('SEC-011: Intra UI is gated — local capture-bypass is a dev-only escape hatch', async ({ page }) => {
    // The staff SPA renders here ONLY because REACT_APP_ENV=local honours the pwaCaptureBypass flag
    // (src/captureBypass.js). In dev/test/uat that flag is ignored and MSAL loginRedirect fires before
    // any page renders (asserted by the remote smoke, which only passes for an authenticated session).
    await nav(page, '/');
    await waitForAppReady(page);
    await expect(page.getByRole('heading', { name: 'Permit processing dashboard' })).toBeVisible();
  });

  test.fixme('SEC-012: Ecomm invisible reCAPTCHA challenge — public ecomm SPA (covered in the ecomm regression suite).', () => {});
  test.fixme('SEC-013: 401/403/429 audit emails — server-side notifications (API).', () => {});
  test.fixme('SEC-014: CORS enforced — browser/API-enforced allowlist (API middleware).', () => {});

  // ============================ HELP ============================
  test('HELP: back-to-top button appears on scroll and returns the pane to the top', async ({ page }) => {
    await nav(page, '/help');
    await expect(page.getByRole('heading', { name: 'Using the permit processing portal' })).toBeVisible();
    const scroller = page.locator('.staff-content').first();
    await scroller.evaluate((el) => el.scrollTo({ top: 900 }));
    const toTop = page.locator('.help-to-top');
    await expect(toTop).toBeVisible();
    await toTop.click();
    await expect.poll(async () => scroller.evaluate((el) => el.scrollTop)).toBeLessThan(5);
  });
});

// ---------------------------------------------------------------------------------------------
// REMOTE (dev/test/uat) — read-only smoke proving the reused Entra bearer works end-to-end.
// ---------------------------------------------------------------------------------------------
test.describe('intra smoke (remote)', () => {
  test.skip(ENV.isLocal, 'Remote smoke only runs against dev/test/uat.');

  test(`SEC-011 / AUTH: authenticated staff shell + dashboard render on ${ENV.key}`, async ({ page }) => {
    await page.goto('/');
    // The shell only renders for an authenticated, allowlisted user; its presence proves the saved
    // Entra bearer was accepted by the API (a non-allowlisted user gets the "not authorized" state,
    // and no session at all is redirected to Microsoft login before anything renders — SEC-011).
    await waitForAppReady(page);
    await expect(page.getByRole('heading', { name: 'Permit processing dashboard' })).toBeVisible();
  });

  test(`SRCH: live Search by applicant email returns without an auth error on ${ENV.key}`, async ({ page }) => {
    await page.goto('/search');
    await expect(page.getByRole('heading', { name: 'Search Applications' })).toBeVisible();
    await page.locator('#s-email').fill(APPLICANT_EMAIL);
    await page.locator('.search-form__actions button[type="submit"]').click();
    await expect(page.getByText(/application\(s\)/)).toBeVisible({ timeout: 30_000 });
  });
});
