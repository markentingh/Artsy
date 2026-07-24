import { Api } from '@/api/Api';

const OpenAI = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/admin/openai';
  const imgPath = '/api/admin/image-generation';
  return {
    getAll: () => api.get(`${apiPath}/get-all`),
    getById: (id) => api.get(`${apiPath}/get-by-id?id=${id}`),
    add: (model) => api.post(`${apiPath}/add`, model),
    update: (model) => api.post(`${apiPath}/update`, model),
    setEnabled: (id, enabled) => api.post(`${apiPath}/set-enabled`, { id, enabled }),
    setPreferred: (id) => api.post(`${apiPath}/set-preferred`, { id }),
    delete: (id) => api.post(`${apiPath}/delete`, { id }),
    getImageModels: () => api.get(`${imgPath}/get-models`),
    saveImageModel: (model) => api.post(`${imgPath}/save-model`, model),
    toggleImageModelActive: (id, active) => api.post(`${imgPath}/toggle-active`, { id, active }),
    deleteImageModel: (id) => api.post(`${imgPath}/delete-model`, { id })
  };
});

export { OpenAI };
