import { Api } from '@/api/Api';

const CustomImages = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api/custom-images';
  return {
    getCustomImages: (limit = 10, offset = 0) => api.get(`${apiPath}/get-custom-images?limit=${limit}&offset=${offset}`),
    uploadCustomImage: (file) => {
      const formData = new FormData();
      formData.append('file', file);
      return api.post(`${apiPath}/upload-custom-image`, formData, { headers: { 'Content-Type': 'multipart/form-data' } });
    },
    deleteCustomImage: (request) => api.post(`${apiPath}/delete-custom-image`, request),
    getCustomImageUrl: (imageId, thumb = false) => `${apiPath}/custom-image/${imageId}${thumb ? '?thumb=true' : ''}`,
  };
});

export { CustomImages };
