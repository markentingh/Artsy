import { Api } from '@/api/Api';

const PrintifyImageMatch = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/admin/printify-image-match';
  return {
    getUnpublishedBlueprints: () => api.get(`${apiPath}/unpublished-blueprints`),
    getBlueprint: (blueprintId) => api.get(`${apiPath}/blueprints/${blueprintId}`),
    getBlueprintImages: (blueprintId) => api.get(`${apiPath}/blueprints/${blueprintId}/images`),
    applyVariants: (blueprintId, imageIndex, data) =>
      api.post(`${apiPath}/blueprints/${blueprintId}/images/${imageIndex}/apply-variants`, data),
    publishBlueprint: (blueprintId) => api.post(`${apiPath}/blueprints/${blueprintId}/publish`),
  };
});

export { PrintifyImageMatch };
