import { Link } from 'react-router-dom';
import { MAINT_GROUPS, MAINT_ENTITIES } from '../../constants/maintEntities';
import './Maintenance.css';

// Code Maintenance menu — mirrors the legacy intra maint_code_menu.jsp. Each tile links to a
// generic list/CRUD screen for one reference table.
function MaintenancePage() {
  return (
    <div className="page-shell">
      <div className="panel">
        <h1 className="page-title">Code Maintenance</h1>
        <p className="page-subtitle">Maintain the reference / lookup codes used throughout the permit application and approval workflow.</p>
        <Link className="page-help-link" to="/help#maintenance">
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> About code maintenance — help
        </Link>

        {MAINT_GROUPS.map((group) => (
          <section className="maint-group" key={group.title}>
            <h2 className="maint-group__title">{group.title}</h2>
            <div className="grid-three maint-tiles">
              {group.entities.map((key) => {
                const entity = MAINT_ENTITIES[key];
                if (!entity) return null;
                return (
                  <Link className="queue-box" to={`/maintenance/${entity.key}`} key={entity.key}>
                    <span className="queue-box__label">{entity.title}</span>
                    <span className="queue-box__desc">{entity.subtitle}</span>
                  </Link>
                );
              })}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}

export default MaintenancePage;
