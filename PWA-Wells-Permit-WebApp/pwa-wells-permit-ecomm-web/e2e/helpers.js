// Shared helpers for the invisible-reCAPTCHA e2e specs.
//
// Two complementary strategies:
//   1. "Widget renders" specs let the REAL invisible Google reCAPTCHA widget mount (local config
//      defaults to Google's universal test site key, which verifies silently and works on any
//      domain) and assert the genuine reCAPTCHA iframe + required disclosure load.
//   2. Token-flow specs install a `window.grecaptcha` stub (see stubRecaptcha) because the live
//      invisible widget won't silently issue a token to an automated browser; the stub lets
//      execute() resolve a known token so we can assert it reaches the API.
// Either way every backend `/api/**` call is intercepted with page.route, so the tests are
// deterministic and need no running .NET API / database / VPN. The flow specs then assert that the
// executed captcha token reaches the API as the `X-Captcha-Token` header on the guarded calls.
import {
  startOfToday,
  addDays,
  isWeekend,
  isCountyHoliday,
  toIsoDate,
} from '../src/pages/Apply/countyHolidays';

// Invisible reCAPTCHA renders a hidden iframe served from google.com/recaptcha (the badge/anchor).
export const RECAPTCHA_IFRAME = 'iframe[src*="recaptcha"]';
// reCAPTCHA's required disclosure, rendered next to every captcha-gated control.
export const DISCLOSURE_TEXT = 'This site is protected by reCAPTCHA';

// Abort Google Maps so the map-backed steps (only mounted briefly, if at all) don't hit the network
// or hang. MapPicker degrades to manual lat/long inputs when the map fails, which is fine here.
export function blockGoogleMaps(page) {
  return page.route(/maps\.(googleapis|gstatic)\.com/, (route) => route.abort());
}

// Injects a minimal, API-compatible `window.google.maps` stub BEFORE the app boots. loadGoogleMaps
// then short-circuits (window.google.maps already present) and never injects the network script, so
// the Location step renders a clean, ready map surface with no Google auth error / CRA overlay —
// ideal for deterministic Help screenshots. The stub is intentionally inert (renders nothing).
export async function stubGoogleMaps(page) {
  await page.addInitScript(() => {
    const noop = () => {};
    class MapStub {
      constructor(container) {
        this.container = container;
        if (container) {
          // A soft, map-like surface so the screenshot reads as an interactive map area.
          container.style.background =
            'repeating-linear-gradient(0deg,#e8eef3,#e8eef3 24px,#dfe7ee 24px,#dfe7ee 25px),' +
            'repeating-linear-gradient(90deg,#e8eef3,#e8eef3 24px,#dfe7ee 24px,#dfe7ee 25px)';
          container.style.position = 'relative';
          const pin = document.createElement('div');
          pin.textContent = '📍';
          pin.style.cssText =
            'position:absolute;left:50%;top:50%;transform:translate(-50%,-100%);font-size:28px;';
          container.appendChild(pin);
        }
      }
      setCenter() {}
      setZoom() {}
      addListener() {}
    }
    class MarkerStub {
      constructor() {}
      setPosition() {}
      addListener() {}
    }
    class GeocoderStub {
      geocode(_req, cb) {
        if (cb) cb([], 'ZERO_RESULTS');
      }
    }
    class AutocompleteStub {
      bindTo() {}
      addListener() {}
      getPlace() {
        return {};
      }
    }
    window.gm_authFailure = noop;
    window.google = {
      maps: {
        Map: MapStub,
        Marker: MarkerStub,
        Geocoder: GeocoderStub,
        places: { Autocomplete: AutocompleteStub },
      },
    };
  });
}

// A project date that satisfies the legacy rule enforced by stepValidators: at least `minDays`
// from today, on a weekday, and not an Alameda County holiday. Uses the app's own date logic so
// the seeded Apply application passes validation without driving the Availability Calendar.
export function validProjectDateIso(minDays) {
  let d = addDays(startOfToday(), minDays);
  while (isWeekend(d) || isCountyHoliday(d)) d = addDays(d, 1);
  return toIsoDate(d);
}

