import axiosInstance from './axiosInstance';

export async function getApplication(appId) {
  const { data } = await axiosInstance.get(`/api/applications/${appId}`);
  return data;
}

export async function searchApplications(params) {
  const { data } = await axiosInstance.get('/api/applications/search', { params });
  return data;
}

export async function updateProjectInfo(appId, payload) {
  const { data } = await axiosInstance.put(`/api/applications/${appId}/project`, payload);
  return data;
}

export async function updateApplicantInfo(appId, payload) {
  const { data } = await axiosInstance.put(`/api/applications/${appId}/applicant`, payload);
  return data;
}

export async function updateHazardInfo(appId, payload) {
  const { data } = await axiosInstance.put(`/api/applications/${appId}/hazard`, payload);
  return data;
}

export async function updateWork(appId, workId, payload) {
  const { data } = await axiosInstance.put(`/api/applications/${appId}/works/${workId}`, payload);
  return data;
}

// POST /api/applications/{appId}/works — staff "Add Work": creates a new work from a work
// category + type (fees seeded from the WORK_TYPES lookup). Returns the refreshed application.
export async function addWork(appId, payload) {
  const { data } = await axiosInstance.post(`/api/applications/${appId}/works`, payload);
  return data;
}

// POST /api/applications/{appId}/works/{workId}/cancel — staff "Cancel Work": sets CAN on the work
// and its specs. Returns the refreshed application.
export async function cancelWork(appId, workId) {
  const { data } = await axiosInstance.post(`/api/applications/${appId}/works/${workId}/cancel`);
  return data;
}

// DELETE /api/applications/{appId}/works/{workId} — staff "Delete Work": removes a not-yet-approved
// work and its specs/conditions. Returns the refreshed application.
export async function deleteWork(appId, workId) {
  const { data } = await axiosInstance.delete(`/api/applications/${appId}/works/${workId}`);
  return data;
}

// PUT /api/applications/{appId}/works/{workId}/wcr — "Enter WCR": saves the Well Completion Report
// grid (State Well #, WCR #, Construction Permit #/WCR #, copy-to-all flag). Approved apps only.
export async function updateWcr(appId, workId, payload) {
  const { data } = await axiosInstance.put(`/api/applications/${appId}/works/${workId}/wcr`, payload);
  return data;
}

// POST /api/applications/{appId}/works/{workId}/specs/{workSpecsId}/geolog (multipart/form-data)
// "Enter GeoLog": uploads a geotechnical log for a single well spec (records geolog_file).
export async function uploadGeolog(appId, workId, workSpecsId, file) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await axiosInstance.post(
    `/api/applications/${appId}/works/${workId}/specs/${workSpecsId}/geolog`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );
  return data;
}

// POST /api/applications/{appId}/works/{workId}/specs/{workSpecsId}/wcr-image (multipart/form-data)
// Per-row "Upload WCR Image" on the Enter WCR grid (records dwr_image).
export async function uploadWcrImage(appId, workId, workSpecsId, file) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await axiosInstance.post(
    `/api/applications/${appId}/works/${workId}/specs/${workSpecsId}/wcr-image`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );
  return data;
}

export async function updateApprovalDetails(appId, payload) {
  const { data } = await axiosInstance.put(`/api/applications/${appId}/approval-details`, payload);
  return data;
}

// POST /api/applications/{appId}/cancel — cancels an application that is not yet approved
// (sets CAN on payment/works/application). Returns the refreshed application.
export async function cancelApplication(appId) {
  const { data } = await axiosInstance.post(`/api/applications/${appId}/cancel`);
  return data;
}

// GET /api/applications/{appId}/permit — permit extras (approver/date + issued permit numbers)
// used by the printable permit page.
export async function getPermitInfo(appId) {
  const { data } = await axiosInstance.get(`/api/applications/${appId}/permit`);
  return data;
}

// POST /api/applications/{appId}/sitemap/staff (multipart/form-data)
// Staff-side site map upload for the Approval Wizard: scans + delivers the file and records it on
// APPLICATION_INFO (sitemap_filename + sitemap_received_date) with the acting staff user as update_by.
export async function uploadSitemap(appId, file) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await axiosInstance.post(
    `/api/applications/${appId}/sitemap/staff`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );
  return data;
}

// ---- Staff "Upload Documents" (APP_DOCUMENT_LINKS) ----

// GET /api/applications/{appId}/documents — uploaded documents joined to their type description.
export async function getDocuments(appId) {
  const { data } = await axiosInstance.get(`/api/applications/${appId}/documents`);
  return data;
}

// POST /api/applications/{appId}/documents/staff (multipart/form-data) — staff upload: scans +
// delivers the file and records an APP_DOCUMENT_LINKS row (type + optional description) with the
// acting staff user as add_by.
export async function uploadDocument(appId, file, documentType, otherTypeDesc) {
  const form = new FormData();
  form.append('file', file);
  form.append('documentType', documentType);
  if (otherTypeDesc) form.append('otherTypeDesc', otherTypeDesc);
  const { data } = await axiosInstance.post(
    `/api/applications/${appId}/documents/staff`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );
  return data;
}

// DELETE /api/applications/{appId}/documents/{seqNum} — removes a single document link.
export async function deleteDocument(appId, seqNum) {
  await axiosInstance.delete(`/api/applications/${appId}/documents/${seqNum}`);
}

// ---- Staff "View/Add Notes" (INSPECTION_NOTES) ----

// GET /api/applications/{appId}/notes — free-text notes, newest first.
export async function getNotes(appId) {
  const { data } = await axiosInstance.get(`/api/applications/${appId}/notes`);
  return data;
}

// POST /api/applications/{appId}/notes — adds a note recorded against the acting staff user.
export async function addNote(appId, notesText) {
  const { data } = await axiosInstance.post(`/api/applications/${appId}/notes`, { notesText });
  return data;
}

// PUT /api/applications/{appId}/extension — staff "New/Edit Post Approval Extension": records a new
// permit extension window (extend_start/end/count/by) + note. Returns the refreshed application.
export async function updateExtension(appId, payload) {
  const { data } = await axiosInstance.put(`/api/applications/${appId}/extension`, payload);
  return data;
}
