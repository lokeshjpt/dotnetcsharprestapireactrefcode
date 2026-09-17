import { useEffect, useRef } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import Button from '../../components/UI/Button';
import config from '../../config';
import SitemapUpload from '../../components/SitemapUpload/SitemapUpload';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import './ConfirmationPage.css';

const FALLBACK_MAX_BYTES = 100 * 1024 * 1024;
const MAX_FILE_BYTES = Number(config.maxFileSizeUpload) > 0 ? Number(config.maxFileSizeUpload) : FALLBACK_MAX_BYTES;

function ConfirmationPage() {
  const location = useLocation();
  const { appId } = useParams();
  const application = location.state?.application;
  const referenceId = application?.appId || appId;
  const paymentType = location.state?.paymentType;

  const maxMb = Math.round(MAX_FILE_BYTES / (1024 * 1024));
  const showToast = useToast();
  const submittedToastShown = useRef(false);

  // Announce the successful submission with a toast (fires once for all payment paths).
  useEffect(() => {
    if (submittedToastShown.current || !referenceId) return;
    submittedToastShown.current = true;
    showToast(`Application ${referenceId} submitted successfully.`, 'success');
  }, [referenceId, showToast]);

  // Self-heal any leftover page scroll-lock. The CC path reaches this page straight from the
  // IntelliPay lightbox, which locks <body> (position:fixed; overflow:hidden) while open; if that
  // lock lingers this page would be clipped and un-scrollable. Clear it on mount unconditionally.
  useEffect(() => {
    const body = document.body;
    if (!body) return;
    body.style.position = '';
    body.style.overflow = '';
    body.style.width = '';
    body.style.height = '';
    body.style.top = '';
    body.style.left = '';
  }, []);

  return (
    <div className="page-shell confirmation-page">
      <div className="panel confirmation-card">
        <div className="confirmation-card__badge">Submitted</div>
        <h1 className="page-title">Application received</h1>
        <p className="page-subtitle">
          This confirms receipt of your application &mdash; it is <strong>not</strong> an approved permit.
          Save the application number below so you can track progress later.
        </p>
        <div className="confirmation-card__number">{referenceId}</div>

        {paymentType === 'CC' && (
          <div className="alert alert-success confirmation-card__payment">
            <strong>&#10003; Credit Card Authorization Approved.</strong> Your card has been
            pre-authorized at $0.00. The permit fee will be charged only after your application
            is approved.
          </div>
        )}

        <div className="confirmation-notice">
          <h2 className="confirmation-notice__title">Important notice &mdash; site map required</h2>
          <p>
            A site map for your project is required for permit approval. If you have the map in an
            electronic format (Adobe PDF preferred) under {maxMb} MB, upload it below. Otherwise,
            please mail or email your site map using the address below and include your application
            number.
          </p>

          <div className="confirmation-upload">
            <SitemapUpload appId={referenceId} />
          </div>

          <div className="confirmation-notice__contact">
            <p>
              <strong>Mailing Address:</strong> Alameda County Public Works Agency &mdash; Water
              Resources, Attn: Wells Permits, 399 Elmhurst St., Hayward, CA 94544-1395
            </p>
            <p>
              <strong>Email:</strong> <a href="mailto:wells@acpwa.org">wells@acpwa.org</a> (include
              your application number)
            </p>
          </div>
        </div>

        <div className="confirmation-card__actions">
          <Link to="/track"><Button>Track status</Button></Link>
          <Link to="/"><Button variant="secondary">Return home</Button></Link>
        </div>
      </div>
    </div>
  );
}

export default ConfirmationPage;
