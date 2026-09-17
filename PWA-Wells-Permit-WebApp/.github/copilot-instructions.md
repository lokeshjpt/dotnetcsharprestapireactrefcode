# PWA Well Permits — Copilot instructions

Monorepo for Alameda County's online well-permit rewrite: **one shared .NET 8 Web API** consumed by
**two React 19 SPAs** (public + staff). Read the root `README.md` for full deployment/CORS/reCAPTCHA
detail — this file captures what's not obvious from a single file.

## Layout

| Folder | What it is |
|--------|------------|
| `pwa-wells-permit-api` | .NET 8 Web API (Clean Architecture, Dapper/SQL Server) |
| `pwa-wells-permit-ecomm-web` | Public eCommerce SPA — anonymous 5-step apply wizard + status tracking (port **3000**) |
| `pwa-wells-permit-intra-web` | Staff intranet SPA — queues, review, approvals, inspections (port **3003**, fixed) |
| `Start-Local.ps1` / `Stop-Local.ps1` | Launch/stop all three apps locally in separate windows |

The intra port is **fixed at 3003** because Entra (MSAL) redirect URIs are registered for it.
`Start-Local.ps1` is intentionally git-ignored (holds dev credentials) — never commit it or the real
secrets it sets.

## Build / test / lint

Start the API first, then the SPAs. `.\Start-Local.ps1` (add `-Offline` to mock FTP/ICAP/Email/IntelliPay) runs everything.

**API** (`pwa-wells-permit-api/`):
```powershell
dotnet build PWA.PermitsApi.sln
dotnet run --no-launch-profile --project src/PWA.PermitsApi.WebApi   # https://localhost:7242/swagger
dotnet test tests/PWA.PermitsApi.Tests                               # unit tests (xUnit)
dotnet test tests/PWA.PermitsApi.Tests --filter "FullyQualifiedName~FeeCalculatorTests"   # single class/test
```
`tests/PWA.PermitsApi.E2E` is a separate live/UI suite that needs a running API + DB (VPN); don't run it in normal loops.

**Either SPA** (run from its folder):
```powershell
npm start                       # dev server (port baked into the start script)
npm test -- --watchAll=false    # Jest/RTL unit tests (CRA)
npm test -- -t "attaches token" # single test by name
npm run build:development        # env-scoped prod build (also :test, :uat, :production via env-cmd)
npm run test:e2e                 # Playwright e2e (auto-starts/reuses :3000 dev server, mocks /api)
npx playwright test apply.spec.js   # single Playwright spec
```
There is no separate lint script — ESLint runs via `react-app` config inside `react-scripts`/CRA.
Playwright specs and their mocking/reCAPTCHA-stub conventions are documented in each app's `e2e/README.md`.

## API architecture (Clean Architecture, `src/`)

Dependency flow is one-directional: `WebApi → Application → Domain`, with `Infrastructure` and
`Persistence` implementing `Application` interfaces (wired in `WebApi/Program.cs`).

- `Domain` — entities mapped from the legacy Java beans / SQL Server schema.
- `Application` — DTOs, `Interfaces/*`, business `Services`, options models, `Notifications`.
- `Infrastructure` — external integrations: `EmailService`, `IntelliPayGateway`, `IcapVirusScanner`, `FtpFileTransferService`, `GoogleReCaptchaVerifier`. Each integration has a `UseMock` flag (see options) for offline runs.
- `Persistence` — **Dapper** repositories over `DapperContext`; raw SQL, no EF. New data access = new `IXxxRepository` in Application + `XxxRepository` in Persistence, registered in `Program.cs`.
- Controllers are thin: inject services, return `ActionResult<Dto>`; audit user comes from the Entra identity (`ResolveActingUser()`), not the request body.

`Program.cs` is the single source of truth for DI, auth, CORS, rate limiting, Serilog, Swagger/Scalar
— read its (heavily commented) sections before changing cross-cutting behavior.

## Dual-client security model (the core non-obvious design)

The **same** API serves an anonymous public SPA and an authenticated staff SPA:

- **Intra (staff)** endpoints carry `[Authorize]` (AzureAD JWT). A global `WhitelistAuthorizationFilter` additionally enforces a DB-backed allowlist (`EEAOWN.app_users`, memory-cached) → non-whitelisted authenticated users get `403`. The intra axios interceptor turns any `403` into a `pwa:not-authorized` app event.
- **Ecomm (public write)** endpoints — `POST /api/applications`, `.../sitemap`, `.../documents`, `GET /api/applications/search` — are anonymous but gated by `[ServiceFilter(typeof(PublicAccessGuardFilter))]`, which passes if **any** proof holds (cheapest-first): a trusted Entra bearer, or a Google reCAPTCHA `X-Captcha-Token`. Blank `Captcha:SecretKey` = fail-open.
- Remaining open public routes are throttled per-IP by the sliding-window rate limiter (`RateLimiting/`, scoped via `PublicRateLimit.AppliesTo`); captcha-guarded/authorized routes are exempt. `401/403/429` responses are audit-emailed by `ErrorStatusAuditMiddleware`.

When adding an endpoint, decide deliberately: staff (`[Authorize]`) vs. public-write (`PublicAccessGuardFilter`) vs. open-read (rate-limited).

## Frontend conventions (both SPAs)

- **Config layering**: `src/config/config.{local,dev,test,uat,prod}.js` selected by `REACT_APP_ENV`, then shallow-merged over `window.RUNTIME_CONFIG` (from `public/configs/config.js`) so deployed builds can be re-pointed without rebuilding. Import the resolved object from `src/config` — never read `process.env` in components.
- **API layer**: all HTTP goes through `src/api/*Api.js` functions built on a shared `src/api/axiosInstance.js` (`baseURL = config.apiBaseUrl`). Ecomm attaches `X-Captcha-Token` via `captchaHeaders(...)`; intra attaches the MSAL bearer in a request interceptor (`authConfig.js`). Don't call `axios` directly from components.
- **Folder-per-component**: each component/page is a folder holding its `.js` + co-located `.css` (e.g. `components/Header/Header.js` + `Header.css`).
- **Styling via design tokens**: use the CSS custom properties in `src/styles/variables.css` (`--color-*`, `--btn-*`, `--spacing-*`, etc.); the two SPAs share the same token set for a consistent ACGov look. Icons come from `lucide-react`.
- State uses Redux Toolkit + React Redux; routing uses React Router v7.

## Deployment

`.github/workflows/*manualdeploy.yml` are **manual** (`workflow_dispatch`) deploys to ACGov
self-hosted Windows runners, progressive `dev → test → uat`, using the reusable actions in
`.github/actions/{apideploy,deploy}`. Release tags are cut from `r/<version>` branches. There is no
CI on push/PR.
