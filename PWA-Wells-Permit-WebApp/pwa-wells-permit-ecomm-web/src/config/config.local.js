const config = {
  apiBaseUrl: 'https://localhost:7242',
  appBaseUrl: 'http://localhost:3000/',
  intelliPayTerminalUrl: '',
  mapApiKey: process.env.REACT_APP_MAP_API_KEY || '',
  // Invisible Google reCAPTCHA v2 site key (public value — safe to commit). Defaults to the real
  // domain-scoped site key; override with REACT_APP_RECAPTCHA_SITE_KEY when needed.
  captchaSiteKey: process.env.REACT_APP_RECAPTCHA_SITE_KEY || '',
};

export default config;
