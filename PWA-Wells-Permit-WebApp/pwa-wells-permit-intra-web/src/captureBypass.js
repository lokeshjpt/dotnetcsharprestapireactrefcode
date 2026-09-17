import config from './config';

// Local-only capture bypass. The Playwright Help-screenshot run (npm run capture:help) mocks every
// /api call and needs the app to render without an interactive Entra sign-in. This is gated on BOTH
// the compile-time env (REACT_APP_ENV=local — dev/test/uat/prod never compile it active) AND a
// runtime localStorage flag the harness sets. It only skips the client-side login redirect / token
// acquisition; the .NET API still enforces Entra authorization on every request, so it is not a
// security bypass.
export function isCaptureBypass() {
  try {
    return config.environment === 'local' && window.localStorage.getItem('pwaCaptureBypass') === '1';
  } catch {
    return false;
  }
}
