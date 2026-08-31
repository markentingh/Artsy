import { Api } from '@/api/Api';

const PrintifyProducts = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api/printify-products';
  return {
    create: (request) => api.post(`${apiPath}/create`, request),
    downloadMockups: (request) => api.post(`${apiPath}/download-mockups`, request),
    uploadArtworkImage: (request) => api.post(`${apiPath}/upload-artwork-image`, request),
    update: (request) => api.post(`${apiPath}/update`, request),
    publish: (request) => api.post(`${apiPath}/publish`, request),
    unpublish: (request) => api.post(`${apiPath}/unpublish`, request),
    archiveUpload: (request) => api.post(`${apiPath}/archive-upload`, request),
    delete: (request) => api.post(`${apiPath}/delete`, request),
    getByCollection: (collectionId) => api.get(`${apiPath}/get-by-collection?collectionId=${collectionId}`),
    getMockups: (collectionId) => api.get(`${apiPath}/get-mockups?collectionId=${collectionId}`),
    getMockupImageUrl: (projectId, collectionId, mockupId) => `${apiPath}/mockup-image?projectId=${projectId}&collectionId=${collectionId}&mockupId=${mockupId}`,
    replaceMockupImage: (formData) => api.post(`${apiPath}/replace-mockup-image`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }),
    ensureProducts: (request) => api.post(`${apiPath}/ensure-products`, request),
    getProducts: (collectionId) => api.get(`${apiPath}/get-products?collectionId=${collectionId}`),
  };
});

export { PrintifyProducts };
