import axiosInstance from './axiosInstance';

// Lightweight identity/authorization probe. Returns { name, email, authorized } on success (200).
// A non-whitelisted authenticated user gets 403 from the API, which rejects this promise (and the
// axios response interceptor also fires the global not-authorized event).
export async function getMe() {
  const { data } = await axiosInstance.get('/api/auth/me');
  return data;
}
