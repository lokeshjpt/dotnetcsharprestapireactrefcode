import { forwardRef, useEffect, useImperativeHandle, useRef } from 'react';
import config from '../../config';

const SITE_KEY = config.captchaSiteKey;

// Whether the captcha gate is active. Disabled (fail-open) when no site key is configured, so the
// Apply/Track flows keep working in environments that have not provisioned a key yet.
export const captchaEnabled = Boolean(SITE_KEY);

const SCRIPT_SRC = 'https://www.google.com/recaptcha/api.js?render=explicit';

// Loads the Google reCAPTCHA API script once and resolves when window.grecaptcha is ready. If a
// grecaptcha implementation is already present — e.g. an e2e stub injected before the app boots —
// it is used as-is and no network script is added, which keeps the tests deterministic and offline.
let readyPromise = null;
function loadRecaptcha() {
  if (typeof window === 'undefined') return Promise.reject(new Error('no window'));
  if (window.grecaptcha && typeof window.grecaptcha.render === 'function') {
    return Promise.resolve(window.grecaptcha);
  }
  if (readyPromise) return readyPromise;
  const pending = new Promise((resolve, reject) => {
    // Ensure the API script is present exactly once.
    if (!document.querySelector('script[data-recaptcha]')) {
      const script = document.createElement('script');
      script.src = SCRIPT_SRC;
      script.async = true;
      script.defer = true;
      script.setAttribute('data-recaptcha', '');
      script.onerror = () => reject(new Error('reCAPTCHA script failed to load'));
      document.head.appendChild(script);
    }
    // With `?render=explicit`, grecaptcha.render is attached asynchronously *after* the script's
    // load event fires (Google loads recaptcha__en.js next). Checking once on `load` therefore
    // races and can see grecaptcha without render yet. Poll until render is actually callable.
    const start = Date.now();
    const poll = () => {
      const grecaptcha = window.grecaptcha;
      if (grecaptcha && typeof grecaptcha.render === 'function') {
        if (typeof grecaptcha.ready === 'function') {
          grecaptcha.ready(() => resolve(window.grecaptcha));
        } else {
          resolve(grecaptcha);
        }
        return;
      }
      if (Date.now() - start > 15000) {
        reject(new Error('grecaptcha unavailable'));
        return;
      }
      setTimeout(poll, 50);
    };
    poll();
  });
  readyPromise = pending;
  // Don't let a transient failure permanently poison future load attempts.
  pending.catch(() => {
    if (readyPromise === pending) readyPromise = null;
  });
  return pending;
}

// execute() watchdog tuning: how often it polls while a call is pending, and how long it will wait
// with NO reCAPTCHA callback and NO visible challenge before treating the gate as unavailable
// (resolving null) rather than leaving the caller's submit spinner up forever.
const WATCHDOG_POLL_MS = 500;
const WATCHDOG_SILENT_LIMIT_MS = 12000;

// True while reCAPTCHA is actually showing its image challenge to the user — an on-screen, sized,
// non-hidden "recaptcha challenge" iframe. execute()'s watchdog consults this so a real person who
// is mid-challenge is never timed out; only a silent, never-answering execute() is abandoned.
function isChallengeVisible() {
  if (typeof document === 'undefined') return false;
  const frames = document.querySelectorAll('iframe[title*="recaptcha challenge"]');
  for (const frame of frames) {
    const rect = frame.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) continue;
    let el = frame;
    let visible = true;
    for (let i = 0; i < 4 && el; i += 1) {
      const style = window.getComputedStyle(el);
      if (style.visibility === 'hidden' || style.display === 'none' || parseFloat(style.opacity) === 0) {
        visible = false;
        break;
      }
      el = el.parentElement;
    }
    if (visible) return true;
  }
  return false;
}

