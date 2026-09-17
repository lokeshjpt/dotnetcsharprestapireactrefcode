# React — Left / Side Navigation (staff apps)

A **collapsible, multi-level left sidebar** with inline-SVG icons, expand/collapse groups, a desktop
icon-only rail (52px), and a full-viewport mobile drawer. Only the staff/intra app uses it; the
public/ecomm app uses top-nav in the header instead.

## Anatomy

```
<aside .sidebar[.sidebar--collapsed][.sidebar--mobile-open]>
  .sidebar__header   -> title ("Permit Processing") + collapse toggle (desktop) + close X (mobile)
  .sidebar__nav
    ul
      li .sidebar__item            (leaf NavLink: Home, Reports, Maintenance, Help)
      li
        button .sidebar__group     (non-navigable parent: Processing, Search, Inspections)
        ul .sidebar__submenu
          li NavLink .sidebar__subitem
<div .sidebar__overlay>            (dimmed backdrop, mobile only)
```

## Data model — declarative `NAV_ITEMS`

Nav is data-driven. A leaf has a `link`; a group has `children` (and is **not** itself navigable).
Icons are hand-written inline SVGs in an `ICONS` map (stroke `currentColor`, 24×24 viewBox).

```js
const NAV_ITEMS = [
  { key: 'home', label: 'Home', link: '/', end: true, icon: ICONS.home },
  {
    key: 'processing', label: 'Processing', icon: ICONS.processing,
    children: [
      { key: 'q-pend',  label: 'Pending Applications', link: '/queue/PEND' },
      { key: 'q-pends', label: 'Applications Pending Sitemaps', link: '/queue/PENDS' },
      { key: 'q-apprv', label: 'Approved Permits', link: '/queue/APPRV' },
    ],
  },
  {
    key: 'search', label: 'Search', icon: ICONS.search,
    children: [
      { key: 's-apps', label: 'Search Applications', link: '/search', end: true },
      { key: 's-cancelled', label: 'Cancelled Applications', link: '/search/cancelled' },
    ],
  },
  { key: 'reports', label: 'Reports', link: '/reports', icon: ICONS.reports },
  { key: 'help', label: 'Help', link: '/help', end: true, icon: ICONS.help },
];
```

## Behavior contract

- **Auto-expand active group**: on mount and on every route change, the group containing the current
  route opens — without collapsing groups the user opened manually.

  ```js
  function isChildActive(link, pathname) {
    return pathname === link || pathname.startsWith(`${link}/`);
  }
  function isGroupActive(item, pathname) {
    return Boolean(item.children?.some((c) => isChildActive(c.link, pathname)));
  }
  useEffect(() => {
    setExpanded((cur) => {
      const next = { ...cur };
      NAV_ITEMS.forEach((it) => { if (it.children && isGroupActive(it, location.pathname)) next[it.key] = true; });
      return next;
    });
  }, [location.pathname]);
  ```

- **Desktop collapse**: a header toggle flips `collapsed`; the rail shrinks to 52px and shows
  icons only (labels/chevrons/submenus hidden, active marker moves to a right border). Chevron
  polyline swaps `15 18 9 12 15 6` ↔ `9 18 15 12 9 6`.
- **Mobile drawer** (≤900px): the aside becomes `position: fixed; width: 100vw` and slides in via
  `transform: translateX(0)` when `--mobile-open`; a dimmed `.sidebar__overlay` appears. On mobile
  the drawer always shows full labels (`effectiveCollapsed = mobileOpen ? false : collapsed`), the
  collapse toggle is hidden, and a close **X** is shown. `App.js` closes it on route change / overlay
  click / X. Group toggle uses `aria-expanded`; each `<NavLink>` carries `end` and `state={{fromNav:true}}`.

## Styles — `components/SideBar/SideBar.css`

