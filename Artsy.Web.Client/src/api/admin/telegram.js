import { Api } from '@/api/Api';

const Telegram = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api/admin/telegram';
  return {
    getWebhookInfo: () => api.get(`${apiPath}/webhook-info`),
    setWebhook: (url) => api.post(`${apiPath}/set-webhook`, { url })
  };
});

export { Telegram };
