// Playwright configuration for the intra staff app.
//
// Target environment is chosen by PWA_ENV (default local); production is refused by resolveEnv().
//   * local            — the CRA dev server (REACT_APP_ENV=local) is booted/reused on port 3003 and
//                        specs use the local capture-bypass + mocked /api (no Entra sign-in).
//   * dev | test | uat — drives the DEPLOYED admin site. The `setup` project signs in to Entra once
//                        (headed) and saves the session to e2e/.auth/intra-<env>.json; the main
//                        `chromium` project reuses that storageState so the bearer token is attached
//                        to every live /api call.
const { defineConfig, devices } = require('@playwright/test');
const { resolveEnv, authStatePath } = require('./e2e/environments');

const ENV = resolveEnv();

// Remote runs go through a headed Entra sign-in (setup) and then reuse the saved storageState.
const remoteProjects = [
  { name: 'setup', testMatch: /auth\.setup\.js/, use: { ...devices['Desktop Chrome'], headless: false } },
  {
    name: 'chromium',
    use: { ...devices['Desktop Chrome'], storageState: authStatePath(ENV.key) },
    dependencies: ['setup'],
    testIgnore: /auth\.setup\.js/,
  },
];

// Local runs need no login; the capture-bypass + mocks render the app directly.
const localProjects = [
  { name: 'chromium', use: { ...devices['Desktop Chrome'] }, testIgnore: /auth\.setup\.js/ },
];

module.exports = defineConfig({
  testDir: './e2e',
  timeout: 120_000,
  expect: { timeout: 20_000 },
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: ENV.app,
    actionTimeout: 20_000,
    trace: 'off',
    screenshot: 'off',
    // Set PWA_SLOWMO=500 (ms) to slow the browser down so a headed run is easy to watch.
    launchOptions: { slowMo: Number(process.env.PWA_SLOWMO) || 0 },
  },
  projects: ENV.isLocal && !ENV.isLive ? localProjects : remoteProjects,
  // Manage a dev server only for local (live or mocked). Reuse the dev server started by
  // Start-Local.ps1 (port 3003, REACT_APP_ENV=local) when present; otherwise boot it. In live mode
  // the specs perform a real Entra sign-in and hit the real local API (no capture-bypass, no mocks).
  webServer: ENV.isLocal
    ? {
        command: 'npm start',
        url: 'http://localhost:3003',
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
