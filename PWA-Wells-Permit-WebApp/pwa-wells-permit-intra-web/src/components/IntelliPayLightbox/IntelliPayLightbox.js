import { useEffect, useRef, useState } from 'react';
import Button from '../UI/Button';
import { preAuthorizePayment, getLightboxScripts } from '../../api/paymentApi';

// Injects a raw HTML/script block (as returned by the IntelliPay autoterminal endpoint)
// into <head>, executing any <script> tags. Mirrors the legacy UpdateAppServlet behaviour
// of printing the lightboxScripts block into the page head.
function injectScriptBlock(html) {
  const container = document.createElement('div');
  container.innerHTML = html;
  const injected = [];
  Array.from(container.childNodes).forEach((node) => {
    if (node.nodeName === 'SCRIPT') {
      const script = document.createElement('script');
      Array.from(node.attributes).forEach((attr) => script.setAttribute(attr.name, attr.value));
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

// Removes the injected <head> scripts/styles and the lightbox iframe modal that
// intellipay.initialize() inserts into <body>, so a remount re-initializes cleanly.
function removeInjectedArtifacts(injected) {
  injected.forEach((node) => node.parentNode && node.parentNode.removeChild(node));
  const modal = document.getElementById('intellipay-lightbox');
  if (modal && modal.parentNode) {
    modal.parentNode.removeChild(modal);
  }
  restorePageScroll();
}

// While its modal is open the IntelliPay bundle locks the page by setting inline styles on <body>.
// It only restores them when the lightbox is closed — clear it explicitly so the staff page stays
// scrollable if we advance immediately on approval.
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

/**
 * Staff "change card" lightbox. Opens the IntelliPay terminal so a staff member can re-vault
 * a new card for an application. On approval the new custid is stored via the pre-auth endpoint
 * ($0 authorization) and onSuccess() is fired.
 */
function IntelliPayLightbox({ appId, amount, existingCustomerId, onSuccess, autoOpen = false }) {
  const [status, setStatus] = useState('');
  const [ready, setReady] = useState(false);
  const [mockMode, setMockMode] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const injectedRef = useRef([]);
  const autoOpenedRef = useRef(false);

  // Persists the vaulted custid returned by the IntelliPay lightbox, then advances.
  async function storeToken(customerId, response) {
    setSubmitting(true);
    let saved = false;
    try {
      await preAuthorizePayment({
        appId: String(appId),
        customerId: String(customerId),
        paymentType: 'CC',
        authorizedAmount: Number(amount) || 0,
        requestedBy: 'staff-portal',
        // Staff "change card" re-vault: the application was already submitted and the applicant
        // already received their confirmation, so do NOT re-send the applicant confirmation email.
        sendConfirmationEmail: false,
      });
      saved = true;
      setStatus(`Card updated. New payment token stored as ${customerId}.`);
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error('preAuthorizePayment failed:', err?.response?.status, err?.response?.data || err?.message);
      setStatus('The payment token could not be saved. Please try again.');
    } finally {
      setSubmitting(false);
    }
    if (saved) {
      restorePageScroll();
      if (onSuccess) onSuccess(customerId, response);
    }
  }

  useEffect(() => {
    let cancelled = false;
    let pollId;

    async function load() {
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

      let attempts = 0;
      pollId = window.setInterval(() => {
        attempts += 1;
        const ip = window.intellipay;
        if (ip && typeof ip.initialize === 'function' && typeof ip.checkform === 'function') {
          window.clearInterval(pollId);
          if (cancelled) return;
          wireCallbacks();
          try {
            ip.initialize();
          } catch {
            setMockMode(true);
          }
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
        ip.setItemLabel('button', 'Update Card');
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
          setStatus(resp?.declinereason || 'Card was not approved. Please try again.');
        });
      }

      if (typeof ip.runOnClose === 'function') {
        ip.runOnClose(() => {
          /* staff dismissed the lightbox; leave page state untouched so they can retry */
        });
      }
    }

    load();
    return () => {
      cancelled = true;
      if (pollId) window.clearInterval(pollId);
      removeInjectedArtifacts(injectedRef.current);
      injectedRef.current = [];
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [appId]);

  // When autoOpen, open the secure popup automatically once the terminal is ready.
  useEffect(() => {
    if (!ready || mockMode || !autoOpen || autoOpenedRef.current) return undefined;
    autoOpenedRef.current = true;
    const t = window.setTimeout(() => {
      const btn = document.getElementById('openLightboxBtn');
      if (btn) btn.click();
    }, 400);
    return () => window.clearTimeout(t);
  }, [ready, mockMode, autoOpen]);

  return (
    <div>
      {/* Hidden ipayfields read by the IntelliPay lightbox. Amount is $0.00 — the card is
          vaulted/pre-authorized here and charged for the real fee later at approval. */}
      <input className="ipayfield" data-ipayname="amount" type="hidden" defaultValue="0.00" />
      <input className="ipayfield" data-ipayname="account" type="hidden" defaultValue={appId} />
      <input className="ipayfield" data-ipayname="invoice" type="hidden" defaultValue={appId} />
      <input className="ipayfield" data-ipayname="custid" type="hidden" defaultValue={appId} />
      <input className="ipayfield" data-ipayname="firstname" type="hidden" defaultValue="" />
      <input className="ipayfield" data-ipayname="lastname" type="hidden" defaultValue="" />

      {!ready && <p className="muted-text">Loading secure payment terminal…</p>}

      {ready && !mockMode && (
        <p className="muted-text">
          Enter the new card in the secure IntelliPay window. The card is stored (authorized for
          $0.00) and charged only when the permit is approved.
        </p>
      )}

      {/* Kept in the DOM from first render (hidden until ready) so the IntelliPay bundle can
          attach its click handler to the data-ipayname="submit" trigger. */}
      <button
        id="openLightboxBtn"
        className="bigBtn ipayfield btn btn-primary"
        data-ipayname="submit"
        type="button"
        disabled={submitting}
        style={{ display: ready && !mockMode ? '' : 'none' }}
      >
        {submitting ? 'Saving…' : 'Update and Change Card'}
      </button>

      {ready && mockMode && (
        <>
          <p className="muted-text">
            IntelliPay is not configured for live card entry in this environment. Continue to store
            a placeholder payment token so the change can be recorded.
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
          className={`alert ${status.includes('updated') || status.includes('stored') ? 'alert-success' : 'alert-error'}`}
          style={{ marginTop: '16px' }}
        >
          {status}
        </div>
      )}
    </div>
  );
}

export default IntelliPayLightbox;