// Invisible Google reCAPTCHA (v2) gate. Rendered near a form's submit control; the parent calls
// `ref.current.execute()` before performing the protected action. For low-risk (human) traffic
// reCAPTCHA returns a token silently; for suspicious/bot traffic it presents an image challenge that
// must be solved by a person before a token is issued. `execute()` resolves to the token string, or
// null when the gate is disabled or the challenge was dismissed/failed. Tokens are single-use, so
// the widget is reset after every execute() to mint a fresh token for the next protected call.
const InvisibleCaptcha = forwardRef(function InvisibleCaptcha(_props, ref) {
  const containerRef = useRef(null);
  const widgetIdRef = useRef(null);
  const resolverRef = useRef(null);
  // Resolves once grecaptcha.render() has completed (or the loader gave up). execute() awaits this
  // so a fast click right after mount waits for the widget instead of falsely failing the gate.
  const readyRef = useRef(null);

  useEffect(() => {
    if (!captchaEnabled) return undefined;
    let cancelled = false;
    const settleWith = (token) => {
      const resolve = resolverRef.current;
      resolverRef.current = null;
      if (resolve) resolve(token);
    };
    readyRef.current = loadRecaptcha()
      .then((grecaptcha) => {
        if (cancelled || !containerRef.current) return widgetIdRef.current;
        if (widgetIdRef.current === null) {
          widgetIdRef.current = grecaptcha.render(containerRef.current, {
            sitekey: SITE_KEY,
            size: 'invisible',
            badge: 'bottomright',
            callback: (token) => settleWith(token || null),
            'error-callback': () => settleWith(null),
            'expired-callback': () => settleWith(null),
          });
        }
        return widgetIdRef.current;
      })
      .catch(() => {
        /* Leave the gate inert; execute() returns null and callers fail-open per captchaEnabled. */
        return null;
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useImperativeHandle(ref, () => ({
    enabled: captchaEnabled,
    async execute() {
      if (!captchaEnabled) return null;
      // Wait for the widget to finish rendering before deciding it is unavailable.
      try {
        await readyRef.current;
      } catch {
        /* readyRef never rejects (its own catch maps failure to null), but be defensive. */
      }
      const grecaptcha = typeof window !== 'undefined' ? window.grecaptcha : null;
      if (!grecaptcha || widgetIdRef.current === null) return null;
      try {
        const token = await new Promise((resolve) => {
          resolverRef.current = resolve;
          // Watchdog against a permanently-pending execute(). reCAPTCHA normally answers within a
          // few seconds — either silently (callback with a token) or by showing an image challenge
          // the user solves. But a site key whose Domains allow-list is missing the current host,
          // or a reCAPTCHA outage, fires no callback at all, which would otherwise leave the
          // caller's submit spinner up forever. Poll while pending: once the silent window elapses
          // with nothing settled AND no challenge on screen for the user to solve, give up and
          // resolve null so the caller surfaces an error instead of hanging. While a challenge IS
          // visible we keep waiting so a real person is never cut off mid-solve.
          let waited = 0;
          const tick = () => {
            if (resolverRef.current !== resolve) return; // already settled by a widget callback
            waited += WATCHDOG_POLL_MS;
            if (waited >= WATCHDOG_SILENT_LIMIT_MS && !isChallengeVisible()) {
              resolverRef.current = null;
              resolve(null);
              return;
            }
            window.setTimeout(tick, WATCHDOG_POLL_MS);
          };
          try {
            grecaptcha.execute(widgetIdRef.current);
            window.setTimeout(tick, WATCHDOG_POLL_MS);
          } catch {
            resolverRef.current = null;
            resolve(null);
          }
        });
        return token || null;
      } finally {
        try {
          grecaptcha.reset(widgetIdRef.current);
        } catch {
          /* widget may already be torn down */
        }
      }
    },
  }), []);

  if (!captchaEnabled) return null;

  return <div ref={containerRef} className="invisible-captcha" aria-hidden="true" />;
});

export default InvisibleCaptcha;
