# React — Design Patterns & Conventions

Cross-cutting patterns that make the two SPAs consistent. Grounded in the ecomm (public) and intra
(staff) apps.

## 1. Single axios instance + interceptors

All HTTP goes through one `axiosInstance` (`baseURL = config.apiBaseUrl`); domain modules
(`api/<domain>Api.js`) export functions that call it. Components never import `axios` directly.

**Public/ecomm** — attach the reCAPTCHA token as `X-Captcha-Token` when present (tokens are
single-use, so each guarded call passes its own):

```js
function captchaHeaders(token, extra = {}) {
  return token ? { ...extra, 'X-Captcha-Token': token } : { ...extra };
}
export async function submitApplication(payload, captchaToken) {
  const { data } = await axiosInstance.post('/api/applications', payload,
    { headers: captchaHeaders(captchaToken) });
  return data;
}
```

**Staff/intra** — a request interceptor acquires the MSAL bearer silently and attaches it; a response
interceptor turns any `403` (allowlist rejection) into a global `pwa:not-authorized` event that
`AuthContext` listens for:

```js
axiosInstance.interceptors.request.use(async (cfg) => {
  const account = msalInstance.getActiveAccount() || msalInstance.getAllAccounts()[0];
  if (account) {
    const res = await msalInstance.acquireTokenSilent({ ...loginRequest, account });
    if (res?.accessToken) cfg.headers.Authorization = `Bearer ${res.accessToken}`;
  }
  cfg.headers['Cache-Control'] = 'no-cache, no-store, must-revalidate';
  return cfg;
});

export const NOT_AUTHORIZED_EVENT = 'pwa:not-authorized';
axiosInstance.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error?.response?.status === 403) window.dispatchEvent(new CustomEvent(NOT_AUTHORIZED_EVENT));
    return Promise.reject(error);
  }
);
```

## 2. Auth (staff only): MSAL + allowlist gate

- `authConfig.js` builds a `PublicClientApplication` from config (`clientId`, `authority` =
  `https://login.microsoftonline.com/{tenantId}`, `redirectUri` = `appBaseUrl`, cache in
  `localStorage`). `loginRequest.scopes` = `['openid','profile','email', 'api://{clientId}/User.Read']`.
- `context/AuthContext.js` tracks the signed-in user and `accessDenied`; on the `not-authorized`
  event it flips `accessDenied`, and `App.js` renders `<NotAuthorized/>` instead of the app.
- The **dev port is fixed** (3003) because Entra redirect URIs are registered for it.

## 3. Data fetching pattern

Page-level `useEffect` + local state (`data`, `loading`, `error`); render `<Loader/>` while loading and
an `.alert-error` on failure. Redux Toolkit is used only for state shared across routes (e.g. an
in-progress wizard); one-off screen data stays local.

```js
const [data, setData] = useState(null);
const [loading, setLoading] = useState(true);
const [error, setError] = useState(null);

useEffect(() => {
  let alive = true;
  (async () => {
    try { const d = await getApplication(appId); if (alive) setData(d); }
    catch (e) { if (alive) setError(e); }
    finally { if (alive) setLoading(false); }
  })();
  return () => { alive = false; };
}, [appId]);
```

## 4. Toasts & modals

- **Toasts**: a `ToastProvider` (in `components/UI/Toaster/`) wraps the app; call its hook for
  success/error notifications after mutations instead of `alert()`.
- **Modals**: the `Modal` primitive (`components/UI/Modal.js`) is an accessible overlay
  (`role="dialog"`, `aria-modal`, Escape + backdrop close, `body.modal-open` scroll-lock). Edit flows
  from a `DetailSection` **Edit** button open a `Modal` containing a `FormSection` of fields.

## 5. Routing

- `react-router-dom` v7. Routes declared centrally in `App.js`. `NavLink` drives active styling.
- Print/standalone routes (e.g. `/permit/:id`, `/hazard/:id`) render **without** header/sidebar/footer
  chrome so they print cleanly.
- Pass `end` for exact matches; child routes use path prefixes so the sidebar auto-expands the group.

## 6. Reference/code data

Status and payment codes live in `constants/` (`statusCodes.js`, `paymentTypes.js`) on the client and
come from the API's reference endpoints as `{ code, label }` for `SelectField`. Keep client constants
in sync with the DB code tables.

## 7. Accessibility (WCAG 2.1 AA posture)

- Visible focus rings on inputs/buttons; `:hover:not(:disabled)`.
- Icon-only controls carry `aria-label`; decorative SVGs use `aria-hidden`.
- Underline links inside body text (not color alone).
- `main#main-content` is focus target (`tabIndex={-1}`); nav has `aria-label`.
- Tests use `jest-axe` to assert no a11y violations.

## 8. Testing

- **Unit/component**: Jest + React Testing Library, co-located `*.test.js` (e.g. `InputField.test.js`,
  `validation.test.js`). Run `npm test -- --watchAll=false`, single test with `-t "name"`.
- **e2e**: Playwright specs in `e2e/`, mock the API with `page.route`, stub reCAPTCHA. `npm run test:e2e`.

## Naming & style conventions

| Thing | Convention |
|-------|------------|
| Component / folder | `PascalCase` (folder + `.js` + `.css` same name) |
| Hooks | `useCamelCase` in `hooks/` |
| API modules | `<domain>Api.js`, exported `verbNoun` functions |
| CSS classes | BEM-ish `.block__element--modifier`; consume tokens |
| Config access | import from `src/config`; never `process.env` in components |
| Icons | `lucide-react`, or inline SVG for nav (inherits `currentColor`) |
