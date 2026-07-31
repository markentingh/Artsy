import { Api } from '@/api/Api';

const Connections = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api';
  return {
    getPrintifyStatus: () => api.get(`${apiPath}/printify/status`),
    connectPrintify: () => api.get(`${apiPath}/printify/connect`),
    getTelegramStatus: () => api.get(`${apiPath}/telegram/status`),
    connectTelegram: () => api.get(`${apiPath}/telegram/connect`),
    getInstagramAccounts: () => api.get(`${apiPath}/instagram/accounts`),
    connectInstagram: () => api.get(`${apiPath}/instagram/connect`),
    exchangeInstagram: (request) => api.post(`${apiPath}/instagram/exchange`, request),
    disconnectInstagram: (request) => api.post(`${apiPath}/instagram/disconnect`, request),
  };
});

export { Connections };
