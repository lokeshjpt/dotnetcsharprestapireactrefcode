import React, {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
} from 'react';
import { useMsal } from '@azure/msal-react';
import { loginRequest, msalInstance } from '../authConfig';
import { getMe } from '../api/authApi';
import { NOT_AUTHORIZED_EVENT } from '../api/axiosInstance';
import { isCaptureBypass } from '../captureBypass';

const AuthContext = createContext(undefined);

export const AuthProvider = ({ children }) => {
  const { instance, accounts, inProgress } = useMsal();
  const [user, setUser] = useState(null);
  const [accessToken, setAccessToken] = useState(null);
  const [loading, setLoading] = useState(true);
  // Application allowlist gate: false until an authenticated user is confirmed NOT on the allowlist
  // (API returns 403). When true, App renders the full-page "not authorized" banner.
  const [accessDenied, setAccessDenied] = useState(false);

  // Memoized token retriever — stable reference. Mirrors maps-tracker AuthContext:
  // silent acquisition with a popup fallback.
  const getToken = useCallback(async () => {
    try {
      const account = msalInstance.getActiveAccount() || accounts[0];
      if (!account) {
        await msalInstance.loginPopup(loginRequest);
      }

      const response = await msalInstance.acquireTokenSilent({
        ...loginRequest,
        account: msalInstance.getActiveAccount() || accounts[0],
      });
      if (response?.accessToken) {
        setAccessToken(response.accessToken);
        msalInstance.setActiveAccount(response.account);
        return response.accessToken;
      }
    } catch (error) {
      console.warn('Silent token failed, fallback to popup:', error);
      try {
        const tokenResponse = await instance.acquireTokenPopup({
          ...loginRequest,
          account: accounts[0],
        });
        if (tokenResponse?.accessToken) {
          setAccessToken(tokenResponse.accessToken);
          return tokenResponse.accessToken;
        }
      } catch (popupError) {
        console.error('Popup login failed:', popupError);
        return null;
      }
    }
    return null;
  }, [instance, accounts]);

  useEffect(() => {
    const initAuth = async () => {
      try {
        setLoading(true);

        // Local capture run: no interactive session exists, so skip MSAL token acquisition entirely
        // (which would otherwise fall back to a login popup). The mocked API needs no bearer token.
        if (isCaptureBypass()) {
          setUser({ name: 'Staff User', username: 'staff.user@acgov.org' });
          try {
            await getMe();
            setAccessDenied(false);
          } catch {
            /* mocked in capture; ignore */
          }
          return;
        }

        if (accounts.length > 0) {
          const currentUser = {
            name: accounts[0].name,
            username: accounts[0].username,
          };
          setUser(currentUser);
          localStorage.setItem('user', JSON.stringify(currentUser));
          await getToken();
        } else {
          const storedUser = localStorage.getItem('user');
          if (storedUser) setUser(JSON.parse(storedUser));
          await getToken();
        }

        // Confirm the signed-in user is on the application allowlist. A 403 here means the user
        // authenticated with Entra but is not authorized to use the intra app → show the banner.
        try {
          await getMe();
          setAccessDenied(false);
        } catch (meErr) {
          if (meErr?.response?.status === 403) {
            setAccessDenied(true);
          }
        }
      } catch (error) {
        console.warn('Auth init failed:', error);
      } finally {
        setLoading(false);
      }
    };

    if (inProgress === 'none') {
      initAuth();
    }
  }, [accounts, inProgress, getToken]);

  // Any 403 from a protected API call (allowlist rejection) flips the app into the denied state.
  useEffect(() => {
    const onDenied = () => setAccessDenied(true);
    window.addEventListener(NOT_AUTHORIZED_EVENT, onDenied);
    return () => window.removeEventListener(NOT_AUTHORIZED_EVENT, onDenied);
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        setAccessToken,
        accessToken,
        getToken,
        loading,
        accessDenied,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
};
