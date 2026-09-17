import localConfig from './config.local';
import devConfig from './config.dev';
import testConfig from './config.test';
import uatConfig from './config.uat';
import prodConfig from './config.prod';

const configs = {
  local: localConfig,
  dev: devConfig,
  test: testConfig,
  uat: uatConfig,
  production: prodConfig,
};

const env = process.env.REACT_APP_ENV || 'local';
const runtime = window.RUNTIME_CONFIG || {};
const selected = configs[env] || localConfig;

const activeConfig = {
  ...selected,
  ...runtime,
  environment: env,
};

export default activeConfig;
