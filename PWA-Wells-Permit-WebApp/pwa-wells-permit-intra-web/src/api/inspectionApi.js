import axiosInstance from './axiosInstance';

export async function getInspections(appId) {
  const { data } = await axiosInstance.get(`/api/inspections/${appId}`);
  return data;
}

export async function addInspection(payload) {
  const { data } = await axiosInstance.post('/api/inspections', payload);
  return data;
}

// The API PUT route is body-only (INSPECTION_ASSIGNMENTS PK = inspectionDate + slotId),
// so the record is identified by fields in the payload, not by a URL id.
export async function updateInspection(payload) {
  const { data } = await axiosInstance.put('/api/inspections', payload);
  return data;
}

// Removes an inspection assignment identified by its composite key (date + slot).
export async function deleteInspection(inspectionDate, slotId) {
  const dateKey = typeof inspectionDate === 'string' ? inspectionDate.slice(0, 10) : inspectionDate;
  await axiosInstance.delete(`/api/inspections/${dateKey}/${slotId}`);
}

export async function getInspectionAvailability(from, to) {
  const { data } = await axiosInstance.get('/api/inspections/availability', {
    params: { from, to },
  });
  return data;
}

export async function scheduleInspection(payload) {
  const { data } = await axiosInstance.post('/api/inspections/schedule', payload);
  return data;
}

// ---- Intra Inspections menu list screens (server-side paged + sorted) ----

// GET /api/inspections/pending — Permits with Pending Inspections (inspection_pending_list.jsp).
export async function searchPendingInspections(params) {
  const { data } = await axiosInstance.get('/api/inspections/pending', { params });
  return data;
}

// GET /api/inspections/pending-wcr — Pending WCR List (pending_dwr_list.jsp).
export async function searchPendingWcr(params) {
  const { data } = await axiosInstance.get('/api/inspections/pending-wcr', { params });
  return data;
}

// GET /api/inspections/pending-geolog — Pending GeoLog List (pending_geo_list.jsp).
export async function searchPendingGeoLog(params) {
  const { data } = await axiosInstance.get('/api/inspections/pending-geolog', { params });
  return data;
}

// GET /api/inspections/hold — Permits On Hold List (hold_list.jsp).
export async function searchHoldList(params) {
  const { data } = await axiosInstance.get('/api/inspections/hold', { params });
  return data;
}

// GET /api/inspections/scheduled — assignment lines in [from, to] for the Inspections Calendar.
export async function getScheduledInspections(from, to) {
  const { data } = await axiosInstance.get('/api/inspections/scheduled', { params: { from, to } });
  return data;
}

