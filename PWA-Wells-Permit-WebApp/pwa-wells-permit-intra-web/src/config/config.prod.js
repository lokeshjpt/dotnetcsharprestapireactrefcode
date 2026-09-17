const config = {
  azureClientId: '10d7d072-b9d4-4fa5-b2e2-be2912901865',
  azureTenantId: '32fdff2c-f86e-4ba3-a47d-6a44a7f45a64',
  apiBaseUrl: 'https://pwawellpermitsapi.acgov.org',
  appBaseUrl: 'https://pwawellpermitsadmin.acgov.org/',
  ecommBaseUrl: 'https://pwawellpermits.alamedacountyca.gov/',
  ssrsServer: 'https://ssrspbip.acgov.org/ReportServer/Pages/ReportViewer.aspx?%2fProd%2fPWA%2fWells',
  mapApiKey: process.env.REACT_APP_MAP_API_KEY || '',
};

export default config;
