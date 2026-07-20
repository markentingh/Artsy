import React, { useEffect, useMemo, useState } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/admin/printify';
import { Printify as PrintifyPublic } from '@/api/user/printify';
import { Projects } from '@/api/user/projects';
import Modal from '@/components/ui/modal';
import Carousel from '@/components/ui/carousel';
import Select from '@/components/forms/select';
import SelectChecklist from '@/components/ui/select-checklist';
import Checkbox from '@/components/forms/checkbox';
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
  const { getBlueprintDetail, getBlueprintVariants, getBlueprintImageUrl } = Printify(session);
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

  const artworkOptions = useMemo(() => {
    return [
      { value: '', label: 'None' },
      { value: 'custom', label: 'Custom Image' },
      ...projectItems.map((item) => ({
        value: `item:${item.id}`,
        label: item.title || 'Untitled Artwork',
      })),
    ];
  }, [projectItems]);

  const handlePlacementSourceChange = (key, value) => {
    setPlacementSettings((prev) => ({
      ...prev,
      [key]: {
        ...prev[key],
        source: value,
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
          dimensions: availableDims.length === 1 ? availableDims[0] : '',
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
      [key]: { ...prev[key], source: 'custom', customImageId: img.id, customItemId: itemId },
    }));
    setCustomImageSelectorTarget(null);
  };

  const getPlacementCarouselImages = (key) => {
    const settings = placementSettings[key];
    if (!settings) return [];
    if (settings.source === 'custom' && settings.customImageId && settings.customItemId) {
      return [getItemReferenceUrl(settings.customItemId, settings.customImageId, true)];
    }
    if (settings.source && settings.source.startsWith('item:')) {
      const itemId = settings.source.substring(5);
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

  const sortedVariants = useMemo(() => {
    if (variants.length === 0) return [];
    const sizeOrder = ['XS', 'S', 'M', 'L', 'XL', '2XL', '3XL', '4XL', '5XL'];
    return [...variants].sort((a, b) => {
      const aInitial = initialVariantIds.includes(a.id) ? 0 : 1;
      const bInitial = initialVariantIds.includes(b.id) ? 0 : 1;
      if (aInitial !== bInitial) return aInitial - bInitial;
      const aColor = a.options?.color || '';
      const bColor = b.options?.color || '';
      if (aColor !== bColor) return aColor.localeCompare(bColor);
      const aSize = a.options?.size || '';
      const bSize = b.options?.size || '';
      const aIdx = sizeOrder.indexOf(aSize);
      const bIdx = sizeOrder.indexOf(bSize);
      if (aIdx !== -1 && bIdx !== -1) return aIdx - bIdx;
      if (aIdx !== -1) return -1;
      if (bIdx !== -1) return 1;
      return aSize.localeCompare(bSize);
    });
  }, [variants, initialVariantIds]);

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
        <div className="max-h-[70vh] overflow-y-auto space-y-4">
          {detail.imageCount > 0 && (
            <Carousel
              images={Array.from({ length: detail.imageCount }, (_, i) => getBlueprintImageUrl(blueprint.id, i))}
              alt={detail.title}
              onImageClick={(src, i) => { setPreviewImage(src); setPreviewIndex(i); }}
            />
          )}

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

          <Select
            name="printProvider"
            label="Print Provider"
            options={providerOptions}
            value={selectedProvider}
            onChange={handleProviderChange}
            className="max-w-xs"
          />

          <hr className="border-gray-200 dark:border-gray-700" />

          {variantsByColor.length > 0 && (
            <div>
              <label className="block text-sm font-medium mb-2">Variants</label>
              <div className="grid grid-cols-3 gap-4">
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
                  return (
                    <div key={group.color}>
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
              <label className="block text-sm font-medium mb-2">Placements</label>
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
                        value={settings.source || ''}
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
