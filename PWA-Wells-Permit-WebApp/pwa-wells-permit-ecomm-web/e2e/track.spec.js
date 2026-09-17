// Track page — verifies the invisible reCAPTCHA gate runs on the "Search" action and that its token
// is forwarded to the API. Track search mints a fresh invisible-reCAPTCHA token and sends it as the
// `X-Captcha-Token` header on GET /api/applications/search (which the API verifies server-side); if
// execute() returns no token the page blocks with a "verification challenge" error. A search that
// returns results with the header present proves the invisible captcha resolved a token silently
// (the whole point of invisible mode for real users).
import { test, expect } from '@playwright/test';
import { installApiMocks, stubRecaptcha, RECAPTCHA_IFRAME, DISCLOSURE_TEXT, STUB_CAPTCHA_TOKEN } from './helpers';

const RESULT = {
  items: [
    {
      appId: '1699999999999',
      addDate: '2024-05-01T00:00:00Z',
      appBusinessName: 'Acme Drilling Co',
      appFirstName: 'Jane',
      appLastName: 'Doe',
      works: [{ drillerName: 'Bob the Driller' }],
      siteCityCode: 'OAK',
      siteLocation: '123 Well Site Rd',
      statusCode: 'PEND',
    },
  ],
  totalCount: 1,
};

test.beforeEach(async ({ page }) => {
  await installApiMocks(page, { search: RESULT });
});

test('invisible reCAPTCHA is wired on the Track page', async ({ page }) => {
  await page.goto('/track');
  await expect(page.locator(RECAPTCHA_IFRAME).first()).toBeAttached();
  await expect(page.getByText(DISCLOSURE_TEXT)).toBeVisible();
});

test('Track search passes the invisible captcha gate and forwards the token', async ({ page }) => {
  // Stub the captcha so the invisible gate resolves a token deterministically (see helpers).
  await stubRecaptcha(page);
  await page.goto('/track');

  await page.locator('#trackAppId').fill('1699999999999');

  const searchReq = page.waitForRequest(
    (r) => r.method() === 'GET' && new URL(r.url()).pathname === '/api/applications/search',
  );
  await page.getByRole('button', { name: 'Search', exact: true }).click();

  // The executed token rides along as X-Captcha-Token so the API can verify the search server-side.
  const req = await searchReq;
  expect(
    req.headers()['x-captcha-token'],
    'X-Captcha-Token header should be present on GET /api/applications/search',
  ).toBe(STUB_CAPTCHA_TOKEN);

  // Results render and no verification error appears -> the invisible captcha issued a token.
  await expect(page.getByText('1 application found')).toBeVisible();
  await expect(page.getByText('Acme Drilling Co - Jane Doe')).toBeVisible();
  await expect(page.getByText('Please complete the verification challenge')).toHaveCount(0);
});
