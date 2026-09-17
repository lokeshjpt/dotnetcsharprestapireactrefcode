import { useEffect, useRef, useState } from 'react';
import { ChevronDown, LogOut } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { msalInstance } from '../../authConfig';
import './UserMenu.css';

function getInitials(name) {
  if (!name) return '?';
  const parts = name.replace(',', '').trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
  return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
}

function getFirstName(user) {
  if (!user) return '';
  if (user.name) {
    // Handle "Last, First" and "First Last" display-name formats.
    const name = user.name.includes(',')
      ? user.name.split(',')[1]?.trim()
      : user.name.split(/\s+/)[0];
    if (name) return name;
  }
  return user.username || '';
}

const UserMenu = () => {
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;
    const onClick = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setOpen(false);
      }
    };
    const onKey = (event) => {
      if (event.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onClick);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onClick);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  if (!user) return null;

  const initials = getInitials(user.name || user.username);
  const email = user.username || '';
  const firstName = getFirstName(user);

  const signOut = () => {
    msalInstance.logoutRedirect({
      account: msalInstance.getActiveAccount(),
    });
  };

  return (
    <div className="user-menu" ref={menuRef}>
      <button
        type="button"
        className="user-menu__trigger"
        onClick={() => setOpen((prev) => !prev)}
        aria-haspopup="true"
        aria-expanded={open}
      >
        <span className="user-menu__avatar" aria-hidden="true">{initials}</span>
        <span className="user-menu__name">{firstName}</span>
        <ChevronDown size={16} aria-hidden="true" className={`user-menu__chevron${open ? ' is-open' : ''}`} />
      </button>

      {open && (
        <div className="user-menu__dropdown" role="menu">
          <div className="user-menu__identity">
            <span className="user-menu__avatar user-menu__avatar--lg">{initials}</span>
            <div className="user-menu__details">
              <div className="user-menu__fullname">{user.name || email}</div>
              {email && <div className="user-menu__email" title={email}>{email}</div>}
            </div>
          </div>
          <div className="user-menu__divider" />
          <button type="button" className="user-menu__action" role="menuitem" onClick={signOut}>
            <LogOut size={16} />
            <span>Sign out</span>
          </button>
        </div>
      )}
    </div>
  );
};

export default UserMenu;
