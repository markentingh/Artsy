import React, { useEffect, useState, useCallback } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import { Connections } from '@/api/user/connections';
import Icon from '@/components/ui/icon';
import Checked from '@/components/ui/checked';
import Tooltip from '@/components/ui/tooltip';
import Message from '@/components/ui/message';
import Button from '@/components/ui/button';
import Select from '@/components/forms/select';

const platforms = [
  { key: 'printify', name: 'Printify', color: 'bg-green-500' },
  { key: 'instagram', name: 'Instagram', color: 'bg-pink-600' }
];

export default function PublishingSection({ projectId, project, onProjectUpdated }) {
  const session = useSession();
  const { updatePublishToPrintify, updateInstagramId, updatePostToInstagram, getInstagramAccounts } = Projects(session);
  const { getPrintifyStatus, connectPrintify, connectInstagram, exchangeInstagram } = Connections(session);

  const [connectionStatus, setConnectionStatus] = useState({});
  const [loadingStatus, setLoadingStatus] = useState({});
  const [connecting, setConnecting] = useState({});
  const [toggling, setToggling] = useState(false);
  const [message, setMessage] = useState(null);
  const [shops, setShops] = useState([]);
  const [selectedShopId, setSelectedShopId] = useState('');
  const [instagramAccounts, setInstagramAccounts] = useState([]);
  const [selectedInstagramAccountId, setSelectedInstagramAccountId] = useState('');

  const apiMap = {
    printify: { getStatus: getPrintifyStatus, connect: connectPrintify },
    instagram: { getStatus: getInstagramAccounts, connect: connectInstagram }
  };

  const fetchStatus = async (key) => {
    const { getStatus } = apiMap[key];
    setLoadingStatus((prev) => ({ ...prev, [key]: true }));
    try {
      const response = await getStatus();
      if (response.data.success) {
        const data = response.data.data;
        if (key === 'instagram') {
          const accounts = Array.isArray(data) ? data : [];
          setInstagramAccounts(accounts);
          setConnectionStatus((prev) => ({
            ...prev,
            [key]: { connected: accounts.length > 0 }
          }));
        } else {
          setConnectionStatus((prev) => ({
            ...prev,
            [key]: {
              connected: data.connected,
              viaApiToken: data.viaApiToken || false
            }
          }));
          if (data.shops && data.shops.length > 0) {
            setShops(data.shops);
          }
        }
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
    platforms.forEach((p) => fetchStatus(p.key));
  }, []);

  useEffect(() => {
    if (project?.printifyStoreId != null) {
      setSelectedShopId(String(project.printifyStoreId));
    }
  }, [project?.printifyStoreId]);

  useEffect(() => {
    if (project?.instagramId != null) {
      setSelectedInstagramAccountId(String(project.instagramId));
    }
  }, [project?.instagramId]);

  const handleInstagramMessage = useCallback(async (event) => {
    if (event.origin !== window.location.origin) return;
    if (!event.data || !event.data.type) return;

    if (event.data.type === 'INSTAGRAM_AUTH_SUCCESS') {
      try {
        const exchangeResp = await exchangeInstagram({ code: event.data.code, state: event.data.state });
        if (exchangeResp.data.success) {
          fetchStatus('instagram');
        } else {
          setMessage({ type: 'error', text: exchangeResp.data.message || 'Instagram connection failed' });
        }
      } catch (err) {
        setMessage({ type: 'error', text: err?.response?.data?.message || 'Instagram connection failed' });
      }
    } else if (event.data.type === 'INSTAGRAM_AUTH_ERROR') {
      setMessage({ type: 'error', text: event.data.message || 'Instagram connection failed' });
    }

    setConnecting((prev) => ({ ...prev, instagram: false }));
    window.removeEventListener('message', handleInstagramMessage);
  }, [exchangeInstagram]);

  const handleConnect = async (key) => {
    setConnecting((prev) => ({ ...prev, [key]: true }));
    setMessage(null);
    try {
      const { connect } = apiMap[key];
      const response = await connect();
      if (response.data.success && response.data.data.viaApiToken) {
        await fetchStatus(key);
      } else if (response.data.success && (response.data.data.url || response.data.data.appId)) {
        if (key === 'instagram') {
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
          window.location.href = response.data.data.url;
        }
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
      setConnecting((prev) => ({ ...prev, [key]: false }));
    }
  };

  const handleShopChange = async (value) => {
    setSelectedShopId(value);
    const storeId = value ? parseInt(value, 10) : null;
    try {
      const response = await updatePublishToPrintify({
        id: projectId,
        publishToPrintify: project?.publishToPrintify ?? true,
        printifyStoreId: storeId
      });
      if (response.data.success) {
        if (onProjectUpdated) onProjectUpdated(response.data.data);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to update store' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update store' });
    }
  };

  const handleInstagramAccountChange = async (value) => {
    setSelectedInstagramAccountId(value);
    const instagramId = value ? value : null;
    try {
      const response = await updateInstagramId({
        id: projectId,
        instagramId: instagramId
      });
      if (response.data.success) {
        if (onProjectUpdated) onProjectUpdated(response.data.data);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to update Instagram account' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update Instagram account' });
    }
  };

  const handleToggle = async (key) => {
    const status = connectionStatus[key];
    if (!status?.connected) return;

    setToggling(true);
    try {
      if (key === 'instagram') {
        const newValue = !project?.postToInstagram;
        const response = await updatePostToInstagram({ id: projectId, postToInstagram: newValue });
        if (response.data.success) {
          if (onProjectUpdated) onProjectUpdated(response.data.data);
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to update publishing setting' });
        }
      } else {
        const newValue = !project?.publishToPrintify;
        const response = await updatePublishToPrintify({ id: projectId, publishToPrintify: newValue });
        if (response.data.success) {
          if (onProjectUpdated) onProjectUpdated(response.data.data);
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to update publishing setting' });
        }
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update publishing setting' });
    } finally {
      setToggling(false);
    }
  };

  const renderPlatformCard = (platform) => {
    const status = connectionStatus[platform.key];
    const isLoadingStatus = loadingStatus[platform.key];
    const isConnecting = connecting[platform.key];
    const isConnected = status?.connected;

    if (platform.key === 'instagram') {
      const isInstagramChecked = isConnected && project?.postToInstagram;
      return (
        <div
          key={platform.key}
          className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 flex flex-col items-center text-center"
        >
          <div className="w-16 h-16 rounded-full bg-pink-600 flex items-center justify-center text-white text-2xl mb-4 overflow-hidden">
            {(() => {
              const selectedAcc = instagramAccounts.find(a => a.id === selectedInstagramAccountId);
              if (selectedAcc?.profilePictureUrl)
                return <img src={selectedAcc.profilePictureUrl} alt={selectedAcc.username || 'IG'} className="w-full h-full object-cover" />;
              return platform.name[0];
            })()}
          </div>
          <h3 className="text-lg font-semibold mb-4">{platform.name}</h3>
          {isLoadingStatus ? (
            <span className="inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300">
              <Icon name="progress_activity" spin className="w-4 h-4" />
              Loading...
            </span>
          ) : !isConnected ? (
            <Button className="mt-auto" onClick={() => handleConnect(platform.key)} disabled={isConnecting}>
              {isConnecting ? 'Connecting...' : 'Connect'}
            </Button>
          ) : (
            <>
              {instagramAccounts.length > 0 && (
                <div className="w-full mb-3">
                  <Select
                    name="instagramAccount"
                    value={selectedInstagramAccountId}
                    onChange={(e) => handleInstagramAccountChange(e.target.value)}
                    placeholder="[Select Account]"
                    options={instagramAccounts.map((acc) => ({
                      value: acc.id,
                      label: acc.username ? `@${acc.username}` : acc.instagramBusinessAccountId
                    }))}
                    className="w-full"
                  />
                </div>
              )}
              <p
                className={`text-sm mb-3 px-3 py-1.5 rounded ${isInstagramChecked ? 'text-gray-600 dark:text-gray-400' : 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200'}`}
              >
                {isInstagramChecked ? 'Will publish Collections to Instagram' : 'Will not publish Collections to Instagram'}
              </p>
              <button
                type="button"
                onClick={() => handleToggle(platform.key)}
                disabled={toggling}
                className="mt-auto cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                title={isInstagramChecked ? 'Click to uncheck' : 'Click to check'}
              >
                <Checked checked={isInstagramChecked} />
              </button>
            </>
          )}
        </div>
      );
    }

    const isChecked = isConnected && project?.publishToPrintify;

    return (
      <div
        key={platform.key}
        className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 flex flex-col items-center text-center"
      >
        <div className={`w-16 h-16 rounded-full ${platform.color} flex items-center justify-center text-white text-2xl mb-4`}>
          {platform.name[0]}
        </div>
        <h3 className="text-lg font-semibold mb-4">{platform.name}</h3>
        {isLoadingStatus ? (
          <span className="inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300">
            <Icon name="progress_activity" spin className="w-4 h-4" />
            Loading...
          </span>
        ) : !isConnected ? (
          <Button className="mt-auto" onClick={() => handleConnect(platform.key)} disabled={isConnecting}>
            {isConnecting ? 'Connecting...' : 'Connect'}
          </Button>
        ) : (
          <>
            {platform.key === 'printify' && shops.length > 0 && (
              <div className="w-full mb-3">
                <Select
                  name="printifyStore"
                  value={selectedShopId}
                  onChange={(e) => handleShopChange(e.target.value)}
                  placeholder="[Select Shop]"
                  options={shops.map((shop) => ({ value: String(shop.id), label: shop.title }))}
                  className="w-full"
                />
              </div>
            )}
            <p
              className={`text-sm mb-3 px-3 py-1.5 rounded ${isChecked ? 'text-gray-600 dark:text-gray-400' : 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200'}`}
            >
              {isChecked ? 'Will publish Collections to Printify' : 'Will not publish Collections to Printify'}
            </p>
            <button
              type="button"
              onClick={() => handleToggle(platform.key)}
              disabled={toggling}
              className="mt-auto cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              title={isChecked ? 'Click to uncheck' : 'Click to check'}
            >
              <Checked checked={isChecked} />
            </button>
          </>
        )}
      </div>
    );
  };

  return (
    <div className="mb-8">
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div className="flex items-center gap-1 mb-4">
        <h2 className="text-xl font-semibold">Publishing</h2>
        <Tooltip text="Connect your print-on-demand platforms to publish your collections as real products. Once connected, toggle publishing to automatically send your collection artwork to the platform for listing and sale." />
      </div>
      <div
        className="grid gap-6"
        style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(0, 20em))' }}
      >
        {platforms.map((platform) => renderPlatformCard(platform))}
      </div>
    </div>
  );
}
