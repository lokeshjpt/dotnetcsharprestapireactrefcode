// One-off capture of the REAL IntelliPay secure card-entry lightbox for the Help page
// (public/help/ecomm/apply-08-intellipay.png). Unlike capture-help.spec.js this must reach the live
// API (/api/payment/lightbox returns the genuine IntelliPay autoterminal bundle), so it mocks every
// OTHER /api call but lets the lightbox call — and the cpteller.com iframe — go to the network.
const fs = require('fs');
const path = require('path');
const { chromium } = require('@playwright/test');

const OUT = path.join(__dirname, '..', 'public', 'help', 'ecomm', 'apply-08-intellipay.png');
const APP_ID = String(Date.now());

function futureWeekdayIso(days) {
  const d = new Date();
  d.setDate(d.getDate() + days);
  while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}

// A fully valid CC application (same shape the capture-help seed uses) so the wizard can jump to
// Verify and "Pay Now and Submit" passes validation.
const SEED = {
  siteLoc: '4825 Mowry Ave, Fremont, CA 94538',
  siteCity: 'FRE', siteCityName: 'Fremont', siteLat: '37.552300', siteLong: '-121.988500',
  appBusinessName: 'Alameda Water Well Services', appLastName: 'Nguyen', appFirstName: 'Linda',
  appAddr: '1200 Fairview Avenue', appCity: 'Hayward', appState: 'CA', appZip: '94542',
  appPhone1: '510', appPhone2: '555', appPhone3: '0142', appEmail: 'linda.nguyen@example.com',
  startDate: futureWeekdayIso(20), endDate: futureWeekdayIso(34), sitehazardrequired: 'N',
  ownLastName: 'Carter', ownFirstName: 'James', ownAddr: '77 Vineyard Court',
  ownCity: 'Pleasanton', ownState: 'CA', ownZip: '94566',
  workCat: 'con', workType: 'con-dom', workDesc: 'Domestic Water Well', workFeeRate: 379,
  workFeeUnit: 'EA', workSiteMax: 0,
  wUse: 'DOM', wUseDesc: 'Domestic', drillerName: 'Pacific Drilling Co.', drillerLic: 'C57-482913',
  dmeth: 'MUD', dmethName: 'Mud Rotary',
  wellSpecs: [{ swellid: '', permit: '', dwr: '', owellnum: 'WELL-1', holediam: '10', casediam: '6', sealdepth: '50', maxdepth: '220', latitude: '37.552300', longitude: '-121.988500' }],
  paymentType: 'CC',
};

const STUB_TOKEN = 'e2e-stub-captcha-token-0123456789';

(async () => {
  const browser = await chromium.launch();
  const ctx = await browser.newContext({
    ignoreHTTPSErrors: true,
    viewport: { width: 1360, height: 1000 },
    deviceScaleFactor: 2,
  });
  const page = await ctx.newPage();
  page.on('console', (m) => console.log('CONSOLE', m.type(), m.text().slice(0, 160)));

  // Seed + reCAPTCHA stub BEFORE the app boots.
  await page.addInitScript((data) => {
    try { if (location.origin.includes('localhost:3000')) sessionStorage.setItem('pwa-permits-ecomm', JSON.stringify(data)); } catch (e) {}
  }, SEED);
  await page.addInitScript((tok) => {
    const w = {}; let n = 0;
    window.grecaptcha = {
      ready(cb) { cb(); },
      render(_c, p) { const id = (n += 1); w[id] = p || {}; return id; },
      execute(id) { const p = w[id]; if (p && typeof p.callback === 'function') p.callback(tok); },
      getResponse() { return tok; },
      reset() {},
    };
  }, STUB_TOKEN);

  // Mock our own API only; let /api/payment/lightbox (and the cpteller iframe) reach the network.
  await page.route('**/api/**', async (route) => {
    const req = route.request();
    let url;
    try { url = new URL(req.url()); } catch { return route.fallback(); }
    if (url.hostname !== 'localhost' && url.hostname !== '127.0.0.1') return route.fallback();
    const pathname = url.pathname;
    if (!pathname.startsWith('/api/')) return route.fallback();
    if (pathname === '/api/payment/lightbox') return route.continue(); // the whole point
    const method = req.method();
    const json = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    if (method === 'GET' && pathname === '/api/ref/cities') return json([{ code: 'FRE', label: 'Fremont' }, { code: 'OAK', label: 'Oakland' }]);
    if (method === 'GET' && pathname.startsWith('/api/ref/')) return json([]);
    if (method === 'GET' && pathname === '/api/inspections/calendar') return json({ validRangeStart: futureWeekdayIso(10), validRangeEnd: futureWeekdayIso(90), days: [] });
    if (method === 'POST' && pathname === '/api/applications') return json({ appId: APP_ID, applicationId: APP_ID });
    if (method === 'GET' && /^\/api\/applications\/[^/]+$/.test(pathname)) return json({ appId: APP_ID, statusCode: 'PEND', works: [] });
    return json({});
  });

  await page.goto('http://localhost:3000/apply', { waitUntil: 'domcontentloaded' });

  // Jump straight to the Verify step (the seed makes every step valid/navigable).
  await page.locator('.wizard-step__button', { hasText: 'Verify' }).click();
  await page.getByRole('button', { name: 'Pay Now and Submit' }).click();

  // The IntelliPay bundle injects and auto-opens its secure modal (#intellipay-lightbox iframe).
  await page.locator('#intellipay-lightbox').waitFor({ state: 'attached', timeout: 30000 });
  // Give the cross-origin cpteller card form time to render inside the modal iframe.
  await page.waitForTimeout(7000);

  await page.screenshot({ path: OUT });
  console.log('SAVED', OUT);
  await browser.close();
})().catch((e) => { console.error('FAILED', e); process.exit(1); });
