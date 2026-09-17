# PWA Well Permits

Monorepo for the PWA online well permit rewrite — one .NET 8 API backend shared by two React 19 frontends.

## Contents

- [pwa-permits-api](#pwa-permits-api) — .NET 8 Web API (backend)
- [pwa-permits-ecomm](#pwa-permits-ecomm) — Public eCommerce SPA (React 19)
- [pwa-permits-intra](#pwa-permits-intra) — Staff intranet SPA (React 19)

### Full local stack

| App                | Start command (from its folder)  | URL                            |
|--------------------|----------------------------------|--------------------------------|
| API                | `dotnet run --no-launch-profile` | https://localhost:7242/swagger |
| eCommerce (public) | `npm start`                      | http://localhost:3000          |
| Intranet (staff)   | `npm start`                      | http://localhost:3003          |

Start the API first. The intranet app is fixed to port **3003** because the Entra (MSAL) app-registration redirect URIs are configured for that port.

---

## pwa-permits-api

.NET 8 Web API scaffold. Follows Clean Architecture with Web API, Application, Domain, Infrastructure, Persistence, and test projects.

### Projects

- `src/PWA.PermitsApi.WebApi` - API host, controllers, dependency injection, Swagger, CORS.
- `src/PWA.PermitsApi.Application` - DTOs, interfaces, business services, options models.
- `src/PWA.PermitsApi.Domain` - core domain entities mapped from legacy Java beans.
- `src/PWA.PermitsApi.Infrastructure` - email, IntelliPay, ICAP, and FTP integrations.
- `src/PWA.PermitsApi.Persistence` - Dapper repositories against SQL Server.
- `tests/PWA.PermitsApi.Tests` - unit tests for application services.

### Local development

1. Set the database connection string through `PWA_DB_CONNECTION_STRING`.
2. Optionally keep credentials in user secrets or your shell profile.
3. Update integration settings in `appsettings.Development.json` or environment variables.

#### Run the API (PowerShell)

```powershell
cd C:\work\pwa\apps\pwa-permit-apps-rewrite\pwa-permits-api\src\PWA.PermitsApi.WebApi
$env:PWA_DB_CONNECTION_STRING = "Server=US01DDB011V,1433;Database=eedpwa;User Id=;Password=;Encrypt=false;TrustServerCertificate=true;"
$env:ASPNETCORE_URLS = "https://localhost:7242;http://localhost:5242"
$env:Ftp__Password = "******"   # real FTP password for sitemap delivery (never commit)
dotnet run --no-launch-profile
```

> Replace `Password=; DEV = `https://alcolibapid.acgov.org/api/VirusScan/scanFile`). A non-clean result fails the upload closed (the file is never stored or delivered).
- **FTP sitemap delivery** (`FtpFileTransferService`) - the scanned file is uploaded to `ftp://{Ftp.Server}/{Ftp.RemoteDirectory}/{file}`. The value recorded in `APPLICATION_INFO.sitemap_filename` is `{Ftp.RecordedPathPrefix}{file}` (the `//pwafile/...` UNC share path staff open from the intra app), matching the legacy `sitemapFileURL + newFileName`. Non-prod uploads land in `countyitd/test`; prod uses `countyitd`.

The FTP **password is a secret** and is left blank in `appsettings.json`. Supply it at runtime via the `Ftp__Password` environment variable (double-underscore binds to `Ftp:Password`). Set `Ftp__UseMock=true` / `Icap__UseMock=true` to run fully offline.

### IIS deployment

Set `PWA_DB_CONNECTION_STRING` on the Application Pool or in `web.config` using an `<environmentVariable />` entry. The application reads the environment variable first and falls back to `ConnectionStrings:DefaultConnection`.

#### Deployed hosts, CORS & Entra redirect URIs

The API is a **single shared backend** for both SPAs. Recommended topology is same-origin: each SPA site reverse-proxies `/api/*` to this API (URL Rewrite + ARR), so the browser only ever talks to its own origin and **CORS is not exercised**. If a SPA calls the API's hostname directly, CORS applies and the calling origin must be allow-listed.

- **CORS** — `Cors:EcommOrigin` and `Cors:IntraOrigin` each accept a **comma-separated list** of origins (scheme+host+port, **no trailing slash** — matching is exact). Each environment has a committed `appsettings.{env}.json` that sets these keys (`local`, `Development`, `TEST`, `UAT`, `Production`).

  | Env | `Cors:EcommOrigin` (public SPA) | `Cors:IntraOrigin` (admin SPA) |
  |-----|----------------------------------|--------------------------------|
  | `local` | `http://localhost:3000` | `http://localhost:3003` |
  | `Development` | `http://localhost:3000,https://pwawellpermitsd.alamedacountyca.gov` | `http://localhost:3003,https://pwawellpermitsadmind.acgov.org` |
  | `TEST` | `https://pwawellpermitst.alamedacountyca.gov` | `https://pwawellpermitsadmint.acgov.org` |
  | `UAT` | `https://pwawellpermitsu.alamedacountyca.gov` | `https://pwawellpermitsadminu.acgov.org` |
  | `Production` | `https://pwawellpermits.alamedacountyca.gov` | `https://pwawellpermitsadmin.acgov.org` |

  Only `local` and `Development` allow `localhost` dev origins; TEST/UAT/Production list deployed hosts only.

- **Entra (Azure AD) redirect URIs** — the intra SPA's sign-in host must be registered on the app registration (`ClientId` `10d7d072-b9d4-4fa5-b2e2-be2912901865`, tenant `32fdff2c-…`). Add each admin host as a **Single-page application (SPA)** platform redirect URI. Without it, login fails with `AADSTS50011` (redirect mismatch). The **ecomm** SPA is public/anonymous and needs **no** redirect URI.

### Public endpoint protection (captcha + rate limiting)

The anonymous public ecomm endpoints — `POST /api/applications`, `POST /api/applications/{id}/sitemap`, `POST /api/applications/{id}/documents`, and `GET /api/applications/search` — require **any** of these proofs (checked cheapest-first); otherwise returns `401`:

1. **Entra bearer token** — valid `Authorization: Bearer …` token (staff SPA path).
2. **Google reCAPTCHA token** — `X-Captcha-Token` header verified against `Captcha:VerifyUrl` with `Captcha:SecretKey` (browser SPA path).

**Fail-open:** when `Captcha:SecretKey` is blank the guard is disabled.

#### reCAPTCHA keys

Google's universal test pair (public, always-pass — safe to commit, local/dev only):

| Role | Value |
|------|-------|
| Site key (frontend) | `6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI` |
| Secret (backend) | `6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe` |

| Env var | Purpose |
|---------|---------|
| `Captcha__SecretKey` | reCAPTCHA secret (enables the guard; blank = fail-open) |

#### Register and provision reCAPTCHA (step by step)

1. Go to <https://www.google.com/recaptcha/admin/create> and sign in.
2. Choose **reCAPTCHA v2 → "Invisible reCAPTCHA badge"**. Add every domain (no scheme/port):
   - `pwawellpermitsd.alamedacountyca.gov`, `pwawellpermitst.alamedacountyca.gov`, `pwawellpermitsu.alamedacountyca.gov`, `pwawellpermits.alamedacountyca.gov`
   - (optional) `localhost`
3. Copy the **Secret key** — never put it in frontend code or commit it.
4. **Configure ecomm SPA site key** via `REACT_APP_RECAPTCHA_SITE_KEY`:
   ```bash
   REACT_APP_RECAPTCHA_SITE_KEY=<your-site-key>
   ```
5. **Configure API secret** via `Captcha__SecretKey`:
   ```powershell
   # User secrets
   dotnet user-secrets set "Captcha:SecretKey" "<your-recaptcha-secret>" --project src/PWA.PermitsApi.WebApi
   # or shell override
   $env:Captcha__SecretKey = "<your-recaptcha-secret>"
   ```
   For IIS (TEST/UAT/Production): set on the Application Pool or in `web.config` `<environmentVariables>`.
6. Restart the API. A guarded call with valid `X-Captcha-Token` returns `2xx`; no token + no bearer returns `401 verification_required`.

#### Rate limiting

All open public requests are **rate-limited per client IP** via sliding window (`RateLimit` section); exceeding returns `429 Too Many Requests`. Captcha-guarded writes and Entra-authorized staff routes are exempt.

| Key | Default | Meaning |
|-----|---------|---------|
| `Enabled` | `true` | Master switch; `false` = no throttling. |
| `PermitLimit` | `120` | Max requests per IP per window. |
| `WindowSeconds` | `60` | Window length in seconds. |
| `SegmentsPerWindow` | `6` | Window split into 6 × 10s slices (smoother than fixed window). |
| `QueueLimit` | `0` | `0` = reject immediately with `429`. |

#### Access / rate-limit audit emails

`401`, `403`, and `429` responses are emailed to the audit distribution list by `ErrorStatusAuditMiddleware`. Each email includes method/path, status, resolved user (or `(anonymous)`), caller IP, and correlation id.

#### Calling a guarded endpoint (examples)

```bash
# Submit an application
curl -k -X POST "https://localhost:7242/api/applications" \
  -H "Content-Type: application/json" \
  -H "X-Captcha-Token: <recaptcha-response-token>" \
  -d '{ "...": "fields matching SubmitApplicationRequest" }'

# Upload a document (multipart)
curl -k -X POST "https://localhost:7242/api/applications/1699999999999/documents" \
  -H "X-Captcha-Token: <recaptcha-response-token>" \
  -F "file=@C:\path\to\doc.pdf"

# Upload a sitemap (multipart)
curl -k -X POST "https://localhost:7242/api/applications/1699999999999/sitemap" \
  -H "X-Captcha-Token: <recaptcha-response-token>" \
  -F "file=@C:\path\to\sitemap.pdf"
```

> `-k` skips local dev TLS cert check — drop it against real hosts.

---

## pwa-permits-ecomm

React 19 public eCommerce SPA. Five-step application wizard for public submissions and a tracking page for status checks.

### Local setup

1. `npm install`
2. Confirm API runs on `https://localhost:7242` or update config files.
3. Verify `REACT_APP_ENV` in `.env.local`.
4. `npm start`

#### Run the app (PowerShell)

```powershell
cd C:\work\pwa\apps\pwa-permit-apps-rewrite\pwa-permits-ecomm
npm start
```

Opens on **http://localhost:3000**.

### Environment files

- `.env.local` — local environment flag.
- `.env.development`, `.env.test`, `.env.uat`, `.env.production` — build-time environment selection.
- `public/configs/config.js` — runtime value overrides for local hosting without rebuilding.

---

## pwa-permits-intra

React 19 staff intranet SPA. Features pending work queues, application detail review, approvals, inspection scheduling, and sitemap uploads.

### Local setup

1. `npm install`
2. Confirm API runs on `https://localhost:7242` or update config files.
3. Verify `REACT_APP_ENV` in `.env.local`.
4. `npm start`

#### Run the app (PowerShell)

```powershell
cd C:\work\pwa\apps\pwa-permit-apps-rewrite\pwa-permits-intra
npm start
```

Opens on **http://localhost:3003**. Port is fixed — Entra (MSAL) redirect URIs are registered for `http://localhost:3003`.

### Environment files

- `.env.local` — local environment flag.
- `.env.development`, `.env.test`, `.env.uat`, `.env.production` — build-time environment selection.
- `public/configs/config.js` — runtime value overrides for local hosting without rebuilding.
