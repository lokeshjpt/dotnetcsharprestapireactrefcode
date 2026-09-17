import './NotAuthorized.css';

/**
 * Full-page block shown when an authenticated Entra user is not on the application allowlist
 * (the API rejects them with 403). Replaces the entire staff UI so no protected data is rendered.
 */
export default function NotAuthorized({ user }) {
  return (
    <div className="not-authorized" role="alert" aria-live="assertive">
      <div className="not-authorized__card">
        <div className="not-authorized__icon" aria-hidden="true">&#9888;</div>
        <h1 className="not-authorized__title">Access Denied</h1>
        <p className="not-authorized__message">
          You are not authorized to access this application. Contact your system administrator
          to request access.
        </p>
        {user?.username ? (
          <p className="not-authorized__user">
            Signed in as <strong>{user.username}</strong>
          </p>
        ) : null}
      </div>
    </div>
  );
}
