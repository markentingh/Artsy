import { Api } from '@/api/Api';

const PersonalizeOrder = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/personalize-order';
  return {
    getOrderItemPlacements: (orderId, orderItemId) => api.get(`${apiPath}/${orderId}/items/${orderItemId}/placements`),
    getProjectQuestions: (orderId, orderItemId) => api.get(`${apiPath}/${orderId}/items/${orderItemId}/project-questions`),
    saveProjectQuestions: (orderId, orderItemId, answers) => api.post(`${apiPath}/${orderId}/items/${orderItemId}/project-questions`, { answers }),
    estimateOrderItemToken: (orderId, orderItemId, artworkItemId, modelId) => api.get(`${apiPath}/${orderId}/items/${orderItemId}/estimate-token?artworkItemId=${artworkItemId}&modelId=${modelId || 0}`),
    getOrderItemArtworks: (orderId, orderItemId) => api.get(`${apiPath}/${orderId}/items/${orderItemId}/artworks`),
    generateOrderItemArtwork: (orderId, orderItemId, artworkItemId, modelId, requestText) => api.post(`${apiPath}/${orderId}/items/${orderItemId}/generate-artwork`, { orderId, orderItemId, artworkItemId, modelId, requestText }),
    acceptOrderItemArtwork: (orderId, orderItemId, artworkId) => api.post(`${apiPath}/${orderId}/items/${orderItemId}/artworks/${artworkId}/accept`, { orderId, orderItemId, artworkId }),
    downloadOrderItemArtworks: (orderId, orderItemId) => `${apiPath}/${orderId}/items/${orderItemId}/download-zip`,
  };
});

export { PersonalizeOrder };