// Intercepts all `/api/**` traffic with canned responses so the SPA runs without a backend.
// Options:
//   cities  – array returned for GET /api/ref/cities (Track city dropdown)
//   search  – object returned for GET /api/applications/search (Track results)
//   appId   – id returned by the guarded POST /api/applications (Apply submit)
export function installApiMocks(page, opts = {}) {
  const {
    cities = [
      { code: 'OAK', label: 'Oakland' },
      { code: 'FRE', label: 'Fremont' },
    ],
    search = { items: [], totalCount: 0 },
    appId = '1700000000000',
  } = opts;

  return page.route('**/api/**', async (route) => {
    const req = route.request();
    const method = req.method();
    const { pathname } = new URL(req.url());

    // The `**/api/**` glob also matches third-party URLs that merely contain "/api/" in their path
    // (e.g. Google Maps' maps.googleapis.com/maps/api/js). Only intercept the app's OWN endpoints,
    // whose path starts with "/api/"; let everything else fall through to other routes / the network.
    if (!pathname.startsWith('/api/')) return route.fallback();

    const json = (body, status = 200) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

    // ---- reference data (Apply falls back to hardcoded lists when these are empty) ----
    if (method === 'GET' && pathname === '/api/ref/cities') return json(cities);
    if (method === 'GET' && pathname.startsWith('/api/ref/')) return json([]);
    if (method === 'GET' && pathname === '/api/inspections/calendar') {
      return json({
        validRangeStart: validProjectDateIso(10),
        validRangeEnd: validProjectDateIso(90),
        days: [],
      });
    }

    // ---- Track search (read; captcha-guarded + rate-limited server-side) ----
    // The SPA now mints a fresh invisible-reCAPTCHA token per search and forwards it as the
    // X-Captcha-Token header; this mock returns canned results regardless.
    if (method === 'GET' && pathname === '/api/applications/search') return json(search);

    // ---- guarded public writes (the captcha token rides along as X-Captcha-Token) ----
    if (method === 'POST' && pathname === '/api/applications') {
      return json({ appId, applicationId: appId });
    }
    if (method === 'POST' && /^\/api\/applications\/[^/]+\/sitemap$/.test(pathname)) {
      return json({ ok: true, sitemapFilename: 'sitemap.pdf' });
    }

    // ---- confirmation page fetch ----
    if (method === 'GET' && /^\/api\/applications\/[^/]+$/.test(pathname)) {
      return json({ appId, statusCode: 'PEND', works: [] });
    }

    return json({});
  });
}

// Seeds a complete, valid application into sessionStorage before the SPA boots. useApplication's
// loadInitial() merges this over its defaults, and the step validators read only formData, so the
// Apply wizard can jump straight to the Verify step and submit — no map/calendar interaction needed.
export async function seedApplication(page, seed) {
  await page.addInitScript((data) => {
    try {
      if (window.location.origin.includes('localhost:3000')) {
        // The app hydrates the wizard from localStorage (useApplication STORAGE_KEY). No activity
        // timestamp is written, so loadInitial() treats it as a fresh, non-expired session.
        window.localStorage.setItem('pwa-permits-ecomm', JSON.stringify(data));
      }
    } catch (e) {
      /* cross-origin (e.g. reCAPTCHA) frame — ignore */
    }
  }, seed);
}

// A deterministic stub token used by stubRecaptcha (and asserted by the flow specs).
export const STUB_CAPTCHA_TOKEN = 'e2e-stub-captcha-token-0123456789';

// Replaces window.grecaptcha with a minimal, API-compatible stub so InvisibleCaptcha.execute()
// resolves a token deterministically. The REAL invisible reCAPTCHA service will not silently issue a
// token to an automated/headless browser (it treats it as suspicious and would demand a challenge),
// which makes the live widget unusable for asserting the token *flow*. The app's InvisibleCaptcha
// loader uses window.grecaptcha directly when it already exists at mount time (skipping the network
// script), so this stub exercises the genuine integration path (render -> execute -> callback ->
// X-Captcha-Token) unchanged. Note: with the stub in place the live reCAPTCHA iframe is not loaded —
// the "widget renders" specs intentionally omit the stub so they verify the real widget loads.
export async function stubRecaptcha(page, token = STUB_CAPTCHA_TOKEN) {
  await page.addInitScript((tok) => {
    const widgets = {};
    let counter = 0;
    window.grecaptcha = {
      ready(cb) {
        cb();
      },
      render(_container, params) {
        const id = (counter += 1);
        widgets[id] = params || {};
        return id;
      },
      // Invisible reCAPTCHA delivers the token by invoking the render callback; mirror that so the
      // component's execute() promise resolves with our known token.
      execute(id) {
        const params = widgets[id];
        if (params && typeof params.callback === 'function') params.callback(tok);
      },
      getResponse() {
        return tok;
      },
      reset() {},
    };
  }, token);
}

// ---------------------------------------------------------------------------------------------------
// Real-flow drivers for the bot-challenge simulator (e2e/bot-challenge.spec.js).
//
// These drive the GENUINE public flows (no grecaptcha stub) up to the point where
// InvisibleCaptcha.execute() runs, so a real invisible-reCAPTCHA challenge can be observed/asserted
// for automated (bot) traffic. The /api backend is still mocked via installApiMocks, so no .NET API is
// needed. Selectors/seed mirror the token-flow specs (track/apply/sitemap) so there is one source of
// truth for how each surface is exercised.
// ---------------------------------------------------------------------------------------------------

// CSS selector for reCAPTCHA's image-challenge popup (the "bframe").
export const CAPTCHA_CHALLENGE_SELECTOR =
  'iframe[title*="recaptcha challenge"], iframe[src*="/recaptcha/"][src*="bframe"]';

// A Track search result whose single row is "Pending Sitemap" (PENDS), so the Upload Sitemap control
// appears — the entry point for the sitemap-upload flow.
export const PENDING_SITEMAP_APP = '1699999999998';
const PENDING_SITEMAP_RESULT = {
  items: [
    {
      appId: PENDING_SITEMAP_APP,
      addDate: '2024-05-02T00:00:00Z',
      appBusinessName: 'Pending Sitemap LLC',
      appFirstName: 'Sam',
      appLastName: 'Smith',
      works: [{ drillerName: 'Drill Co' }],
      siteCityCode: 'FRE',
      siteLocation: '9 Sitemap Way',
      statusCode: 'PENDS',
    },
  ],
  totalCount: 1,
};

