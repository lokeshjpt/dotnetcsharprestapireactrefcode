// Captures the screenshots used by the intra in-app Help page (public/help/intra/*.png).
//
// This is NOT part of a normal assertion suite — it only runs when CAPTURE=1 (see the `capture:help`
// npm script). Because the intra app is Entra-protected, the run is HEADED: the first navigation
// triggers a Microsoft sign-in that a real staff user completes in the opened browser window. Every
// /api/** call is mocked (see e2e/helpers.js) with canned, non-PII data, so the screenshots contain
// no real applicant information and no backend/database/VPN is required.
const fs = require('fs');
const path = require('path');
const { test } = require('@playwright/test');
const { installApiMocks, enableCaptureBypass, injectCaptureStyles, waitForAppReady, DETAIL_APP_ID } = require('./helpers');

const CAPTURE = process.env.CAPTURE === '1';
const OUT_DIR = path.join(__dirname, '..', 'public', 'help', 'intra');

test.describe('Help screenshots (intra)', () => {
  test.skip(!CAPTURE, 'Set CAPTURE=1 (npm run capture:help) to regenerate Help screenshots.');

  test.use({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 2 });

  test('Capture every staff page', async ({ page }) => {
    fs.mkdirSync(OUT_DIR, { recursive: true });
    await enableCaptureBypass(page);
    await injectCaptureStyles(page);
    await installApiMocks(page);

    const shot = async (name, locator) => {
      await page.waitForTimeout(500);
      if (locator) {
        await locator.screenshot({ path: path.join(OUT_DIR, name) });
      } else {
        await page.screenshot({ path: path.join(OUT_DIR, name) });
      }
    };

    // Navigating directly between two already-loaded SPA routes is intermittently aborted by
    // Chromium (ERR_ABORTED) when in-flight (mocked) fetches are cancelled mid-navigation. Retry the
    // whole about:blank -> target sequence; about:blank first forces a clean full document load.
    const isNavError = (e) => {
      const m = String(e);
      return m.includes('ERR_ABORTED') || m.includes('interrupted') || m.includes('net::');
    };
    const nav = async (url) => {
      let lastErr;
      for (let attempt = 0; attempt < 8; attempt += 1) {
        try {
          await page.goto('about:blank', { waitUntil: 'load' });
          await page.goto(url, { waitUntil: 'domcontentloaded' });
          return;
        } catch (e) {
          if (!isNavError(e)) throw e;
          lastErr = e;
          await page.waitForTimeout(700);
        }
      }
      throw lastErr;
    };

    // 1) Local capture bypass renders the app without an Entra sign-in; wait for the staff shell.
    await nav('/');
    await waitForAppReady(page);

    const shell = () => page.locator('.page-shell').first();

    // Dashboard — full app chrome (header + sidebar + content) so the overview shows navigation.
    await page.getByRole('heading', { name: 'Permit processing dashboard' }).waitFor();
    await shot('home-01-dashboard.png');

    // From here on capture individual .page-shell elements. Several pages (detail, search) are taller
    // than the default viewport, and the app's fixed-height layout scrolls .staff-content internally
    // — which makes element screenshots capture the content scrolled a few px right (clipping the
    // left edge). A tall viewport lets every page fit with no internal scroll, so captures are clean.
    await page.setViewportSize({ width: 1440, height: 2200 });

    // 2) Work queues — Pending Applications, then the three other Processing queues.
    await nav('/queue/PEND');
    await page.getByRole('link', { name: 'Open' }).first().waitFor();
    await shot('queue-01-pending.png', shell());

    await nav('/queue/PENDS');
    await page.getByRole('link', { name: 'Open' }).first().waitFor();
    await shot('queue-02-pends.png', shell());

    await nav('/queue/APPRV');
    await page.getByRole('link', { name: 'Open' }).first().waitFor();
    await shot('queue-03-apprv.png', shell());

    await nav('/queue/PAYFL');
    await page.getByRole('link', { name: 'Open' }).first().waitFor();
    await shot('queue-04-payfl.png', shell());

    // 3) Application detail (includes the approval wizard).
    await nav(`/applications/${DETAIL_APP_ID}`);
    await page.locator('.approval-wizard').first().waitFor();
    await page.getByRole('heading', { name: `Application ${DETAIL_APP_ID}` }).waitFor();
    await shot('detail-01-review.png', shell());
    await shot('detail-02-approval.png', page.locator('.approval-wizard').first());

    // 4) Search — run a search so the results table is populated.
    await nav('/search');
    await page.getByRole('heading', { name: 'Search Applications' }).waitFor();
    await page.locator('#s-appBusinessName').fill('Well');
    await page.locator('.search-form__actions button[type="submit"]').click();
    await page.getByText(/application\(s\)/).waitFor();
    await shot('search-01-results.png', shell());

    // 4b) History Permits — the pre-system permit search (does not auto-run, so click Search).
    await nav('/search/history-permits');
    await page.getByRole('heading', { name: /Pre-System History Permits/ }).waitFor();
    await page.locator('.search-form__actions button[type="submit"]').click();
    await page.getByText(/permit\(s\)/).waitFor();
    await shot('search-02-history-permits.png', shell());

    // 4c) History Well Locations.
    await nav('/search/history-wells');
    await page.getByRole('heading', { name: 'History Well Locations' }).waitFor();
    await page.locator('.search-form__actions button[type="submit"]').click();
    await page.getByText(/well\(s\)/).waitFor();
    await shot('search-03-history-wells.png', shell());

    // 4d) Cancelled Applications.
    await nav('/search/cancelled');
    await page.getByRole('heading', { name: 'Cancelled Applications' }).waitFor();
    await page.locator('.search-form__actions button[type="submit"]').click();
    await page.getByText(/application\(s\)/).waitFor();
    await shot('search-04-cancelled.png', shell());

    // 5) Inspections — pending list.
    await nav('/inspections/list');
    await page.getByRole('heading', { name: 'Permits with Pending Inspections' }).waitFor();
    await page.getByText(/permit\(s\)/).waitFor();
    await shot('inspections-01-list.png', shell());

    // 5b) Pending WCR List (auto-runs on mount).
    await nav('/inspections/pending-wcr');
    await page.getByRole('heading', { name: 'Pending WCR List' }).waitFor();
    await page.getByText(/permit\(s\)/).waitFor();
    await shot('inspections-03-pending-wcr.png', shell());

    // 5c) Pending GeoLog List (auto-runs on mount).
    await nav('/inspections/pending-geolog');
    await page.getByRole('heading', { name: 'Pending GeoLog List' }).waitFor();
    await page.getByText(/permit\(s\)/).waitFor();
    await shot('inspections-04-pending-geolog.png', shell());

    // 5d) Permits On Hold List (auto-runs on mount).
    await nav('/inspections/hold');
    await page.getByRole('heading', { name: 'Permits on Hold Status' }).waitFor();
    await page.getByText(/permit\(s\)/).waitFor();
    await shot('inspections-05-hold.png', shell());

    // 6) Inspections — calendar.
    await nav('/inspections/calendar');
    await page.getByRole('heading', { name: 'Inspections Calendar' }).waitFor();
    await page.waitForTimeout(700);
    await shot('inspections-02-calendar.png', shell());

    // 7) Reports hub.
    await nav('/reports');
    await page.getByRole('heading', { name: 'Reports', exact: true }).waitFor();
    await shot('reports-01-hub.png', shell());

    // 8) Code Maintenance menu (static tiles, no data fetch).
    await nav('/maintenance');
    await page.getByRole('heading', { name: 'Code Maintenance' }).waitFor();
    await shot('maintenance-01-menu.png', shell());
  });
});
