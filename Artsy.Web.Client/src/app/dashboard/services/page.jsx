import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Telegram } from '@/api/admin/telegram';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';

export default function DashboardServices() {
  const session = useSession();
  const { getWebhookInfo, setWebhook } = Telegram(session);

  const [webhookUrl, setWebhookUrl] = useState('');
  const [maxConnections, setMaxConnections] = useState(0);
  const [editUrl, setEditUrl] = useState('');
  const [isEditing, setIsEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);

  const fetchWebhookInfo = async () => {
    try {
      const response = await getWebhookInfo();
      if (response.data.success) {
        const url = response.data.data.url;
        setWebhookUrl(url);
        setEditUrl(url);
        setMaxConnections(response.data.data.maxConnections);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to load webhook info' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load webhook info' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWebhookInfo();
  }, []);

  const handleEdit = () => {
    setEditUrl(webhookUrl);
    setIsEditing(true);
  };

  const handleCancel = () => {
    setEditUrl(webhookUrl);
    setIsEditing(false);
  };

  const normalizeWebhookUrl = (url) => {
    const path = 'api/webhooks/telegram';
    let trimmed = url.trim();
    if (trimmed.endsWith('/')) {
      trimmed = trimmed.slice(0, -1);
    }
    if (trimmed.toLowerCase().endsWith(path)) {
      return trimmed;
    }
    return trimmed + '/' + path;
  };

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const normalizedUrl = normalizeWebhookUrl(editUrl);
      const response = await setWebhook(normalizedUrl);
      if (response.data.success) {
        setWebhookUrl(normalizedUrl);
        setEditUrl(normalizedUrl);
        setIsEditing(false);
        setMessage({ type: 'success', text: 'Webhook URL updated successfully.' });
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to update webhook URL.' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update webhook URL.' });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <h1 className="text-3xl font-bold mb-4">Services</h1>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 mb-6">
        <h2 className="text-xl font-semibold mb-4">Telegram</h2>

        {loading ? (
          <div className="inline-flex items-center gap-2 text-gray-600 dark:text-gray-400">
            <Icon name="progress_activity" spin className="w-5 h-5" />
            Loading webhook info...
          </div>
        ) : (
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Webhook URL
              </label>
              <div className="flex items-center gap-2">
                {isEditing ? (
                  <input
                    type="text"
                    value={editUrl}
                    onChange={(e) => setEditUrl(e.target.value)}
                    className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
                    placeholder="https://your-domain.com/api/webhooks/telegram"
                  />
                ) : (
                  <span className="flex-1 px-3 py-2 border border-gray-200 dark:border-gray-700 rounded bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-gray-100">
                    {webhookUrl || 'Not configured'}
                  </span>
                )}

                {isEditing ? (
                  <>
                    <button
                      type="button"
                      onClick={handleSave}
                      disabled={saving}
                      className="px-4 py-2 bg-primary-600 text-white rounded hover:bg-primary-700 transition disabled:opacity-50"
                    >
                      {saving ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      type="button"
                      onClick={handleCancel}
                      className="px-4 py-2 bg-gray-200 text-gray-800 rounded hover:bg-gray-300 transition"
                    >
                      Cancel
                    </button>
                  </>
                ) : (
                  <button
                    type="button"
                    onClick={handleEdit}
                    className="icon"
                    title="Edit webhook URL"
                  >
                    <Icon name="edit" className="w-5 h-5" />
                  </button>
                )}
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Max Connections
              </label>
              <span className="inline-block px-3 py-2 border border-gray-200 dark:border-gray-700 rounded bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-gray-100">
                {maxConnections}
              </span>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
