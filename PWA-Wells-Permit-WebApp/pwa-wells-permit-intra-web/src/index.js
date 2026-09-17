import React from 'react';
import ReactDOM from 'react-dom/client';
import { HashRouter } from 'react-router-dom';
import { MsalProvider } from '@azure/msal-react';
import App from './App';
import './index.css';
import { loginRequest, msalInstance } from './authConfig';
import { AuthProvider } from './context/AuthContext';
import { isCaptureBypass } from './captureBypass';

function renderRoot() {
  const root = ReactDOM.createRoot(document.getElementById('root'));
  root.render(
    <React.StrictMode>
      <MsalProvider instance={msalInstance}>
        <AuthProvider>
          <HashRouter>
            <App />
          </HashRouter>
        </AuthProvider>
      </MsalProvider>
    </React.StrictMode>
  );
}

// MSAL Azure AD login handling using the redirect flow — mirrors maps-tracker index.js.
async function renderApp() {
  await msalInstance.initialize();

  // Local capture run: render immediately, skipping the Entra redirect (see isCaptureBypass).
  if (isCaptureBypass()) {
    renderRoot();
    return;
  }

  msalInstance
    .handleRedirectPromise()
    .then((result) => {
      // 1. Redirect just completed
      if (result) {
        msalInstance.setActiveAccount(result.account);
        renderRoot();
        return;
      }

      // 2. App loads with an existing session
      const accounts = msalInstance.getAllAccounts();
      if (accounts.length >= 1) {
        msalInstance.setActiveAccount(accounts[0]);
        renderRoot();
        return;
      }

      // 3. No session → login redirect (stop rendering until the redirect returns)
      msalInstance.loginRedirect(loginRequest);
    })
    .catch((err) => {
      // no_token_request_cache_error: stale/missing cache entry — not a real error.
      if (err.errorCode === 'no_token_request_cache_error') {
        const accounts = msalInstance.getAllAccounts();
        if (accounts.length >= 1) {
          msalInstance.setActiveAccount(accounts[0]);
          renderRoot();
        } else {
          msalInstance.loginRedirect(loginRequest);
        }
        return;
      }
      console.error('MSAL handleRedirectPromise error:', err);
      renderRoot();
    });
}

renderApp();
