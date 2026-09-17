# Invisible reCAPTCHA end-to-end tests (Playwright)

These Playwright specs verify the **invisible Google reCAPTCHA** integration on the three public
ecomm surfaces that use it:

| Spec              | Surface        | What it proves |
|-------------------|----------------|----------------|
| `apply.spec.js`   | `/apply`       | The **real** invisible reCAPTCHA widget + disclosure render, and submitting the application attaches the captcha token as the **`X-Captcha-Token`** header on `POST /api/applications`. |
| `track.spec.js`   | `/track`       | The **real** invisible reCAPTCHA widget + disclosure render, and clicking **Search** mints a token and forwards it as the **`X-Captcha-Token`** header on `GET /api/applications/search` (verified server-side). |
| `sitemap.spec.js` | Track → Upload | Uploading a site map for a *Pending Sitemap* application attaches the captcha token as the **`X-Captcha-Token`** header on `POST /api/applications/{id}/sitemap`. |

## How it works

- **The "widget renders" specs use the REAL invisible reCAPTCHA.** Local config defaults to Google's
  universal test site key (`6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI`), which verifies silently and
  works on **any** domain (incl. `localhost`). These specs assert the genuine reCAPTCHA iframe loads
  and the required disclosure shows, so they need outbound internet to `www.google.com/recaptcha`.
- **The token-flow specs stub the captcha.** The live invisible reCAPTCHA will **not** silently issue
  a token to an automated/headless browser (it treats automation as suspicious and would demand an
  interactive challenge), so it can't be used to assert the token *flow* deterministically. Those
  specs install a tiny `window.grecaptcha` stub (`helpers.stubRecaptcha`) so `execute()` resolves a
  known token. Because `InvisibleCaptcha`'s loader uses `window.grecaptcha` directly when it already
  exists at mount, the stub exercises the app's **genuine** integration path
  (`InvisibleCaptcha.execute()` → `captchaHeaders` → `X-Captcha-Token`) unchanged — only Google's
  own service (which the backend re-verifies) is replaced.
- **The backend is mocked.** Every `/api/**` call is intercepted with `page.route` and answered with
  canned data (`e2e/helpers.js`), so **no .NET API, database or VPN is required**. This lets each
  test read the outgoing request headers and assert the captcha token is attached.
- **Apply is pre-seeded.** The Apply wizard's map + availability calendar are hard to drive
  deterministically, so a fully valid application is seeded into `sessionStorage` before the SPA
  boots; the test jumps to the **Verify** step and submits. This exercises the exact production
  submit path.

## Running

```bash
# from pwa-permits-ecomm/
npm run test:e2e            # headless, auto-starts the CRA dev server on :3000
npm run test:e2e:headed     # watch it run in a browser
npm run test:e2e:ui         # Playwright UI mode
npm run test:e2e:report     # open the last HTML report
```

The config (`playwright.config.js`) auto-starts `npm start` (env `REACT_APP_ENV=local`,
`BROWSER=none`) and **reuses an already-running dev server** on port 3000 if you have one (e.g. from
`Start-Local.ps1`). First run also needs the browser binary: `npx playwright install chromium`.

## Notes

- The two **"widget renders"** specs need internet to reCAPTCHA (they load the real widget). If your
  network blocks `www.google.com/recaptcha` they will fail; the token-flow specs are stubbed and
  don't need it.
- Tests run serially (`workers: 1`) because they share one dev server.

## Seeing the reCAPTCHA puzzle under bot traffic (`sim:bot`)

`npm run test:e2e:headed` will **never** show an image puzzle, by design:

1. Local config defaults to Google's **universal test key** (`6Le…MXjiZKhI`), which always verifies
   silently and **never challenges** — regardless of how bot-like the traffic is.
2. The token-flow specs install a `window.grecaptcha` **stub**, so the real widget (and any
   challenge) never loads.

To actually surface the "select all the crosswalks" puzzle you need **all** of:

- a **real** invisible reCAPTCHA v2 site key whose allowed-domains include `localhost`;
- that key's **Security Preference = "Most Secure"** at <https://www.google.com/recaptcha/admin>
  (the only reliable lever that makes v2 challenge low-score/automated traffic);
- the **genuine** widget (no stub);
- **bot-like** automation driving `execute()`.

**`bot-challenge.spec.js`** (`npm run sim:bot`) does the last two on the **real public flows** — it is
both the interactive bot simulator and an opt-in assertion. It drives the genuine captcha-gated
surfaces with the **real** widget (no stub) and asserts reCAPTCHA presents its image-challenge popup
to the automated (bot) browser:

| `BOT_FLOW`  | Flow it drives |
|-------------|----------------|
| `track`     | `/track` → fill an App ID → **Search** (mints a token for `GET /api/applications/search`). |
| `apply`     | `/apply` (seeded valid application) → **Verify** → **Submit Application** (`POST /api/applications`). |
| `sitemap`   | `/track` → search a *Pending Sitemap* app → **Upload Sitemap** → attach a PDF → **Upload Sitemap File** (`POST /api/applications/{id}/sitemap`). |

The `/api` backend is still mocked (`e2e/helpers.js`), so **no .NET API is needed** — only outbound
access to `www.google.com/recaptcha`. Playwright's default automation fingerprint
(`navigator.webdriver=true`, ephemeral profile with no Google cookies) is what makes reCAPTCHA
challenge. The spec is **skipped unless `SIM_BOT=1`** and **self-skips** if the dev server is on the
universal test key (which can never challenge), so it can't flake CI.

```bash
# from pwa-permits-ecomm/ — with the CRA dev server (:3000) serving a real "Most Secure" localhost
# key (already wired in .env.local / Start-Local.ps1). npm start / sim:bot bake the key at startup,
# so restart the dev server after changing it.
$env:NODE_PATH = (Resolve-Path .\node_modules)

npm run sim:bot                 # all three flows (track, apply, sitemap), headed — asserts the puzzle
npm run sim:bot:solve           # same, but pauses on the challenge so you can solve it by hand

# just one flow:
$env:SIM_BOT = '1'; $env:BOT_FLOW = 'apply'; npx playwright test bot-challenge.spec.js --headed
```

- **`sim:bot`** asserts the challenge appears and moves on (an automated test can't *solve* a puzzle).
- **`sim:bot:solve`** sets `BOT_HOLD=1`, which leaves the challenge open (up to 20 min) and resolves
  when you solve/close it — useful to watch or manually complete the flow.
- Runs with `--workers=1` under `sim:bot:solve` so the single browser window stays interactive.

If a run reports the challenge never appeared, the key is likely **not** "Most Secure", your IP has a
good reCAPTCHA reputation, or the dev server is on the test key (the spec will self-skip in that case).

### Local keys are wired (but never committed)

- **Site key (public)** lives in `pwa-permits-ecomm/.env.local` as `REACT_APP_RECAPTCHA_SITE_KEY`.
  `.env.local` is git-ignored, and `npm start` (which `sim:bot` reuses on :3000) reads it. Because
  CRA bakes env vars at start, restart the dev server after changing it.
- **Secret (sensitive)** for the .NET API lives in **dotnet user-secrets** (stored in your user
  profile, never in the repo), loaded automatically in the `Development` environment:

  ```bash
  # from pwa-permits-api/
  dotnet user-secrets set "Captcha:SecretKey" "<real secret>" --project src\PWA.PermitsApi.WebApi
  ```

  With the secret set, `POST /api/applications` (and the other guarded public endpoints) verify the
  `X-Captcha-Token` server-side against Google's siteverify; a blank secret disables the guard
  (fail-open). Validate a secret quickly by POSTing `secret`+`response=dummy` to
  `https://www.google.com/recaptcha/api/siteverify` — `invalid-input-response` means the secret is
  good (only the token was bad), `invalid-input-secret` means the secret itself is wrong.

## Help screenshots

The in-app Help page images (`public/help/ecomm/*.png`) are generated, not hand-made:

- **`capture-help.spec.js`** (`npm run capture:help`) — regenerates the Apply / Track / Sitemap
  screenshots. Fully mocked and deterministic (no backend), same as the specs above.
- **`capture-intellipay.js`** — a **manual, live-only** one-off that captures the genuine IntelliPay
  secure card-entry lightbox (`apply-08-intellipay.png`). It mocks every app `/api` call **except**
  `GET /api/payment/lightbox`, which it lets reach the running .NET API so the real IntelliPay
  autoterminal bundle loads and its cross-origin cpteller.com card form renders. It is **not** a
  Playwright spec (won't run in `test:e2e`) and needs the API on `https://localhost:7242` plus
  outbound access to `secure.cpteller.com`:

  ```bash
  # from pwa-permits-ecomm/, with the CRA dev server (:3000) and the .NET API (:7242) running
  $env:NODE_PATH = (Resolve-Path .\node_modules); node e2e\capture-intellipay.js
  ```