// A complete, valid application seed for the Apply wizard (merged over useApplication's defaults, so
// only the fields the step validators require are supplied). Shared by apply.spec.js and the
// bot-challenge simulator. Dates use validProjectDateIso so the legacy 10–90 day / weekday / holiday
// rule passes without driving the availability calendar.
export function buildSeed() {
  return {
    siteLoc: '123 Well Site Rd, Oakland, CA 94607',
    siteCity: 'OAK',
    siteCityName: 'Oakland',
    siteLat: '37.804400',
    siteLong: '-122.271200',

    appBusinessName: 'Acme Drilling Co',
    appLastName: 'Doe',
    appFirstName: 'Jane',
    appAddr: '500 Main Street',
    appCity: 'Oakland',
    appState: 'CA',
    appZip: '94607',
    appPhone1: '510',
    appPhone2: '555',
    appPhone3: '1234',
    appEmail: 'jane.doe@example.com',

    startDate: validProjectDateIso(20),
    endDate: validProjectDateIso(34),
    sitehazardrequired: 'N',
    ownLastName: 'Roe',
    ownFirstName: 'Richard',
    ownAddr: '742 Owner Ave',
    ownCity: 'Fremont',
    ownState: 'CA',
    ownZip: '94536',

    workCat: 'con',
    workType: 'con-dom',
    workDesc: 'Domestic Water Well',
    workFeeRate: 379,
    workFeeUnit: 'EA',
    workSiteMax: 0,

    wUse: 'DOM',
    wUseDesc: 'Domestic',
    drillerName: 'Bob the Driller',
    drillerLic: 'C57-123456',
    dmeth: 'MUD',
    dmethName: 'Mud Rotary',
    wellSpecs: [
      {
        swellid: '',
        permit: '',
        dwr: '',
        owellnum: 'W-1',
        holediam: '8',
        casediam: '6',
        sealdepth: '20',
        maxdepth: '150',
        latitude: '37.804400',
        longitude: '-122.271200',
      },
    ],

    paymentType: 'CHECK',
    acctName: 'Acme Drilling Co',
  };
}

export const REAL_FLOWS = ['track', 'apply', 'sitemap'];

// Drives one real captcha-gated public flow to the point where InvisibleCaptcha.execute() fires so
// the caller can observe/assert the live challenge. For `track` and `apply` the gated action IS the
// first thing the flow does, so grecaptcha is never stubbed. For `sitemap` the upload control is only
// reachable through a Track search, which has its OWN captcha gate that would itself challenge and
// block before any results load; so the search step alone is stubbed to get past it, then the stub is
// removed so the real SitemapUpload widget loads and its upload execute() presents the genuine
// challenge on POST /api/applications/{id}/sitemap.
export async function driveRealFlow(page, flow) {
  if (flow === 'track') {
    await installApiMocks(page, { search: { items: [], totalCount: 0 } });
    await page.goto('/track');
    await page.locator('#trackAppId').fill('1699999999999');
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    return;
  }

  if (flow === 'sitemap') {
    await installApiMocks(page, { search: PENDING_SITEMAP_RESULT });
    // Stub only the Track-search gate so results load without a challenge (its addInitScript sets
    // window.grecaptcha at document start; the SPA does not navigate again so it won't re-run).
    await stubRecaptcha(page);
    await page.goto('/track');
    await page.locator('#trackAppId').fill(PENDING_SITEMAP_APP);
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    const openPanel = page.getByRole('button', { name: 'Upload Sitemap', exact: true });
    await openPanel.waitFor({ timeout: 15000 });
    // Drop the stub so the upload panel's InvisibleCaptcha loads the REAL reCAPTCHA (its loader adds
    // the network script only when window.grecaptcha is absent), making the upload gate genuine.
    await page.evaluate(() => {
      try {
        delete window.grecaptcha;
      } catch {
        window.grecaptcha = undefined;
      }
    });
    await openPanel.click();
    const fileInput = page.getByLabel(`Site map file for application ${PENDING_SITEMAP_APP}`);
    await fileInput.setInputFiles({
      name: 'sitemap.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test sitemap'),
    });
    await page.getByRole('button', { name: 'Upload Sitemap File', exact: true }).click();
    return;
  }

  if (flow === 'apply') {
    await blockGoogleMaps(page);
    await installApiMocks(page, { appId: '1700000000000' });
    await seedApplication(page, buildSeed());
    await page.goto('/apply');
    await page.locator('.wizard-step__button', { hasText: 'Verify' }).click();
    await page.getByText('Review your application below').waitFor({ timeout: 15000 });
    await page.getByRole('button', { name: 'Submit Application' }).click();
    return;
  }

  throw new Error(`Unknown flow "${flow}" — expected one of ${REAL_FLOWS.join(', ')}`);
}

