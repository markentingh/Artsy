import React, { createContext, useContext, useEffect, useMemo, useState, useRef } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/admin/printify';
import { Printify as PrintifyPublic } from '@/api/user/printify';
import { Projects } from '@/api/user/projects';
import { CustomImages } from '@/api/user/customImages';

const ProductBlueprintContext = createContext(null);

export function ProductBlueprintProvider({
  children,
  show,
  blueprint,
  existingConfig,
  projectId,
  onSave,
  onClose,
}) {
  const session = useSession();
  const { getBlueprintDetail, getBlueprintVariants, getBlueprintImageUrl, getBlueprintImages } = Printify(session);
  const { getVariantAvailability } = PrintifyPublic(session);
  const {
    getItems, getItemPreviews, getItemPreviewUrl, getItemReferences,
    uploadItemReference, deleteItemReference, getItemReferenceUrl,
    getItemArtwork, getProductBlueprintImages,
    createBlueprint, updateBlueprint, updateBlueprintVariants, updateBlueprintPricing,
    updateBlueprintDetails, updateBlueprintPlacement, updateProductBlueprintImage,
  } = Projects(session);
  const { getCustomImageUrl } = CustomImages(session);

  const [detail, setDetail] = useState(null);
  const [printProviders, setPrintProviders] = useState([]);
  const [variants, setVariants] = useState([]);
  const [selectedProvider, setSelectedProvider] = useState('');
  const [selectedVariants, setSelectedVariants] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [saveMessage, setSaveMessage] = useState(null);
  const [previewImage, setPreviewImage] = useState(null);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [descriptionExpanded, setDescriptionExpanded] = useState(false);
  const [projectItems, setProjectItems] = useState([]);
  const [itemPreviews, setItemPreviews] = useState({});
  const [itemArtworkMap, setItemArtworkMap] = useState({});
  const [placementSettings, setPlacementSettings] = useState([]);
  const [customImageSelectorTarget, setCustomImageSelectorTarget] = useState(null);
  const [outOfStockIds, setOutOfStockIds] = useState(new Set());
  const [blueprintImages, setBlueprintImages] = useState([]);
  const [prompt, setPrompt] = useState('');
  const [productName, setProductName] = useState('');
  const [productDescription, setProductDescription] = useState('');
  const [safetyInfo, setSafetyInfo] = useState('');
  const [variantPrices, setVariantPrices] = useState({});
  const [productBlueprintImages, setProductBlueprintImages] = useState([]);

  const initialSelectedVariants = useRef([]);
  const initialPlacementSettings = useRef([]);
  const initialVariantPrices = useRef({});
  const initialProductBlueprintImages = useRef([]);

  const isEditing = !!existingConfig;
  const [projectBlueprintId, setProjectBlueprintId] = useState(existingConfig?.id);

  const loadVariants = async (blueprintId, printProviderId) => {
    try {
      const resp = await getBlueprintVariants(blueprintId, printProviderId);
      if (resp.data.success) {
        const variantList = resp.data.data.variants || [];
        setVariants(variantList);

        getVariantAvailability(blueprintId, printProviderId)
          .then((availResp) => {
            if (availResp.data.success) {
              const inStockIds = new Set(availResp.data.data.inStockVariantIds || []);
              const outOfStock = new Set(variantList.map((v) => v.id).filter((id) => !inStockIds.has(id)));
              setOutOfStockIds(outOfStock);
            }
          })
          .catch(() => { });
      }
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to load variants' });
    }
  };

  useEffect(() => {
    if (!show || !blueprint) return;
    setLoading(true);
    setMessage(null);
    setDetail(null);
    setPrintProviders([]);
    setVariants([]);
    setSelectedProvider('');
    setSelectedVariants([]);
    setDescriptionExpanded(false);
    setPlacementSettings([]);
    setOutOfStockIds(new Set());
    setBlueprintImages([]);
    setPrompt('');
    setProductBlueprintImages([]);

    (async () => {
      if (projectId) {
        try {
          const itemsResp = await getItems(projectId);
          if (itemsResp.data.success) {
            const items = itemsResp.data.data || [];
            setProjectItems(items);
            const previewsMap = {};
            const artworkMap = {};
            for (const item of items) {
              try {
                const prevResp = await getItemPreviews(item.id);
                if (prevResp.data.success) {
                  previewsMap[item.id] = prevResp.data.data || [];
                }
              } catch { /* ignore */ }
              try {
                const artResp = await getItemArtwork(item.id);
                if (artResp.data.success) {
                  artworkMap[item.id] = artResp.data.data || null;
                }
              } catch { /* ignore */ }
            }
            setItemPreviews(previewsMap);
            setItemArtworkMap(artworkMap);
          }
        } catch { /* ignore */ }
      }

      try {
        const resp = await getBlueprintDetail(blueprint.id);
        if (resp.data.success) {
          const data = resp.data.data;
          setDetail(data.blueprint);
          setPrintProviders(data.printProviders || []);

          try {
            const imgResp = await getBlueprintImages(blueprint.id);
            if (imgResp.data.success) {
              setBlueprintImages(imgResp.data.data || []);
            }
          } catch { /* ignore */ }

          if (existingConfig) {
            const cfg = JSON.parse(existingConfig.blueprintJson || '{}');
            if (existingConfig.printProviderId) {
              setSelectedProvider(String(existingConfig.printProviderId));
              await loadVariants(blueprint.id, existingConfig.printProviderId);
              if (cfg.variantIds) {
                setSelectedVariants(cfg.variantIds);
                initialSelectedVariants.current = [...cfg.variantIds];
              }
            }
            try {
              const placement = JSON.parse(existingConfig.placementJson || '[]');
              setPlacementSettings(Array.isArray(placement) ? placement : []);
              initialPlacementSettings.current = JSON.parse(JSON.stringify(placement));
            } catch { /* ignore */ }
            setPrompt(existingConfig.prompt || '');
            setProductName(existingConfig.name || data.blueprint?.title || '');
            setProductDescription(existingConfig.description || '');
            setSafetyInfo(existingConfig.safetyInfo || '');
            try {
              const pricing = JSON.parse(existingConfig.pricingJson || '[]');
              const priceMap = {};
              pricing.forEach(p => { priceMap[p.variantId] = parseFloat(p.price).toFixed(2); });
              setVariantPrices(priceMap);
              initialVariantPrices.current = { ...priceMap };
            } catch { /* ignore */ }
            if (existingConfig.id) {
              try {
                const pbiResp = await getProductBlueprintImages(existingConfig.id);
                if (pbiResp.data.success) {
                  const pbiData = pbiResp.data.data || [];
                  setProductBlueprintImages(pbiData);
                  initialProductBlueprintImages.current = JSON.parse(JSON.stringify(pbiData));
                }
              } catch { /* ignore */ }
            }
          } else if (data.blueprint?.printProviderId) {
            setSelectedProvider(String(data.blueprint.printProviderId));
            await loadVariants(blueprint.id, data.blueprint.printProviderId);
          } else if (data.printProviders?.length > 0) {
            const firstProvider = String(data.printProviders[0].id);
            setSelectedProvider(firstProvider);
            await loadVariants(blueprint.id, data.printProviders[0].id);
          }
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to load blueprint' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load blueprint' });
      } finally {
        setLoading(false);
      }
    })();
  }, [show, blueprint]);

  const providerOptions = useMemo(() => {
    return printProviders.map((p) => ({
      value: String(p.id),
      label: p.title,
    }));
  }, [printProviders]);

  const variantColorOptions = useMemo(() => {
    const colorMap = new Map();
    for (const v of variants) {
      const color = v.color || 'Default';
      if (!colorMap.has(color)) {
        colorMap.set(color, { value: color, label: color });
      }
    }
    return Array.from(colorMap.values()).sort((a, b) =>
      a.label.localeCompare(b.label, undefined, { numeric: true, sensitivity: 'base' })
    );
  }, [variants]);

  const variantsByColor = useMemo(() => {
    if (variants.length === 0) return [];
    const sizeOrder = ['XS', 'S', 'M', 'L', 'XL', '2XL', '3XL', '4XL', '5XL'];
    const groups = new Map();
    for (const variant of variants) {
      const color = variant.color || 'Default';
      if (!groups.has(color)) {
        groups.set(color, []);
      }
      groups.get(color).push(variant);
    }
    return Array.from(groups.entries()).map(([color, vars]) => ({
      color,
      variants: vars.sort((a, b) => {
        const aSize = a.size || '';
        const bSize = b.size || '';
        const aIdx = sizeOrder.indexOf(aSize);
        const bIdx = sizeOrder.indexOf(bSize);
        if (aIdx !== -1 && bIdx !== -1) return aIdx - bIdx;
        if (aIdx !== -1) return -1;
        if (bIdx !== -1) return 1;
        return aSize.localeCompare(bSize);
      }),
    }));
  }, [variants]);

  const imagesByColor = useMemo(() => {
    const map = new Map();
    if (variants.length === 0 || blueprintImages.length === 0) return map;

    for (const group of variantsByColor) {
      const indices = blueprintImages
        .filter(img => (img.variantColors || []).includes(group.color))
        .map(img => img.imageIndex);
      const uniqueIndices = [...new Set(indices)];
      const urls = uniqueIndices.map(i => getBlueprintImageUrl(blueprint.id, i, true));
      map.set(group.color, urls);
    }
    return map;
  }, [variantsByColor, blueprintImages, blueprint, getBlueprintImageUrl]);

  const decorationMethodKeys = [
    'dtg', 'dtf', 'embroidery', 'sublimation',
    'digital_printing', 'digital printing',
    'engraving', 'fiber_laser', 'fiber laser', 'co2_laser', 'co2 laser',
  ];

  const decorationMethodLabels = {
    'dtg': 'Direct to Garment',
    'dtf': 'Direct to Film',
    'embroidery': 'Embroidery',
    'sublimation': 'Sublimation',
    'digital_printing': 'Digital Printing',
    'digital printing': 'Digital Printing',
    'engraving': 'Engraving',
    'fiber_laser': 'Fiber Laser',
    'fiber laser': 'Fiber Laser',
    'co2_laser': 'CO2 Laser',
    'co2 laser': 'CO2 Laser',
  };

  const formatDecorationMethod = (method) => {
    if (!method) return '—';
    const key = method.toLowerCase();
    return decorationMethodLabels[key] || method.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
  };

  const formatPosition = (position) => {
    let result = position;
    for (const key of decorationMethodKeys) {
      const escaped = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      result = result.replace(new RegExp(escaped, 'gi'), '');
    }
    return result
      .replace(/_/g, ' ')
      .replace(/\s+/g, ' ')
      .trim()
      .replace(/\b\w/g, (c) => c.toUpperCase());
  };

  const allPlaceholders = useMemo(() => {
    const groups = new Map();
    for (const variant of variants) {
      if (!selectedVariants.includes(variant.id)) continue;
      const phs = variant.placeholders || [];
      for (const ph of phs) {
        const cleanPosition = formatPosition(ph.position);
        if (!groups.has(cleanPosition)) {
          groups.set(cleanPosition, {
            key: cleanPosition,
            position: ph.position,
            decorationMethods: new Map(),
          });
        }
        const group = groups.get(cleanPosition);
        if (ph.decoration_method) {
          if (!group.decorationMethods.has(ph.decoration_method)) {
            group.decorationMethods.set(ph.decoration_method, new Set());
          }
          group.decorationMethods.get(ph.decoration_method).add(`${ph.width}x${ph.height}`);
        }
      }
    }
    return Array.from(groups.values()).map((g) => ({
      ...g,
      decorationMethods: Array.from(g.decorationMethods.entries()).map(([method, dims]) => ({
        method,
        dimensions: Array.from(dims),
      })),
    })).sort((a, b) => a.key.localeCompare(b.key, undefined, { numeric: true, sensitivity: 'base' }));
  }, [variants, selectedVariants]);

  useEffect(() => {
    if (allPlaceholders.length === 0) return;
    setPlacementSettings((prev) => {
      let changed = false;
      const next = [...prev];
      for (const ph of allPlaceholders) {
        const existing = next.find(p => p.position === ph.position);
        const dm = existing?.decorationMethod || ph.decorationMethods[0]?.method || '';
        const methodData = ph.decorationMethods.find((d) => d.method === dm);
        const availableDims = methodData?.dimensions || [];
        const dims = existing?.dimensions || (availableDims.length > 0 ? availableDims[0] : '');
        if (!existing) {
          next.push({ position: ph.position, decorationMethod: dm, dimensions: dims, source: '', itemId: null, customImageId: null, cropX: 'center', cropY: 'center' });
          changed = true;
        } else if (existing.decorationMethod !== dm || existing.dimensions !== dims) {
          Object.assign(existing, { decorationMethod: dm, dimensions: dims });
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [allPlaceholders]);

  const artworkOptions = useMemo(() => {
    return [
      { value: '', label: 'None' },
      { value: 'custom', label: 'Custom Image' },
      ...projectItems.map((item) => ({
        value: item.id,
        label: item.title || 'Untitled Artwork',
      })),
    ];
  }, [projectItems]);

  const handleSave = async () => {
    if (!selectedProvider) {
      setMessage({ type: 'error', text: 'Please select a print provider.' });
      return;
    }
    if (selectedVariants.length === 0) {
      setMessage({ type: 'error', text: 'Please select at least one variant.' });
      return;
    }

    setSaving(true);

    const selectedVariantColors = [...new Set(
      selectedVariants.map(id => variants.find(v => v.id === id)?.color).filter(Boolean)
    )];

    const config = {
      printProviderId: parseInt(selectedProvider),
      variantIds: selectedVariants,
      variantColors: selectedVariantColors,
    };
    const blueprintJson = JSON.stringify(config);
    const placementJson = JSON.stringify(placementSettings);

    const currentPricingJson = JSON.stringify(
      Object.entries(variantPrices)
        .filter(([_, price]) => price !== '')
        .map(([variantId, price]) => ({ variantId: parseInt(variantId), price: parseFloat(price) || 0 }))
    );
    const initialPricingJson = JSON.stringify(
      Object.entries(initialVariantPrices.current)
        .filter(([_, price]) => price !== '')
        .map(([variantId, price]) => ({ variantId: parseInt(variantId), price: parseFloat(price) || 0 }))
    );

    const variantsChanged = JSON.stringify(selectedVariants) !== JSON.stringify(initialSelectedVariants.current);
    const placementsChanged = placementJson !== JSON.stringify(initialPlacementSettings.current);
    const pricingChanged = currentPricingJson !== initialPricingJson;

    const changedPrompts = productBlueprintImages.filter(img => {
      const initial = initialProductBlueprintImages.current.find(i => i.id === img.id);
      return !initial || (initial.prompt || '') !== (img.prompt || '') || (initial.imageId || null) !== (img.imageId || null);
    });

    const name = productName || detail?.title || blueprint.title;

    try {
      if (projectBlueprintId) {
        const promises = [];

        if (variantsChanged) {
          promises.push(updateBlueprintVariants({
            id: projectBlueprintId,
            blueprintJson,
            printProviderId: parseInt(selectedProvider),
          }));
        }

        if (placementsChanged) {
          promises.push(updateBlueprintPlacement({
            id: projectBlueprintId,
            placementJson,
          }));
        }

        if (pricingChanged) {
          promises.push(updateBlueprintPricing({
            id: projectBlueprintId,
            pricingJson: currentPricingJson,
          }));
        }

        promises.push(updateBlueprintDetails({
          id: projectBlueprintId,
          name,
          description: productDescription,
          prompt,
          safetyInfo,
        }));

        for (const img of changedPrompts) {
          promises.push(updateProductBlueprintImage({
            id: img.id,
            title: img.title,
            variantColor: img.variantColor,
            prompt: img.prompt || '',
            imageId: img.imageId || null,
          }));
        }

        await Promise.all(promises);
      } else {
        const resp = await createBlueprint({
          projectId,
          blueprintId: blueprint.id,
          name,
          blueprintJson,
          placementJson,
          prompt,
          description: productDescription,
          safetyInfo,
          pricingJson: currentPricingJson,
          printProviderId: parseInt(selectedProvider),
        });
        if (!resp.data.success) {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to create blueprint' });
          setSaving(false);
          return;
        }
        // Store the newly created blueprint ID so subsequent saves update instead of creating duplicates
        if (resp.data.data?.id) {
          setProjectBlueprintId(resp.data.data.id);
        }
      }

      initialSelectedVariants.current = [...selectedVariants];
      initialPlacementSettings.current = JSON.parse(JSON.stringify(placementSettings));
      initialVariantPrices.current = { ...variantPrices };
      initialProductBlueprintImages.current = JSON.parse(JSON.stringify(productBlueprintImages));

      if (onSave) {
        onSave({ blueprintId: blueprint.id, name });
      }
      setSaveMessage('Changes saved successfully');
      setTimeout(() => setSaveMessage(null), 5000);
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save blueprint' });
    } finally {
      setSaving(false);
    }
  };

  const getPlacementCarouselImages = (position) => {
    const settings = placementSettings.find(p => p.position === position);
    if (!settings) return [];
    if (settings.source === 'custom' && settings.customImageId) {
      return [getCustomImageUrl(settings.customImageId, true)];
    }
    if (settings.source === 'item' && settings.itemId) {
      const itemId = settings.itemId;
      const artwork = itemArtworkMap[itemId];
      if (artwork && artwork.artworkType === 'custom' && artwork.customImageId) {
        return [getCustomImageUrl(artwork.customImageId, true)];
      }
      const previews = itemPreviews[itemId] || [];
      return previews.map((p) => getItemPreviewUrl(itemId, p.id, true));
    }
    return [];
  };

  const value = {
    show,
    blueprint,
    existingConfig,
    projectId,
    onSave,
    onClose,
    isEditing,
    projectBlueprintId,
    detail,
    setDetail,
    printProviders,
    setPrintProviders,
    variants,
    setVariants,
    selectedProvider,
    setSelectedProvider,
    selectedVariants,
    setSelectedVariants,
    loading,
    setLoading,
    saving,
    setSaving,
    message,
    setMessage,
    saveMessage,
    previewImage,
    setPreviewImage,
    previewIndex,
    setPreviewIndex,
    descriptionExpanded,
    setDescriptionExpanded,
    projectItems,
    setProjectItems,
    itemPreviews,
    setItemPreviews,
    itemArtworkMap,
    setItemArtworkMap,
    placementSettings,
    setPlacementSettings,
    customImageSelectorTarget,
    setCustomImageSelectorTarget,
    outOfStockIds,
    setOutOfStockIds,
    blueprintImages,
    setBlueprintImages,
    prompt,
    setPrompt,
    productName,
    setProductName,
    productDescription,
    setProductDescription,
    safetyInfo,
    setSafetyInfo,
    variantPrices,
    setVariantPrices,
    productBlueprintImages,
    setProductBlueprintImages,
    loadVariants,
    handleSave,
    providerOptions,
    variantColorOptions,
    variantsByColor,
    imagesByColor,
    allPlaceholders,
    artworkOptions,
    formatDecorationMethod,
    formatPosition,
    getPlacementCarouselImages,
    getBlueprintImageUrl,
    getItemReferenceUrl,
    getItemPreviewUrl,
  };

  return (
    <ProductBlueprintContext.Provider value={value}>
      {children}
    </ProductBlueprintContext.Provider>
  );
}

export function useProductBlueprint() {
  const context = useContext(ProductBlueprintContext);
  if (!context) {
    throw new Error('useProductBlueprint must be used within a ProductBlueprintProvider');
  }
  return context;
}
