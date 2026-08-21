import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/admin/printify';
import { PrintifyImageMatch } from '@/api/admin/printifyImageMatch';
import { createPrintifyScraperHubConnection } from '@/api/admin/printifyScraper';
import ButtonOutline from '@/components/ui/button-outline';
import List, { Item } from '@/components/ui/list';
import { Accordion } from '@/components/ui/accordion';
import CarouselElements from '@/components/ui/carousel-elements';
import ProductImagePreview from '@/app/dashboard/project/components/ProductImagePreview';
import { TYPE_OPTIONS, POSITION_OPTIONS } from '@/context/printifyBlueprint';

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

export default function PrintifyColorsWizard({ blueprintId, onComplete, onCancel, onError }) {
  const session = useSession();

  // Keep the latest refs of props and APIs so effects/callbacks never go stale
  const printifyRef = useRef(Printify(session));
  const imageMatchRef = useRef(PrintifyImageMatch(session));
  const sessionRef = useRef(session);
  const onCompleteRef = useRef(onComplete);
  const onErrorRef = useRef(onError);
  const blueprintIdRef = useRef(blueprintId);
  const imageCountRef = useRef(0);

  printifyRef.current = Printify(session);
  imageMatchRef.current = PrintifyImageMatch(session);
  sessionRef.current = session;
  onCompleteRef.current = onComplete;
  onErrorRef.current = onError;
  blueprintIdRef.current = blueprintId;

  const hubRef = useRef(null);

  const [detail, setDetail] = useState(null);
  const [images, setImages] = useState([]);
  const [imageCount, setImageCount] = useState(0);
  imageCountRef.current = imageCount;

  const [providers, setProviders] = useState([]);
  const [variantColorsByProvider, setVariantColorsByProvider] = useState({});
  const [imageIndex, setImageIndex] = useState(0);
  const [selectedColors, setSelectedColors] = useState({});
  const [selectedType, setSelectedType] = useState(String(0));
  const [selectedPosition, setSelectedPosition] = useState('1');
  const [status, setStatus] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [showPreview, setShowPreview] = useState(false);
  const [previewIndex, setPreviewIndex] = useState(0);

  const printifyUrl = useMemo(() => {
    if (!detail) return null;
    return `https://printify.com/app/products/${blueprintId}/${slugify(detail.brand || '')}/${slugify(detail.title || '')}`;
  }, [detail, blueprintId]);

  const advanceToImage = (nextIndex, currentImages, currentProviders) => {
    if (nextIndex < 0) nextIndex = 0;
    if (nextIndex >= imageCountRef.current && imageCountRef.current > 0) {
      if (onCompleteRef.current) onCompleteRef.current();
      return;
    }

    const img = currentImages.find((i) => i.imageIndex === nextIndex) || {
      imageIndex: nextIndex,
      type: 0,
      position: 1,
      variantColors: [],
    };

    const allColors = (currentProviders || []).flatMap((p) => p.colors || []);
    let defaultColors = {};
    if (allColors.length === 1 && allColors[0]?.name) {
      defaultColors = { [allColors[0].name]: true };
    } else if (img.variantColors?.length === 1) {
      const match = allColors.find((c) => c.name === img.variantColors[0]);
      if (match?.name) defaultColors = { [match.name]: true };
    }

    setImageIndex(nextIndex);
    setSelectedColors(defaultColors);
    setSelectedType(String(img.type || 0));
    setSelectedPosition(String(img.position || 1));
    setStatus('');
  };

  const loadBlueprint = async () => {
    setStatus('Loading blueprint...');
    setError(null);

    const { getBlueprintDetail, getBlueprintImages, getBlueprintVariants } = printifyRef.current;

    try {
      const [detailResp, imagesResp] = await Promise.all([
        getBlueprintDetail(blueprintIdRef.current),
        getBlueprintImages(blueprintIdRef.current),
      ]);

      let nextImageCount = 0;
      if (detailResp.data.success) {
        setDetail(detailResp.data.data.blueprint);
        nextImageCount = detailResp.data.data.blueprint?.imageCount || 0;
        setImageCount(nextImageCount);
      }

      const loadedImages = imagesResp.data.success ? (imagesResp.data.data || []) : [];
      setImages(loadedImages);

      const token = sessionRef.current.token;
      const connection = createPrintifyScraperHubConnection(token);
      connection.on('PrintifyScraperProgress', (event) => {
        console.log('[PrintifyScraperHub] Progress:', event);
        if (event.data?.message) setStatus(event.data.message);
      });

      await connection.start();
      hubRef.current = connection;

      setStatus('Scraping colors...');
      const colorResp = await connection.invoke('GetProviderColors', blueprintIdRef.current);
      await connection.stop();
      hubRef.current = null;

      if (!colorResp?.success) throw new Error(colorResp?.message || 'No colors found');

      const nextProviders = colorResp.data.providers || [];
      setProviders(nextProviders);

      setStatus('Loading variant colors...');
      const providerColors = {};
      await Promise.all(nextProviders.map(async (p) => {
        try {
          const resp = await getBlueprintVariants(blueprintIdRef.current, p.printProviderId);
          if (resp.data?.success) {
            const colors = (resp.data.data.variants || [])
              .map((v) => v.color)
              .filter(Boolean);
            providerColors[p.printProviderId] = [...new Set(colors)];
          }
        } catch {}
      }));
      setVariantColorsByProvider(providerColors);

      // make sure imageCount is up to date for the navigation check
      imageCountRef.current = nextImageCount;
      advanceToImage(0, loadedImages, nextProviders);
    } catch (err) {
      const message = err?.message || 'Failed to load colors';
      if (onErrorRef.current) {
        onErrorRef.current(message);
      } else {
        setStatus('');
        setError(message);
      }
    }
  };

  useEffect(() => {
    loadBlueprint();
    return () => {
      if (hubRef.current) {
        hubRef.current.stop().catch(() => {});
        hubRef.current = null;
      }
    };
    // Only re-run when the blueprint id changes; refs keep the latest props/apis
  }, [blueprintId]);

  const allColors = useMemo(() =>
    (providers || []).flatMap((p) => p.colors || []),
    [providers]
  );

  const groupedColors = useMemo(() => groupColors(allColors), [allColors]);
  const providerColorGroups = useMemo(() =>
    (providers || []).map((p) => ({
      title: p.name || `Provider ${p.printProviderId || ''}`,
      printProviderId: p.printProviderId,
      groups: groupColors(p.colors || []),
    })),
    [providers]
  );

  const allVariantColors = useMemo(() => {
    const set = new Set();
    Object.values(variantColorsByProvider).forEach((arr) => arr.forEach((c) => set.add(c)));
    return [...set];
  }, [variantColorsByProvider]);

  const allColorsSelected = useMemo(() =>
    allColors.length > 0 && allColors.every((c) => selectedColors[c.name]),
    [allColors, selectedColors]
  );

  const handleColorToggle = (colorName) => {
    setSelectedColors((prev) => ({ ...prev, [colorName]: !prev[colorName] }));
  };

  const handleSelectAllNone = () => {
    if (allColorsSelected) {
      setSelectedColors({});
    } else {
      const all = {};
      allColors.forEach((c) => { all[c.name] = true; });
      setSelectedColors(all);
    }
  };

  const currentImage = useMemo(() =>
    images.find((i) => i.imageIndex === imageIndex) || { imageIndex, variantColors: [], type: 0, position: 1 },
    [images, imageIndex]
  );

  const renderColorGroups = (groups, variantColors) =>
    groups.map((group, i) => {
      const matchedVariant = variantColors?.includes(group.name);
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

  const handleApply = async (goBack = false) => {
    setSaving(true);
    setStatus(goBack ? 'Applying and going back...' : 'Applying...');

    try {
      const selected = Object.entries(selectedColors).filter(([_, v]) => v).map(([k]) => k);
      const resp = await imageMatchRef.current.applyVariants(blueprintIdRef.current, imageIndex, {
        selectedColors: selected,
        position: parseInt(selectedPosition, 10),
        type: parseInt(selectedType, 10),
      });
      if (!resp.data.success) throw new Error(resp.data.message || 'Failed to apply variants');

      const nextImages = images.map((img) =>
        img.imageIndex === imageIndex
          ? { ...img, variantColors: selected, type: parseInt(selectedType, 10), position: parseInt(selectedPosition, 10) }
          : img
      );
      setImages(nextImages);

      if (goBack) {
        advanceToImage(imageIndex - 1, nextImages, providers);
      } else {
        advanceToImage(imageIndex + 1, nextImages, providers);
      }
    } catch (err) {
      const message = err?.message || 'Failed to apply variants';
      if (onErrorRef.current) {
        onErrorRef.current(message);
      } else {
        setStatus('');
        setError(message);
      }
    } finally {
      setSaving(false);
    }
  };

  const handleBack = () => handleApply(true);

  const handleOpenImagePreview = (index) => {
    setPreviewIndex(index);
    setShowPreview(true);
  };

  const getImageUrl = (idx) => printifyRef.current.getBlueprintImageUrl(blueprintIdRef.current, idx);

  const blueprintImageCarouselElements = useMemo(() => {
    if (!blueprintId || !imageCount) return [];
    return Array.from({ length: imageCount }, (_, i) => {
      const img = images.find((x) => x.imageIndex === i);
      const variantColors = img?.variantColors || [];
      const label = variantColors.join(', ') || 'No variants';
      const isCurrent = i === imageIndex;
      return (
        <div
          key={i}
          className={`shrink-0 flex mt-1 flex-col items-center w-24 ${isCurrent ? 'ring-2 ring-primary-500 rounded' : ''}`}
        >
          <img
            src={getImageUrl(i)}
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
  }, [blueprintId, imageCount, images, imageIndex]);

  if (error) {
    return (
      <div className="border border-red-300 dark:border-red-700 rounded-lg p-4 bg-red-50 dark:bg-red-900/30">
        <h4 className="text-sm font-semibold text-red-700 dark:text-red-300 mb-1">
          Error loading colors
        </h4>
        <p className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
        <div className="flex items-center gap-2 mt-3">
          <ButtonOutline onClick={loadBlueprint}>
            Retry
          </ButtonOutline>
          {onCancel && (
            <ButtonOutline onClick={onCancel} color="gray">
              Cancel
            </ButtonOutline>
          )}
        </div>
      </div>
    );
  }

  if (!providers || providers.length === 0) {
    return (
      <div className="text-sm text-gray-700 dark:text-gray-300 text-center py-4">
        {status || 'Loading colors...'}
      </div>
    );
  }

  return (
    <div className="border border-gray-300 dark:border-gray-600 rounded-lg p-4 bg-gray-50 dark:bg-gray-900">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-lg font-semibold text-gray-800 dark:text-gray-200">
          {detail?.title || `Blueprint ${blueprintId}`}
        </h3>
        <div className="flex items-center gap-3">
          {printifyUrl && (
            <a href={printifyUrl} target="_blank" rel="noopener noreferrer" className="text-sm text-blue-600 dark:text-blue-400 hover:underline">
              View on Printify
            </a>
          )}
          {onCancel && (
            <ButtonOutline onClick={onCancel} color="gray" size="small">
              Close
            </ButtonOutline>
          )}
        </div>
      </div>

      <div className="flex gap-4">
        {/* Image */}
        <div className="shrink-0">
          <img
            src={getImageUrl(imageIndex)}
            alt={`Blueprint ${blueprintId} - Image ${imageIndex}`}
            width={250}
            height={250}
            className="rounded-lg border border-gray-300 dark:border-gray-600"
          />
        </div>

        {/* Color list */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center justify-between mb-2">
            <h4 className="text-sm font-semibold text-gray-700 dark:text-gray-300">
              Select Colors for Image {imageIndex + 1}
            </h4>
            <button
              type="button"
              onClick={handleSelectAllNone}
              className="text-xs text-blue-600 dark:text-blue-400 hover:underline"
            >
              {allColorsSelected ? 'Select None' : 'Select All'}
            </button>
          </div>
          {providers.length > 1 ? (
            <Accordion
              items={providerColorGroups.map((p) => ({
                title: p.title,
                content: (
                  <List className="max-h-none overflow-visible">
                    {renderColorGroups(p.groups, variantColorsByProvider[p.printProviderId] || [])}
                  </List>
                ),
              }))}
              defaultExpandedIndex={0}
            />
          ) : (
            <List className="max-h-none overflow-visible">
              {renderColorGroups(groupedColors, allVariantColors)}
            </List>
          )}
          <div className="mt-3 flex flex-wrap items-end gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Type
              </label>
              <select
                className="w-auto inline-block rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-3 py-2 text-sm text-gray-900 dark:text-gray-100 mb-3"
                value={selectedType}
                onChange={(e) => setSelectedType(e.target.value)}
              >
                {TYPE_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Position
              </label>
              <select
                className="w-auto inline-block rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-3 py-2 text-sm text-gray-900 dark:text-gray-100 mb-3"
                value={selectedPosition}
                onChange={(e) => setSelectedPosition(e.target.value)}
              >
                {POSITION_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="flex items-center gap-2">
            {imageIndex > 0 && (
              <ButtonOutline onClick={handleBack} color="gray" disabled={saving}>
                Back
              </ButtonOutline>
            )}
            <ButtonOutline onClick={() => handleApply(false)} color="blue" disabled={saving}>
              {saving ? 'Applying...' : 'Apply Variants'}
            </ButtonOutline>
          </div>
        </div>
      </div>

      <div className="mt-4 w-full">
        <h5 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
          Blueprint Images ({imageCount})
        </h5>
        <CarouselElements
          elements={blueprintImageCarouselElements}
          className="w-full"
          gap={16}
        />
      </div>

      {showPreview && (
        <ProductImagePreview
          show={showPreview}
          images={Array.from({ length: imageCount }, (_, i) => getImageUrl(i))}
          alt={detail?.title || 'Blueprint Image'}
          defaultIndex={previewIndex}
          onClose={() => setShowPreview(false)}
        />
      )}
    </div>
  );
}
