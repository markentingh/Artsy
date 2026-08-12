import { Api } from '@/api/Api';
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';

const createPrintifyScraperHubConnection = (token) => {
  const builder = new HubConnectionBuilder();
  const connection = builder
    .withUrl('/hubs/printify-scraper', {
      accessTokenFactory: () => token || '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
  return connection;
};

export { createPrintifyScraperHubConnection };
