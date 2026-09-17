import axiosInstance from './axiosInstance';

export async function approveApplication(appId, payload) {
  const { data } = await axiosInstance.post(`/api/approval/${appId}/approve`, payload);
  return data;
}
