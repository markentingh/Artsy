import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Connections } from '@/api/user/connections';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';

const services = [
  { key: 'telegram', name: 'Telegram', color: 'bg-blue-400' },
  { key: 'printify', name: 'Printify', color: 'bg-green-500' },
  { key: 'meta', name: 'Meta', color: 'bg-blue-600' }
];

const emptyStatus = {
  connected: false,
  viaApiToken: false,
  userId: '',
  shopNames: '',
  instagramBusinessAccountId: '',
  telegramUserId: '',
  telegramChatId: ''
};

export default function DashboardConnections() {
  const session = useSession();
  const {
    getPrintifyStatus,
    connectPrintify,
    getTelegramStatus,
    connectTelegram,
    getMetaStatus,
    connectMeta
  } = Connections(session);

  const [statusMap, setStatusMap] = useState({
    telegram: { ...emptyStatus },
    printify: { ...emptyStatus },
    meta: { ...emptyStatus }
  });
  const [loading, setLoading] = useState({});
  const [loadingStatus, setLoadingStatus] = useState({
    telegram: true,
    printify: true,
    meta: true
  });
  const [message, setMessage] = useState(null);

  const apiMap = {
    telegram: { getStatus: getTelegramStatus, connect: connectTelegram },
    printify: { getStatus: getPrintifyStatus, connect: connectPrintify },
    meta: { getStatus: getMetaStatus, connect: connectMeta }
  };

  const fetchStatus = async (key) => {
    const { getStatus } = apiMap[key];
    try {
      const response = await getStatus();
      if (response.data.success) {
        const shops = response.data.data.shops || [];
        const shopNames = shops.map((shop) => shop.title).join(', ');

        setStatusMap((prev) => ({
          ...prev,
          [key]: {
            connected: response.data.data.connected,
            viaApiToken: response.data.data.viaApiToken || false,
            userId: response.data.data.userId || '',
            shopNames,
            instagramBusinessAccountId: response.data.data.instagramBusinessAccountId || '',
            telegramUserId: response.data.data.telegramUserId || '',
            telegramChatId: response.data.data.telegramChatId || ''
          }
        }));
      }
    } catch (error) {
      setMessage({
        type: 'error',
        text: error?.response?.data?.message || `Failed to load ${key} connection status`
      });
    } finally {
      setLoadingStatus((prev) => ({ ...prev, [key]: false }));
    }
  };

  useEffect(() => {
    services.forEach((service) => fetchStatus(service.key));
  }, []);

  const handleConnect = async (key) => {
    setLoading((prev) => ({ ...prev, [key]: true }));
    setMessage(null);
    try {
      const { connect } = apiMap[key];
      const response = await connect();
      if (response.data.success && response.data.data.viaApiToken) {
        await fetchStatus(key);
      } else if (response.data.success && response.data.data.botUsername && response.data.data.token) {
        const { botUsername, token } = response.data.data;
        window.location.href = `tg://resolve?domain=${encodeURIComponent(botUsername)}&start=${encodeURIComponent(token)}`;
      } else if (response.data.success && response.data.data.url) {
        window.location.href = response.data.data.url;
      } else {
        setMessage({
          type: 'error',
          text: response.data.message || `Failed to start ${key} connection`
        });
      }
    } catch (error) {
      setMessage({
        type: 'error',
        text: error?.response?.data?.message || `Failed to start ${key} connection`
      });
    } finally {
      setLoading((prev) => ({ ...prev, [key]: false }));
    }
  };

  const renderCard = (service) => {
    const status = statusMap[service.key];
    const isLoading = loading[service.key];
    const isLoadingStatus = loadingStatus[service.key];

    return (
      <div
        key={service.key}
        className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 flex flex-col items-center text-center"
      >
        <div className={`w-16 h-16 rounded-full ${service.color} flex items-center justify-center text-white text-2xl font-bold mb-4`}>
          {service.name[0]}
        </div>
        <h2 className="text-xl font-semibold mb-2">{service.name}</h2>
        <div className="mb-4">
          {status.connected ? (
            <span className="inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
              <Icon name="check" className="w-4 h-4" />
              Connected
            </span>
          ) : isLoadingStatus ? (
            <span className="inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300">
              <Icon name="progress_activity" spin className="w-4 h-4" />
              Loading...
            </span>
          ) : (
            <span className="inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300">
              <Icon name="close" className="w-4 h-4" />
              Not connected
            </span>
          )}
          {!status.connected && !isLoadingStatus && service.key === 'telegram' && (
            <p className="pt-4 text-xs text-amber-600 dark:text-amber-400">
              Make sure Telegram is installed before connecting.
            </p>
          )}
        </div>
        {status.connected && (
          <div className="mb-4 text-sm text-gray-600 dark:text-gray-400 space-y-1">
            {status.userId && <p>User ID: {status.userId}</p>}
            {status.shopNames && <p>Shops: {status.shopNames}</p>}
            {status.instagramBusinessAccountId && <p>IG Business ID: {status.instagramBusinessAccountId}</p>}
          </div>
        )}
        {!isLoadingStatus && !(service.key === 'printify' && status.viaApiToken) && (
          <button
            type="button"
            onClick={() => handleConnect(service.key)}
            disabled={isLoading}
            className="mt-auto px-4 py-2 bg-primary-600 text-white rounded hover:bg-primary-700 transition disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isLoading ? 'Connecting...' : status.connected ? 'Reconnect' : 'Connect'}
          </button>
        )}
      </div>
    );
  };

  return (
    <div>
      <h1 className="text-3xl font-bold mb-4">Connections</h1>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 max-w-4xl">
        {renderCard(services[0])}
        {renderCard(services[1])}
        {renderCard(services[2])}
      </div>
    </div>
  );
}
