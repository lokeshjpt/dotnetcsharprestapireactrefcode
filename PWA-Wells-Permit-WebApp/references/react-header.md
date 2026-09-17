# React — Header

The top **header bar**: a navy (`#22507a`) full-width strip with a brand block on the left (mobile
hamburger + agency logos + app title) and app-specific controls on the right (staff: `<UserMenu/>`;
public: top-nav links). It is `role`-accessible and keyboard operable.

## Component — `components/Header/Header.js` (staff)

```jsx
import { useNavigate } from 'react-router-dom';
import { Menu } from 'lucide-react';
import UserMenu from '../UserMenu/UserMenu';
import './Header.css';

function Header({ onMenuToggle }) {
  const navigate = useNavigate();

  return (
    <header className="portal-header">
      <div className="portal-header__left">
        <button
          type="button"
          className="portal-header__hamburger"
          onClick={onMenuToggle}
          aria-label="Toggle navigation menu"
        >
          <Menu size={22} />
        </button>
        <div
          className="portal-header__brand"
          onClick={() => navigate('/')}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); navigate('/'); }
          }}
        >
          <img className="portal-header__logo"
               src={`${process.env.PUBLIC_URL}/assets/pwalogo.png`}
               alt="Alameda County Public Works Agency logo" />
          <img className="portal-header__logo"
               src={`${process.env.PUBLIC_URL}/assets/itdlogo.png`}
               alt="Alameda County Information Technology Department logo" />
          <span className="portal-header__title">PWA Wells Permit &mdash; Process Applications</span>
        </div>
      </div>
      <div className="portal-header__right">
        <UserMenu />
      </div>
    </header>
  );
}

export default Header;
```

## Styles — `components/Header/Header.css`

```css
.portal-header {
  background: #22507a;
  color: #fff;
  padding: 8px 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 20px;
}

.portal-header__left { display: flex; align-items: center; gap: 12px; min-width: 0; }

.portal-header__hamburger {
  display: none;            /* shown ≤900px only */
  align-items: center; justify-content: center;
  width: 38px; height: 38px;
  background: rgba(255,255,255,0.12);
  border: 1px solid rgba(255,255,255,0.25);
  border-radius: 6px; color: #fff; cursor: pointer; flex-shrink: 0;
}
.portal-header__hamburger:hover,
.portal-header__hamburger:focus-visible { background: rgba(255,255,255,0.22); outline: none; }

.portal-header__brand { cursor: pointer; display: flex; align-items: center; gap: 12px; min-width: 0; }

.portal-header__logo {
  height: 34px; width: auto; display: block;
  background: #fff; border-radius: 4px; padding: 3px;   /* white chip behind logos */
}

.portal-header__title { font-size: 1.25rem; font-weight: 700; line-height: 1.1; }
.portal-header__right { display: flex; align-items: center; gap: 24px; }

@media (max-width: 900px) {
  .portal-header { padding: 8px 14px; }
  .portal-header__hamburger { display: inline-flex; }
  .portal-header__title { font-size: 1rem; }
}
@media (max-width: 560px) {
  .portal-header__title { display: none; }   /* logos + controls only on phones */
}
```

## Conventions & accessibility

- **Brand block** navigates home; it is a `role="button"` with `tabIndex={0}` and Enter/Space key
  handling so it's operable without a mouse.
- **Two logos** (`pwalogo.png`, `itdlogo.png`) sit on white rounded chips; drop them in
  `public/assets/`. Each `<img>` has a descriptive `alt`.
- **Hamburger** (`lucide-react` `Menu`) is hidden on desktop, shown ≤900px, and calls `onMenuToggle`
  passed from `App.js` (toggles `mobileNavOpen`, which drives the `SideBar` drawer).
- **Right slot** holds the app-specific control: `<UserMenu/>` for staff (signed-in identity + sign
  out), or top-nav `<NavLink>`s for the public portal (which has no sidebar).
- Title uses an `&mdash;` between product and page context and truncates gracefully on small screens.

### Adapting for a new app
Change only: the two `alt`/logo files, the `portal-header__title` text, and the right-slot control.
Keep the classes and colors.
