import axiosInstance from './axiosInstance';

// Report + dashboard queue-count endpoints (ported from the legacy intra DisplayReportServlet).

export async function getReconciliationReport({ start, end, payType = '' }) {
  const { data } = await axiosInstance.get('/api/reports/reconciliation', {
    params: { start, end, payType },
  });
  return data;
}

export async function getCompletedWorksReport({ start, end, inspectorId = '', cityCode = '' }) {
  const { data } = await axiosInstance.get('/api/reports/completed-works', {
    params: { start, end, inspectorId, cityCode },
  });
  return data;
}

export async function getCompletedInspectionsReport({ fromDate = '', toDate = '', inspectorId = '' }) {
  const { data } = await axiosInstance.get('/api/reports/completed-inspections', {
    params: { fromDate, toDate, inspectorId },
  });
  return data;
}

export async function getExtractReport(payload) {
  const { data } = await axiosInstance.post('/api/reports/extract', payload);
  return data;
}

export async function getQueueCounts() {
  const { data } = await axiosInstance.get('/api/reports/queue-counts');
  return data;
}
