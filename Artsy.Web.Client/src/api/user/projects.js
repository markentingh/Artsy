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
    reorderItems: (request) => api.post(`${apiPath}/reorder-items`, request),
    getBlueprints: (projectId) => api.get(`${apiPath}/get-blueprints?projectId=${projectId}`),
    createBlueprint: (request) => api.post(`${apiPath}/create-blueprint`, request),
    deleteBlueprint: (request) => api.post(`${apiPath}/delete-blueprint`, request),
    updateBlueprint: (request) => api.post(`${apiPath}/update-blueprint`, request),
    getBlueprintPlaceholders: (projectId) => api.get(`${apiPath}/get-blueprint-placeholders?projectId=${projectId}`),
    updateBlueprintPlacement: (request) => api.post(`${apiPath}/update-blueprint-placement`, request),
    updateItemTitle: (request) => api.post(`${apiPath}/update-item-title`, request),
    updateItemSocialMedia: (request) => api.post(`${apiPath}/update-item-social-media`, request),
    getItemArtwork: (itemId) => api.get(`${apiPath}/get-item-artwork?itemId=${itemId}`),
    updateItemPrompt: (request) => api.post(`${apiPath}/update-item-prompt`, request),
    updateItemImageModel: (request) => api.post(`${apiPath}/update-item-image-model`, request),
    updateItemArtworkType: (request) => api.post(`${apiPath}/update-item-artwork-type`, request),
    getItemPreviews: (itemId) => api.get(`${apiPath}/get-item-previews?itemId=${itemId}`),
    generateItemPreview: (request) => api.post(`${apiPath}/generate-item-preview`, request),
    getItemPreviewUrl: (itemId, previewId, thumb = false) => `${apiPath}/item/${itemId}/preview/${previewId}${thumb ? '?thumb=true' : ''}`,
    getItemReferences: (itemId) => api.get(`${apiPath}/get-item-references?itemId=${itemId}`),
    uploadItemReference: (itemId, file) => {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('itemId', itemId);
      return api.post(`${apiPath}/upload-item-reference`, formData, { headers: { 'Content-Type': 'multipart/form-data' } });
    },
    deleteItemReference: (request) => api.post(`${apiPath}/delete-item-reference`, request),
    getItemReferenceUrl: (itemId, referenceId, thumb = false) => `${apiPath}/item/${itemId}/reference/${referenceId}${thumb ? '?thumb=true' : ''}`,
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
    updateKey: (request) => api.post(`${apiPath}/update-key`, request),
    updatePublishToPrintify: (request) => api.post(`${apiPath}/update-publish-to-printify`, request)
  };
});

export { Projects };
