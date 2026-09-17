const config = {
  azureClientId: '10d7d072-b9d4-4fa5-b2e2-be2912901865',
  azureTenantId: '32fdff2c-f86e-4ba3-a47d-6a44a7f45a64',
  apiBaseUrl: 'https://pwawellpermitsapid.acgov.org',
  appBaseUrl: 'https://pwawellpermitsadmind.acgov.org/',
  ecommBaseUrl: 'https://pwawellpermitsd.alamedacountyca.gov/',
  ssrsServer: 'https://ssrspbid.acgov.org/ReportServer/Pages/ReportViewer.aspx?%2fDevelopment%2fPWA%2fWells',
  mapApiKey: process.env.REACT_APP_MAP_API_KEY || '',
};

export default config;
