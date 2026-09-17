import { useState } from 'react';
import axios from 'axios';
import config from '../../config';
import InputField from '../../components/UI/InputField';
import Button from '../../components/UI/Button';
import { useToast } from '../../components/UI/Toaster/ToastProvider';

function FileUploadPage() {
  const [appId, setAppId] = useState('');
  const [file, setFile] = useState(null);
  const [message, setMessage] = useState('');
  const showToast = useToast();

  const handleUpload = async () => {
    if (!file || !appId) {
      setMessage('Provide an application id and choose a file before uploading.');
      return;
    }

    const formData = new FormData();
    formData.append('appId', appId);
    formData.append('file', file);

    try {
      const { data } = await axios.post(`${config.apiBaseUrl}/api/files/upload`, formData);
      setMessage(`Uploaded ${data.fileName} to ${data.remotePath}.`);
      showToast(`Uploaded ${data.fileName}.`, 'success');
    } catch {
      setMessage('Upload failed. Check API availability and allowed file types.');
      showToast('File upload failed.', 'error');
    }
  };

  return (
    <div className="page-shell">
      <div className="panel">
        <h1 className="page-title">Upload sitemap</h1>
        <p className="page-subtitle">Route a sitemap through ICAP scanning and FTP delivery.</p>
        <div style={{ maxWidth: '420px' }}>
          <InputField id="uploadAppId" label="Application number" value={appId} onChange={(event) => setAppId(event.target.value)} />
          <label className="ui-field">
            <span className="ui-field__label">Attachment</span>
            <input className="ui-field__input" type="file" onChange={(event) => setFile(event.target.files?.[0] || null)} />
          </label>
          <Button onClick={handleUpload}>Upload file</Button>
        </div>
        {message && <div className={`alert ${message.startsWith('Uploaded') ? 'alert-success' : 'alert-error'}`} style={{ marginTop: '16px' }}>{message}</div>}
      </div>
    </div>
  );
}

export default FileUploadPage;
