// Environment matrix for the ecomm (public) regression runner.
//
// PRODUCTION IS DELIBERATELY EXCLUDED: the regression suite may create/track applications and must
// never be pointed at prod. `normalizeEnv` throws if anyone tries.
//
// `app` is the SPA base URL Playwright drives; `api` is the .NET API the SPA talks to (informational
// here — the deployed SPA already targets its own API; for local the dev server is booted with
// REACT_APP_ENV=local). Values mirror src/config/config.<env>.js.
const ENVIRONMENTS = {
  local: { app: 'http://localhost:3000', api: 'https://localhost:7242', reactAppEnv: 'local' },
  dev: { app: 'https://pwawellpermitsd.alamedacountyca.gov', api: 'https://pwawellpermitsapid.acgov.org', reactAppEnv: 'dev' },
  test: { app: 'https://pwawellpermitst.alamedacountyca.gov', api: 'https://pwawellpermitsapit.acgov.org', reactAppEnv: 'test' },
  uat: { app: 'https://pwawellpermitsu.alamedacountyca.gov', api: 'https://pwawellpermitsapiu.acgov.org', reactAppEnv: 'uat' },
};

const ALLOWED = Object.keys(ENVIRONMENTS);

// Normalises an environment key and hard-blocks production. Returns one of ALLOWED.
function normalizeEnv(raw) {
  const key = String(raw || 'local').trim().toLowerCase();
  if (key === 'prod' || key === 'production' || key === 'prd') {
    throw new Error('Production is NOT an allowed regression target. Choose: ' + ALLOWED.join(', ') + '.');
  }
  if (key === 'tst') return 'test'; // tolerate the Java-side "tst" alias
  if (!ENVIRONMENTS[key]) {
    throw new Error(`Unknown environment "${raw}". Choose one of: ${ALLOWED.join(', ')}.`);
  }
  return key;
}

// PWA_LIVE=1 opts the LOCAL run into "live" mode: instead of mocking every /api/** call, the suite
// drives the REAL local .NET API (real DB writes). The guarded public-write endpoints accept a valid
// Entra bearer as proof, so the live suite reuses the bearer captured by the intra auth.setup (one
// Microsoft sign-in unlocks both apps). dev/test/uat already hit their own real API.
function isLiveRequested() {
  return /^(1|y|yes|true|on)$/i.test(String(process.env.PWA_LIVE || ''));
}

// Resolves the active environment from PWA_ENV (default local). Used by playwright.config.js + specs.
function resolveEnv() {
  const key = normalizeEnv(process.env.PWA_ENV);
  const isLocal = key === 'local';
  const isLive = isLocal ? isLiveRequested() : true;
  return { key, ...ENVIRONMENTS[key], isLocal, isLive };
}

module.exports = { ENVIRONMENTS, ALLOWED, normalizeEnv, resolveEnv };
