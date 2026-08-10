import React, { useEffect, useState, useCallback, lazy, Suspense } from 'react';
import { useSession } from '@/context/session';
import { Connections } from '@/api/user/connections';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';
import Button from '@/components/ui/button';
import Spinner from '@/components/ui/spinner';
const ReconnectInstagramModal = lazy(() => import('@/components/modals/ReconnectInstagramModal'));

const staticServices = [
  { key: 'telegram', name: 'Telegram', color: 'bg-blue-400' },
  { key: 'printify', name: 'Printify', color: 'bg-green-500' },
];

const emptyStatus = {
  connected: false,
  viaApiToken: false,
  userId: '',
  shopNames: '',
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
    getInstagramAccounts,
    connectInstagram,
    exchangeInstagram,
    disconnectInstagram,
  } = Connections(session);

  const [statusMap, setStatusMap] = useState({
    telegram: { ...emptyStatus },
    printify: { ...emptyStatus },
  });
  const [loading, setLoading] = useState({});
  const [loadingStatus, setLoadingStatus] = useState({
    telegram: true,
    printify: true,
  });
  const [instagramAccounts, setInstagramAccounts] = useState([]);
  const [loadingInstagram, setLoadingInstagram] = useState(true);
  const [connectingInstagram, setConnectingInstagram] = useState(false);
  const [message, setMessage] = useState(null);
  const [showReconnectModal, setShowReconnectModal] = useState(false);

  const staticApiMap = {
    telegram: { getStatus: getTelegramStatus, connect: connectTelegram },
    printify: { getStatus: getPrintifyStatus, connect: connectPrintify },
  };

  const fetchStatus = async (key) => {
    const { getStatus } = staticApiMap[key];
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

  const fetchInstagramAccounts = useCallback(async () => {
    try {
      const response = await getInstagramAccounts();
      if (response.data.success) {
        setInstagramAccounts(response.data.data || []);
      }
    } catch (error) {
      setMessage({
        type: 'error',
        text: error?.response?.data?.message || 'Failed to load Instagram accounts'
      });
    } finally {
      setLoadingInstagram(false);
    }
  }, [getInstagramAccounts]);

  useEffect(() => {
    staticServices.forEach((service) => fetchStatus(service.key));
    fetchInstagramAccounts();
  }, []);

  const handleConnect = async (key) => {
    setLoading((prev) => ({ ...prev, [key]: true }));
    setMessage(null);
    try {
      const { connect } = staticApiMap[key];
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

  const handleInstagramMessage = useCallback(async (event) => {
    if (event.origin !== window.location.origin) return;
    if (!event.data || !event.data.type) return;

    if (event.data.type === 'INSTAGRAM_AUTH_SUCCESS') {
      try {
        const exchangeResp = await exchangeInstagram({ code: event.data.code, state: event.data.state });
        if (exchangeResp.data.success) {
          setLoadingInstagram(true);
          fetchInstagramAccounts();
        } else {
          setMessage({ type: 'error', text: exchangeResp.data.message || 'Instagram connection failed' });
        }
      } catch (err) {
        setMessage({ type: 'error', text: err?.response?.data?.message || 'Instagram connection failed' });
      }
    } else if (event.data.type === 'INSTAGRAM_AUTH_ERROR') {
      setMessage({ type: 'error', text: event.data.message || 'Instagram connection failed' });
    }

    setConnectingInstagram(false);
    window.removeEventListener('message', handleInstagramMessage);
  }, [fetchInstagramAccounts, exchangeInstagram]);

  const handleConnectInstagram = async () => {
    setConnectingInstagram(true);
    setMessage(null);
    try {
      const response = await connectInstagram();
      if (response.data.success && response.data.data.appId) {
        const { appId, redirectUri, state } = response.data.data;
        const scope = encodeURIComponent('instagram_business_basic,instagram_business_manage_messages,instagram_business_manage_comments,instagram_business_content_publish,instagram_business_manage_insights');
        const oauthUrl = `https://www.instagram.com/oauth/authorize?force_reauth=true&client_id=${encodeURIComponent(appId)}&redirect_uri=${encodeURIComponent(redirectUri)}&response_type=code&scope=${scope}&state=${encodeURIComponent(state)}`;
        const width = 600;
        const height = 700;
        const left = (screen.width - width) / 2;
        const top = (screen.height - height) / 2;
        window.open(
          oauthUrl,
          'InstagramBusinessLogin',
          `width=${width},height=${height},top=${top},left=${left},scrollbars=yes`
        );
        window.addEventListener('message', handleInstagramMessage);
      } else {
        setMessage({
          type: 'error',
          text: response.data.message || 'Failed to start Instagram connection'
        });
      }
    } catch (error) {
      setMessage({
        type: 'error',
        text: error?.response?.data?.message || 'Failed to start Instagram connection'
      });
    } finally {
      setConnectingInstagram(false);
    }
  };

  const handleDisconnectInstagram = async (accountId) => {
    try {
      const response = await disconnectInstagram({ id: accountId });
      if (response.data.success) {
        setInstagramAccounts((prev) => prev.filter((a) => a.id !== accountId));
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to disconnect' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to disconnect' });
    }
  };

  const renderStaticCard = (service) => {
    const status = statusMap[service.key];
    const isLoading = loading[service.key];
    const isLoadingStatus = loadingStatus[service.key];

    return (
      <div
        key={service.key}
        className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 flex flex-col items-center text-center"
      >
        <div className={`w-16 h-16 rounded-full ${service.color} flex items-center justify-center text-white text-2xl mb-4`}>
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
          </div>
        )}
        {!isLoadingStatus && !(service.key === 'printify' && status.viaApiToken) && (
          <Button className="mt-auto" onClick={() => handleConnect(service.key)} disabled={isLoading}>
            {isLoading ? 'Connecting...' : status.connected ? 'Reconnect' : 'Connect'}
          </Button>
        )}
      </div>
    );
  };

  const renderInstagramCard = (account) => (
    <div
      key={account.id}
      className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 flex flex-col items-center text-center"
    >
      <div className="w-16 h-16 rounded-full bg-pink-600 flex items-center justify-center text-white text-2xl mb-4 overflow-hidden">
        {account.profilePictureUrl ? (
          <img src={account.profilePictureUrl} alt="IG" className="w-full h-full object-cover" onError={() => setShowReconnectModal(true)} />
        ) : (
          'I'
        )}
      </div>
      <h2 className="text-xl font-semibold mb-2">Instagram</h2>
      <div className="mb-4">
        <span className="inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
          <Icon name="check" className="w-4 h-4" />
          Connected
        </span>
      </div>
      <div className="mb-4 text-sm text-gray-600 dark:text-gray-400 space-y-1">
        {account.username && <p>@{account.username}</p>}
        <p>IG ID: {account.instagramBusinessAccountId}</p>
      </div>
      <div className="flex gap-2 mt-auto">
        <Button className="cancel" onClick={() => handleDisconnectInstagram(account.id)}>
          Disconnect
        </Button>
        <Button onClick={handleConnectInstagram} disabled={connectingInstagram}>
          {connectingInstagram ? 'Connecting...' : 'Reconnect'}
        </Button>
      </div>
    </div>
  );

  const renderAddInstagramCard = () => (
    <div
      key="add-instagram"
      onClick={handleConnectInstagram}
      className="border-2 border-dashed border-gray-300 dark:border-gray-600 rounded-lg p-6 flex flex-col items-center text-center cursor-pointer hover:border-pink-500 dark:hover:border-pink-500 transition min-h-[200px] justify-center"
    >
      <div className="w-16 h-16 rounded-full border-2 border-dashed border-gray-300 dark:border-gray-600 flex items-center justify-center text-gray-400 dark:text-gray-500 mb-4">
        <Icon name="add" className="w-8 h-8" />
      </div>
      <p className="text-sm font-medium text-gray-500 dark:text-gray-400">
        Connect another Instagram Account
      </p>
    </div>
  );

  const renderInstagramPlaceholder = () => (
    <div
      key="instagram-placeholder"
      className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 flex flex-col items-center text-center"
    >
      <div className="w-16 h-16 rounded-full bg-pink-600 flex items-center justify-center text-white text-2xl mb-4">
        I
      </div>
      <h2 className="text-xl font-semibold mb-2">Instagram</h2>
      <div className="mb-4">
        {loadingInstagram ? (
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
      </div>
      {!loadingInstagram && (
        <Button className="mt-auto" onClick={handleConnectInstagram} disabled={connectingInstagram}>
          {connectingInstagram ? 'Connecting...' : 'Connect'}
        </Button>
      )}
    </div>
  );

  return (
    <div>
      <h1 className="text-3xl mb-4">Connections</h1>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div
        className="grid gap-6"
        style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(0, 20em))' }}
      >
        {staticServices.map((service) => renderStaticCard(service))}
        {loadingInstagram || instagramAccounts.length === 0
          ? renderInstagramPlaceholder()
          : instagramAccounts.map((account) => renderInstagramCard(account))}
        {!loadingInstagram && instagramAccounts.length > 0 && renderAddInstagramCard()}
      </div>
      {showReconnectModal && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ReconnectInstagramModal
            show={showReconnectModal}
            onClose={() => setShowReconnectModal(false)}
            onReconnected={() => setShowReconnectModal(false)}
          />
        </Suspense>
      )}
    </div>
  );
}
