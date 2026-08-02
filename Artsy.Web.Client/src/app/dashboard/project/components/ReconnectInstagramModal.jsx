import React, { useState, useCallback, useRef, useEffect } from 'react';
import { useSession } from '@/context/session';
import { Connections } from '@/api/user/connections';
import ButtonOutline from '@/components/ui/button-outline';
import Button from '@/components/ui/button';
import Icon from '@/components/ui/icon';

export default function ReconnectInstagramModal({ show, onClose, onReconnected }) {
  const session = useSession();
  const { connectInstagram, exchangeInstagram } = Connections(session);
  const [reconnecting, setReconnecting] = useState(false);
  const onReconnectedRef = useRef(onReconnected);

  useEffect(() => {
    onReconnectedRef.current = onReconnected;
  }, [onReconnected]);

  const handleInstagramMessage = useCallback(async (event) => {
    if (event.origin !== window.location.origin) return;
    if (!event.data || !event.data.type) return;

    window.removeEventListener('message', handleInstagramMessage);

    if (event.data.type === 'INSTAGRAM_AUTH_SUCCESS') {
      try {
        const exchangeResp = await exchangeInstagram({ code: event.data.code, state: event.data.state });
        if (exchangeResp.data.success) {
          if (onReconnectedRef.current) onReconnectedRef.current();
        } else {
          setReconnecting(false);
        }
      } catch {
        setReconnecting(false);
      }
    } else if (event.data.type === 'INSTAGRAM_AUTH_ERROR') {
      setReconnecting(false);
    }
  }, [exchangeInstagram]);

  useEffect(() => {
    return () => {
      window.removeEventListener('message', handleInstagramMessage);
    };
  }, [handleInstagramMessage]);

  const handleReconnect = async () => {
    setReconnecting(true);
    try {
      const response = await connectInstagram();
      if (response.data.success && (response.data.data.appId || response.data.data.url)) {
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
        setReconnecting(false);
      }
    } catch {
      setReconnecting(false);
    }
  };

  if (!show) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl p-6 max-w-md w-full mx-4">
        <div className="flex items-center gap-2 mb-4">
          <Icon name="warning" className="w-6 h-6 text-yellow-500" />
          <h3 className="text-lg font-semibold">Instagram Reconnection Required</h3>
        </div>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
          Your Instagram access token has expired. Please reconnect your Instagram account to continue posting.
        </p>
        <div className="buttons flex justify-end gap-2">
          <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
          <Button onClick={handleReconnect} disabled={reconnecting}>
            {reconnecting ? (
              <span className="inline-flex items-center">
                <Icon name="progress_activity" spin className="w-4 h-4 mr-1" />
                Connecting...
              </span>
            ) : 'Reconnect'}
          </Button>
        </div>
      </div>
    </div>
  );
}
