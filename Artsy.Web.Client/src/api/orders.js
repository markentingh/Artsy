import { Api } from '@/api/Api';

const Orders = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/orders';
  return {
    getOrders: () => api.get(apiPath),
    getOrder: (id) => api.get(`${apiPath}/${id}`),
    getOrderImages: (orderId) => api.get(`${apiPath}/${orderId}/images`),
    refreshOrders: () => api.post(`${apiPath}/refresh`),
  };
});

export { Orders };
