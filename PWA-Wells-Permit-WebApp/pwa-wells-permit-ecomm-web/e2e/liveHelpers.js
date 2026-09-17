// Helpers for the LIVE ecomm suite (PWA_LIVE=1) that drives the REAL local .NET API — no /api mocks.
//
// The public write endpoints (POST /api/applications, .../sitemap, .../documents) are protected by the
// server-side PublicAccessGuardFilter, which accepts EITHER a valid Google reCAPTCHA token OR a valid Entra
// bearer ("...or sign in with an authorized account"). An automated browser can't silently solve the
// real invisible reCAPTCHA, so the live suite instead reuses the Entra bearer captured by the intra
// auth.setup (one Microsoft sign-in unlocks both apps) and attaches it as an Authorization header on
// every real /api call. The request still hits the real API and writes to the real DB — only an auth
// header is added; no response is mocked.
const fs = require('fs');
const os = require('os');
const path = require('path');
const { resolveEnv } = require('./environments');

const ENV = resolveEnv();
const BEARER_FILE = path.join(os.tmpdir(), `pwa-e2e-bearer-${ENV.key}.txt`);

// Reads the Entra access token saved by the intra auth.setup. Returns null when absent (the caller
// should skip the live write specs with a clear message telling the user to sign in via intra first).
function readBearer() {
  try {
    const token = fs.readFileSync(BEARER_FILE, 'utf8').trim();
    return token || null;
  } catch {
    return null;
  }
}

// Attaches `Authorization: Bearer <token>` to every real /api call so the guarded public writes pass
// the server-side captcha guard. Third-party URLs that merely contain "/api/" (e.g. Google Maps) are
// left untouched. Call this BEFORE navigating.
async function attachBearer(page, token) {
  if (!token) return;
  await page.route('**/api/**', async (route) => {
    const req = route.request();
    let pathname;
    try {
      pathname = new URL(req.url()).pathname;
    } catch {
      return route.fallback();
    }
    if (!pathname.startsWith('/api/')) return route.fallback();
    const headers = { ...req.headers(), authorization: `Bearer ${token}` };
    return route.continue({ headers });
  });
}

module.exports = { BEARER_FILE, readBearer, attachBearer };
