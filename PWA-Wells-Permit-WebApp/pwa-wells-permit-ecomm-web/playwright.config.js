// Playwright configuration for the ecomm invisible-reCAPTCHA end-to-end tests.
//
// The specs exercise the REAL invisible Google reCAPTCHA widget (using Google's universal test site
// key, which always solves silently) so they need outbound internet to www.google.com/recaptcha. The
// .NET API is NOT required: every /api/** call is intercepted with page.route and answered with
// canned data, so the tests run without the backend, database or VPN. See e2e/README.md.
const { defineConfig, devices } = require('@playwright/test');
const { resolveEnv } = require('./e2e/environments');

// Target environment is chosen by PWA_ENV (default local); production is refused by resolveEnv().
// For local we boot/reuse the CRA dev server; for dev/test/uat we drive the already-deployed SPA.
const ENV = resolveEnv();

module.exports = defineConfig({
  testDir: './e2e',
  // Real reCAPTCHA + CRA dev server means individual steps can be a little slow.
  timeout: 90_000,
  expect: { timeout: 15_000 },
  // A single dev server is shared, and each spec drives the live reCAPTCHA service, so run serially.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: ENV.app,
    actionTimeout: 15_000,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    // Set PWA_SLOWMO=500 (ms) to slow the browser down so a headed run is easy to watch.
    launchOptions: { slowMo: Number(process.env.PWA_SLOWMO) || 0 },
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  // Only manage a dev server for local. Boots `npm start` (CRA dev server) on port 3000 with the
  // local config (test reCAPTCHA site key); reuses an already-running dev server when present.
  webServer: ENV.isLocal
    ? {
        command: 'npm start',
        url: 'http://localhost:3000',
        reuseExistingServer: true,
        timeout: 180_000,
        env: {
          BROWSER: 'none',
          REACT_APP_ENV: 'local',
          CI: 'false',
        },
      }
    : undefined,
});
