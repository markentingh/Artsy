import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import { Connections } from '@/api/user/connections';
import Icon from '@/components/ui/icon';
import Checked from '@/components/ui/checked';
import Message from '@/components/ui/message';

const platforms = [
  { key: 'printify', name: 'Printify', color: 'bg-green-500' }
];

export default function PublishingSection({ projectId, project, onProjectUpdated }) {
  const session = useSession();
  const { updatePublishToPrintify } = Projects(session);
  const { getPrintifyStatus, connectPrintify } = Connections(session);

  const [connectionStatus, setConnectionStatus] = useState({});
  const [loadingStatus, setLoadingStatus] = useState({});
  const [connecting, setConnecting] = useState({});
  const [toggling, setToggling] = useState(false);
  const [message, setMessage] = useState(null);

  const apiMap = {
    printify: { getStatus: getPrintifyStatus, connect: connectPrintify }
  };

  const fetchStatus = async (key) => {
    const { getStatus } = apiMap[key];
    setLoadingStatus((prev) => ({ ...prev, [key]: true }));
    try {
      const response = await getStatus();
      if (response.data.success) {
        setConnectionStatus((prev) => ({
          ...prev,
          [key]: {
            connected: response.data.data.connected,
            viaApiToken: response.data.data.viaApiToken || false
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
    platforms.forEach((p) => fetchStatus(p.key));
  }, []);

  const handleConnect = async (key) => {
    setConnecting((prev) => ({ ...prev, [key]: true }));
    setMessage(null);
    try {
      const { connect } = apiMap[key];
      const response = await connect();
      if (response.data.success && response.data.data.viaApiToken) {
        await fetchStatus(key);
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
      setConnecting((prev) => ({ ...prev, [key]: false }));
    }
  };

  const handleToggle = async (key) => {
    const status = connectionStatus[key];
    if (!status?.connected) return;

    const newValue = !project?.publishToPrintify;
    setToggling(true);
    try {
      const response = await updatePublishToPrintify({ id: projectId, publishToPrintify: newValue });
      if (response.data.success) {
        if (onProjectUpdated) onProjectUpdated(response.data.data);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to update publishing setting' });
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
          <button
            type="button"
            onClick={() => handleConnect(platform.key)}
            disabled={isConnecting}
            className="mt-auto px-4 py-2 bg-primary-600 text-white rounded hover:bg-primary-700 transition disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isConnecting ? 'Connecting...' : 'Connect'}
          </button>
        ) : (
          <>
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
      <h2 className="text-xl font-semibold mb-4">Publishing</h2>
      <div
        className="grid gap-6"
        style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(0, 20em))' }}
      >
        {platforms.map((platform) => renderPlatformCard(platform))}
      </div>
    </div>
  );
}
