import { Api } from '@/api/Api';

const Hangfire = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/admin/hangfire';
  return {
    getOrdersHistory: (range = '24h') => api.get(`${apiPath}/orders-history?range=${range}`),
  };
});

export { Hangfire };
