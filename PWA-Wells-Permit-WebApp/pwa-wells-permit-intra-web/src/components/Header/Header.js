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
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault();
              navigate('/');
            }
          }}
        >
          <img
            className="portal-header__logo"
            src={`${process.env.PUBLIC_URL}/assets/pwalogo.png`}
            alt="Alameda County Public Works Agency logo"
          />
          <img
            className="portal-header__logo"
            src={`${process.env.PUBLIC_URL}/assets/itdlogo.png`}
            alt="Alameda County Information Technology Department logo"
          />
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
