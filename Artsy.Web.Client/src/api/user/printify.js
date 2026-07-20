import { Api } from '@/api/Api';

const Printify = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api/printify';
  return {
    getBlueprints: (keyword = '', brand = '', start = 0, length = 20) => {
      const params = new URLSearchParams();
      if (keyword) params.append('keyword', keyword);
      if (brand) params.append('brand', brand);
      params.append('start', start);
      params.append('length', length);
      return api.get(`${apiPath}/blueprints?${params.toString()}`);
    },
    getBrands: () => api.get(`${apiPath}/brands`),
    getBlueprintDetail: (blueprintId) => api.get(`${apiPath}/blueprints/${blueprintId}`),
    getBlueprintVariants: (blueprintId, printProviderId) =>
      api.get(`${apiPath}/blueprints/${blueprintId}/print-providers/${printProviderId}/variants`),
    getVariantAvailability: (blueprintId, printProviderId) =>
      api.get(`${apiPath}/blueprints/${blueprintId}/print-providers/${printProviderId}/variant-availability`),
    getBlueprintImageUrl: (blueprintId, index = 0, thumb = false) =>
      `${apiPath}/blueprint-image?blueprintId=${blueprintId}&index=${index}${thumb ? '&thumb=true' : ''}`,
  };
});

export { Printify };
