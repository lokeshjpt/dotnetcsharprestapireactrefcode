const config = {
  apiBaseUrl: 'https://pwawellpermitsapi.acgov.org',
  appBaseUrl: 'https://pwawellpermits.alamedacountyca.gov/',
  intelliPayTerminalUrl: 'https://secure.intellipay.example',
  mapApiKey: process.env.REACT_APP_MAP_API_KEY || '',
  // Invisible Google reCAPTCHA v2 site key. The real, domain-scoped key is injected at build/deploy
  // time via REACT_APP_RECAPTCHA_SITE_KEY (never committed). When blank the captcha gate is disabled
  // (fail-open) and the API relies on rate limiting until the key + matching secret are provisioned.
  captchaSiteKey: process.env.REACT_APP_RECAPTCHA_SITE_KEY || '',
};

export default config;
