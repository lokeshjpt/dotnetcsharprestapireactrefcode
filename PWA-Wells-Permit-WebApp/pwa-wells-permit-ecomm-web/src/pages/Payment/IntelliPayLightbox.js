import { useEffect, useRef, useState } from 'react';
import Button from '../../components/UI/Button';
import { preAuthorizePayment, getLightboxScripts } from '../../api/paymentApi';

// Marks the injected IntelliPay <script> tags so we can detect a prior injection and never
// execute the bundle twice.
const INTELLIPAY_BUNDLE_MARKER = 'data-intellipay-bundle';

// True once the IntelliPay bundle has been injected/executed on this page. The bundle declares
// top-level `const` identifiers (e.g. intellipay_iso2namestate), so re-executing it in the same
// global scope throws "Identifier '…' has already been declared". Detect via the live global it
// defines or the marker on its <script> tags (which we intentionally leave in <head>).
function isIntelliPayBundleLoaded() {
  return (
    !!window.intellipay ||
    !!document.querySelector(`script[${INTELLIPAY_BUNDLE_MARKER}]`)
  );
}

// Injects a raw HTML/script block (as returned by the IntelliPay autoterminal endpoint)
// into <head>, executing any <script> tags. Mirrors the legacy app_verify.jsp behaviour
// of printing the lightboxScripts block into the page head. Injection is idempotent: if the
// bundle was already injected on a previous mount, it is NOT executed again (that would
// redeclare its top-level consts and throw a SyntaxError).
function injectScriptBlock(html) {
  if (isIntelliPayBundleLoaded()) {
    return [];
  }
  const container = document.createElement('div');
  container.innerHTML = html;
  const injected = [];
  Array.from(container.childNodes).forEach((node) => {
    if (node.nodeName === 'SCRIPT') {
      const script = document.createElement('script');
      Array.from(node.attributes).forEach((attr) => script.setAttribute(attr.name, attr.value));
      script.setAttribute(INTELLIPAY_BUNDLE_MARKER, '1');
      script.text = node.textContent || '';
      document.head.appendChild(script);
      injected.push(script);
    } else if (node.nodeType === 1) {
      document.head.appendChild(node);
      injected.push(node);
    }
  });
  return injected;
}

// On unmount we HIDE the shared lightbox modal (and restore page scroll) but intentionally leave
// the #intellipay-lightbox <iframe> in the DOM. intellipay.initialize() self-nullifies after its
// first call and can only insert that iframe once; removing it would strand the terminal (a remount
// could never recreate it — initialize() is now a no-op), which is exactly what made the popup fail
// to open on the first attempt. Keeping it lets the next mount reuse the already-loaded, already
// "ready" iframe (rebinding the fresh trigger button via checkform()). The injected <head> scripts
// are likewise left in place (removing the <script> nodes does NOT un-declare their globals).
function hideInjectedArtifacts() {
  const modal = document.getElementById('intellipay-lightbox');
  if (modal) modal.classList.remove('intellipay-md-show');
  restorePageScroll();
}

// Guarantees the lightbox is usable for the CURRENT mount. intellipay.initialize() inserts the
// #intellipay-lightbox <iframe> modal and calls checkform() — but only the first time (it replaces
// itself with a no-op afterwards). checkform() is what reads the .ipayfield inputs and binds the
// click handler (intellipay.onSubmit → autoOpen) to the element with data-ipayname="submit". Because
// React renders a brand-new #openLightboxBtn node on every mount, we must call checkform() again on
// each mount to bind THAT node; otherwise clicking it (or the auto-open) does nothing. Returns false
// if the terminal could not be prepared.
function ensureLightbox(ip) {
  try {
    if (!document.getElementById('intellipay-lightbox') && typeof ip.initialize === 'function') {
      ip.initialize(); // inserts the iframe modal once + binds the current button via checkform()
    } else if (typeof ip.checkform === 'function') {
      ip.checkform(); // iframe already present: re-read fields and (re)bind the current button node
    }
    return true;
  } catch {
    return false;
  }
}

// While its modal is open the IntelliPay bundle locks the page by setting inline styles on <body>
// (position:fixed; overflow:hidden; width:100vw; height:100vh). It only restores them when the
// lightbox is closed — but on card approval we navigate away immediately, so the lock would persist
// and leave the next page (the confirmation page) clipped and un-scrollable. Clear it explicitly.
function restorePageScroll() {
  const body = document.body;
  if (body) {
    body.style.position = '';
    body.style.overflow = '';
    body.style.width = '';
    body.style.height = '';
    body.style.top = '';
    body.style.left = '';
  }
  if (window.intellipay) {
    try {
      window.intellipay.bodysave = null;
    } catch {
      /* ignore */
    }
  }
}

