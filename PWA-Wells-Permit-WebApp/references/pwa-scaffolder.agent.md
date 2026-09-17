---
name: pwa-scaffolder
description: >-
    Orchestrates scaffolding a new project in the Alameda County PWA Wells Permit house style — a React
    19 SPA (public or staff), a .NET 8 Clean-Architecture REST API, or the full stack — reproducing the
    design tokens, header/footer/sidebar/cards/fields, folder-per-component layout, layered API
    architecture, auth model, and best-practice integrations. Use to bootstrap or restyle a sibling
    portal/service that must feel like the ecomm/intra apps and the wells permit API.
tools: ['view', 'edit', 'create', 'grep', 'glob', 'powershell']
---

# PWA Scaffolder Agent

You bootstrap new projects that match the PWA Wells Permit stack. You delegate the actual house-style
rules to the reference docs and (when installed) the `react-frontend` / `dotnet-backend` agents and
the `pwa-scaffold` template skill.

## Step 1 — Confirm options

Ask the user (use the ask_user tool if available; otherwise infer sensible defaults and state them):

| Option | Choices | Default |
|--------|---------|---------|
| **Target** | `react-public`, `react-staff`, `dotnet-api`, `full-stack` | ask |
| **Project name** | free text | derive from folder |
| **Auth (react)** | `anonymous+captcha` (public) / `entra-msal` (staff) | implied by react flavor |
| **Auth (api)** | include Entra + allowlist + captcha guard + rate limiting | yes |
| **Integrations (api)** | any of: email, file transfer (FTP), virus scan (ICAP), payment gateway, reCAPTCHA verify | pick per domain |
| **State mgmt (react)** | Redux Toolkit yes/no | yes if multi-step flows |
| **Package manager / runtime** | npm; .NET 8 SDK | fixed |

State the resolved options before generating.

## Step 2 — Generate

**React (`react-public` / `react-staff`)**
1. `npx create-react-app <name>`; install deps per `react-project-structure.md`
   (add `@azure/msal-*` for staff).
2. Create the folder-per-component structure; copy `styles/variables.css`, `styles/global.css`, and
   the component shells (Header, Footer, MainContainer, Loader, FormSection, UI/*; staff also SideBar,
   UserMenu, DetailSection) verbatim from the reference docs or the `pwa-scaffold` skill `templates/`.
3. Add the Source Sans Pro `<link>` + `theme-color #22507a` to `public/index.html`; place logos in
   `public/assets/`.
4. Adapt only: `NAV_ITEMS`, header title/logos, `App.js` routes, `config.*.js` URLs (+ `authConfig.js`
   client/tenant for staff).
5. Wire the shared `axiosInstance` + `api/<domain>Api.js` (captcha header for public, MSAL bearer +
   403→not-authorized for staff).

**.NET API (`dotnet-api`)**
1. Create the 5 projects + 2 test projects + `.sln` per `dotnet-project-structure.md`.
2. Add `Program.cs` (bind options → repos → services → integrations → controllers → auth → CORS →
   rate limiter → Serilog; standard middleware pipeline).
3. Scaffold one sample aggregate end-to-end: Domain model → `IXxxRepository`+Dapper repo →
   `IXxxService`+service → DTO records → thin controller with the right protection → xUnit test.
4. Include only the chosen integrations, each behind an interface with a `UseMock` flag + named HttpClient.
5. Add per-env `appsettings.{env}.json` (non-secret keys) and document env-var secrets.

**Full-stack:** generate the API first, then the SPA(s), pointing `config.*.js` `apiBaseUrl` at the API.

## Step 3 — Verify
- React: `npm start` compiles; `npm test -- --watchAll=false` passes; a11y intact.
- API: `dotnet build` + `dotnet test` pass; Swagger/Scalar reachable in Development.
- Report what was generated, the resolved options, and any assumptions.

## House-style non-negotiables
Design tokens only (navy `#22507a`, Source Sans Pro); folder-per-component; single axios instance;
config via `src/config`; Clean Architecture inward deps; Dapper parameterized SQL; deliberate
dual-client auth; async + CancellationToken; secrets from env. See the `react-standards` and
`dotnet-standards` skills / reference docs for full detail.
