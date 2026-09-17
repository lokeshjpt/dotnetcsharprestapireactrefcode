import axiosInstance from './axiosInstance';

// Code Maintenance API — reference-table CRUD (parity with the legacy intra MaintCodeServlet).
// Reads hit /api/maint/<endpoint>; writes require an authenticated staff session (the axios
// instance attaches the MSAL bearer token). Errors surface the server's 400/404/409 message.

export async function listMaint(endpoint) {
  const { data } = await axiosInstance.get(`/api/maint/${endpoint}`);
  return data;
}

export async function createMaint(endpoint, body) {
  const { data } = await axiosInstance.post(`/api/maint/${endpoint}`, body);
  return data;
}

export async function updateMaint(endpoint, body) {
  const { data } = await axiosInstance.put(`/api/maint/${endpoint}`, body);
  return data;
}

// Delete either by a single path key (/endpoint/{code}) or by query params for composite keys.
export async function deleteMaint(endpoint, { pathKey, query } = {}) {
  if (pathKey != null) {
    const { data } = await axiosInstance.delete(`/api/maint/${endpoint}/${encodeURIComponent(pathKey)}`);
    return data;
  }
  const { data } = await axiosInstance.delete(`/api/maint/${endpoint}`, { params: query });
  return data;
}

// Inspection Max Slots Per Day is a singleton control row rather than a list.
export async function getInspectionSlots() {
  const { data } = await axiosInstance.get('/api/maint/inspection-slots');
  return data;
}

export async function updateInspectionSlots(maxSlotsPerDay) {
  const { data } = await axiosInstance.put('/api/maint/inspection-slots', { maxSlotsPerDay });
  return data;
}

// Extract a readable message from a maintenance write error (validation array / conflict / notfound).
export function maintErrorMessage(err, fallback = 'The request could not be completed.') {
  const data = err?.response?.data;
  if (data?.errors && Array.isArray(data.errors) && data.errors.length) {
    return data.errors.join(' ');
  }
  if (typeof data?.message === 'string' && data.message) {
    return data.message;
  }
  if (typeof data === 'string' && data) {
    return data;
  }
  return fallback;
}
