import axiosInstance from './axiosInstance';

// Attaches the reCAPTCHA response token (when present) as the X-Captcha-Token header the API's
// public-endpoint guard reads. Tokens are single-use, so each protected call needs its own.
function captchaHeaders(captchaToken, extra = {}) {
  return captchaToken ? { ...extra, 'X-Captcha-Token': captchaToken } : { ...extra };
}

export async function submitApplication(payload, captchaToken) {
  const { data } = await axiosInstance.post('/api/applications', payload, {
    headers: captchaHeaders(captchaToken),
  });
  return data;
}

export async function getApplication(appId) {
  const { data } = await axiosInstance.get(`/api/applications/${appId}`);
  return data;
}

export async function searchApplications(params, captchaToken) {
  const { data } = await axiosInstance.get('/api/applications/search', {
    params,
    headers: captchaHeaders(captchaToken),
  });
  return data;
}

// POST {apiBaseUrl}/api/applications/{applicationId}/sitemap (multipart/form-data)
// Records the site map directly on APPLICATION_INFO (sitemap_filename + sitemap_received_date),
// matching the legacy ProcessFileUploadServlet / updateSitemap flow.
export async function uploadSitemap(applicationId, file, captchaToken) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await axiosInstance.post(
    `/api/applications/${applicationId}/sitemap`,
    form,
    { headers: captchaHeaders(captchaToken, { 'Content-Type': 'multipart/form-data' }) }
  );
  return data;
}
