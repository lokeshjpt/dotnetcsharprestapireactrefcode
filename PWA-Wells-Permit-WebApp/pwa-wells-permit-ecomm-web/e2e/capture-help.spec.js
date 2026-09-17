// Captures the screenshots used by the in-app Help page (public/help/ecomm/*.png).
//
// This is NOT part of the normal e2e assertion suite — it only runs when CAPTURE=1 (see the
// `capture:help` npm script) so `npm run test:e2e` stays fast and side-effect free. It reuses the
// same API mocks / captcha stub / valid-application seed as the assertion specs, so every screenshot
// shows the genuine UI driven exactly the way a real applicant would drive it.
import fs from 'fs';
import path from 'path';
import { test, expect } from '@playwright/test';
import { installApiMocks, seedApplication, stubRecaptcha, stubGoogleMaps } from './helpers';

const CAPTURE = process.env.CAPTURE === '1';
const OUT_DIR = path.join(__dirname, '..', 'public', 'help', 'ecomm');

// A realistic, fully valid application so every Apply step renders populated (see stepValidators).
function buildSeed() {
  return {
    siteLoc: '4825 Mowry Ave, Fremont, CA 94538',
    siteCity: 'FRE',
    siteCityName: 'Fremont',
    siteLat: '37.552300',
    siteLong: '-121.988500',

    appBusinessName: 'Alameda Water Well Services',
    appLastName: 'Nguyen',
    appFirstName: 'Linda',
    appAddr: '1200 Fairview Avenue',
    appCity: 'Hayward',
    appState: 'CA',
    appZip: '94542',
    appPhone1: '510',
    appPhone2: '555',
    appPhone3: '0142',
    appEmail: 'linda.nguyen@example.com',

    startDate: futureWeekdayIso(20),
    endDate: futureWeekdayIso(34),
    sitehazardrequired: 'N',
    ownLastName: 'Carter',
    ownFirstName: 'James',
    ownAddr: '77 Vineyard Court',
    ownCity: 'Pleasanton',
    ownState: 'CA',
    ownZip: '94566',

    workCat: 'con',
    workType: 'con-dom',
    workDesc: 'Domestic Water Well',
    workFeeRate: 379,
    workFeeUnit: 'EA',
    workSiteMax: 0,

    wUse: 'DOM',
    wUseDesc: 'Domestic',
    drillerName: 'Pacific Drilling Co.',
    drillerLic: 'C57-482913',
    dmeth: 'MUD',
    dmethName: 'Mud Rotary',
    wellSpecs: [
      {
        swellid: '',
        permit: '',
        dwr: '',
        owellnum: 'WELL-1',
        holediam: '10',
        casediam: '6',
        sealdepth: '50',
        maxdepth: '220',
        latitude: '37.552300',
        longitude: '-121.988500',
      },
    ],

    paymentType: 'CHECK',
    acctName: 'Alameda Water Well Services',
  };
}

// A weekday date `days` out (skips weekends only — good enough for a screenshot).
function futureWeekdayIso(days) {
  const d = new Date();
  d.setDate(d.getDate() + days);
  while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}

const CITIES = [
  { code: 'OAK', label: 'Oakland' },
  { code: 'FRE', label: 'Fremont' },
  { code: 'LIV', label: 'Livermore' },
  { code: 'HAY', label: 'Hayward' },
  { code: 'PLE', label: 'Pleasanton' },
];

const TRACK_RESULTS = {
  totalCount: 3,
  items: [
    {
      appId: '1706112045000',
      addDate: '2026-01-24T00:00:00Z',
      appBusinessName: 'Alameda Water Well Services',
      appFirstName: 'Linda',
      appLastName: 'Nguyen',
      works: [{ drillerName: 'Pacific Drilling Co.' }],
      siteCityCode: 'FRE',
      siteLocation: '4825 Mowry Ave',
      statusCode: 'PEND',
    },
    {
      appId: '1705431600000',
      addDate: '2026-01-16T00:00:00Z',
      appBusinessName: 'Tri-Valley Boring',
      appFirstName: 'Marcus',
      appLastName: 'Reed',
      works: [{ drillerName: 'Reed & Sons' }],
      siteCityCode: 'OAK',
      siteLocation: '900 Broadway',
      statusCode: 'PENDS',
    },
    {
      appId: '1704140400000',
      addDate: '2026-01-01T00:00:00Z',
      appBusinessName: 'Bayview Geotech',
      appFirstName: 'Sara',
      appLastName: 'Kim',
      works: [{ drillerName: 'Bayview Drilling' }],
      siteCityCode: 'LIV',
      siteLocation: '55 Vineyard Ave',
      statusCode: 'APPRV',
    },
  ],
};

test.describe('Help screenshots (ecomm)', () => {
  test.skip(!CAPTURE, 'Set CAPTURE=1 (npm run capture:help) to regenerate Help screenshots.');

  test.beforeAll(() => {
    fs.mkdirSync(OUT_DIR, { recursive: true });
  });

  test.use({ viewport: { width: 1360, height: 1000 }, deviceScaleFactor: 2 });

  test('Apply wizard — every step', async ({ page }) => {
    await stubRecaptcha(page);
    await stubGoogleMaps(page);
    await installApiMocks(page);
    await seedApplication(page, buildSeed());
    await page.goto('/apply');

    const shell = page.locator('.page-shell').first();

    // Step 1 — Location. A stubbed map renders a clean surface (no Google auth error); the seeded
    // address, city and coordinates are all shown.
    await expect(page.getByRole('heading', { name: 'Project Location' })).toBeVisible();
    await page.waitForTimeout(600);
    await shell.screenshot({ path: path.join(OUT_DIR, 'apply-01-location.png') });

    const steps = [
      ['Applicant', 'apply-02-applicant.png'],
      ['Project', 'apply-03-project.png'],
      ['Work Type', 'apply-04-worktype.png'],
      ['Work Info', 'apply-05-workinfo.png'],
      ['Payment', 'apply-06-payment.png'],
      ['Verify', 'apply-07-verify.png'],
    ];
    for (const [label, file] of steps) {
      await page.locator('.wizard-step__button', { hasText: label }).click();
      await page.waitForTimeout(400);
      await shell.screenshot({ path: path.join(OUT_DIR, file) });
    }
  });

  test('Track — search form and results', async ({ page }) => {
    await stubRecaptcha(page);
    await installApiMocks(page, { search: TRACK_RESULTS, cities: CITIES });
    await page.goto('/track');

    const shell = page.locator('.page-shell').first();
    await expect(page.getByText('Track an application')).toBeVisible();
    await shell.screenshot({ path: path.join(OUT_DIR, 'track-01-search.png') });

    await page.locator('#trackBusiness').fill('Well Services');
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    await expect(page.getByText('3 applications found')).toBeVisible();
    await shell.screenshot({ path: path.join(OUT_DIR, 'track-02-results.png') });
  });

  test('Sitemap — upload panel', async ({ page }) => {
    await stubRecaptcha(page);
    await installApiMocks(page, { search: TRACK_RESULTS, cities: CITIES });
    await page.goto('/track');

    await page.locator('#trackBusiness').fill('Boring');
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    await expect(page.getByText('3 applications found')).toBeVisible();

    await page.getByRole('button', { name: 'Upload Sitemap', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Upload Sitemap File', exact: true })).toBeVisible();

    // Capture just the panel (the expanded results region) so the screenshot is focused.
    const results = page.locator('.track-page__results');
    await results.screenshot({ path: path.join(OUT_DIR, 'sitemap-01-upload.png') });
  });
});
