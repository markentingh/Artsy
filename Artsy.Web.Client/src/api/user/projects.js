import { Api } from '@/api/Api';

const Projects = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api/projects';
  return {
    getAll: () => api.get(`${apiPath}/get-all`),
    create: (project) => api.post(`${apiPath}/create`, project)
  };
});

export { Projects };
