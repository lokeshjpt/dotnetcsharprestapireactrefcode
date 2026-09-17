import { PublicClientApplication } from '@azure/msal-browser';
import config from './config';

const { azureClientId: clientId, azureTenantId: tenantId, appBaseUrl: baseUrl } = config;

export const msalConfig = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri: baseUrl,
    postLogoutRedirectUri: baseUrl,
    navigateToLoginRequestUrl: true,
  },
  cache: {
    cacheLocation: 'localStorage',
    storeAuthStateInCookie: false,
  },
};

export const loginRequest = {
  scopes: ['openid', 'profile', 'email', `api://${clientId}/User.Read`],
};

export const msalInstance = new PublicClientApplication(msalConfig);