function IntelliPayLightbox({ appId, amount, existingCustomerId, onSuccess, autoOpen = false }) {
  const [status, setStatus] = useState('');
  const [ready, setReady] = useState(false);
  const [mockMode, setMockMode] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const injectedRef = useRef([]);

  // Persists the vaulted custid returned by the IntelliPay lightbox, then advances.
  async function storeToken(customerId, response) {
    setSubmitting(true);
    let saved = false;
    try {
      // appId is a 13-digit identifier and must be sent as a string (a JSON number is rejected by
      // the API's string binding); amount must be numeric. Coerce defensively so a stray type from
      // the IntelliPay response or fee computation can't cause a spurious "could not be saved".
      await preAuthorizePayment({
        appId: String(appId),
        customerId: String(customerId),
        paymentType: 'CC',
        authorizedAmount: Number(amount) || 0,
        cardBrand: response?.cardbrand || null,
        requestedBy: 'public-portal',
        // PCI-safe snapshot of the lightbox vault response, stored in payment_history (mirrors the
        // legacy Java ecomm). Never send nonce/hmac/methodhint/cardnumdisplay.
        response: {
          call: response?.call || '',
          status: response?.status || '',
          authcode: response?.authcode || '',
          cardbrand: response?.cardbrand || '',
          declinereason: response?.declinereason || '',
          amount: response?.amount != null ? String(response.amount) : '',
          fee: response?.fee != null ? String(response.fee) : '',
        },
      });
      saved = true;
      setStatus(`Payment token stored successfully as ${customerId}.`);
    } catch (err) {
      // Surface the real failure for diagnostics while showing a friendly message to the user.
      // eslint-disable-next-line no-console
      console.error('preAuthorizePayment failed:', err?.response?.status, err?.response?.data || err?.message);
      setStatus('The payment token could not be saved. Please try again.');
    } finally {
      setSubmitting(false);
    }
    // Advance only after a confirmed save, and outside the try/catch so an error while navigating
    // (or resetting the wizard) can't be misreported as a payment-token save failure.
    if (saved) {
      // Release the IntelliPay body scroll-lock before navigating so the confirmation page renders
      // fully and scrollable (the lightbox's own restore never runs because we leave on approval).
      restorePageScroll();
      onSuccess();
    }
  }

  useEffect(() => {
    let cancelled = false;
    let pollId;

    async function load() {
      // If the bundle was already injected on a previous mount (e.g. navigating back to the verify
      // step, or the [appId] effect re-running), reuse the live window.intellipay instead of
      // re-injecting — re-executing the bundle would throw "Identifier '…' has already been
      // declared". Just re-wire callbacks and re-initialize the lightbox for this mount.
      const existing = window.intellipay;
      if (existing && typeof existing.checkform === 'function') {
        if (cancelled) return;
        wireCallbacks();
        if (!ensureLightbox(existing)) setMockMode(true);
        setReady(true);
        return;
      }

      let scripts = '';
      try {
        scripts = await getLightboxScripts();
      } catch {
        scripts = '';
      }
      if (cancelled) return;

      // Mock/stub block (API in UseMock mode or credentials absent) => manual fallback.
      if (!scripts || scripts.indexOf('intellipay') === -1) {
        setMockMode(true);
        setReady(true);
        return;
      }

      injectedRef.current = injectScriptBlock(scripts);

      // The IntelliPay bundle defines window.intellipay and its methods synchronously as the
      // injected <script> tags execute. Poll defensively in case any part loads late.
      let attempts = 0;
      pollId = window.setInterval(() => {
        attempts += 1;
        const ip = window.intellipay;
        if (ip && typeof ip.checkform === 'function') {
          window.clearInterval(pollId);
          if (cancelled) return;
          wireCallbacks();
          // The bundle normally runs initialize() on the window 'load' event — but in this SPA
          // that event fired long before the script was injected, so it never runs on its own.
          // ensureLightbox() invokes it (or checkform() on a remount): it inserts the lightbox
          // iframe and wires the submit trigger (via checkform) to the .ipayfield button, which
          // must already be in the DOM.
          if (!ensureLightbox(ip)) setMockMode(true);
          setReady(true);
        } else if (attempts > 40) {
          window.clearInterval(pollId);
          if (cancelled) return;
          setMockMode(true);
          setReady(true);
        }
      }, 100);
    }

    function wireCallbacks() {
      const ip = window.intellipay;
      try {
        ip.setItemLabel('button', 'Submit Payment');
        ip.setItemLabel('successmessage', ' ');
        ip.disable('account');
        ip.disable('email');
      } catch {
        /* label/disable are best-effort */
      }

      ip.runOnApproval((resp) => {
        const customerId =
          resp.custid || resp.customerid || resp.customer_id || resp.customerId || `cust-${appId}`;
        storeToken(customerId, resp);
      });

      if (typeof ip.runOnNonApproval === 'function') {
        ip.runOnNonApproval((resp) => {
          setStatus(resp?.declinereason || 'Payment was not approved. Please try again.');
        });
      }

      if (typeof ip.runOnClose === 'function') {
        ip.runOnClose(() => {
          /* user dismissed the lightbox; leave the page state untouched so they can retry */
        });
      }
    }

    load();
    return () => {
      cancelled = true;
      if (pollId) window.clearInterval(pollId);
      hideInjectedArtifacts();
      injectedRef.current = [];
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [appId]);

  // When embedded inline on the verify step (autoOpen), open the secure popup automatically once the
  // terminal is ready so the single "Pay Now and Submit" click flows straight into card entry.
  // intellipay.autoOpen() is race-safe: it sets intellipay.isAutoOpen=true and either opens
  // immediately (if the secure iframe has already posted "ready") or opens the moment it does — so a
  // freshly inserted iframe that is still loading still auto-opens. The timer + its cleanup make this
  // fire exactly once even under React StrictMode's mount→unmount→remount in development.
  useEffect(() => {
    if (!ready || mockMode || !autoOpen) return undefined;
    const t = window.setTimeout(() => {
      const ip = window.intellipay;
      const modal = document.getElementById('intellipay-lightbox');
      if (modal && modal.classList.contains('intellipay-md-show')) return; // already open
      if (ip && typeof ip.autoOpen === 'function') ip.autoOpen();
    }, 300);
    return () => window.clearTimeout(t);
  }, [ready, mockMode, autoOpen]);

  return (
    <div>
      {/* Hidden ipayfields read by the IntelliPay lightbox. Amount is $0.00 — the card is
          vaulted/pre-authorized here and charged for the real fee later by staff. These and the
          submit trigger below are always in the DOM so intellipay.checkform() can wire them. */}
      <input className="ipayfield" data-ipayname="amount" type="hidden" defaultValue="0.00" />
      <input className="ipayfield" data-ipayname="account" type="hidden" defaultValue={appId} />
      <input className="ipayfield" data-ipayname="invoice" type="hidden" defaultValue={appId} />
      <input className="ipayfield" data-ipayname="custid" type="hidden" defaultValue={appId} />
      <input className="ipayfield" data-ipayname="firstname" type="hidden" defaultValue="" />
      <input className="ipayfield" data-ipayname="lastname" type="hidden" defaultValue="" />

      {!ready && <p className="muted-text">Loading secure payment terminal…</p>}

      {ready && !mockMode && (
        <p className="muted-text">
          Enter your card in the secure IntelliPay window. The card is stored (authorized for
          $0.00) and charged only after staff approves your permit.
        </p>
      )}

      {/* Kept in the DOM from first render (hidden until ready / when mocked) so the IntelliPay
          bundle can attach its click handler to the data-ipayname="submit" trigger. */}
      <button
        id="openLightboxBtn"
        className="bigBtn ipayfield"
        data-ipayname="submit"
        type="button"
        disabled={submitting}
        style={{ display: ready && !mockMode ? '' : 'none' }}
      >
        {submitting ? 'Saving…' : 'Pay Now and Submit'}
      </button>

      {ready && mockMode && (
        <>
          <p className="muted-text">
            IntelliPay is not configured for live card entry in this environment. Continue to store
            a placeholder payment token so the application can be submitted.
          </p>
          <Button
            onClick={() => storeToken(existingCustomerId || `cust-${appId}`)}
            disabled={submitting}
          >
            {submitting ? 'Authorizing…' : 'Store payment token'}
          </Button>
        </>
      )}

      {status && (
        <div
          className={`alert ${status.includes('successfully') ? 'alert-success' : 'alert-error'}`}
          style={{ marginTop: '16px' }}
        >
          {status}
        </div>
      )}
    </div>
  );
}

export default IntelliPayLightbox;
