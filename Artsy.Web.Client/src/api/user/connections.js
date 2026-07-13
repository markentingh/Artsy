import { Api } from '@/api/Api';

const Connections = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api';
  return {
    getPrintifyStatus: () => api.get(`${apiPath}/printify/status`),
    connectPrintify: () => api.get(`${apiPath}/printify/connect`),
    getTelegramStatus: () => api.get(`${apiPath}/telegram/status`),
    connectTelegram: () => api.get(`${apiPath}/telegram/connect`),
    getMetaStatus: () => api.get(`${apiPath}/meta/status`),
    connectMeta: () => api.get(`${apiPath}/meta/connect`),
  };
});

export { Connections };
