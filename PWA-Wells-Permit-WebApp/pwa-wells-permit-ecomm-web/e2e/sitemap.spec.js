// Sitemap upload — the most direct proof of the invisible-captcha integration. The upload control
// (shown on Track for any application still "Pending Sitemap") runs InvisibleCaptcha.execute() and
// attaches the token as the `X-Captcha-Token` header on POST /api/applications/{id}/sitemap. We
// reach it through the real Track flow, upload a file, and assert the header is present/non-empty.
import { test, expect } from '@playwright/test';
import { installApiMocks, stubRecaptcha, STUB_CAPTCHA_TOKEN } from './helpers';

const PENDS_APP = '1699999999998';

const RESULT = {
  items: [
    {
      appId: PENDS_APP,
      addDate: '2024-05-02T00:00:00Z',
      appBusinessName: 'Pending Sitemap LLC',
      appFirstName: 'Sam',
      appLastName: 'Smith',
      works: [{ drillerName: 'Drill Co' }],
      siteCityCode: 'FRE',
      siteLocation: '9 Sitemap Way',
      statusCode: 'PENDS', // Pending Sitemap -> shows the "Upload Sitemap" button
    },
  ],
  totalCount: 1,
};

test.beforeEach(async ({ page }) => {
  await installApiMocks(page, { search: RESULT });
});

test('invisible reCAPTCHA token is attached to the sitemap upload', async ({ page }) => {
  // Stub the captcha so execute() deterministically yields a token (see helpers.stubRecaptcha).
  await stubRecaptcha(page);
  await page.goto('/track');

  // Find the Pending-Sitemap application.
  await page.locator('#trackAppId').fill(PENDS_APP);
  await page.getByRole('button', { name: 'Search', exact: true }).click();

  // The "Upload Sitemap" toggle only appears for a Pending-Sitemap row.
  const openPanel = page.getByRole('button', { name: 'Upload Sitemap', exact: true });
  await expect(openPanel).toBeVisible();
  await openPanel.click();

  // Choose a small file.
  const fileInput = page.getByLabel(`Site map file for application ${PENDS_APP}`);
  await fileInput.setInputFiles({
    name: 'sitemap.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.4 test sitemap'),
  });

  // Uploading runs the invisible captcha; its token must ride along as X-Captcha-Token.
  const uploadReq = page.waitForRequest(
    (r) =>
      r.method() === 'POST' &&
      /^\/api\/applications\/[^/]+\/sitemap$/.test(new URL(r.url()).pathname),
  );
  await page.getByRole('button', { name: 'Upload Sitemap File', exact: true }).click();

  const req = await uploadReq;
  const token = req.headers()['x-captcha-token'];
  expect(token, 'X-Captcha-Token header should be present on the sitemap upload').toBe(
    STUB_CAPTCHA_TOKEN,
  );

  // On success the row advances out of Pending Sitemap into Pending Approval.
  await expect(page.getByText('Pending Approval')).toBeVisible();
});
