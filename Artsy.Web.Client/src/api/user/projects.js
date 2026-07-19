import { Api } from '@/api/Api';

const Projects = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api/projects';
  return {
    getAll: () => api.get(`${apiPath}/get-all`),
    getById: (id) => api.get(`${apiPath}/get-by-id?id=${id}`),
    getCollections: (projectId) => api.get(`${apiPath}/get-collections?projectId=${projectId}`),
    getItems: (projectId) => api.get(`${apiPath}/get-items?projectId=${projectId}`),
    createItem: (request) => api.post(`${apiPath}/create-item`, request),
    deleteItem: (request) => api.post(`${apiPath}/delete-item`, request),
    getItemBlueprints: (itemId) => api.get(`${apiPath}/get-item-blueprints?itemId=${itemId}`),
    createItemBlueprint: (request) => api.post(`${apiPath}/create-item-blueprint`, request),
    deleteItemBlueprint: (request) => api.post(`${apiPath}/delete-item-blueprint`, request),
    updateItemBlueprint: (request) => api.post(`${apiPath}/update-item-blueprint`, request),
    updateItemTitle: (request) => api.post(`${apiPath}/update-item-title`, request),
    getItemArtwork: (itemId) => api.get(`${apiPath}/get-item-artwork?itemId=${itemId}`),
    updateItemPrompt: (request) => api.post(`${apiPath}/update-item-prompt`, request),
    updateItemImageModel: (request) => api.post(`${apiPath}/update-item-image-model`, request),
    getItemPreviews: (itemId) => api.get(`${apiPath}/get-item-previews?itemId=${itemId}`),
    generateItemPreview: (request) => api.post(`${apiPath}/generate-item-preview`, request),
    getItemPreviewUrl: (itemId, previewId, thumb = false) => `${apiPath}/item/${itemId}/preview/${previewId}${thumb ? '?thumb=true' : ''}`,
    getItemQuestions: (itemId) => api.get(`${apiPath}/get-item-questions?itemId=${itemId}`),
    createItemQuestion: (request) => api.post(`${apiPath}/create-item-question`, request),
    updateItemQuestion: (request) => api.post(`${apiPath}/update-item-question`, request),
    deleteItemQuestion: (request) => api.post(`${apiPath}/delete-item-question`, request),
    getQuestions: (projectId) => api.get(`${apiPath}/get-questions?projectId=${projectId}`),
    getChecklist: (projectId) => api.get(`${apiPath}/get-checklist?projectId=${projectId}`),
    createQuestion: (request) => api.post(`${apiPath}/create-question`, request),
    updateQuestion: (request) => api.post(`${apiPath}/update-question`, request),
    deleteQuestion: (request) => api.post(`${apiPath}/delete-question`, request),
    getArtwork: (projectId, options = {}) => {
      const params = new URLSearchParams({ projectId });
      if (options.collectionId) params.append('collectionId', options.collectionId);
      if (typeof options.start === 'number') params.append('start', options.start);
      if (typeof options.length === 'number') params.append('length', options.length);
      return api.get(`${apiPath}/get-artwork?${params.toString()}`);
    },
    getCollectionArtworkUrl: (collectionId, artworkId, index) => `${apiPath}/collection/${collectionId}/artwork/${artworkId}/${index}`,
    create: (project) => api.post(`${apiPath}/create`, project),
    updateTitle: (request) => api.post(`${apiPath}/update-title`, request),
    updateKey: (request) => api.post(`${apiPath}/update-key`, request)
  };
});

export { Projects };
