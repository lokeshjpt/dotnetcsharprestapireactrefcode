// Apply page — verifies the invisible reCAPTCHA is wired on the multi-step wizard and that the
// executed captcha token is attached as the `X-Captcha-Token` header when the application is
// submitted. The "wired" test uses the real widget; the token-flow test stubs window.grecaptcha
// (helpers.stubRecaptcha) because the live invisible widget won't silently issue a token to an
// automated browser.
//
// The wizard's Location step uses a Google Map and the Project step uses an availability calendar,
// which are awkward to drive deterministically. Instead we pre-seed a fully valid application into
// sessionStorage (useApplication.loadInitial merges it over defaults; the step validators read only
// formData), jump to the Verify step and submit. This exercises the exact production submit path:
// InvisibleCaptcha.execute() -> submitApplication(payload, token) -> X-Captcha-Token header.
import { test, expect } from '@playwright/test';
import {
  installApiMocks,
  blockGoogleMaps,
  seedApplication,
  stubRecaptcha,
  buildSeed,
  RECAPTCHA_IFRAME,
  DISCLOSURE_TEXT,
  STUB_CAPTCHA_TOKEN,
} from './helpers';

const APP_ID = '1700000000000';

test.beforeEach(async ({ page }) => {
  await blockGoogleMaps(page);
  await installApiMocks(page, { appId: APP_ID });
});

test('invisible reCAPTCHA is wired on the Apply page', async ({ page }) => {
  await page.goto('/apply');

  // The invisible reCAPTCHA widget renders (its iframe is attached) and the required disclosure shows.
  await expect(page.locator(RECAPTCHA_IFRAME).first()).toBeAttached();
  await expect(page.getByText(DISCLOSURE_TEXT)).toBeVisible();
});

test('invisible reCAPTCHA token is attached to the Apply submit', async ({ page }) => {
  // Stub the captcha so execute() deterministically yields a token (see helpers.stubRecaptcha).
  await stubRecaptcha(page);
  await seedApplication(page, buildSeed());
  await page.goto('/apply');

  // All steps are valid from the seeded application, so jump straight to Verify.
  await page.locator('.wizard-step__button', { hasText: 'Verify' }).click();
  await expect(page.getByText('Review your application below')).toBeVisible();

  // Submitting runs the invisible captcha; its token must ride along as X-Captcha-Token.
  const submitReq = page.waitForRequest(
    (r) => r.method() === 'POST' && new URL(r.url()).pathname === '/api/applications',
  );
  await page.getByRole('button', { name: 'Submit Application' }).click();

  const req = await submitReq;
  const token = req.headers()['x-captcha-token'];
  expect(token, 'X-Captcha-Token header should be present on POST /api/applications').toBe(
    STUB_CAPTCHA_TOKEN,
  );

  // The application is recorded and the app advances to the confirmation page.
  await page.waitForURL('**/confirmation/**');
});
