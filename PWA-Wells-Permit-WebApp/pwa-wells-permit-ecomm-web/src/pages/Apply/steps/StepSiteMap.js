import FormSection from '../../../components/FormSection/FormSection';
import Button from '../../../components/UI/Button';
import config from '../../../config';

const FALLBACK_MAX_BYTES = 100 * 1024 * 1024;
const MAX_FILE_BYTES = Number(config.maxFileSizeUpload) > 0 ? Number(config.maxFileSizeUpload) : FALLBACK_MAX_BYTES;
const ACCEPT = 'application/pdf,image/png,image/jpeg,image/gif,image/tiff,.pdf,.png,.jpg,.jpeg,.gif,.tif,.tiff';

function formatSize(bytes) {
  const num = Number(bytes) || 0;
  if (num < 1024) return `${num} B`;
  if (num < 1024 * 1024) return `${(num / 1024).toFixed(1)} KB`;
  return `${(num / (1024 * 1024)).toFixed(1)} MB`;
}

function StepSiteMap({ formData, setDocument, removeDocument, errors = {} }) {
  const doc = formData.document;
  const maxMb = Math.round(MAX_FILE_BYTES / (1024 * 1024));

  const onFile = (e) => {
    const file = e.target.files && e.target.files[0];
    if (file) setDocument(file, 'SITEMAP');
  };

  return (
    <FormSection title="Site Map Upload" borderColor="m-blue">
      <p className="text-muted">
        Attach a site map in Adobe PDF or image format (up to {maxMb} MB). The file is scanned
        for viruses before it is accepted.
      </p>

      {!doc && (
        <div className="form-row">
          <label className="form-label" htmlFor="sitemapFile">Site Map</label>
          <div className="form-col">
            <input id="sitemapFile" type="file" className="form-control" accept={ACCEPT} onChange={onFile} aria-invalid={errors.document ? true : undefined} aria-describedby={errors.document ? 'sitemapFile-error' : undefined} />
            {errors.document && <span id="sitemapFile-error" className="field-error" role="alert">{errors.document}</span>}
          </div>
        </div>
      )}

      {doc && (
        <div className="form-row">
          <span className="form-label">Selected File</span>
          <div className="form-col">
            <div className="repeatable-row">
              <span>
                <strong>{doc.fileName}</strong> ({formatSize(doc.fileSize)}{doc.contentType ? `, ${doc.contentType}` : ''})
              </span>
              <Button variant="danger" onClick={removeDocument}>Remove</Button>
            </div>
            {errors.document && <span className="field-error">{errors.document}</span>}
          </div>
        </div>
      )}
    </FormSection>
  );
}

export default StepSiteMap;
