import { Api } from '@/api/Api';

const Orders = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/orders';
  return {
    getOrders: () => api.get(apiPath),
    getOrder: (id) => api.get(`${apiPath}/${id}`),
    getOrderImages: (orderId) => api.get(`${apiPath}/${orderId}/images`),
    getOrderItemPlacements: (orderId, orderItemId) => api.get(`${apiPath}/${orderId}/items/${orderItemId}/placements`),
    estimateOrderItemToken: (orderId, orderItemId, artworkItemId, modelId) => api.get(`${apiPath}/${orderId}/items/${orderItemId}/estimate-token?artworkItemId=${artworkItemId}&modelId=${modelId || 0}`),
    refreshOrders: () => api.post(`${apiPath}/refresh`),
  };
});

export { Orders };
