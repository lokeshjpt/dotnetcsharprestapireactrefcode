import axiosInstance from './axiosInstance';

export async function getConditions(appId) {
  const { data } = await axiosInstance.get(`/api/applications/${appId}/conditions`);
  return data;
}

// Replace the conditions applied to a single work of the application. The legacy intra edits permit
// conditions per work (an application with multiple works has one condition set per work), so the
// endpoint is scoped by workId. Returns the refreshed per-work conditions payload.
export async function updateWorkConditions(appId, workId, payload) {
  const { data } = await axiosInstance.put(
    `/api/applications/${appId}/works/${workId}/conditions`,
    payload,
  );
  return data;
}
