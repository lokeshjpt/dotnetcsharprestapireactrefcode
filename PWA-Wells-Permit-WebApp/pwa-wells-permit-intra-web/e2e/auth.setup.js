// Entra (Azure AD) authentication setup for the intra regression suite.
//
// The intra SPA is Entra-protected: index.js triggers an MSAL login redirect when no cached account
// exists, and axiosInstance attaches the acquired bearer to every /api call. MSAL caches its tokens
// in localStorage (see src/authConfig.js -> cache.cacheLocation = 'localStorage'), which Playwright's
// storageState captures — so we sign in ONCE interactively, save the state, and every later run
// reuses the bearer token without another login.
//
// This runs as the `setup` project (a dependency of the main `chromium` project) for every LIVE run
// (dev/test/uat always; local only when PWA_LIVE=1). The non-live local suite uses the capture-bypass
// path instead, so no login is needed there. Setup is skipped when a recent saved state already
// exists (set FORCE_LOGIN=1 to sign in again).
const fs = require('fs');
const path = require('path');
const os = require('os');
const { test: setup, expect } = require('@playwright/test');
const { resolveEnv, authStatePath } = require('./environments');

const ENV = resolveEnv();
const AUTH_FILE = authStatePath(ENV.key);
// Shared file that carries the raw Entra access token to the ecomm live suite, so ONE Microsoft
// sign-in unlocks the guarded public-write endpoints for both apps (the API's captcha guard accepts a
// valid Entra bearer as proof). Keyed by env so parallel envs don't clash.
const BEARER_FILE = path.join(os.tmpdir(), `pwa-e2e-bearer-${ENV.key}.txt`);
// Reuse a saved session for up to 8 hours (a comfortable working day inside the MSAL refresh-token
// lifetime); after that, sign in again so the token stays valid.
const MAX_AGE_MS = 8 * 60 * 60 * 1000;

function haveFreshState() {
  try {
    if (process.env.FORCE_LOGIN === '1') return false;
    const stat = fs.statSync(AUTH_FILE);
    return Date.now() - stat.mtimeMs < MAX_AGE_MS;
  } catch {
    return false;
  }
}

// Pulls the raw MSAL access-token JWT out of a Playwright storageState. Newer @azure/msal-browser
// encrypts the cached token values in localStorage ({id,nonce,data}), so this often can't recover the
// JWT; the live capture below (sniffing a real request's Authorization header) is the reliable path
// and this stays only as a best-effort fallback.
function extractBearer(state) {
  for (const origin of state.origins || []) {
    for (const item of origin.localStorage || []) {
      if (!/accesstoken/i.test(item.name)) continue;
      try {
        const parsed = JSON.parse(item.value);
        if (parsed && parsed.credentialType === 'AccessToken' && parsed.secret) return parsed.secret;
      } catch {
        /* not the entry we want */
      }
    }
  }
  return null;
}

setup('authenticate with Entra', async ({ page }) => {
  setup.skip(ENV.isLocal && !ENV.isLive, 'Local (mocked) uses capture-bypass; no Entra sign-in required.');
  setup.skip(haveFreshState(), `Reusing saved Entra session (${AUTH_FILE}). Set FORCE_LOGIN=1 to re-login.`);

  // Give a real person time to complete the Microsoft sign-in in the opened (headed) browser.
  setup.setTimeout(5 * 60 * 1000);

  // Sniff the Entra bearer from the SPA's own /api calls (axiosInstance attaches it after MSAL
  // acquires a token). This is reliable regardless of MSAL's encrypted localStorage cache format.
  let sniffedBearer = null;
  await page.route('**/api/**', async (route) => {
    const req = route.request();
    if (!sniffedBearer) {
      const auth = req.headers()['authorization'];
      if (auth && /^Bearer\s+/i.test(auth)) sniffedBearer = auth.replace(/^Bearer\s+/i, '').trim();
    }
    return route.continue();
  });

  await page.goto(ENV.app + '/');

  // After a successful sign-in the SPA renders the staff shell (sidebar). MSAL may bounce through
  // login.microsoftonline.com first; we just wait for the authenticated app to appear.
  await expect(page.locator('.sidebar').first()).toBeVisible({ timeout: 5 * 60 * 1000 });

  // Give the dashboard's authenticated /api calls a moment to fire so we capture the bearer. If the
  // landing view makes none, nudge to Search (which always queries the API).
  for (let i = 0; i < 20 && !sniffedBearer; i += 1) {
    await page.waitForTimeout(500);
    if (!sniffedBearer && i === 6) {
      await page.goto(ENV.app + '/search').catch(() => {});
    }
  }

  fs.mkdirSync(path.dirname(AUTH_FILE), { recursive: true });
  const state = await page.context().storageState({ path: AUTH_FILE });
  console.log(`Saved Entra session to ${AUTH_FILE}`);

  const bearer = sniffedBearer || extractBearer(state);
  if (bearer) {
    fs.writeFileSync(BEARER_FILE, bearer, 'utf8');
    console.log(`Saved Entra bearer for the ecomm live suite to ${BEARER_FILE}`);
  } else {
    console.warn('Could not capture an access token; ecomm live writes may 401. Re-run the intra live setup.');
  }
});
