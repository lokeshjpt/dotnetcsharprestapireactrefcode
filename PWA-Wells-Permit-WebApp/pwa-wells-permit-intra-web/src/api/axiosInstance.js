import axios from 'axios';
import config from '../config';
import { loginRequest, msalInstance } from '../authConfig';

const axiosInstance = axios.create({
  baseURL: config.apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  },
});

// Attach the Azure AD bearer token to every request (parity with maps-tracker http.js).
// The token is acquired silently from the active MSAL account; the API validates it and
// reads ClaimTypes.Name (the signed-in UPN/email) for created/updated-by audit fields.
axiosInstance.interceptors.request.use(async (cfg) => {
  try {
    const account = msalInstance.getActiveAccount() || msalInstance.getAllAccounts()[0];
    if (account) {
      const response = await msalInstance.acquireTokenSilent({
        ...loginRequest,
        account,
      });
      if (response?.accessToken) {
        cfg.headers.Authorization = `Bearer ${response.accessToken}`;
      }
    }
  } catch (error) {
    console.warn('Token acquisition failed for request:', error);
  }
  cfg.headers['Cache-Control'] = 'no-cache, no-store, must-revalidate';
  cfg.headers.Pragma = 'no-cache';
  cfg.headers.Expires = '0';
  return cfg;
});

// Global event fired when the API rejects an authenticated user with 403 (not on the application
// allowlist). AuthContext listens for it and flips the app into the "not authorized" state.
export const NOT_AUTHORIZED_EVENT = 'pwa:not-authorized';

// Detect allowlist rejections (403). The API returns a ProblemDetails body with code
// "not_authorized"; we also treat any bare 403 as a not-authorized signal since every intra
// endpoint is gated by the same allowlist.
axiosInstance.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error?.response?.status === 403) {
      window.dispatchEvent(new CustomEvent(NOT_AUTHORIZED_EVENT));
    }
    return Promise.reject(error);
  }
);

export default axiosInstance;
