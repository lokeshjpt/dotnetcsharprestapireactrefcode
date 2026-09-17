#!/usr/bin/env node
// Interactive launcher for the intra (staff) regression suite.
//
//   npm run test:regression
//
// Prompts for:
//   1. Environment — local | dev | test | uat   (production is refused)
//   2. Applicant email id — used to find/track the application under test in Search
//   3. Headed (visible browser)? — default yes, so you can watch the run
//
// For dev/test/uat the first run opens a real browser so you can sign in to Entra; the resulting
// session (bearer token) is saved to e2e/.auth/intra-<env>.json and reused on later runs. Set
// FORCE_LOGIN=1 to sign in again.
//
// Non-interactive overrides (skip the matching prompt): PWA_ENV, PWA_APPLICANT_EMAIL, PWA_HEADED,
// or the --env=<env> / --email=<addr> / --headed / --headless flags. Other CLI args are forwarded
// to `playwright test`.
const readline = require('readline');
const { spawn } = require('child_process');
const { ALLOWED, normalizeEnv } = require('./environments');

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function parseArgs(argv) {
  const flags = {};
  const passthrough = [];
  for (const arg of argv) {
    const m = /^--(env|email)=(.*)$/.exec(arg);
    if (m) {
      flags[m[1]] = m[2];
    } else if (arg === '--headed') {
      flags.headed = true;
    } else if (arg === '--headless') {
      flags.headed = false;
    } else if (arg === '--live') {
      flags.live = true;
    } else {
      passthrough.push(arg);
    }
  }
  return { flags, passthrough };
}

function ask(rl, question) {
  return new Promise((resolve) => rl.question(question, (a) => resolve(a.trim())));
}

async function main() {
  const { flags, passthrough } = parseArgs(process.argv.slice(2));
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });

  try {
    // ---- Environment ----
    let env = flags.env || process.env.PWA_ENV;
    while (true) {
      if (!env) {
        console.log('\nSelect target environment (production is not allowed):');
        ALLOWED.forEach((e, i) => console.log(`  ${i + 1}) ${e}`));
        const answer = await ask(rl, 'Environment [1-4 or name]: ');
        const byIndex = ALLOWED[Number(answer) - 1];
        env = byIndex || answer;
      }
      try {
        env = normalizeEnv(env);
        break;
      } catch (err) {
        console.error('  ✗ ' + err.message);
        env = '';
      }
    }

    // ---- Applicant email ----
    let email = flags.email || process.env.PWA_APPLICANT_EMAIL;
    while (!email || !EMAIL_RE.test(email)) {
      email = await ask(rl, 'Applicant email id: ');
      if (!EMAIL_RE.test(email)) console.error('  ✗ Please enter a valid email address.');
    }

    // ---- Headed (visible browser)? default yes ----
    let headed = flags.headed;
    if (headed === undefined && process.env.PWA_HEADED !== undefined) {
      headed = !/^(0|n|no|false|off)$/i.test(process.env.PWA_HEADED);
    }
    if (headed === undefined) {
      if (process.stdin.isTTY) {
        const answer = await ask(rl, 'Run tests headed (visible browser)? (y/n) [y]: ');
        headed = !/^n/i.test(answer);
      } else {
        headed = false; // non-interactive (piped/CI): stay headless unless asked
      }
    }

    rl.close();

    // Live mode: real Entra sign-in + real local API (no capture-bypass, no mocks). Remote envs are
    // always live; local is live only when --live (or PWA_LIVE=1) is passed.
    const live = flags.live || /^(1|y|yes|true|on)$/i.test(String(process.env.PWA_LIVE || '')) || env !== 'local';

    console.log(`\n▶ Running intra regression against "${env}" — applicant <${email}>` +
      ` (${headed ? 'headed' : 'headless'}${live ? ', live/real-API' : ''})`);
    if (live) {
      console.log('  A browser will open for Entra sign-in on the first run (token saved for reuse).');
    }
    console.log('');

    const passArgs = [...passthrough];
    if (headed && !passArgs.includes('--headed')) passArgs.push('--headed');
    const args = ['npx', 'playwright', 'test', ...passArgs];
    // Pass a single command string (not an args array) with shell:true to avoid Node's DEP0190
    // warning. The forwarded args are simple Playwright flags/paths, so plain concatenation is fine.
    const child = spawn(args.join(' '), {
      stdio: 'inherit',
      shell: true,
      env: {
        ...process.env,
        PWA_ENV: env,
        PWA_APPLICANT_EMAIL: email,
        ...(live ? { PWA_LIVE: '1' } : {}),
        // When watching a headed run, default a gentle slow-motion so screens are easy to follow.
        ...(headed && !process.env.PWA_SLOWMO ? { PWA_SLOWMO: '450' } : {}),
      },
    });
    child.on('exit', (code) => process.exit(code == null ? 1 : code));
  } catch (err) {
    rl.close();
    console.error(err.message || err);
    process.exit(1);
  }
}

main();
