import { Api } from '@/api/Api';

const AITokens = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/ai-tokens';
  return {
    getBalance: () => api.get(`${apiPath}/balance`),
  };
});

export { AITokens };
