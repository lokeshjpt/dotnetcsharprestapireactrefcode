// OPT-IN live check + interactive simulator: proves that *bot* traffic is actually challenged by the
// real invisible reCAPTCHA on the genuine public flows — Track search, Apply submit, and Sitemap
// upload. This is the "bot simulator" (npm run sim:bot) as well as the CI-style assertion.
//
// It is SKIPPED by default so it never runs in normal `npm run test:e2e` / CI, because it is
// deliberately non-deterministic (it depends on Google's live risk engine) and needs a real key:
//   - set SIM_BOT=1 to opt in;
//   - the dev server must serve a REAL localhost-enabled invisible v2 key whose Security Preference is
//     "Most Secure" (see e2e/README.md). On the universal TEST key the spec self-skips, because that
//     key always solves silently and can never challenge;
//   - BOT_FLOW=track|apply|sitemap runs just one flow (default: all three);
//   - BOT_HOLD=1 (with --headed) leaves the challenge open so you can solve it by hand.
//
// Unlike the other specs it does NOT stub grecaptcha — the whole point is to load the genuine widget
// and let it present the image puzzle on a real flow. The /api backend is still mocked (helpers), so
// no .NET API is needed.
import { test, expect } from '@playwright/test';
import { driveRealFlow, REAL_FLOWS, CAPTCHA_CHALLENGE_SELECTOR } from './helpers';

const TEST_KEY = '6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI';
const OPTED_IN = process.env.SIM_BOT === '1';
const HOLD = process.env.BOT_HOLD === '1';
const HOLD_MS = 20 * 60_000;

const requested = process.env.BOT_FLOW;
const FLOWS = requested ? [requested] : REAL_FLOWS;

test.describe('bot traffic is challenged by the real invisible reCAPTCHA on the public flows (opt-in)', () => {
  for (const flow of FLOWS) {
    test(`the "${flow}" flow surfaces the reCAPTCHA image challenge for bot traffic`, async ({ page }) => {
      test.skip(!OPTED_IN, 'Set SIM_BOT=1 (with a real "Most Secure" localhost key) to run this live challenge check.');
      test.skip(!REAL_FLOWS.includes(flow), `BOT_FLOW="${flow}" is not one of: ${REAL_FLOWS.join(', ')}.`);
      test.setTimeout(HOLD ? HOLD_MS + 60_000 : 120_000);

      // Drive the genuine captcha-gated flow (no stub) to the point where execute() runs.
      await driveRealFlow(page, flow);

      // The invisible widget's anchor iframe carries the active site key in its `k` query param;
      // the universal test key never challenges, so self-skip on it rather than fail.
      const anchor = page.locator('iframe[src*="/recaptcha/"]').first();
      await expect(anchor).toBeAttached({ timeout: 20_000 });
      const src = (await anchor.getAttribute('src')) || '';
      let activeKey = null;
      try {
        activeKey = new URL(src).searchParams.get('k');
      } catch {
        /* non-URL src — leave null */
      }
      test.skip(
        !activeKey || activeKey === TEST_KEY,
        `Dev server is on the universal TEST key (${activeKey}); restart it with a real ` +
          'REACT_APP_RECAPTCHA_SITE_KEY (Start-Local.ps1 / .env.local) to exercise the challenge.',
      );

      // Bot traffic -> reCAPTCHA presents the image challenge in its "bframe" popup.
      const challenge = page.locator(CAPTCHA_CHALLENGE_SELECTOR).first();
      await expect(
        challenge,
        `Expected the reCAPTCHA image challenge to appear for the "${flow}" flow under bot automation. ` +
          'If it did not, the key may not be "Most Secure", or your IP has good reCAPTCHA reputation.',
      ).toBeVisible({ timeout: 30_000 });

      if (HOLD) {
        // eslint-disable-next-line no-console
        console.log(
          `\n[sim:bot] "${flow}" challenge is open — solve it in the browser window. ` +
            'Waiting until you solve/close it (up to 20 min)...\n',
        );
        await challenge.waitFor({ state: 'hidden', timeout: HOLD_MS }).catch(() => {});
      }
    });
  }
});
