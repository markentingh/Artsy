import { Api } from '@/api/Api';

const Instagram = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api/instagram';
  return {
    getAccounts: () => api.get(`${apiPath}/accounts`),
    postToSocialMedia: (request) => api.post(`${apiPath}/post-to-social-media`, request),
    checkPosted: (collectionId) => api.get(`${apiPath}/collection-posted?collectionId=${collectionId}`),
    getPost: (collectionId) => api.get(`${apiPath}/collection-post?collectionId=${collectionId}`),
  };
});

export { Instagram };
