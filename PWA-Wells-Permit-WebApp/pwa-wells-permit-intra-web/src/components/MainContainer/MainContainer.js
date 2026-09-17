import { useEffect, useRef, useState } from 'react';
import { ChevronDown } from 'lucide-react';
import './MainContainer.css';
import '../../styles/global.css';

const ENV = process.env.REACT_APP_ENV || 'local';
const SHOW_BANNER = ENV !== 'production';

const ENVIRONMENTS = [
  { label: 'LOCAL', key: 'local', url: 'http://localhost:3003' },
  { label: 'DEV', key: 'dev', url: 'https://pwawellpermitsadmind.acgov.org' },
  { label: 'TEST', key: 'test', url: 'https://pwawellpermitsadmint.acgov.org' },
  { label: 'UAT', key: 'uat', url: 'https://pwawellpermitsadminu.acgov.org' },
  { label: 'PROD', key: 'production', url: 'https://pwawellpermitsadmin.acgov.org' },
];

function EnvSwitcher() {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    function handleOutsideClick(event) {
      if (ref.current && !ref.current.contains(event.target)) {
        setOpen(false);
      }
    }

    document.addEventListener('mousedown', handleOutsideClick);
    return () => document.removeEventListener('mousedown', handleOutsideClick);
  }, []);

  return (
    <div className="env-banner-wrapper">
      <div className="env-switcher" ref={ref}>
        <button className="env-banner" onClick={() => setOpen((value) => !value)}>
          {ENV.toUpperCase()}
          <ChevronDown size={13} className={`env-chevron${open ? ' env-chevron--open' : ''}`} />
        </button>
        {open && (
          <ul className="env-dropdown">
            {ENVIRONMENTS.map((environment) => (
              <li key={environment.key}>
                <a
                  className={`env-dropdown-item${environment.key === ENV ? ' env-dropdown-item--active' : ''}`}
                  href={environment.url}
                >
                  <span className="env-dropdown-label">{environment.label}</span>
                  <span className="env-dropdown-url">{environment.url.replace(/^https?:\/\//, '')}</span>
                </a>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function MainContainer({ children }) {
  return (
    <div className="main-bg">
      {SHOW_BANNER && <EnvSwitcher />}
      {children}
    </div>
  );
}

export default MainContainer;
