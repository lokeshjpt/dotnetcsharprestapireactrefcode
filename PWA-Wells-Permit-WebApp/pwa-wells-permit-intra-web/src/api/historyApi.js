import axiosInstance from './axiosInstance';

// GET /api/history/permits — search the legacy HIST_PERMITS table ("Pre-System History Permits
// 1987-April 2005"). Returns { items, totalCount } for server-side paging.
export async function searchHistoryPermits(params) {
  const { data } = await axiosInstance.get('/api/history/permits', { params });
  return data;
}

// GET /api/history/wells — search the legacy HIST_WELL_LOC table ("History Well Locations").
export async function searchHistoryWells(params) {
  const { data } = await axiosInstance.get('/api/history/wells', { params });
  return data;
}

// GET /api/history/cities — distinct city name/code pairs for the well-edit city dropdown.
export async function getHistoryCities() {
  const { data } = await axiosInstance.get('/api/history/cities');
  return data;
}

// ---- History permit detail / edit / delete (legacy s=HD + updhist / delhist) ----

export async function getHistoryPermit(permitNum) {
  const { data } = await axiosInstance.get(`/api/history/permits/${encodeURIComponent(permitNum)}`);
  return data;
}

export async function updateHistoryPermit(permitNum, payload) {
  const { data } = await axiosInstance.put(`/api/history/permits/${encodeURIComponent(permitNum)}`, payload);
  return data;
}

// POST /api/history/permits — add a new pre-system history permit (legacy a=add on updhist).
export async function createHistoryPermit(payload) {
  const { data } = await axiosInstance.post('/api/history/permits', payload);
  return data;
}

export async function deleteHistoryPermit(permitNum) {
  await axiosInstance.delete(`/api/history/permits/${encodeURIComponent(permitNum)}`);
}

// GET /api/history/permits/{permitNum}/files/{fileType} — stream a stored uploaded file
// (fileType: document | permit | wellrpt). Returns a Blob for window.open display.
export async function downloadHistoryPermitFile(permitNum, fileType) {
  const { data } = await axiosInstance.get(
    `/api/history/permits/${encodeURIComponent(permitNum)}/files/${encodeURIComponent(fileType)}`,
    { responseType: 'blob' },
  );
  return data;
}

// ---- History well detail / edit / delete (legacy s=HWD + updHistWell / delHistWell) ----

export async function getHistoryWell(wellKey) {
  const { data } = await axiosInstance.get(`/api/history/wells/${encodeURIComponent(wellKey)}`);
  return data;
}

export async function updateHistoryWell(wellKey, payload) {
  const { data } = await axiosInstance.put(`/api/history/wells/${encodeURIComponent(wellKey)}`, payload);
  return data;
}

// POST /api/history/wells — add a new history well location (legacy a=add on updHistWell).
export async function createHistoryWell(payload) {
  const { data } = await axiosInstance.post('/api/history/wells', payload);
  return data;
}

export async function deleteHistoryWell(wellKey) {
  await axiosInstance.delete(`/api/history/wells/${encodeURIComponent(wellKey)}`);
}
