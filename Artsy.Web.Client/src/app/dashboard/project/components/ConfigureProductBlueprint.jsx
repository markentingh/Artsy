import React, { useEffect, useMemo, useState, useRef } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/admin/printify';
import { Printify as PrintifyPublic } from '@/api/user/printify';
import { Projects } from '@/api/user/projects';
import Modal from '@/components/ui/modal';
import Carousel from '@/components/ui/carousel';
import Tooltip from '@/components/ui/tooltip';
import Select from '@/components/forms/select';
import TextArea from '@/components/forms/textarea';
import SelectChecklist from '@/components/ui/select-checklist';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';
import ProductImagePreview from './ProductImagePreview';
import CustomImageSelector from './CustomImageSelector';

export default function ConfigureProductBlueprint({
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
  const { getItems, getItemPreviews, getItemPreviewUrl, getItemReferences, uploadItemReference, deleteItemReference, getItemReferenceUrl } = Projects(session);

  const [detail, setDetail] = useState(null);
  const [printProviders, setPrintProviders] = useState([]);
  const [variants, setVariants] = useState([]);
  const [selectedProvider, setSelectedProvider] = useState('');
  const [selectedVariants, setSelectedVariants] = useState([]);
  const [initialVariantIds, setInitialVariantIds] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [previewImage, setPreviewImage] = useState(null);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [descriptionExpanded, setDescriptionExpanded] = useState(false);
  const [projectItems, setProjectItems] = useState([]);
  const [itemPreviews, setItemPreviews] = useState({});
  const [placementSettings, setPlacementSettings] = useState({});
  const [customImageSelectorTarget, setCustomImageSelectorTarget] = useState(null);
  const [outOfStockIds, setOutOfStockIds] = useState(new Set());
  const [blueprintImages, setBlueprintImages] = useState([]);
  const [prompt, setPrompt] = useState('');
  const scrollRef = useRef(null);
  const [scrollMaxHeight, setScrollMaxHeight] = useState('none');

  const isEditing = !!existingConfig;

  useEffect(() => {
    if (!show || !blueprint) return;
    setLoading(true);
    setMessage(null);
    setDetail(null);
    setPrintProviders([]);
    setVariants([]);
    setSelectedProvider('');
    setSelectedVariants([]);
    setInitialVariantIds([]);
    setDescriptionExpanded(false);
    setPlacementSettings({});
    setOutOfStockIds(new Set());
    setBlueprintImages([]);
    setPrompt('');

    (async () => {
      if (projectId) {
        try {
          const itemsResp = await getItems(projectId);
          if (itemsResp.data.success) {
            const items = itemsResp.data.data || [];
            setProjectItems(items);
            const previewsMap = {};
            for (const item of items) {
              try {
                const prevResp = await getItemPreviews(item.id);
                if (prevResp.data.success) {
                  previewsMap[item.id] = prevResp.data.data || [];
                }
              } catch { /* ignore */ }
            }
            setItemPreviews(previewsMap);
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
            if (cfg.printProviderId) {
              setSelectedProvider(String(cfg.printProviderId));
              await loadVariants(blueprint.id, cfg.printProviderId);
              if (cfg.variantIds) {
                setSelectedVariants(cfg.variantIds);
                setInitialVariantIds(cfg.variantIds);
              }
            }
            try {
              const placement = JSON.parse(existingConfig.placementJson || '{}');
              setPlacementSettings(placement);
            } catch { /* ignore */ }
            setPrompt(existingConfig.prompt || '');
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
          .catch(() => {});
      }
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to load variants' });
    }
  };

  const handleProviderChange = async (e) => {
    const providerId = e.target.value;
    setSelectedProvider(providerId);
    setSelectedVariants([]);
    setVariants([]);
    setOutOfStockIds(new Set());
    if (providerId && blueprint) {
      await loadVariants(blueprint.id, parseInt(providerId));
    }
  };

  const handleVariantToggle = (variantId) => {
    setSelectedVariants((prev) =>
      prev.includes(variantId)
        ? prev.filter((v) => v !== variantId)
        : [...prev, variantId]
    );
  };

  const handleColorVariantsChange = (color, values) => {
    setSelectedVariants((prev) => {
      const colorVariantIds = variantsByColor.find((g) => g.color === color)?.variants.map((v) => String(v.id)) || [];
      const otherIds = prev.filter((id) => !colorVariantIds.includes(String(id)));
      return [...otherIds, ...values.map(Number)];
    });
  };

  const handleSave = () => {
    if (!selectedProvider) {
      setMessage({ type: 'error', text: 'Please select a print provider.' });
      return;
    }
    if (selectedVariants.length === 0) {
      setMessage({ type: 'error', text: 'Please select at least one variant.' });
      return;
    }

    setSaving(true);
    const config = {
      printProviderId: parseInt(selectedProvider),
      variantIds: selectedVariants,
    };

    if (onSave) {
      onSave({
        blueprintId: blueprint.id,
        name: detail?.title || blueprint.title,
        blueprintJson: JSON.stringify(config),
        placementJson: JSON.stringify(placementSettings),
        prompt,
      });
    }
    setSaving(false);
  };

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
      const next = { ...prev };
      for (const ph of allPlaceholders) {
        const existing = next[ph.key] || {};
        const dm = existing.decorationMethod || ph.decorationMethods[0]?.method || '';
        const methodData = ph.decorationMethods.find((d) => d.method === dm);
        const availableDims = methodData?.dimensions || [];
        const dims = existing.dimensions || (availableDims.length > 0 ? availableDims[0] : '');
        if (existing.decorationMethod !== dm || existing.dimensions !== dims) {
          next[ph.key] = { ...existing, decorationMethod: dm, dimensions: dims };
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

  const handlePlacementSourceChange = (key, value) => {
    setPlacementSettings((prev) => ({
      ...prev,
      [key]: {
        ...prev[key],
        source: value === 'custom' ? 'custom' : (value ? 'item' : ''),
        itemId: value && value !== 'custom' ? value : null,
        customImageId: value === 'custom' ? prev[key]?.customImageId || null : null,
        customItemId: value === 'custom' ? prev[key]?.customItemId || null : null,
      },
    }));
  };

  const handlePlacementDecorationMethodChange = (key, value) => {
    setPlacementSettings((prev) => {
      const ph = allPlaceholders.find((p) => p.key === key);
      const methodData = ph?.decorationMethods.find((d) => d.method === value);
      const availableDims = methodData?.dimensions || [];
      return {
        ...prev,
        [key]: {
          ...prev[key],
          decorationMethod: value,
          dimensions: availableDims.length > 0 ? availableDims[0] : '',
        },
      };
    });
  };

  const handlePlacementDimensionsChange = (key, value) => {
    setPlacementSettings((prev) => ({
      ...prev,
      [key]: {
        ...prev[key],
        dimensions: value,
      },
    }));
  };

  const handleSelectCustomImage = (img) => {
    if (!customImageSelectorTarget) return;
    const { key, itemId } = customImageSelectorTarget;
    setPlacementSettings((prev) => ({
      ...prev,
      [key]: { ...prev[key], source: 'custom', itemId, customImageId: img.id, customItemId: itemId },
    }));
    setCustomImageSelectorTarget(null);
  };

  const getPlacementCarouselImages = (key) => {
    const settings = placementSettings[key];
    if (!settings) return [];
    if (settings.source === 'custom' && settings.customImageId && settings.itemId) {
      return [getItemReferenceUrl(settings.itemId, settings.customImageId, true)];
    }
    if (settings.source === 'item' && settings.itemId) {
      const itemId = settings.itemId;
      const previews = itemPreviews[itemId] || [];
      return previews.map((p) => getItemPreviewUrl(itemId, p.id, true));
    }
    return [];
  };

  const variantsByColor = useMemo(() => {
    if (variants.length === 0) return [];
    const sizeOrder = ['XS', 'S', 'M', 'L', 'XL', '2XL', '3XL', '4XL', '5XL'];
    const groups = new Map();
    for (const variant of variants) {
      const color = variant.options?.color || 'Default';
      if (!groups.has(color)) {
        groups.set(color, []);
      }
      groups.get(color).push(variant);
    }
    return Array.from(groups.entries()).map(([color, vars]) => ({
      color,
      variants: vars.sort((a, b) => {
        const aSize = a.options?.size || '';
        const bSize = b.options?.size || '';
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
      const variantIds = new Set(group.variants.map(v => String(v.id)));
      const indices = blueprintImages
        .filter(img => (img.variants || []).some(vId => variantIds.has(String(vId))))
        .map(img => img.imageIndex);
      const uniqueIndices = [...new Set(indices)];
      const urls = uniqueIndices.map(i => getBlueprintImageUrl(blueprint.id, i));
      map.set(group.color, urls);
    }
    return map;
  }, [variantsByColor, blueprintImages, blueprint, detail, getBlueprintImageUrl]);


  useEffect(() => {
    const updateMaxHeight = () => {
      if (scrollRef.current) {
        const rect = scrollRef.current.getBoundingClientRect();
        setScrollMaxHeight(`calc(100vh - ${rect.top + 80}px)`);
      }
    };
    updateMaxHeight();
    window.addEventListener('resize', updateMaxHeight);
    setTimeout(updateMaxHeight, 10);
    return () => window.removeEventListener('resize', updateMaxHeight);
  }, [show, loading]);

  if (!show) return null;

  const providerOptions = printProviders.map((p) => ({
    value: String(p.id),
    label: p.title,
  }));

  return (
    <Modal
      title={isEditing ? 'Edit Product Blueprint' : 'Configure Product Blueprint'}
      onClose={onClose}
      top
      className="min-w-[40em] max-w-full"
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Spinner className="text-4xl" />
        </div>
      ) : detail ? (
        <div ref={scrollRef} className="overflow-y-auto space-y-4 px-[1em]" style={{ maxHeight: scrollMaxHeight }}>
          <div className="space-y-1">
            <h3 className="text-lg font-medium">{detail.title}</h3>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              {detail.brand} {detail.model ? `· ${detail.model}` : ''}
            </p>
            {detail.description && (
              <div className="text-sm text-gray-500 dark:text-gray-400">
                <div
                  className={descriptionExpanded ? '' : 'line-clamp-2'}
                  dangerouslySetInnerHTML={{ __html: detail.description }}
                />
                <button
                  type="button"
                  onClick={() => setDescriptionExpanded((prev) => !prev)}
                  className="text-primary-600 dark:text-primary-400 hover:underline mt-1"
                >
                  {descriptionExpanded ? 'Read less...' : 'Read more...'}
                </button>
              </div>
            )}
          </div>

          <hr className="border-gray-200 dark:border-gray-700" />

          <div className="max-w-xs">
            <div className="flex items-center gap-1 mb-1">
              <label htmlFor="printProvider" className="block text-sm font-medium">Print Provider</label>
              <Tooltip marginTop={2} text="Select the company that will manufacture and ship this product. Different providers may offer different print methods, materials, and shipping regions." />
            </div>
            <Select
              name="printProvider"
              options={providerOptions}
              value={selectedProvider}
              onChange={handleProviderChange}
              className="mb-0"
            />
          </div>

          <hr className="border-gray-200 dark:border-gray-700" />

          {variantsByColor.length > 0 && (
            <div>
              <div className="flex items-center gap-1 mb-2">
                <label className="block text-sm font-medium">Variants</label>
                <Tooltip marginTop={2} text="Choose which sizes and colors of this product you want to offer. Only selected variants will be available for sale. Out-of-stock variants cannot be selected." />
              </div>
              <div className="grid grid-cols-[repeat(auto-fill,250px)] gap-4">
                {variantsByColor.map((group) => {
                  const options = group.variants.map((v) => {
                    const size = v.options?.size || v.title;
                    const isOutOfStock = outOfStockIds.has(v.id);
                    return {
                      value: String(v.id),
                      label: size,
                      note: isOutOfStock ? { text: 'Out of Stock', type: 'red' } : null,
                    };
                  });
                  const selectedValues = group.variants
                    .filter((v) => selectedVariants.includes(v.id))
                    .map((v) => String(v.id));
                  const colorImages = imagesByColor.get(group.color) || [];
                  return (
                    <div key={group.color}>
                      {colorImages.length > 0 ? (
                        <div className="aspect-square w-full mb-2 rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
                          <Carousel
                            images={colorImages}
                            alt={`${detail.title} - ${group.color}`}
                            singleImage
                            infiniteScroll
                            onImageClick={(src) => {
                              const allImages = Array.from({ length: detail.imageCount || 0 }, (_, i) => getBlueprintImageUrl(blueprint.id, i));
                              const globalIdx = allImages.indexOf(src);
                              setPreviewImage(src);
                              setPreviewIndex(globalIdx >= 0 ? globalIdx : 0);
                            }}
                            imageClassName="!max-h-none w-full h-full object-contain"
                          />
                        </div>
                      ) : (
                        <div className="aspect-square w-full mb-2 rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 bg-gray-100 dark:bg-gray-700 flex items-center justify-center text-sm text-gray-400 dark:text-gray-500">No Preview</div>
                      )}
                      <label className="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">{group.color}</label>
                      <SelectChecklist
                        name={`color-variants-${group.color}`}
                        options={options}
                        values={selectedValues}
                        onChange={(vals) => handleColorVariantsChange(group.color, vals)}
                        placeholder="Select sizes"
                      />
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          <hr className="border-gray-200 dark:border-gray-700" />

          {allPlaceholders.length > 0 && (
            <div>
              <div className="flex items-center gap-1 mb-2">
                <label className="block text-sm font-medium">Placements</label>
                <Tooltip marginTop={2} text="Each placement represents a print area on the product. Choose which artwork to display in each area, and select the decoration method and dimensions for printing. At least one placement must be configured — the rest are optional." />
              </div>
              <div className="grid grid-cols-[repeat(auto-fill,200px)] gap-4">
                {allPlaceholders.map((ph) => {
                  const settings = placementSettings[ph.key] || { source: '', customImageId: null };
                  const carouselImages = getPlacementCarouselImages(ph.key);
                  return (
                    <div key={ph.key} className="p-3 rounded-lg bg-gray-50 dark:bg-gray-700">
                      <div className="w-full aspect-square mb-2 rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
                        {carouselImages.length > 0 ? (
                          <Carousel
                            images={carouselImages}
                            alt={formatPosition(ph.position)}
                            singleImage
                            infiniteScroll
                            imageClassName="!max-h-none w-full h-full object-cover"
                          />
                        ) : (
                          <div className="w-full h-full flex items-center justify-center text-xs text-gray-400">
                            No Image
                          </div>
                        )}
                      </div>
                      <p className="text-sm font-medium mb-2">{formatPosition(ph.position)}</p>
                      {(() => {
                        const dmOptions = ph.decorationMethods.map((d) => ({
                          value: d.method,
                          label: formatDecorationMethod(d.method),
                        }));
                        const selectedDm = settings.decorationMethod || dmOptions[0]?.value || '';
                        const dimOptions = (ph.decorationMethods.find((d) => d.method === selectedDm)?.dimensions || []).map((dim) => ({
                          value: dim,
                          label: dim.replace('x', ' × '),
                        }));
                        const selectedDim = settings.dimensions || dimOptions[0]?.value || '';
                        return (
                          <>
                            <Select
                              name={`placement-dm-${ph.key}`}
                              options={dmOptions}
                              value={selectedDm}
                              onChange={(e) => handlePlacementDecorationMethodChange(ph.key, e.target.value)}
                              className="mb-2 w-full"
                            />
                            <Select
                              name={`placement-dims-${ph.key}`}
                              options={dimOptions}
                              value={selectedDim}
                              onChange={(e) => handlePlacementDimensionsChange(ph.key, e.target.value)}
                              className="mb-2 w-full"
                            />
                          </>
                        );
                      })()}
                      <Select
                        name={`placement-${ph.key}`}
                        options={artworkOptions}
                        value={settings.source === 'item' ? (settings.itemId || '') : (settings.source || '')}
                        onChange={(e) => handlePlacementSourceChange(ph.key, e.target.value)}
                        className="mb-0 w-full"
                      />
                      {settings.source === 'custom' && (
                        <ButtonOutline
                          onClick={() => setCustomImageSelectorTarget({ key: ph.key, itemId: projectItems[0]?.id })}
                          className="mb-0 mt-2 w-full"
                        >
                          <Icon name="image" className="mr-2" />
                          <span>Select</span>
                        </ButtonOutline>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          <hr className="border-gray-200 dark:border-gray-700" />

          <div>
            <div className="flex items-center gap-1 mb-1">
              <label className="block text-sm font-medium">Image Prompt</label>
              <Tooltip marginTop={2} text="Write a prompt to describe the product being displayed in any way — being worn or used by any person you describe. This will be used in your product listing and advertising." />
            </div>
            <TextArea
              name="prompt"
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder="Describe how the product should be displayed..."
              rows={5}
            />
          </div>
        </div>
      ) : (
        <p className="text-sm text-gray-500 dark:text-gray-400">No blueprint data available.</p>
      )}

      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>
          Cancel
        </ButtonOutline>
        <ButtonOutline onClick={handleSave} disabled={saving || loading}>
          {isEditing ? 'Save Changes' : 'Save Blueprint'}
        </ButtonOutline>
      </div>

      <ProductImagePreview
        show={!!previewImage}
        images={detail?.imageCount > 0
          ? Array.from({ length: detail.imageCount }, (_, i) => getBlueprintImageUrl(blueprint.id, i))
          : []}
        alt={detail?.title || ''}
        defaultIndex={previewIndex}
        onClose={() => setPreviewImage(null)}
      />

      {customImageSelectorTarget && (
        <CustomImageSelector
          show={!!customImageSelectorTarget}
          itemId={projectItems[0]?.id}
          projectId={projectId}
          selectedImageId={placementSettings[customImageSelectorTarget.key]?.customImageId}
          onSelect={handleSelectCustomImage}
          onClose={() => setCustomImageSelectorTarget(null)}
        />
      )}
    </Modal>
  );
}
