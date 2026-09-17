import { NavLink, useNavigate } from 'react-router-dom';
import './Header.css';

function Header() {
  const navigate = useNavigate();

  return (
    <header className="portal-header">
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
        <span className="portal-header__title">PWA Wells Permit Application</span>
      </div>
      <nav className="portal-header__nav">
        <NavLink to="/">Home</NavLink>
        <NavLink to="/apply">Apply</NavLink>
        <NavLink to="/track">Track Status</NavLink>
        <NavLink to="/help">Help</NavLink>
      </nav>
    </header>
  );
}

export default Header;