```css
:root {
  --sidebar-width:           220px;
  --sidebar-collapsed-width: 52px;
  --sidebar-bg:              #2c3e50;
  --sidebar-header-bg:       #243342;
  --sidebar-text:            #d8dde1;
  --sidebar-text-hover:      #ffffff;
  --sidebar-active-bg:       #22507a;
  --sidebar-active-text:     #ffffff;
  --sidebar-hover-bg:        rgba(255, 255, 255, 0.08);
  --sidebar-icon-size:       18px;
  --sidebar-transition:      0.2s ease;
}

.sidebar { display: flex; flex-direction: column; width: var(--sidebar-width);
  background: var(--sidebar-bg); transition: width var(--sidebar-transition); flex-shrink: 0; overflow: hidden; }
.sidebar--collapsed { width: var(--sidebar-collapsed-width); }

.sidebar__header { display: flex; align-items: center; justify-content: space-between;
  padding: 0 10px; height: 52px; background: var(--sidebar-header-bg);
  border-bottom: 1px solid rgba(255,255,255,0.08); flex-shrink: 0; }
.sidebar--collapsed .sidebar__header { justify-content: center; }
.sidebar__title { font-size: 15px; font-weight: 700; color: #fff;
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

.sidebar__toggle, .sidebar__close {
  display: flex; align-items: center; justify-content: center;
  background: none; border: none; border-radius: 4px; cursor: pointer;
  color: var(--sidebar-text); flex-shrink: 0; padding: 0;
  transition: background var(--sidebar-transition), color var(--sidebar-transition); }
.sidebar__toggle { width: 28px; height: 28px; }
.sidebar__toggle:hover, .sidebar__close:hover { background: var(--sidebar-hover-bg); color: var(--sidebar-text-hover); }
.sidebar__toggle svg { width: 16px; height: 16px; }
.sidebar__close { display: none; width: 34px; height: 34px; }   /* mobile only */
.sidebar__close svg { width: 20px; height: 20px; }

.sidebar__nav { flex: 1; overflow-y: auto; overflow-x: hidden; padding: 8px 0; }
.sidebar__nav ul { list-style: none; margin: 0; padding: 0; }
.sidebar__nav li + li { margin-top: 2px; }

.sidebar__item {
  display: flex; align-items: center; gap: 16px; padding: 10px 14px;
  color: var(--sidebar-text); text-decoration: none; font-size: 13px; white-space: nowrap;
  border-left: 3px solid transparent;
  transition: background var(--sidebar-transition), color var(--sidebar-transition), border-color var(--sidebar-transition); }
.sidebar__item:hover { background: var(--sidebar-hover-bg); color: var(--sidebar-text-hover); }
.sidebar__item--active { background: var(--sidebar-active-bg); color: var(--sidebar-active-text); border-left-color: #fff; }

button.sidebar__item { width: 100%; background: none; border: none;
  border-left: 3px solid transparent; font: inherit; font-size: 13px; text-align: left; cursor: pointer; }
.sidebar__chevron { display: flex; align-items: center; justify-content: center; margin-left: auto; flex-shrink: 0; }
.sidebar__chevron svg { width: 14px; height: 14px; }
.sidebar__group--current { color: var(--sidebar-text-hover); }
.sidebar__group--current .sidebar__label { font-weight: 600; }

.sidebar__submenu { list-style: none; margin: 0; padding: 2px 0; background: rgba(0,0,0,0.16); }
.sidebar__subitem { display: block; padding: 8px 14px 8px 50px; color: var(--sidebar-text);
  text-decoration: none; font-size: 12.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
  border-left: 3px solid transparent;
  transition: background var(--sidebar-transition), color var(--sidebar-transition), border-color var(--sidebar-transition); }
.sidebar__subitem:hover { background: var(--sidebar-hover-bg); color: var(--sidebar-text-hover); }
.sidebar__subitem--active { background: var(--sidebar-active-bg); color: var(--sidebar-active-text); border-left-color: #fff; }

.sidebar__icon { display: flex; align-items: center; justify-content: center;
  width: var(--sidebar-icon-size); height: var(--sidebar-icon-size); flex-shrink: 0; }
.sidebar__icon svg { width: 100%; height: 100%; }
.sidebar__label { overflow: hidden; text-overflow: ellipsis; }

/* Collapsed rail: center icon, active marker on the right border */
.sidebar--collapsed .sidebar__item { justify-content: center; padding: 12px 0; border-left: none; border-right: 3px solid transparent; }
.sidebar--collapsed .sidebar__item--active { border-right-color: #fff; }

.sidebar__nav::-webkit-scrollbar { width: 4px; }
.sidebar__nav::-webkit-scrollbar-thumb { background: rgba(255,255,255,0.15); border-radius: 2px; }

.sidebar__overlay { display: none; }
@media (max-width: 900px) {
  .sidebar__overlay { display: block; position: fixed; inset: 0; background: rgba(0,0,0,0.45);
    opacity: 0; visibility: hidden; transition: opacity var(--sidebar-transition), visibility var(--sidebar-transition); z-index: 1090; }
  .sidebar__overlay.is-visible { opacity: 1; visibility: visible; }
  .sidebar, .sidebar--collapsed { position: fixed; top: 0; left: 0; bottom: 0;
    width: 100vw; max-width: 100vw; transform: translateX(-100%);
    transition: transform var(--sidebar-transition); z-index: 1100; box-shadow: 2px 0 12px rgba(0,0,0,0.3); }
  .sidebar--mobile-open, .sidebar--collapsed.sidebar--mobile-open { transform: translateX(0); }
  .sidebar__toggle { display: none; }
  .sidebar__close { display: flex; }
  .sidebar--collapsed .sidebar__title { display: inline; }
}
```

## Color reference

| Part | Color |
|------|-------|
| Rail background | `#2c3e50` |
| Header background | `#243342` |
| Text (idle) | `#d8dde1` |
| Text (hover) | `#ffffff` (bg `rgba(255,255,255,.08)`) |
| Active item bg + white left border | `#22507a` + `#ffffff` |
| Submenu background | `rgba(0,0,0,.16)` |
| Mobile overlay | `rgba(0,0,0,.45)` (z 1090; drawer z 1100) |

## Rules

1. Keep nav **declarative** in `NAV_ITEMS`; groups are non-navigable toggles.
2. Icons are inline SVG (`stroke="currentColor"`) so they inherit idle/hover/active colors.
3. Wire `mobileOpen`/`onClose` from `App.js`; close the drawer on route change.
4. Full a11y: `aria-expanded` on groups, `aria-label` on toggles, `aria-hidden` on the overlay.

### Adapting for a new app
Replace `NAV_ITEMS` (+ the `ICONS` you need) and the `.sidebar__title`. Everything else is generic.
