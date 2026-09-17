# Intra regression suite

Playwright regression tests for the **staff** Wells Permit admin app, driven by an interactive runner
that asks for the target environment and the applicant email to test with. For deployed environments
it signs you in to **Entra** once and reuses the bearer token on later runs.

## Run it

```bash
npm run test:regression
```

You'll be prompted for:

1. **Environment** — `local`, `dev`, `test`, or `uat`. **Production is refused.**
2. **Applicant email id** — used by the live Search (remote) to find the application under test.
3. **Run headed (visible browser)?** — defaults to **yes**, so a browser opens and you can watch the
   run (slowed slightly for readability). Answer `n` for a headless run.

For `dev` / `test` / `uat`, the **first** run opens a real browser so you can complete the Microsoft
sign-in. The session is saved to `e2e/.auth/intra-<env>.json` and reused on later runs (MSAL caches
its tokens in `localStorage`, which Playwright's `storageState` persists). It's reused for up to 8
hours; force a fresh sign-in with `FORCE_LOGIN=1`.

Non-interactive overrides:

```bash
$env:PWA_ENV='dev'; $env:PWA_APPLICANT_EMAIL='qa@example.com'; npm run test:regression
node e2e/run.js --env=local --email=qa@example.com --headed regression.spec.js
```

`--headed` / `--headless` (or `PWA_HEADED=1` / `PWA_HEADED=0`) skip the headed prompt. When run
non-interactively (piped/CI) with no headed choice given, it stays headless. Set `PWA_SLOWMO=<ms>` to
change the watch pace (the runner defaults it to `450` for headed runs).

> `e2e/.auth/` holds live bearer tokens and is git-ignored — never commit it.

## What runs where

| Env | Auth | What the suite does |
|-----|------|---------------------|
| `local` | Capture-bypass (no sign-in) | **Deterministic regressions**: `/api/**` mocked with canned, PII-free data; the app renders without Entra. No backend/DB/VPN needed. |
| `dev` / `test` / `uat` | Real Entra sign-in (token reused) | **Read-only smoke** proving the reused bearer works end-to-end: the authenticated staff shell + dashboard render, and a live Search by the applicant email returns without an authorization error. Nothing is mutated. |

## Covered regressions (`regression.spec.js`, local)

Every case in `ref/TEST-PLAN.md` is present as a titled, traceable test. UI-observable behaviour is
asserted for real; mutations are verified **assert-on-request** (wait for the exact METHOD + path the
action must fire, with a per-test route override so the post-mutation re-render stays stable); and
cases whose only effect is a backend / 3rd-party side effect are recorded as `test.fixme` with the
reason so the plan ID is still traceable in the report.

- **AUTH** — capture-bypass renders the staff dashboard without a sign-in.
- **UPD** — edit Project / Applicant / Site-Hazard info (PUT `/project` · `/applicant` · `/hazard`);
  edit a work (read-only category/type, editable driller → PUT `/works/{id}`). `UPD-005/006`
  (terminal-edit 409, non-whitelisted 403) are API-enforced → fixme.
- **WRK-1xx** — add a work (category → type → driller → license → drill-method → POST `/works`);
  cancel work, delete work (only when >1 work), Enter WCR / Enter GeoLog visibility.
- **PAY-1xx** — approval-wizard payment gate: Check / Cash (acct + amount) / Fee-Exempt / CC radios,
  fine amount, `paymentLocked` state, "Update Payment"; PaymentSection type description "Credit Card".
- **APP-2xx** — cancel application (confirm → POST `/cancel`); header actions (Upload Documents,
  Notes, Preview, Print Site Hazard, Cancel visibility). `APP-203` audit email → fixme.
- **PRM** — permit preview shows the **PREVIEW** flag + "Payment Type: Credit Card"; approved permit
  shows "Paid By" + Print button and no flag; reprint / Print-Site-Hazard header buttons.
- **APV** — the five approval gates (Sitemap, Conditions, Payment, Site Visit Type, Inspections /
  Review), the `N of 5 complete` summary, per-step completion ✓, the "Approve Now" enablement banner,
  and approve → POST `/approval/{id}/approve`. `APV-009` (CC charge decline) → fixme.
- **CND** — the per-work "Work Permit Approval Conditions" modal (available vs selected, Update /
  No-Specials → PUT `/conditions`). `CND-004` (post-add refresh) → fixme.
- **SRCH** — App-ID deep-link opens the application; name search; the prefiltered queues (Pending,
  Pending Sitemaps, Failed Payments, Cancelled, generic `/queue/{code}`); history permits & wells.
  Non-existent prefiltered queues / stateful return-to-search → fixme.
- **RPT** — Reconciliation, Completed Work, Completed Inspections, Extract, and the Reports hub render.
- **MNT** — Code Maintenance hub + City Codes add (POST `/api/maint/cities`), edit, delete modal.
  Server-enforced uniqueness / auth and cross-feature label mappings → fixme.
- **INS** — the inspection list pages (pending inspections, pending WCR / GeoLog, on-hold, calendar)
  and the Review-path approval gate. Availability-driven booking / public availability → fixme.
- **SEC** — `SEC-011` proves the intra UI is gated (capture-bypass is a dev-only escape hatch; the
  remote smoke proves the Entra redirect). All API-enforced security cases (401/403/429, hCaptcha
  verify, rate limiter, CORS, audit emails) are `test.fixme` pointing at the .NET API test project.
- **HELP** — the floating *back-to-top* button appears after scrolling the `.staff-content` pane and
  returns it to the top (guards the fix for the button that previously watched `window` scroll).

Run summary (local): **64 passed, 31 skipped** (documented `test.fixme` + the 2 remote-only smokes),
0 failed.

`capture-help.spec.js` (screenshot capture) is unaffected and still runs only under `CAPTURE=1`.
