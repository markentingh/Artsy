import { Api } from '@/api/Api';

const Subscriptions = (args) => Api({ ...args, useToken: false }).endpoints(({ api }) => {
  return {
    getActiveSubscriptions: () => api.get('/api/subscriptions'),
  };
});

export { Subscriptions };
