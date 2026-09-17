# Ecomm regression suite

Playwright regression tests for the **public** Wells Permit app, driven by an interactive runner
that asks for the target environment and the applicant email to test with.

## Run it

```bash
npm run test:regression
```

You'll be prompted for:

1. **Environment** — `local`, `dev`, `test`, or `uat`. **Production is refused.**
2. **Applicant email id** — stamped onto the seeded application (and asserted on the submit payload).
3. **Run headed (visible browser)?** — defaults to **yes**, so a browser opens and you can watch the
   run (slowed slightly for readability). Answer `n` for a headless run.

Non-interactive overrides (skip the matching prompt) — handy for CI or repeat runs:

```bash
# via env vars
$env:PWA_ENV='local'; $env:PWA_APPLICANT_EMAIL='qa@example.com'; npm run test:regression

# via flags (any extra args are forwarded to `playwright test`)
node e2e/run.js --env=local --email=qa@example.com --headed regression.spec.js
```

`--headed` / `--headless` (or `PWA_HEADED=1` / `PWA_HEADED=0`) skip the headed prompt. When run
non-interactively (piped/CI) with no headed choice given, it stays headless. Set `PWA_SLOWMO=<ms>` to
change the watch pace (the runner defaults it to `450` for headed runs).

## What runs where

| Env | Server | What the suite does |
|-----|--------|---------------------|
| `local` | CRA dev server on `:3000` (auto-started/reused) | **Deterministic UI regressions**: `/api/**` mocked, reCAPTCHA stubbed, application seeded into `localStorage`. No backend/DB/VPN needed. |
| `dev` / `test` / `uat` | The deployed public SPA | **Non-destructive smoke**: the Apply page loads and the invisible reCAPTCHA is wired. No throwaway applications are created on shared environments. |

## Covered regressions (`regression.spec.js`, local)

Every public-facing case in `ref/TEST-PLAN.md` is present as a titled, traceable test. UI-observable
behaviour is asserted for real; submissions are verified **assert-on-request** (wait for the exact
`POST /api/applications` and inspect the payload); and cases whose only effect is a 3rd-party / backend
side effect (IntelliPay vault, ICAP scan, PCI logging) are recorded as `test.fixme` with the reason so
the plan ID stays traceable in the report.

- **EC** — single-work Check application submits from Verify (forwards `X-Captcha-Token`, carries the
  applicant email, reaches confirmation); required-field validation blocks Save + marks `aria-invalid`;
  multi-work list with a running Total; **multi-character** well-spec input (Modal focus-steal fix);
  fee recalculates with the well count; wells preset to the project site coordinates. Time-based
  inactivity warning → fixme.
- **PAY** — Check (`CHECK`) and Fee-Exempt (`EXMPT`) submit with the right `paymentType`; the
  payment-type **description** is shown, never the raw code. CC vault / decline / PCI-field exclusion
  and staff-only Cash → fixme (3rd-party lightbox + server-side).
- **WRK** — Construction (Well Use + well-spec table), Investigation (borehole / diameter / depth),
  Destruction (State Well # / Permit # / DWR #); *"If Other Method, identify"* always visible;
  drilling-method static fallback incl. "Other"; the same work type added twice sums without a crash.
- **SM** — a valid site map uploads with the captcha token attached; the picker restricts to the
  allowed site-map types. ICAP infected/unavailable and staff-only document upload → fixme.
- **TRK** — track by App ID + secondary key returns the status summary; unknown ID → friendly
  not-found; the search forwards an `X-Captcha-Token`. Sticky-header / mobile scroll → fixme (visual).
- **SEC-012** — the ecomm reCAPTCHA gate is wired on the Apply flow (the server enforces the 401 when
  the token is missing — see the intra/API SEC section).

Run summary (local): **21 passed, 10 skipped** (documented `test.fixme` + the 1 remote-only smoke),
0 failed.

The existing `apply.spec.js`, `track.spec.js`, `sitemap.spec.js` and `bot-challenge.spec.js` still
cover the invisible-reCAPTCHA token flows.
