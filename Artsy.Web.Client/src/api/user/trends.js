import { Api } from '@/api/Api';
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';

const Trends = (args) => Api({ ...args }).endpoints(({ api }) => {
  const apiPath = '/api/trends';
  return {
    getRecent: (limit = 20) => api.get(`${apiPath}/recent?limit=${limit}`),
    deleteTrend: (request) => api.post(`${apiPath}/delete`, request),
  };
});

const createTrendHubConnection = (token) => {
  const builder = new HubConnectionBuilder();
  const connection = builder
    .withUrl('/hubs/trend-research', {
      accessTokenFactory: () => token || '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
  return connection;
};

export { Trends, createTrendHubConnection };
