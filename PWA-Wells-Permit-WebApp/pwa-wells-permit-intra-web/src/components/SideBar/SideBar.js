import { useEffect, useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import './SideBar.css';

const ICONS = {
  home: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 10.5 12 3l9 7.5" />
      <path d="M5 9.5V21h14V9.5" />
      <path d="M9.5 21v-6h5v6" />
    </svg>
  ),
  processing: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M22 12h-6l-2 3h-4l-2-3H2" />
      <path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" />
    </svg>
  ),
  search: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="11" cy="11" r="7" />
      <line x1="21" y1="21" x2="16.5" y2="16.5" />
    </svg>
  ),
  inspections: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M9 3h6a1 1 0 0 1 1 1v1H8V4a1 1 0 0 1 1-1z" />
      <path d="M8 5H6a1 1 0 0 0-1 1v13a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V6a1 1 0 0 0-1-1h-2" />
      <path d="M9 13l2 2 4-4" />
    </svg>
  ),
  reports: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="4" y1="20" x2="20" y2="20" />
      <rect x="5" y="12" width="3" height="6" />
      <rect x="10.5" y="8" width="3" height="10" />
      <rect x="16" y="4" width="3" height="14" />
    </svg>
  ),
  maintenance: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  ),
  help: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  ),
};

const NAV_ITEMS = [
  {
    key: 'home',
    label: 'Home',
    link: '/',
    end: true,
    icon: ICONS.home,
  },
  {
    key: 'processing',
    label: 'Processing',
    icon: ICONS.processing,
    children: [
      { key: 'q-pend', label: 'Pending Applications', link: '/queue/PEND' },
      { key: 'q-pends', label: 'Applications Pending Sitemaps', link: '/queue/PENDS' },
      { key: 'q-apprv', label: 'Approved Permits', link: '/queue/APPRV' },
      { key: 'q-payfl', label: 'Failed Payments List', link: '/queue/PAYFL' },
    ],
  },
  {
    key: 'search',
    label: 'Search',
    icon: ICONS.search,
    children: [
      { key: 's-apps', label: 'Search Applications', link: '/search', end: true },
      { key: 's-hist-permits', label: 'History Permits (1987–2005)', link: '/search/history-permits' },
      { key: 's-hist-wells', label: 'History Well Locations', link: '/search/history-wells' },
      { key: 's-cancelled', label: 'Cancelled Applications', link: '/search/cancelled' },
    ],
  },
  {
    key: 'inspections',
    label: 'Inspections',
    icon: ICONS.inspections,
    children: [
      { key: 'i-list', label: 'Inspections List', link: '/inspections/list' },
      { key: 'i-wcr', label: 'Pending WCR List', link: '/inspections/pending-wcr' },
      { key: 'i-geolog', label: 'Pending GeoLog List', link: '/inspections/pending-geolog' },
      { key: 'i-hold', label: 'Permits On Hold List', link: '/inspections/hold' },
      { key: 'i-calendar', label: 'Inspections Calendar', link: '/inspections/calendar' },
    ],
  },
  {
    key: 'reports',
    label: 'Reports',
    link: '/reports',
    icon: ICONS.reports,
  },
  {
    key: 'maintenance',
    label: 'Code Maintenance',
    link: '/maintenance',
    icon: ICONS.maintenance,
  },
  {
    key: 'help',
    label: 'Help',
    link: '/help',
    end: true,
    icon: ICONS.help,
  },
];

// A child is "active" when the current path is that child's link (or nested under it).
function isChildActive(link, pathname) {
  return pathname === link || pathname.startsWith(`${link}/`);
}

// A group is "active" (and should be auto-expanded) when any of its children match the path. Group
// parents are not navigable, so there is no parent link to compare against.
function isGroupActive(item, pathname) {
  return Boolean(item.children?.some((child) => isChildActive(child.link, pathname)));
}

function initialExpanded(pathname) {
  const state = {};
  NAV_ITEMS.forEach((item) => {
    if (item.children && isGroupActive(item, pathname)) state[item.key] = true;
  });
  return state;
}

