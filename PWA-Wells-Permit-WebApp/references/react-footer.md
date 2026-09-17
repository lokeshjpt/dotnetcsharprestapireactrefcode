# React — Footer

A slim, always-present footer bar with a support/help-desk line on the left and a dynamic copyright on
the right. Sits outside the scrolling content pane in the fixed-viewport shell.

## Component — `components/Footer/Footer.js`

```jsx
import './Footer.css';

function Footer() {
  return (
    <footer className="app-footer">
      <div className="app-footer__inner">
        <span>For assistance, contact the PWA Help Desk at (510) 670-5755</span>
        <span>&copy; {new Date().getFullYear()} County of Alameda Public Works Agency</span>
      </div>
    </footer>
  );
}

export default Footer;
```

## Styles — `components/Footer/Footer.css`

House pattern (grounded in the shell + token conventions):

```css
.app-footer {
  flex-shrink: 0;                       /* never collapses in the .app-shell flex column */
  background: var(--color-primary);     /* #22507a navy, matching the header */
  color: #fff;
  font-size: 12px;
}

.app-footer__inner {
  max-width: var(--layout-max);
  margin: 0 auto;
  padding: 8px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

@media (max-width: 560px) {
  .app-footer__inner { flex-direction: column; gap: 4px; text-align: center; }
}
```

## Conventions

- **Dynamic year**: `{new Date().getFullYear()}` — never hard-code the year.
- **Two spans, space-between**: left = support/help-desk contact, right = copyright.
- Rendered **once** in `App.js` as the last child of `.app-shell`, so it stays pinned while only
  `.staff-content` scrolls.
- Uses tokens (`--color-primary`, `--layout-max`); collapses to a stacked, centered layout on phones.

### Adapting for a new app
Change only the two text strings (support line + agency name). Keep the structure and tokens.
