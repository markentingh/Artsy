import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Telegram } from '@/api/admin/telegram';
import { Printify } from '@/api/admin/printify';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';
import Button from '@/components/ui/button';

export default function DashboardServices() {
  const session = useSession();
  const { getWebhookInfo, setWebhook } = Telegram(session);
  const { getCatalogCount, refreshCatalog, fetchPrintProviders, fetchVariants, fetchShipping, downloadCatalogImage } = Printify(session);

  const [webhookUrl, setWebhookUrl] = useState('');
  const [maxConnections, setMaxConnections] = useState(0);
  const [editUrl, setEditUrl] = useState('');
  const [isEditing, setIsEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);

  const [catalogCount, setCatalogCount] = useState(0);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [progress, setProgress] = useState(null);
  const [allVariants, setAllVariants] = useState(false);
  const [productImages, setProductImages] = useState(false);

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
    fetchCatalogCount();
  }, []);

  const fetchCatalogCount = async () => {
    try {
      const response = await getCatalogCount();
      if (response.data.success) {
        setCatalogCount(response.data.data.count);
      }
    } catch (error) {
      // Ignore load errors
    } finally {
      setCatalogLoading(false);
    }
  };

  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

  const handleRefreshCatalog = async () => {
    setRefreshing(true);
    setMessage(null);
    setProgress(null);
    try {
      const response = await refreshCatalog(allVariants);
      if (!response.data.success) {
        setMessage({ type: 'error', text: response.data.message || 'Failed to refresh catalog' });
        return;
      }

      const { count, newBlueprints: newBps, existingBlueprints: existingBps, images: imgList } = response.data.data;
      setCatalogCount(count);

      const newBpList = newBps || [];
      const existingBpList = existingBps || [];
      const allBps = [...newBpList, ...existingBpList];
      const imgs = imgList || [];
      let providersDone = 0;
      let variantsDone = 0;
      let shippingDone = 0;
      let imagesDownloaded = 0;
      let imagesSkipped = 0;

      const updateProgress = (phase, detail) => {
        setProgress({
          phase,
          detail,
          blueprints: { done: providersDone, total: allBps.length },
          variants: { done: variantsDone },
          shipping: { done: shippingDone },
          images: { downloaded: imagesDownloaded, skipped: imagesSkipped, total: imgs.length },
        });
      };

      const processFullBlueprint = async (blueprintId, index, total) => {
        updateProgress('providers', `Blueprint ${index + 1}/${total}`);

        let providers = [];
        try {
          const ppResp = await fetchPrintProviders(blueprintId);
          if (ppResp.data.success) {
            providers = ppResp.data.data.printProviders || [];
            providersDone++;
          }
        } catch {}
        await sleep(500);

        for (let j = 0; j < providers.length; j++) {
          const { printProviderId } = providers[j];
          updateProgress('variants', `Blueprint ${index + 1}/${total}, Provider ${j + 1}/${providers.length}`);
          try {
            await fetchVariants(blueprintId, printProviderId);
            variantsDone++;
          } catch {}
          await sleep(500);

          updateProgress('shipping', `Blueprint ${index + 1}/${total}, Provider ${j + 1}/${providers.length}`);
          try {
            await fetchShipping(blueprintId, printProviderId);
            shippingDone++;
          } catch {}
          await sleep(500);
        }
      };

      const processVariantsOnly = async (blueprintId, index, total) => {
        updateProgress('providers', `Blueprint ${index + 1}/${total} (variants only)`);

        let providers = [];
        try {
          const ppResp = await fetchPrintProviders(blueprintId);
          if (ppResp.data.success) {
            providers = ppResp.data.data.printProviders || [];
            providersDone++;
          }
        } catch {}
        await sleep(500);

        for (let j = 0; j < providers.length; j++) {
          const { printProviderId } = providers[j];
          updateProgress('variants', `Blueprint ${index + 1}/${total}, Provider ${j + 1}/${providers.length}`);
          try {
            await fetchVariants(blueprintId, printProviderId);
            variantsDone++;
          } catch {}
          await sleep(500);
        }
      };

      for (let i = 0; i < newBpList.length; i++) {
        await processFullBlueprint(newBpList[i], i, allBps.length);
      }

      for (let i = 0; i < existingBpList.length; i++) {
        await processVariantsOnly(existingBpList[i], newBpList.length + i, allBps.length);
      }

      if (productImages) {
        for (let i = 0; i < imgs.length; i++) {
          const img = imgs[i];
          updateProgress('images', `Image ${i + 1}/${imgs.length}`);
          try {
            const dlResp = await downloadCatalogImage(img.blueprintId, img.index, img.url);
            if (dlResp.data.success) {
              if (dlResp.data.data.downloaded) imagesDownloaded++;
              else imagesSkipped++;
            }
          } catch {}
        }
      }

      updateProgress('done', 'Complete!');
      setProgress((prev) => ({ ...prev, done: true }));
      setMessage({
        type: 'success',
        text: `Catalog refreshed. ${count} blueprints, ${providersDone} provider sets, ${variantsDone} variant sets, ${shippingDone} shipping records, ${imagesDownloaded} images downloaded (${imagesSkipped} already existed).`,
      });
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to refresh catalog' });
    } finally {
      setRefreshing(false);
    }
  };

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
      <h1 className="text-3xl mb-4">Services</h1>
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
                    <Button onClick={handleSave} disabled={saving}>
                      {saving ? 'Saving...' : 'Save'}
                    </Button>
                    <Button color="gray" className="cancel" onClick={handleCancel}>
                      Cancel
                    </Button>
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

      <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 mb-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-xl font-semibold">Printify</h2>
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2">
              <label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                Cached Blueprints
              </label>
              {catalogLoading ? (
                <Icon name="progress_activity" spin className="w-5 h-5 text-gray-600 dark:text-gray-400" />
              ) : (
                <span className="inline-block px-3 py-1 border border-gray-200 dark:border-gray-700 rounded bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-gray-100 text-sm">
                  {catalogCount}
                </span>
              )}
            </div>
            <label className="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300 cursor-pointer">
              <input
                type="checkbox"
                checked={allVariants}
                onChange={(e) => setAllVariants(e.target.checked)}
                disabled={refreshing}
                className="w-4 h-4 text-primary-600 border-gray-300 rounded focus:ring-primary-500"
              />
              All Product Variants
            </label>
            <label className="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300 cursor-pointer">
              <input
                type="checkbox"
                checked={productImages}
                onChange={(e) => setProductImages(e.target.checked)}
                disabled={refreshing}
                className="w-4 h-4 text-primary-600 border-gray-300 rounded focus:ring-primary-500"
              />
              Product Images
            </label>
            <Button onClick={handleRefreshCatalog} disabled={refreshing}>
              {refreshing ? (
                <span className="inline-flex items-center gap-2">
                  <Icon name="progress_activity" spin className="w-4 h-4" />
                  Refreshing...
                </span>
              ) : (
                'Refresh Catalog'
              )}
            </Button>
          </div>
        </div>

        <div className="space-y-4">

          {progress && (
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs text-gray-600 dark:text-gray-400">
                <span>
                  {progress.phase === 'providers' && `Fetching print providers... ${progress.detail}`}
                  {progress.phase === 'variants' && `Fetching variants... ${progress.detail}`}
                  {progress.phase === 'shipping' && `Fetching shipping... ${progress.detail}`}
                  {progress.phase === 'images' && `Downloading images... ${progress.images.downloaded + progress.images.skipped}/${progress.images.total}`}
                  {progress.phase === 'done' && 'Complete!'}
                </span>
              </div>
              <div className="w-full h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                <div
                  className="h-full bg-primary-600 rounded-full transition-all duration-200"
                  style={{
                    width: `${
                      progress.phase === 'done' ? 100 :
                      progress.phase === 'images'
                        ? Math.round(((progress.images.downloaded + progress.images.skipped) / Math.max(progress.images.total, 1)) * 100)
                        : Math.round((progress.blueprints.done / Math.max(progress.blueprints.total, 1)) * 100)
                    }%`,
                  }}
                />
              </div>
              {progress.done && (
                <div className="text-xs text-gray-500 dark:text-gray-400 space-y-0.5">
                  <div>Blueprints: {progress.blueprints.done}/{progress.blueprints.total}</div>
                  <div>Variants: {progress.variants.done} sets</div>
                  <div>Shipping: {progress.shipping.done} records</div>
                  <div>Images: {progress.images.downloaded} downloaded, {progress.images.skipped} skipped</div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
