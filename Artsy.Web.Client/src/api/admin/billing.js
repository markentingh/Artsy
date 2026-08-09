import { Api } from '@/api/Api';

const Billing = (args) => Api({ ...args, useToken: true }).endpoints(({ api }) => {
  const apiPath = '/api/admin/billing';
  return {
    getProducts: () => api.get(`${apiPath}/products`),
    saveProduct: (product) => api.post(`${apiPath}/products/save`, product),
    archiveProduct: (id) => api.post(`${apiPath}/products/archive`, { id }),
    getSubscriptions: () => api.get(`${apiPath}/subscriptions`),
    saveSubscription: (subscription) => api.post(`${apiPath}/subscriptions/save`, subscription),
    archiveSubscription: (id) => api.post(`${apiPath}/subscriptions/archive`, { id }),
    reorderSubscriptions: (ids) => api.post(`${apiPath}/subscriptions/reorder`, { ids }),
    setFeaturedSubscription: (id) => api.post(`${apiPath}/subscriptions/set-featured`, { id }),
    getUserSubscriptions: () => api.get(`${apiPath}/user-subscriptions`),
    cancelUserSubscription: (id) => api.post(`${apiPath}/user-subscriptions/cancel`, { id }),
    startUserSubscription: (request) => api.post(`${apiPath}/user-subscriptions/start`, request),
    getInvoices: () => api.get(`${apiPath}/invoices`),
  };
});

export { Billing };
