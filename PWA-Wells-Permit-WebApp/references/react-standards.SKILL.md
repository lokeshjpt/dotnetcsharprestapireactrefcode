---
name: react-standards
description: >-
    React 19 house-style standards for the Alameda County PWA Wells Permit SPAs — folder-per-component
    structure, CSS design tokens, shared global utilities, the navy header, footer, collapsible
    multi-level left sidebar + mobile drawer, FormSection/DetailSection cards, InputField/SelectField/
    PhoneField/Button field primitives, plain-function validation, and cross-cutting patterns (single
    axios instance, MSAL auth + allowlist gate, toasts, modals, accessibility). Use when building,
    restyling, or scaffolding a React frontend that must match this look, feel, and conventions.
user-invocable: true
---

# React Standards (PWA Wells Permit house style)

Reproduce the ecomm (public) / intra (staff) React 19 SPAs' look, feel, and structure in any new or
existing React app. This skill indexes granular reference docs; read the one you need, copy the code
blocks verbatim, then adapt names to the new domain.

## When to use

- "Build/scaffold a React frontend in the PWA / ACGov house style."
- "Add a header / footer / left sidebar / cards / form fields / validation like the wells permit app."
- "Apply the design tokens / accessibility rules to this React app."

## Two flavors

| App | Nav | Auth | Extra |
|-----|-----|------|-------|
| **public / ecomm** (port 3000) | top-nav in header | anonymous + reCAPTCHA | captcha header on writes |
| **staff / intra** (port 3003, fixed) | collapsible left sidebar + mobile drawer | Microsoft Entra (MSAL) | `UserMenu`, `AuthContext`, allowlist gate |

Both share the same tokens, components, and config pattern.

## Reference docs (in this references library)

| Topic | File |
|-------|------|
| Project structure, config layering, app shell | `react-project-structure.md` |
| Design tokens + `global.css` utilities | `react-styling.md` |
| Header | `react-header.md` |
| Footer | `react-footer.md` |
| Left / side navigation | `react-side-nav.md` |
| Cards: FormSection + DetailSection | `react-cards.md` |
| Fields: Input/Select/Phone/Button | `react-fields.md` |
| Validation | `react-validation.md` |
| Cross-cutting patterns, a11y, testing | `react-design-patterns.md` |

## Non-negotiables (the "house style" checklist)

1. **Design tokens only** — colors/spacing/font come from `styles/variables.css`; never hard-code hex.
   Primary navy `#22507a`; font **Source Sans Pro** via a `<link>` in `public/index.html`.
2. **Folder-per-component** with a co-located `.css` of the same name; `*.test.js` beside it.
3. **Single axios instance** in `api/axiosInstance.js`; domain calls in `api/<domain>Api.js`; never
   call `axios` from components. Ecomm adds `X-Captcha-Token`; intra adds the MSAL bearer and maps
   403 → `pwa:not-authorized`.
4. **Config via `src/config`** (build-time `REACT_APP_ENV` file + `window.RUNTIME_CONFIG` override);
   never read `process.env` in components.
5. **Compose UI from primitives** — `FormSection`/`DetailSection` cards, `InputField`/`SelectField`/
   `PhoneField`/`Button`, `.form-row`/`.grid-two`/`.grid-three`.
6. **Validation is plain functions** returning a message-or-`null`, composed into an `errors` object;
   client validation is UX only (API re-validates).
7. **WCAG 2.1 AA posture** — visible focus, `aria-label` on icon buttons, `aria-hidden` on decorative
   SVGs, `jest-axe` in tests.

## Scaffold procedure

1. `npx create-react-app`; install deps (see `react-project-structure.md`).
2. Copy `styles/variables.css`, `styles/global.css`, and the component shells verbatim from the
   reference docs (or the `pwa-scaffold` skill's `templates/`).
3. Add the Source Sans Pro `<link>` + `theme-color` to `public/index.html`; drop logos in
   `public/assets/`.
4. Adapt only: `NAV_ITEMS`, header title/logos, routes in `App.js`, and `config.*.js` URLs.

> Companion: the `pwa-scaffold` skill ships pixel-exact, copy-ready template files. This skill is the
> standards/why-and-how documentation for them.