function SideBar({ mobileOpen = false, onClose = () => {} }) {
  const [collapsed, setCollapsed] = useState(false);
  const location = useLocation();
  const [expanded, setExpanded] = useState(() => initialExpanded(location.pathname));

  // Keep the group containing the active route open as the user navigates, without collapsing
  // any group the user opened manually.
  useEffect(() => {
    setExpanded((current) => {
      const next = { ...current };
      NAV_ITEMS.forEach((item) => {
        if (item.children && isGroupActive(item, location.pathname)) next[item.key] = true;
      });
      return next;
    });
  }, [location.pathname]);

  const toggleGroup = (key) =>
    setExpanded((current) => ({ ...current, [key]: !current[key] }));

  // On mobile the drawer is always shown with full labels regardless of the desktop collapse
  // state, so the hamburger menu never renders as icon-only.
  const effectiveCollapsed = mobileOpen ? false : collapsed;

  return (
    <>
      <div
        className={`sidebar__overlay${mobileOpen ? ' is-visible' : ''}`}
        onClick={onClose}
        aria-hidden="true"
      />
      <aside className={`sidebar${effectiveCollapsed ? ' sidebar--collapsed' : ''}${mobileOpen ? ' sidebar--mobile-open' : ''}`}>
        <div className="sidebar__header">
          {!effectiveCollapsed && <span className="sidebar__title">Permit Processing</span>}
          <button
            className="sidebar__toggle"
            onClick={() => setCollapsed((value) => !value)}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            title={collapsed ? 'Expand' : 'Collapse'}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              {collapsed
                ? <polyline points="9 18 15 12 9 6" />
                : <polyline points="15 18 9 12 15 6" />}
            </svg>
          </button>
          <button
            className="sidebar__close"
            onClick={onClose}
            aria-label="Close menu"
            title="Close menu"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>

        <nav className="sidebar__nav" aria-label="Main navigation">
          <ul>
            {NAV_ITEMS.map((item) => {
              const { key, label, link, icon, end, children } = item;
              const hasChildren = Array.isArray(children) && children.length > 0;

              // Group parents (Processing, Search) are not navigable — the row only expands or
              // collapses its submenu.
              if (hasChildren) {
                const isExpanded = !effectiveCollapsed && !!expanded[key];
                const groupActive = isGroupActive(item, location.pathname);

                return (
                  <li key={key}>
                    <button
                      type="button"
                      className={`sidebar__item sidebar__group${groupActive ? ' sidebar__group--current' : ''}`}
                      onClick={() => toggleGroup(key)}
                      aria-expanded={isExpanded}
                      title={effectiveCollapsed ? label : undefined}
                    >
                      <span className="sidebar__icon" aria-hidden="true">{icon}</span>
                      {!effectiveCollapsed && <span className="sidebar__label">{label}</span>}
                      {!effectiveCollapsed && (
                        <span className="sidebar__chevron" aria-hidden="true">
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            {isExpanded
                              ? <polyline points="6 15 12 9 18 15" />
                              : <polyline points="9 6 15 12 9 18" />}
                          </svg>
                        </span>
                      )}
                    </button>

                    {isExpanded && (
                      <ul className="sidebar__submenu">
                        {children.map((child) => (
                          <li key={child.key}>
                            <NavLink
                              to={child.link}
                              end={child.end}
                              onClick={onClose}
                              state={{ fromNav: true }}
                              className={({ isActive }) => `sidebar__subitem${isActive ? ' sidebar__subitem--active' : ''}`}
                            >
                              {child.label}
                            </NavLink>
                          </li>
                        ))}
                      </ul>
                    )}
                  </li>
                );
              }

              return (
                <li key={key}>
                  <NavLink
                    to={link}
                    end={end}
                    onClick={onClose}
                    state={{ fromNav: true }}
                    className={({ isActive }) => `sidebar__item${isActive ? ' sidebar__item--active' : ''}`}
                    title={effectiveCollapsed ? label : undefined}
                  >
                    <span className="sidebar__icon" aria-hidden="true">{icon}</span>
                    {!effectiveCollapsed && <span className="sidebar__label">{label}</span>}
                  </NavLink>
                </li>
              );
            })}
          </ul>
        </nav>
      </aside>
    </>
  );
}

export default SideBar;
