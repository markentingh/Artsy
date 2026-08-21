import React, { useEffect, useState, useRef, useCallback, useMemo } from 'react';
import { useSession } from '@/context/session';
import { Telegram } from '@/api/admin/telegram';
import { Printify } from '@/api/admin/printify';
import { PrintifyImageMatch } from '@/api/admin/printifyImageMatch';
import { createPrintifyScraperHubConnection } from '@/api/admin/printifyScraper';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';
import Button from '@/components/ui/button';
import ButtonOutline from '@/components/ui/button-outline';
import List, { Item } from '@/components/ui/list';
import { Accordion } from '@/components/ui/accordion';
import Tooltip from '@/components/ui/tooltip';
import CarouselElements from '@/components/ui/carousel-elements';
import ProductImagePreview from '@/app/dashboard/project/components/ProductImagePreview';
import ConfigurePrintifyBlueprint from '@/app/dashboard/printify/components/ConfigurePrintifyBlueprint';
import PrintifyColorsWizard from '@/app/dashboard/printify/components/PrintifyColorsWizard';
import { TYPE_OPTIONS } from '@/context/printifyBlueprint';

function groupColors(colors) {
  const map = new Map();
  for (const c of colors) {
    if (!map.has(c.name)) map.set(c.name, new Set());
    if (c.hex) map.get(c.name).add(c.hex);
  }
  return Array.from(map.entries())
    .map(([name, hexes]) => ({ name, hexes: Array.from(hexes) }))
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' }));
}

