import axiosInstance from './axiosInstance';

export async function getStates() {
  const { data } = await axiosInstance.get('/api/ref/states');
  return data;
}

export async function getCities() {
  const { data } = await axiosInstance.get('/api/ref/cities');
  return data;
}

export async function getPaymentTypes() {
  const { data } = await axiosInstance.get('/api/ref/payment-types');
  return data;
}

export async function getWorkCategories() {
  const { data } = await axiosInstance.get('/api/ref/work-categories');
  return data;
}

export async function getWorkTypes(cat) {
  const { data } = await axiosInstance.get('/api/ref/work-types', {
    params: cat ? { cat } : undefined,
  });
  return data;
}

export async function getWellUseTypes(cat, type) {
  const { data } = await axiosInstance.get('/api/ref/well-use-types', {
    params: { cat, type },
  });
  return data;
}

export async function getDrillMethods() {
  const { data } = await axiosInstance.get('/api/ref/drill-methods');
  return data;
}