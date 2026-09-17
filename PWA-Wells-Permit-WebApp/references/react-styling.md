# React — Styling & Design Tokens

The look & feel comes from **two shared stylesheets** that both SPAs import verbatim:
`src/styles/variables.css` (design tokens) and `src/styles/global.css` (reset + utilities).
Component CSS files consume the tokens; **never hard-code colors, spacing, or fonts** — use the
variables.

Font: **Source Sans Pro** (400/600/700) loaded via a `<link>` in `public/index.html`
(`theme-color` = `#22507a`). CRA's default `index.html` has no font link — you must add it.

## Design tokens — `src/styles/variables.css`

```css
:root {
  --border-m-blue:  #337ab7;
  --border-orange:  #e67e22;
  --border-l-blue:  #5bc0de;
  --border-default: #dddddd;

  --btn-primary-bg:     #22507a;
  --btn-primary-border: #1b3f61;
  --btn-primary-hover:  #1b3f61;
  --btn-danger-bg:      #a11f1c;
  --btn-danger-hover:   #8b1a1a;
  --btn-default-bg:     #f5f5f5;
  --btn-default-border: #ccc;

  --table-header-bg:    #22507a;
  --table-header-color: #ffffff;
  --table-stripe-bg:    #f9f9f9;
  --table-border:       #dee2e6;

  --input-border:       #ababab;
  --input-disabled-bg:  #f9f9f7;
  --input-focus-border: #66afe9;
  --label-color:        #444444;
  --label-font-size:    13px;

  --section-header-bg: #f5f5f5;

  --text-primary: #333333;
  --text-muted:   #4d4d4d;
  --mandatory-red: #a11f1c;
  --link-color:   #1f4e79;

  --loader-bg:    rgba(255, 255, 255, 0.88);
  --loader-color: #337ab7;

  --font-family:    'Source Sans Pro', sans-serif;
  --font-size-base: 14px;
  --border-radius:  4px;
  --spacing-sm:     8px;
  --spacing-md:     15px;
  --spacing-lg:     20px;

  --color-primary: #22507a;
  --color-primary-dark: #1b3f61;
  --color-accent: #22507a;
  --color-background: #f4f4f4;
  --color-surface: #ffffff;
  --color-border: #d8e0ea;
  --color-text: #333333;
  --color-muted: #4d4d4d;
  --color-success: #0e5c2f;
  --color-warning: #e67e22;
  --color-danger: #d9534f;
  --shadow-card: 3px 3px 3px rgba(0, 0, 0, 0.05);
  --radius-md: 4px;
  --radius-sm: 4px;
  --layout-max: 1680px;
}
```

**Brand palette summary**

| Role | Token | Value |
|------|-------|-------|
| Primary navy (header, table head, active nav, buttons) | `--color-primary` / `--btn-primary-bg` | `#22507a` |
| Primary hover / dark | `--color-primary-dark` | `#1b3f61` |
| Danger / mandatory | `--btn-danger-bg` / `--mandatory-red` | `#a11f1c` |
| App background | `--color-background` | `#f4f4f4` |
| Body text | `--text-primary` | `#333333` |
| Link | `--link-color` | `#1f4e79` |
| Card shadow | `--shadow-card` | `3px 3px 3px rgba(0,0,0,.05)` |

## Global utilities — `src/styles/global.css` (what to reuse)

`global.css` starts with `@import './variables.css';` + a box-sizing reset, then defines these
reusable classes. Prefer them over new CSS.

**Forms**
- `.form-row` — flex row wrapping a `.form-label` (min-width 160px, weight 600) + `.form-col` field.
- `.form-control` — the standard input/select/textarea: 1px `--input-border`, radius `--border-radius`,
  `:focus` → `--input-focus-border` + `0 0 0 2px rgba(102,175,233,.3)` glow. `.is-invalid` → red border.
  `:disabled` → `--input-disabled-bg`.
- `.mandatory` — red asterisk. `.field-error` — 12px red message under a field. `.text-muted` — 12px hint.
- `.form-col-fixed-sm` (100px) / `.form-col-fixed-md` (180px), `.upper-case`.

**Buttons** (see `react-fields.md` for the `<Button>` wrapper)
- `.btn` base + `.btn-primary` (navy), `.btn-danger` (red), `.btn-default` (grey), `.btn-link` (text).

**Tables**
- `.data-table` — full-width, navy header (`--table-header-bg`), zebra even rows (`--table-stripe-bg`),
  row hover `#e8f0fe`, **sticky** `thead th` (position: sticky; top: 0). Wrap lists in `.table-scroll`
  (which becomes a bounded scroll box under 900px).
- `table.simple-table` — lighter alternative with `#edf4fb` headers.

**Layout / surfaces**
- `.app-shell` — 100dvh flex column, `overflow: hidden` (fixed-viewport shell).
- `.page-shell` — page padding wrapper. `.panel` — white card w/ subtle shadow.
- `.page-title` (1.6rem/700), `.page-subtitle`, `.page-help-link` (+ `__icon`).
- `.grid-two` / `.grid-three` — responsive CSS grids (collapse to 1 column ≤900px).
- `.status-pill` (+ `--approved` green variant).
- `.alert` + `.alert-error` / `.alert-success`.

**Radio groups**: `.radio-group` > `.radio-label` (flex, gap 5px).

## Responsive breakpoints

| Width | Effect |
|-------|--------|
| ≤ 900px | grids collapse to 1 col; sidebar becomes full-viewport drawer; header hamburger shows; `.table-scroll` becomes a 70dvh scroll box |
| ≤ 760px | `DetailSection` two-column body → single column |
| ≤ 560px | header title hidden |

## Rules

1. Import order: `index.css` → `@import 'styles/global.css'` → `global.css` → `@import 'variables.css'`.
2. Consume tokens; don't invent new hex values. If a color is genuinely new, add a token.
3. One co-located `.css` per component; class names are BEM-ish (`.block__element--modifier`).
4. Accessibility: visible focus rings, `:hover:not(:disabled)`, underline links inside body text.
