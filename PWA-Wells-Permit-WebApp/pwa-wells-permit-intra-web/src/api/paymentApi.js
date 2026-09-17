import axiosInstance from './axiosInstance';

export async function getPayment(appId) {
  const { data } = await axiosInstance.get(`/api/payment/${appId}`);
  return data;
}

export async function chargePayment(payload) {
  const { data } = await axiosInstance.post('/api/payment/charge', payload);
  return data;
}

export async function preAuthorizePayment(payload) {
  const { data } = await axiosInstance.post('/api/payment/preauth', payload);
  return data;
}

export async function getLightboxScripts() {
  const { data } = await axiosInstance.get('/api/payment/lightbox');
  return data?.scripts || '';
}

export async function updatePayment(appId, payload) {
  const { data } = await axiosInstance.put(`/api/payment/${appId}`, payload);
  return data;
}
