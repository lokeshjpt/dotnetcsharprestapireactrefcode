import { useRef, useState } from 'react';
import Button from '../UI/Button';
import config from '../../config';
import { uploadSitemap } from '../../api/applicationApi';
import { useToast } from '../UI/Toaster/ToastProvider';
import InvisibleCaptcha, { captchaEnabled } from '../Captcha/InvisibleCaptcha';
import './SitemapUpload.css';

const FALLBACK_MAX_BYTES = 100 * 1024 * 1024;
const MAX_FILE_BYTES = Number(config.maxFileSizeUpload) > 0 ? Number(config.maxFileSizeUpload) : FALLBACK_MAX_BYTES;
const ACCEPT = 'application/pdf,image/png,image/jpeg,image/gif,image/tiff,.pdf,.png,.jpg,.jpeg,.gif,.tif,.tiff';

// Shared site-map upload control used on the confirmation page and the track page (for any
// application still Pending Sitemap). Encapsulates the file picker, size validation, upload call,
// result alert and toast so both places behave identically.
function SitemapUpload({ appId, onUploaded, onClose }) {
  const fileRef = useRef(null);
  const captchaRef = useRef(null);
  const [uploading, setUploading] = useState(false);
  const [uploadResult, setUploadResult] = useState(null); // { ok, message }
  const maxMb = Math.round(MAX_FILE_BYTES / (1024 * 1024));
  const showToast = useToast();

  const handleUpload = async () => {
    const file = fileRef.current && fileRef.current.files && fileRef.current.files[0];
    if (!file) {
      setUploadResult({ ok: false, message: 'Please choose a site-map file first.' });
      return;
    }
    if (file.size > MAX_FILE_BYTES) {
      setUploadResult({ ok: false, message: `The file exceeds the ${maxMb} MB limit.` });
      return;
    }
    setUploading(true);
    setUploadResult(null);
    // Invisible captcha gate: humans pass silently; bots are challenged before the upload runs.
    const token = await captchaRef.current?.execute();
    if (captchaEnabled && !token) {
      setUploadResult({ ok: false, message: 'Security verification could not be completed. Please try again in a moment.' });
      setUploading(false);
      return;
    }
    try {
      await uploadSitemap(appId, file, token);
      setUploadResult({
        ok: true,
        message: `Sitemap file successfully uploaded on ${new Date().toLocaleDateString()}.`,
      });
      showToast('Site map uploaded successfully.', 'success');
      if (fileRef.current) fileRef.current.value = '';
      if (typeof onUploaded === 'function') onUploaded();
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error('uploadSitemap failed:', err?.response?.status, err?.response?.data || err?.message);
      setUploadResult({
        ok: false,
        message: 'The site map could not be uploaded. Please try again, or mail/email it using the address below.',
      });
      showToast('Site map upload failed.', 'error');
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="sitemap-upload">
      <div className="sitemap-upload__row">
        <input
          ref={fileRef}
          type="file"
          className="form-control"
          accept={ACCEPT}
          aria-label={`Site map file for application ${appId}`}
          disabled={uploading || !appId}
        />
        <Button onClick={handleUpload} disabled={uploading || !appId}>
          {uploading ? 'Uploading\u2026' : 'Upload Sitemap File'}
        </Button>
        {typeof onClose === 'function' && (
          <Button variant="secondary" onClick={onClose} disabled={uploading}>
            Close
          </Button>
        )}
      </div>
      {uploadResult && (
        <div
          className={`alert ${uploadResult.ok ? 'alert-success' : 'alert-error'}`}
          role={uploadResult.ok ? 'status' : 'alert'}
        >
          {uploadResult.message}
        </div>
      )}
      <InvisibleCaptcha ref={captchaRef} />
      {captchaEnabled && (
        <p className="captcha-notice">
          This site is protected by reCAPTCHA and the Google{' '}
          <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Privacy Policy</a>{' '}
          and{' '}
          <a href="https://policies.google.com/terms" target="_blank" rel="noopener noreferrer">Terms of Service</a>{' '}
          apply.
        </p>
      )}
    </div>
  );
}

export default SitemapUpload;
