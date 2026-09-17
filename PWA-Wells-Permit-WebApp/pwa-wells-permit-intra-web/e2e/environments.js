// Environment matrix for the intra (staff) regression runner.
//
// PRODUCTION IS DELIBERATELY EXCLUDED — the suite signs in with a real Entra account and reads live
// data; it must never target prod. `normalizeEnv` throws on any prod alias.
//
// `app` is the staff SPA base URL Playwright drives; `api` is the .NET API it calls. Values mirror
// src/config/config.<env>.js. For local the CRA dev server (REACT_APP_ENV=local, redirect URI
// http://localhost:3003/) is booted/reused; dev/test/uat drive the deployed admin site.
const ENVIRONMENTS = {
  local: { app: 'http://localhost:3003', api: 'https://localhost:7242', reactAppEnv: 'local' },
  dev: { app: 'https://pwawellpermitsadmind.acgov.org', api: 'https://pwawellpermitsapid.acgov.org', reactAppEnv: 'dev' },
  test: { app: 'https://pwawellpermitsadmint.acgov.org', api: 'https://pwawellpermitsapit.acgov.org', reactAppEnv: 'test' },
  uat: { app: 'https://pwawellpermitsadminu.acgov.org', api: 'https://pwawellpermitsapiu.acgov.org', reactAppEnv: 'uat' },
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

// PWA_LIVE=1 opts the LOCAL run into "live" mode: instead of the capture-bypass + mocked /api path,
// the suite performs a real Entra sign-in (auth.setup) and drives the real local .NET API (real DB
// writes). dev/test/uat are always live (they have no bypass/mocks), so the flag only changes local.
function isLiveRequested() {
  return /^(1|y|yes|true|on)$/i.test(String(process.env.PWA_LIVE || ''));
}

// Resolves the active environment from PWA_ENV (default local). Used by playwright.config.js + specs.
function resolveEnv() {
  const key = normalizeEnv(process.env.PWA_ENV);
  const isLocal = key === 'local';
  // Remote envs are inherently live; local is live only when explicitly requested.
  const isLive = isLocal ? isLiveRequested() : true;
  return { key, ...ENVIRONMENTS[key], isLocal, isLive };
}

const path = require('path');
// The Playwright storageState file that persists the signed-in Entra session (MSAL caches its tokens
// in localStorage, which storageState captures) so later runs reuse the bearer token without a
// fresh interactive login. One file per environment.
function authStatePath(key) {
  return path.join(__dirname, '.auth', `intra-${key}.json`);
}

module.exports = { ENVIRONMENTS, ALLOWED, normalizeEnv, resolveEnv, authStatePath };
