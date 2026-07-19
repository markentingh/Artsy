import { Api } from '@/api/Api';

const Printify = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/admin/printify';
  return {
    getCatalogCount: () => api.get(`${apiPath}/catalog-count`),
    refreshCatalog: () => api.post(`${apiPath}/refresh-catalog`),
    fetchPrintProviders: (blueprintId) =>
      api.post(`${apiPath}/fetch-print-providers`, { blueprintId }),
    fetchVariants: (blueprintId, printProviderId) =>
      api.post(`${apiPath}/fetch-variants`, { blueprintId, printProviderId }),
    fetchShipping: (blueprintId, printProviderId) =>
      api.post(`${apiPath}/fetch-shipping`, { blueprintId, printProviderId }),
    downloadCatalogImage: (blueprintId, index, url) =>
      api.post(`${apiPath}/download-catalog-image`, { blueprintId, index, url }),
    searchBlueprints: (keyword = '', brand = '', start = 0, length = 20) => {
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
    getBlueprintImageUrl: (blueprintId, index = 0, thumb = false) =>
      `${apiPath}/blueprint-image?blueprintId=${blueprintId}&index=${index}${thumb ? '&thumb=true' : ''}`,
  };
});

export { Printify };