function slugify(text) {
  if (!text) return '';
  return text
    .toLowerCase()
    .replace(/&/g, 'and')
    .replace(/\+/g, 'plus')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

export default function DashboardServices() {
  const session = useSession();
  const { getWebhookInfo, setWebhook } = Telegram(session);
  const { getCatalogCount, refreshCatalog, fetchPrintProviders, fetchVariants, fetchShipping, downloadCatalogImage, downloadBlueprintImages, convertVariants, convertImageVariants, getVariantOptionKeys, loadVariantOptions, getBlueprintImages, getBlueprintImageUrl } = Printify(session);
  const { getUnpublishedBlueprints, getBlueprintImages: getMatchBlueprintImages, applyVariants, publishBlueprint } = PrintifyImageMatch(session);

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
  const [catalogAction, setCatalogAction] = useState('refresh');
  const [convertProgress, setConvertProgress] = useState(null);
  const [variantOptionKeys, setVariantOptionKeys] = useState(null);
  const [maxOptionKeys, setMaxOptionKeys] = useState(null);

  // Printify Scraper state
  const scraperHubRef = useRef(null);
  const [scraperRunning, setScraperRunning] = useState(false);
  const [scraperStatus, setScraperStatus] = useState('');
  const [scraperProgress, setScraperProgress] = useState(null);
  const [scraperPanel, setScraperPanel] = useState(null);
  const [scraperError, setScraperError] = useState(null);
  const [selectedColors, setSelectedColors] = useState({});
  const [selectedType, setSelectedType] = useState(String(0));
  const [selectedPosition, setSelectedPosition] = useState('1');
  const [blueprintImageVariantMap, setBlueprintImageVariantMap] = useState({});
  const [configureBlueprintId, setConfigureBlueprintId] = useState(null);
  const [showPreview, setShowPreview] = useState(false);
  const [previewIndex, setPreviewIndex] = useState(0);

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

  useEffect(() => {
    if (!scraperPanel?.blueprintId) {
      setBlueprintImageVariantMap({});
      return;
    }
    getBlueprintImages(scraperPanel.blueprintId).then((resp) => {
      if (resp.data?.success) {
        const map = {};
        (resp.data.data || []).forEach((img) => {
          map[img.imageIndex] = img.variantColors || [];
        });
        setBlueprintImageVariantMap(map);
      }
    });
  }, [scraperPanel?.blueprintId]);

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

    if (catalogAction === 'convertVariants') {
      try {
        setConvertProgress({ current: 0, total: 0 });
        // 1. Get the list of (blueprintId, printProviderId) pairs with empty Color
        const resp = await convertVariants();
        if (!resp.data.success) {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to get variant pairs' });
          setRefreshing(false);
          setConvertProgress(null);
          return;
        }

        const pairs = resp.data.data.pairs || [];
        const total = pairs.length;
        setConvertProgress({ current: 0, total });

        if (total === 0) {
          setMessage({ type: 'success', text: 'No variants with empty colors found.' });
          setRefreshing(false);
          setConvertProgress(null);
          return;
        }

        // 2. Loop through pairs and call fetchVariants for each
        let completed = 0;
        let errors = 0;
        for (const pair of pairs) {
          try {
            await fetchVariants(pair.blueprintId, pair.printProviderId);
          } catch {
            errors++;
          }
          completed++;
          setConvertProgress({ current: completed, total });
        }

        setMessage({ type: 'success', text: `Fetched variants from ${completed}/${total} providers.${errors > 0 ? ` (${errors} errors)` : ''}` });
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to convert variants' });
      } finally {
        setRefreshing(false);
        setConvertProgress(null);
      }
      return;
    }

    if (catalogAction === 'convertImageVariants') {
      try {
        const resp = await convertImageVariants();
        if (resp.data.success)
          setMessage({ type: 'success', text: `Converted ${resp.data.data.inserted} image variants.` });
        else
          setMessage({ type: 'error', text: resp.data.message || 'Failed to convert image variants' });
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to convert image variants' });
      } finally {
        setRefreshing(false);
      }
      return;
    }

    if (catalogAction === 'getVariantOptions') {
      try {
        const resp = await getVariantOptionKeys();
        if (resp.data.success) {
          setVariantOptionKeys(resp.data.data.keys || []);
          setMaxOptionKeys(resp.data.data.maxKeys ?? null);
          setMessage({ type: 'success', text: `Found ${(resp.data.data.keys || []).length} distinct option keys.` });
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to get variant option keys' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to get variant option keys' });
      } finally {
        setRefreshing(false);
      }
      return;
    }

    if (catalogAction === 'loadVariantOptions') {
      try {
        const resp = await loadVariantOptions();
        if (resp.data.success) {
          setMessage({ type: 'success', text: `Loaded options into ${resp.data.data.updated} variants.` });
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to load variant options' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load variant options' });
      } finally {
        setRefreshing(false);
      }
      return;
    }

    const isAllVariants = catalogAction === 'allVariants';
    try {
      const response = await refreshCatalog(isAllVariants, productImages);
      if (!response.data.success) {
        setMessage({ type: 'error', text: response.data.message || 'Failed to refresh catalog' });
        return;
      }

      const { count, newBlueprints: newBps, existingBlueprints: existingBps, retired: retiredIds, images: imgList } = response.data.data;
      const retired = retiredIds || [];
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
      let imagesDone = 0;
      const imagesTotal = imgs.length;

      const updateProgress = (phase, detail) => {
        setProgress({
          phase,
          detail,
          blueprints: { done: providersDone, total: allBps.length },
          variants: { done: variantsDone },
          shipping: { done: shippingDone },
          images: { downloaded: imagesDone, skipped: imagesSkipped, total: imagesTotal },
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

      setMessage({
        type: 'info',
        text: `Catalog refreshed. ${count} blueprints, ${newBpList.length} new, ${existingBpList.length} existing, ${retired.length} retired.`,
      });

      if (productImages) {
        for (let i = 0; i < imgs.length; i++) {
          const blueprintId = imgs[i];
          try {
            const dlResp = await downloadBlueprintImages(blueprintId);
            if (dlResp.data.success) {
              imagesDownloaded += dlResp.data.data.downloaded || 0;
              if (dlResp.data.data.skipped) imagesSkipped++;
            }
          } catch {}
          imagesDone++;
          updateProgress('images', `Image ${imagesDownloaded}`);
        }
      }

      updateProgress('done', 'Complete!');
      setProgress((prev) => ({ ...prev, done: true }));
      setMessage({
        type: 'success',
        text: `Catalog refreshed. ${count} blueprints, ${newBpList.length} new, ${existingBpList.length} existing, ${retired.length} retired, ${providersDone} provider sets, ${variantsDone} variant sets, ${shippingDone} shipping records, ${imagesDownloaded} images downloaded.`,
      });
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to refresh catalog' });
    } finally {
      setRefreshing(false);
    }
  };

  // --- Printify Scraper ---

  const matchQueueRef = useRef([]);
  const matchQueueIndexRef = useRef(0);
  const matchImagesRef = useRef([]);

  const setupScraperHub = useCallback(async () => {
    if (scraperHubRef.current) return scraperHubRef.current;

    const connection = createPrintifyScraperHubConnection(session.token);

    connection.on('PrintifyScraperProgress', (event) => {
      console.log('[PrintifyScraperHub] Progress:', event);
      const { data } = event;
      if (data?.message) setScraperStatus(data.message);
    });

    await connection.start();
    scraperHubRef.current = connection;
    return connection;
  }, [session.token]);

  const handleWizardError = (message) => {
    const bp = matchQueueRef.current[matchQueueIndexRef.current];
    setScraperStatus(`Error: ${message}`);
    setScraperError({ blueprintId: bp?.id, title: bp?.title, message: message || 'Failed to load blueprint' });
    setScraperPanel(null);
  };

  const handleWizardComplete = async () => {
    const bp = matchQueueRef.current[matchQueueIndexRef.current];
    if (!bp) return;
    setScraperPanel(null);
    setScraperStatus('Publishing blueprint...');
    try {
      const resp = await publishBlueprint(bp.id);
      if (!resp.data?.success) throw new Error(resp.data?.message || 'Failed to publish blueprint');
      await loadMatchBlueprint(matchQueueIndexRef.current + 1);
    } catch (error) {
      setScraperStatus(`Error: ${error?.message || 'Failed to publish blueprint'}`);
      setScraperError({ blueprintId: bp.id, title: bp.title, message: error?.message || 'Failed to publish blueprint' });
    }
  };

  const loadMatchBlueprint = async (queueIndex) => {
    const queue = matchQueueRef.current;
    if (queueIndex >= queue.length) {
      setScraperRunning(false);
      setScraperStatus('Complete!');
      setScraperPanel(null);
      setScraperError(null);
      setMessage({ type: 'success', text: `Done! Processed all ${queue.length} blueprints.` });
      return;
    }
    matchQueueIndexRef.current = queueIndex;
    const bp = queue[queueIndex];
    setScraperProgress({ processed: queueIndex + 1, total: queue.length });
    setScraperStatus(`Processing blueprint ${queueIndex + 1}/${queue.length}: ${bp.title}`);
    setScraperError(null);
    setScraperPanel({ blueprintId: bp.id, blueprintTitle: bp.title });
  };

  const advanceToImage = (imageIndex, providers) => {
    const bp = matchQueueRef.current[matchQueueIndexRef.current];
    if (!bp) return;
    if (imageIndex < 0) imageIndex = 0;
    if (imageIndex >= bp.imageCount) {
      setScraperPanel(null);
      setScraperStatus('Publishing blueprint...');
      publishBlueprint(bp.id).then((resp) => {
        if (resp.data?.success) {
          loadMatchBlueprint(matchQueueIndexRef.current + 1);
        } else {
          setScraperStatus(`Error publishing: ${resp.data?.message || 'Unknown error'}`);
          setScraperError({ blueprintId: bp.id, title: bp.title, message: resp.data?.message || 'Failed to publish blueprint' });
        }
      }).catch((err) => {
        setScraperStatus(`Error: ${err?.message || 'Failed to publish blueprint'}`);
        setScraperError({ blueprintId: bp.id, title: bp.title, message: err?.message || 'Failed to publish blueprint' });
      });
      return;
    }

    const images = matchImagesRef.current;
    const img = images.find((i) => i.imageIndex === imageIndex) || { imageIndex, type: 0, position: 1, variantColors: [] };
    const allColors = (providers || []).flatMap((p) => p.colors || []);
    let defaultColors = {};
    if (allColors.length === 1 && allColors[0]?.name) {
      defaultColors = { [allColors[0].name]: true };
    } else if (img.variantColors?.length === 1) {
      const match = allColors.find((c) => c.name === img.variantColors[0]);
      if (match?.name) defaultColors = { [match.name]: true };
    }
    setSelectedColors(defaultColors);
    setSelectedType(String(img.type || 0));
    setSelectedPosition(String(img.position || 1));
    setScraperPanel({
      blueprintId: bp.id,
      blueprintTitle: bp.title,
      printifyUrl: `https://printify.com/app/products/${bp.id}/${slugify(bp.brand || '')}/${slugify(bp.title || '')}`,
      imageIndex,
      imageCount: bp.imageCount,
      imageBase64: getBlueprintImageUrl(bp.id, imageIndex),
      type: img.type || 0,
      position: img.position || 1,
      providers,
      variantColors: img.variantColors || [],
    });
    setScraperError(null);
  };

  const handleMatchImagesToVariants = async () => {
    setScraperRunning(true);
    setScraperStatus('Loading unpublished blueprints...');
    setScraperProgress(null);
    setScraperPanel(null);
    setScraperError(null);
    setMessage(null);

    try {
      const resp = await getUnpublishedBlueprints();
      if (!resp.data.success) throw new Error(resp.data.message);
      const queue = resp.data.data || [];
      matchQueueRef.current = queue;
      if (queue.length === 0) {
        setScraperRunning(false);
        setScraperStatus('No unpublished blueprints found.');
        setMessage({ type: 'success', text: 'No unpublished blueprints found.' });
        return;
      }
      await loadMatchBlueprint(0);
    } catch (error) {
      setScraperRunning(false);
      setScraperStatus(`Error: ${error?.message || 'Failed to start matching'}`);
      setMessage({ type: 'error', text: error?.message || 'Failed to start matching' });
    }
  };

  const handleColorToggle = (colorName) => {
    setSelectedColors(prev => ({ ...prev, [colorName]: !prev[colorName] }));
  };

  const allColors = useMemo(() =>
    (scraperPanel?.providers || []).flatMap((p) => p.colors || []),
    [scraperPanel?.providers]
  );

  const groupedColors = useMemo(() => groupColors(allColors), [allColors]);
  const providerColorGroups = useMemo(() =>
    (scraperPanel?.providers || []).map((p) => ({ title: p.name || `Provider ${p.printProviderId || ''}`, groups: groupColors(p.colors || []) })),
    [scraperPanel?.providers]
  );

  const renderColorGroups = (groups) =>
    groups.map((group, i) => {
      const matchedVariant = scraperPanel.variantColors?.includes(group.name);
      return (
        <Item key={i} hover>
          <input
            type="checkbox"
            checked={!!selectedColors[group.name]}
            onChange={() => handleColorToggle(group.name)}
            className="w-4 h-4 mr-3 text-primary-600 border-gray-300 rounded focus:ring-primary-500"
          />
          {group.hexes.length > 0 && (
            <div className="flex items-center gap-1 mr-3 shrink-0">
              {group.hexes.map((hex, hi) => (
                <span
                  key={hi}
                  className="w-5 h-5 rounded-full border border-gray-300 dark:border-gray-600"
                  style={{ backgroundColor: hex }}
                  title={hex}
                />
              ))}
            </div>
          )}
          <span className="text-sm text-gray-700 dark:text-gray-300">
            {group.name}
            {matchedVariant && (
              <span className="ml-2 text-xs text-green-600 dark:text-green-400">(variant exists)</span>
            )}
          </span>
        </Item>
      );
    });

  const allColorsSelected = useMemo(() =>
    allColors.length > 0 && allColors.every((c) => selectedColors[c.name]),
    [allColors, selectedColors]
  );

  const handleSelectAllNone = () => {
    if (allColorsSelected) {
      setSelectedColors({});
    } else {
      const all = {};
      allColors.forEach((c) => {
        all[c.name] = true;
      });
      setSelectedColors(all);
    }
  };

  const handleViewBlueprint = () => {
    if (scraperPanel?.blueprintId) setConfigureBlueprintId(scraperPanel.blueprintId);
  };

  const handleConfigureClose = () => {
    setConfigureBlueprintId(null);
  };

  const handleOpenImagePreview = (index) => {
    setPreviewIndex(index);
    setShowPreview(true);
  };

  const handleCloseImagePreview = () => {
    setShowPreview(false);
  };

  const handleApplyVariants = async (goBack = false) => {
    if (!scraperPanel) return;
    const { blueprintId, imageIndex } = scraperPanel;
    const selected = Object.entries(selectedColors).filter(([_, v]) => v).map(([k]) => k);
    setScraperStatus(goBack ? 'Applying variants and going back...' : 'Applying variants...');
    try {
      const resp = await applyVariants(blueprintId, imageIndex, {
        selectedColors: selected,
        position: parseInt(selectedPosition, 10),
        type: parseInt(selectedType, 10),
      });
      if (!resp.data.success) throw new Error(resp.data.message || 'Failed to apply variants');
      if (goBack && imageIndex > 0) {
        advanceToImage(imageIndex - 1, scraperPanel.providers);
      } else {
        const next = imageIndex + 1;
        if (next >= scraperPanel.imageCount) {
          setScraperPanel(null);
          setScraperStatus('Publishing blueprint...');
          await publishBlueprint(blueprintId);
          await loadMatchBlueprint(matchQueueIndexRef.current + 1);
        } else {
          advanceToImage(next, scraperPanel.providers);
        }
      }
    } catch (error) {
      setScraperStatus(`Error applying variants: ${error?.message}`);
      setMessage({ type: 'error', text: error?.message || 'Failed to apply variants' });
    }
  };

  const handleBackVariants = async () => {
    await handleApplyVariants(true);
  };

  const handleSkipBlueprint = async () => {
    const bp = scraperError || (scraperPanel ? { blueprintId: scraperPanel.blueprintId, title: scraperPanel.blueprintTitle } : null);
    if (!bp) return;
    setScraperStatus(`Skipping ${bp.title || 'blueprint'}...`);
    setScraperError(null);
    loadMatchBlueprint(matchQueueIndexRef.current + 1);
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

  const blueprintImageCarouselElements = useMemo(() => {
    if (!scraperPanel?.blueprintId || !scraperPanel?.imageCount) return [];
    return Array.from({ length: scraperPanel.imageCount }, (_, i) => {
      const variantColors = blueprintImageVariantMap[i] || [];
      const label = variantColors.join(', ') || 'No variants';
      const isCurrent = i === scraperPanel.imageIndex;
      return (
        <div
          key={i}
          className={`shrink-0 flex mt-1 flex-col items-center w-24 ${isCurrent ? 'ring-2 ring-primary-500 rounded' : ''}`}
        >
          <img
            src={getBlueprintImageUrl(scraperPanel.blueprintId, i)}
            alt={`Image ${i + 1}`}
            className="w-24 h-24 object-cover rounded-t cursor-pointer"
            onClick={() => handleOpenImagePreview(i)}
          />
          <div className="w-24 text-xs text-center text-gray-600 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 p-1 rounded-b break-words" title={label}>
            {label}
          </div>
        </div>
      );
    });
  }, [scraperPanel, blueprintImageVariantMap, getBlueprintImageUrl, handleOpenImagePreview]);

  const previewImages = useMemo(() => {
    if (!scraperPanel?.blueprintId || !scraperPanel?.imageCount) return [];
    return Array.from({ length: scraperPanel.imageCount }, (_, i) => getBlueprintImageUrl(scraperPanel.blueprintId, i));
  }, [scraperPanel, getBlueprintImageUrl]);

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
          <div className="flex items-center gap-4">
            <h2 className="text-xl font-semibold">Printify</h2>
            <div className="flex items-center gap-2">
              <label className="pl-5 text-sm font-medium text-gray-700 dark:text-gray-300">
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
          </div>
          <div className="flex items-center gap-4">
            {catalogAction === 'refresh' && (
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
            )}
            <div className="flex items-center gap-2">
              <select
                className="w-auto inline-block rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-3 py-2 text-sm text-gray-900 dark:text-gray-100"
                value={catalogAction}
                onChange={(e) => setCatalogAction(e.target.value)}
                disabled={refreshing}
              >
                <option value="refresh">Refresh Catalog</option>
                <option value="allVariants">All Product Variants</option>
                <option value="convertVariants">Find Missing Variants</option>
                <option value="convertImageVariants">Convert Image Variants</option>
                <option value="matchImages">Match Images to Variants</option>
                <option value="getVariantOptions">Get Variant Options</option>
                <option value="loadVariantOptions">Load Variant Options</option>
              </select>
              <Tooltip text={
                catalogAction === 'refresh' ? 'Fetches the full Printify catalog (blueprints, print providers, variants, shipping, and images) and updates the local database.' :
                catalogAction === 'allVariants' ? 'Fetches all product variants from Printify for every blueprint and print provider, including out-of-stock items.' :
                catalogAction === 'convertVariants' ? 'Finds variants in the database with missing Color or Size, re-fetches them from the Printify API, and updates the Color and Size columns.' :
                catalogAction === 'convertImageVariants' ? 'Converts image variant data from the Options JSON field into structured Color and Size columns for existing image variant records.' :
                catalogAction === 'matchImages' ? 'Scrapes Printify blueprint pages for Provider Info colors, then lets you manually match colors to each blueprint image and publish the blueprint.' :
                catalogAction === 'getVariantOptions' ? 'Returns all distinct top-level option keys from the Options JSON across every PrintifyBlueprintVariants record, plus the maximum number of keys found in any single record.' :
                catalogAction === 'loadVariantOptions' ? 'Scans all records in PrintifyBlueprintVariants and populates the missing option columns (Depth, Design, Finish, etc.) from the corresponding keys in the Options JSON.' :
                'Select an action to see its description.'
              } />
              <div className="pl-10">
                {catalogAction === 'matchImages' ? (
                  <Button onClick={handleMatchImagesToVariants} disabled={scraperRunning}>
                    {scraperRunning ? (
                      <span className="inline-flex items-center gap-2">
                        <Icon name="progress_activity" spin className="w-4 h-4" />
                        Running...
                      </span>
                    ) : (
                      'Run Command'
                    )}
                  </Button>
                ) : (
                  <Button onClick={handleRefreshCatalog} disabled={refreshing}>
                    {refreshing ? (
                      <span className="inline-flex items-center gap-2">
                        <Icon name="progress_activity" spin className="w-4 h-4" />
                        Refreshing...
                      </span>
                    ) : (
                      'Run Command'
                    )}
                  </Button>
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-4">

          {variantOptionKeys && variantOptionKeys.length > 0 && (
            <div className="text-center">
              <h4 className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                Distinct Variant Option Keys (max {maxOptionKeys ?? 0} per variant)
              </h4>
              <ul className="inline-block text-left list-disc list-inside text-sm text-gray-600 dark:text-gray-400">
                {variantOptionKeys.map((item, i) => (
                  <li key={i}>
                    <span className="capitalize">{item.key}</span>
                    {
                    //<span className="text-gray-500 dark:text-gray-500 ml-1">(max: {item.maxCount})</span>
                    }
                  </li>
                ))}
              </ul>
            </div>
          )}

          {convertProgress && (
            <div className="space-y-2">
              <div className="text-xs text-gray-600 dark:text-gray-400">
                {convertProgress.current} of {convertProgress.total} variants converted
              </div>
              <div className="w-full h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                <div
                  className="h-full bg-primary-600 rounded-full transition-all duration-200"
                  style={{
                    width: `${Math.round((convertProgress.current / Math.max(convertProgress.total, 1)) * 100)}%`,
                  }}
                />
              </div>
            </div>
          )}

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

          {/* Printify Scraper UI */}
          {catalogAction === 'matchImages' && (scraperRunning || scraperStatus) && (
            <div className="space-y-3 mt-4">
              {/* Progress bar */}
              {scraperProgress && (
                <div className="w-full h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-primary-600 rounded-full transition-all duration-200"
                    style={{
                      width: `${Math.round((scraperProgress.processed / Math.max(scraperProgress.total, 1)) * 100)}%`,
                    }}
                  />
                </div>
              )}
              {scraperProgress && (
                <div className="text-xs text-gray-500 dark:text-gray-400">
                  Blueprint {scraperProgress.processed}/{scraperProgress.total}
                </div>
              )}

              {/* Status message (below progress bar, centered) */}
              {scraperStatus && (
                <div className="text-sm text-gray-700 dark:text-gray-300 text-center">
                  {scraperStatus}
                </div>
              )}

              {/* Error message with Skip button */}
              {scraperError && (
                <div className="border border-red-300 dark:border-red-700 rounded-lg p-4 bg-red-50 dark:bg-red-900/30">
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex-1">
                      <h4 className="text-sm font-semibold text-red-700 dark:text-red-300 mb-1">
                        {scraperError.title}
                      </h4>
                      <p className="text-sm text-red-600 dark:text-red-400">
                        {scraperError.message}
                      </p>
                      {scraperError.url && (
                        <a href={scraperError.url} target="_blank" rel="noopener noreferrer" className="text-sm text-blue-600 dark:text-blue-400 hover:underline mt-1 inline-block">
                          View on Printify
                        </a>
                      )}
                    </div>
                    <Button color="gray" onClick={handleSkipBlueprint}>
                      Skip
                    </Button>
                  </div>
                </div>
              )}

              {/* Panel: image + color checkboxes + Apply button */}
              {scraperPanel && (
                <PrintifyColorsWizard
                  blueprintId={scraperPanel.blueprintId}
                  onComplete={handleWizardComplete}
                  onError={handleWizardError}
                  onCancel={() => {
                    setScraperPanel(null);
                    setScraperRunning(false);
                  }}
                />
              )}
            </div>
          )}
        </div>
      </div>
      {showPreview && (
        <ProductImagePreview
          show={showPreview}
          images={previewImages}
          alt={scraperPanel?.blueprintTitle || 'Blueprint Image'}
          defaultIndex={previewIndex}
          onClose={handleCloseImagePreview}
        />
      )}
      {configureBlueprintId && (
        <ConfigurePrintifyBlueprint
          show={!!configureBlueprintId}
          blueprint={{ id: configureBlueprintId }}
          onClose={handleConfigureClose}
          onSave={handleConfigureClose}
        />
      )}
    </div>
  );
}
