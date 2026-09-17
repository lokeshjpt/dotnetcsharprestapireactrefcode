# React — Project Structure

Standard layout for a PWA-house-style React SPA. Two flavors share the same structure:

- **public / ecomm** — anonymous portal, top-nav only, adds reCAPTCHA.
- **staff / intra** — Entra (MSAL) auth, collapsible left sidebar, adds a user menu + `AuthContext`.

## Stack

- **React 19** + **react-scripts (CRA) 5**
- `react-router-dom` **v7**
- `axios`
- `lucide-react` (icons; the sidebar/chevrons use hand-written inline SVG)
- `@reduxjs/toolkit` + `react-redux` (optional; used for cross-page state)
- Staff only: `@azure/msal-browser` + `@azure/msal-react`
- Testing: `@testing-library/*`, `jest-axe`, Playwright (e2e). Env selection via `env-cmd`.

## Folder-per-component, co-located CSS

Every component/page is a **folder** named after the component, containing its `.js` and a co-located
`.css` of the same name (plus `*.test.js` next to it). No barrel files, no CSS-in-JS.

```
src/
  index.js               # ReactDOM root; imports index.css
  index.css              # imports styles/global.css
  App.js                 # routes + app shell (Header / SideBar / Footer / MainContainer)
  App.css                # staff-only shell layout (.app-shell, .staff-layout, .staff-content)
  authConfig.js          # staff only: MSAL PublicClientApplication + loginRequest
  styles/
    variables.css        # design tokens (:root custom properties) — see react-styling.md
    global.css           # reset + shared utilities (.form-*, .btn*, .data-table, .panel, ...)
  config/
    index.js             # selects config.{env} by REACT_APP_ENV, merges window.RUNTIME_CONFIG
    config.local.js
    config.dev.js
    config.test.js
    config.uat.js
    config.prod.js
  api/
    axiosInstance.js     # shared axios; baseURL = config.apiBaseUrl; interceptors
    <domain>Api.js       # one module of endpoint functions per domain area
  components/
    Header/  Footer/  MainContainer/  Loader/  FormSection/
    SideBar/  UserMenu/  DetailSection/   # staff only
    MapPicker/  Modal/
    UI/                  # primitives: Button, InputField, SelectField, PhoneField,
                         #   Pagination, SortableHeader, Modal, ui.css, Toaster/
  constants/             # code tables (statusCodes.js, paymentTypes.js)
  context/               # staff only: AuthContext.js
  hooks/                 # shared hooks
  utils/                 # pure helpers (e.g. googleMapsLoader.js)
  pages/
    <Feature>/<Feature>.js + .css   # route-level screens; sub-steps in steps/ subfolders
```

## Config layering (do not read `process.env` in components)

`src/config/index.js` resolves a single config object:

```js
const env = process.env.REACT_APP_ENV || 'local';
const runtime = window.RUNTIME_CONFIG || {};   // from public/configs/config.js at runtime
const selected = configs[env] || localConfig;

const config = { ...selected, ...runtime, environment: env };
export default config;
```

- Build-time env picks the file (`REACT_APP_ENV`, set per `.env.{env}` via `env-cmd`).
- `window.RUNTIME_CONFIG` (served from `public/configs/config.js`) overrides **without a rebuild** —
  lets ops re-point a deployed build's `apiBaseUrl`, `appBaseUrl`, etc.
- Components import the resolved object: `import config from '../config';`.

Each `config.*.js` exports the same shape, e.g. staff:

```js
const config = {
  azureClientId: '...', azureTenantId: '...',
  apiBaseUrl: 'https://localhost:7242',
  appBaseUrl: 'http://localhost:3003/',
  ecommBaseUrl: 'http://localhost:3000/',
  mapApiKey: process.env.REACT_APP_MAP_API_KEY || '...',
};
export default config;
```

## API layer

All HTTP goes through `src/api/*Api.js` functions built on a **single** `axiosInstance`. Never call
`axios` directly from a component/page. See `react-design-patterns.md` for the interceptor details
(captcha header on ecomm, MSAL bearer + 403→`not-authorized` event on intra).

## App shell (staff)

Fixed-viewport shell: header, sidebar, footer stay put; only the content pane scrolls.

```jsx
<div className="app-shell">
  <MainContainer>
    <Header onMenuToggle={() => setMobileNavOpen(o => !o)} />
    <div className="staff-layout">
      <SideBar mobileOpen={mobileNavOpen} onClose={() => setMobileNavOpen(false)} />
      <main id="main-content" className="staff-content" tabIndex={-1}>
        <div className="staff-content__inner">
          <Routes>{/* ... */}</Routes>
        </div>
      </main>
    </div>
  </MainContainer>
  <Footer />
</div>
```

Route changes close the mobile drawer (`useEffect` on `location.pathname`). Standalone print routes
(e.g. `/permit/:id`) render **without** the chrome. The whole app is gated by the allowlist:
`accessDenied` from `AuthContext` swaps in `<NotAuthorized/>`.

Public/ecomm shell is simpler: `Header` (top nav, no sidebar) + page container + `Footer`.

## Bootstrap a new app

```bash
npx create-react-app my-app && cd my-app
npm i react-router-dom axios lucide-react @reduxjs/toolkit react-redux
npm i -D env-cmd jest-axe @testing-library/jest-dom @testing-library/react @testing-library/user-event @playwright/test
# staff only:
npm i @azure/msal-browser @azure/msal-react
```

`package.json` scripts follow this pattern (port baked into `start`):

```json
"start": "set PORT=3003 && react-scripts start",
"build:development": "env-cmd -f .env.development react-scripts build",
"build:test": "env-cmd -f .env.test react-scripts build",
"test:e2e": "playwright test"
```
