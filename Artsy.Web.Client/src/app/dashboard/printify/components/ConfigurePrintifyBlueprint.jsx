import React, { useEffect, useMemo, useState } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/admin/printify';
import { Printify as PrintifyPublic } from '@/api/user/printify';
import Modal from '@/components/ui/modal';
import Select from '@/components/forms/select';
import SelectChecklist from '@/components/ui/select-checklist';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';

const IMAGE_TYPE_NONE = 0;
const IMAGE_TYPE_BEFORE = 1;
const IMAGE_TYPE_AFTER = 2;

const TYPE_OPTIONS = [
  { value: String(IMAGE_TYPE_NONE), label: 'None' },
  { value: String(IMAGE_TYPE_BEFORE), label: 'Before Artwork Applied' },
  { value: String(IMAGE_TYPE_AFTER), label: 'After Artwork Applied' },
];

const POSITION_NONE = 0;
const POSITION_FRONT = 1;
const POSITION_BACK = 2;
const POSITION_TOP = 3;
const POSITION_BOTTOM = 4;
const POSITION_LEFT = 5;
const POSITION_RIGHT = 6;

const POSITION_OPTIONS = [
  { value: String(POSITION_NONE), label: 'None' },
  { value: String(POSITION_FRONT), label: 'Front' },
  { value: String(POSITION_BACK), label: 'Back' },
  { value: String(POSITION_TOP), label: 'Top' },
  { value: String(POSITION_BOTTOM), label: 'Bottom' },
  { value: String(POSITION_LEFT), label: 'Left Side' },
  { value: String(POSITION_RIGHT), label: 'Right Side' },
];

export default function ConfigurePrintifyBlueprint({ show, blueprint, onClose, onSave }) {
  const session = useSession();
  const {
    getBlueprintDetail,
    getBlueprintVariants,
    getBlueprintImageUrl,
    getBlueprintImages,
    saveBlueprintImages,
  } = Printify(session);
  const { getVariantAvailability } = PrintifyPublic(session);

  const [detail, setDetail] = useState(null);
  const [printProviders, setPrintProviders] = useState([]);
  const [variants, setVariants] = useState([]);
  const [selectedProvider, setSelectedProvider] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [published, setPublished] = useState(false);
  const [imageSettings, setImageSettings] = useState({});
  const [initialImageSettings, setInitialImageSettings] = useState({});
  const [descriptionExpanded, setDescriptionExpanded] = useState(false);
  const [outOfStockIds, setOutOfStockIds] = useState(new Set());

  useEffect(() => {
    if (!show || !blueprint) return;
    setLoading(true);
    setMessage(null);
    setDetail(null);
    setPrintProviders([]);
    setVariants([]);
    setSelectedProvider('');
    setPublished(false);
    setImageSettings({});
    setInitialImageSettings({});
    setDescriptionExpanded(false);
    setOutOfStockIds(new Set());

    (async () => {
      try {
        const resp = await getBlueprintDetail(blueprint.id);
        if (resp.data.success) {
          const data = resp.data.data;
          setDetail(data.blueprint);
          setPublished(data.blueprint.published || false);
          setPrintProviders(data.printProviders || []);

          if (data.printProviders?.length > 0) {
            const firstProvider = String(data.printProviders[0].id);
            setSelectedProvider(firstProvider);
            await loadVariants(blueprint.id, data.printProviders[0].id);
          }
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to load blueprint' });
        }

        const imagesResp = await getBlueprintImages(blueprint.id);
        if (imagesResp.data.success) {
          const settings = {};
          for (const img of imagesResp.data.data || []) {
            settings[img.imageIndex] = {
              variantIds: (img.variants || []).map(String),
              type: String(img.type),
              position: String(img.position ?? POSITION_FRONT),
            };
          }
          setImageSettings(settings);
          setInitialImageSettings(JSON.parse(JSON.stringify(settings)));
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
    setVariants([]);
    setOutOfStockIds(new Set());
    if (providerId && blueprint) {
      await loadVariants(blueprint.id, parseInt(providerId));
    }
  };

  const handleImageSettingChange = (index, field, value) => {
    setImageSettings((prev) => ({
      ...prev,
      [index]: {
        variantIds: field === 'variantIds' ? value : (prev[index]?.variantIds || []),
        type: field === 'type' ? value : (prev[index]?.type || '0'),
        position: field === 'position' ? value : (prev[index]?.position || String(POSITION_FRONT)),
      },
    }));
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
      const phs = variant.placeholders || [];
      for (const ph of phs) {
        const cleanPosition = formatPosition(ph.position);
        const groupKey = `${cleanPosition}|${ph.width}|${ph.height}`;
        if (!groups.has(groupKey)) {
          groups.set(groupKey, {
            key: groupKey,
            label: cleanPosition,
            position: ph.position,
            decorationMethods: new Set(),
            height: ph.height,
            width: ph.width,
          });
        }
        if (ph.decoration_method) {
          groups.get(groupKey).decorationMethods.add(ph.decoration_method);
        }
      }
    }
    return Array.from(groups.values()).map((g) => ({
      ...g,
      decorationMethods: Array.from(g.decorationMethods),
    })).sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true, sensitivity: 'base' }));
  }, [variants]);

  const variantOptions = useMemo(() => {
    return variants.map((v) => {
      const color = v.options?.color || '';
      const size = v.options?.size || '';
      const label = color || size
        ? [color, size].filter(Boolean).join(' · ')
        : v.title;
      return { value: String(v.id), label };
    }).sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true, sensitivity: 'base' }));
  }, [variants]);

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

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const images = [];
      if (detail?.imageCount > 0) {
        for (let i = 0; i < detail.imageCount; i++) {
          const settings = imageSettings[i] || { variantIds: [], type: '0', position: String(POSITION_FRONT) };
          images.push({
            imageIndex: i,
            variants: (settings.variantIds || []).map((v) => parseInt(v) || 0),
            type: parseInt(settings.type) || 0,
            position: parseInt(settings.position) || 0,
          });
        }
      }

      await saveBlueprintImages(blueprint.id, { images, published });
      if (onSave) onSave();
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save' });
    } finally {
      setSaving(false);
    }
  };

  const allImagesHaveVariants = useMemo(() => {
    if (!detail?.imageCount || detail.imageCount === 0) return false;
    for (let i = 0; i < detail.imageCount; i++) {
      const settings = imageSettings[i];
      if (!settings || !settings.variantIds || settings.variantIds.length === 0) return false;
    }
    return true;
  }, [detail, imageSettings]);

  const hasSettingsChanged = useMemo(() => {
    if (!detail?.imageCount || detail.imageCount === 0) return false;
    for (let i = 0; i < detail.imageCount; i++) {
      const current = imageSettings[i] || { variantIds: [], type: '0', position: String(POSITION_FRONT) };
      const initial = initialImageSettings[i] || { variantIds: [], type: '0', position: String(POSITION_FRONT) };
      const currentVariants = (current.variantIds || []).slice().sort().join(',');
      const initialVariants = (initial.variantIds || []).slice().sort().join(',');
      if (currentVariants !== initialVariants) return true;
      if ((current.type || '0') !== (initial.type || '0')) return true;
      if ((current.position || String(POSITION_FRONT)) !== (initial.position || String(POSITION_FRONT))) return true;
    }
    return false;
  }, [detail, imageSettings, initialImageSettings]);

  const handlePublish = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const images = [];
      if (detail?.imageCount > 0) {
        for (let i = 0; i < detail.imageCount; i++) {
          const settings = imageSettings[i] || { variantIds: [], type: '0', position: String(POSITION_FRONT) };
          images.push({
            imageIndex: i,
            variants: (settings.variantIds || []).map((v) => parseInt(v) || 0),
            type: parseInt(settings.type) || 0,
            position: parseInt(settings.position) || 0,
          });
        }
      }

      await saveBlueprintImages(blueprint.id, { images, published: true });
      setPublished(true);
      setInitialImageSettings(JSON.parse(JSON.stringify(imageSettings)));
      if (onSave) onSave();
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to publish' });
    } finally {
      setSaving(false);
    }
  };

  const handleUnpublish = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const images = [];
      if (detail?.imageCount > 0) {
        for (let i = 0; i < detail.imageCount; i++) {
          const settings = imageSettings[i] || { variantIds: [], type: '0', position: String(POSITION_FRONT) };
          images.push({
            imageIndex: i,
            variants: (settings.variantIds || []).map((v) => parseInt(v) || 0),
            type: parseInt(settings.type) || 0,
            position: parseInt(settings.position) || 0,
          });
        }
      }

      await saveBlueprintImages(blueprint.id, { images, published: false });
      setPublished(false);
      if (onSave) onSave();
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to unpublish' });
    } finally {
      setSaving(false);
    }
  };

  if (!show) return null;

  const providerOptions = printProviders.map((p) => ({
    value: String(p.id),
    label: p.title,
  }));

  return (
    <Modal
      title="Configure Blueprint"
      onClose={onClose}
      top
      className="min-w-[50em] max-w-full"
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
        <div className="max-h-[60vh] overflow-y-auto space-y-4 p-2">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-medium">{detail.title}</h3>
            {published && (
              <span className="px-2 py-0.5 rounded text-xs font-bold bg-green-500 text-white whitespace-nowrap">
                Published
              </span>
            )}
          </div>

          <div className="space-y-1">
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
            placeholder="Select a print provider"
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
                  return (
                    <div key={group.color}>
                      <label className="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">{group.color}</label>
                      <SelectChecklist
                        name={`color-variants-${group.color}`}
                        options={options}
                        values={[]}
                        checkboxes={false}
                        placeholder="View sizes"
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
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-200 dark:border-gray-700">
                      <th className="text-left py-2 px-3">Position</th>
                      <th className="text-left py-2 px-3">Decoration Method</th>
                      <th className="text-right py-2 px-3">Width</th>
                      <th className="text-right py-2 px-3">Height</th>
                    </tr>
                  </thead>
                  <tbody>
                    {allPlaceholders.map((ph) => {
                      return (
                        <tr key={ph.key} className="border-b border-gray-100 dark:border-gray-700">
                          <td className="py-2 px-3">{ph.label}</td>
                          <td className="py-2 px-3 text-gray-500 dark:text-gray-400">
                            {ph.decorationMethods.length > 0
                              ? ph.decorationMethods.map(formatDecorationMethod).join(', ')
                              : '—'}
                          </td>
                          <td className="py-2 px-3 text-right text-gray-500 dark:text-gray-400">{ph.width}px</td>
                          <td className="py-2 px-3 text-right text-gray-500 dark:text-gray-400">{ph.height}px</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          <hr className="border-gray-200 dark:border-gray-700" />

          {detail.imageCount > 0 && (
            <div>
              <label className="block text-sm font-medium mb-2">Images</label>
              <div className="grid grid-cols-4 gap-4">
                {Array.from({ length: detail.imageCount }, (_, i) => {
                  const settings = imageSettings[i] || { variantIds: [], type: '0', position: String(POSITION_FRONT) };
                  return (
                    <div key={i} className="rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
                      <img
                        src={getBlueprintImageUrl(blueprint.id, i)}
                        alt={`${detail.title} ${i + 1}`}
                        className="w-full aspect-square object-cover"
                      />
                      <div className="p-2 space-y-2">
                        <SelectChecklist
                          name={`img-variant-${i}`}
                          options={variantOptions}
                          values={settings.variantIds || []}
                          onChange={(vals) => handleImageSettingChange(i, 'variantIds', vals)}
                          placeholder="Variants"
                        />
                        <Select
                          name={`img-type-${i}`}
                          options={TYPE_OPTIONS}
                          value={settings.type}
                          onChange={(e) => handleImageSettingChange(i, 'type', e.target.value)}
                          className="mb-0"
                        />
                        <Select
                          name={`img-position-${i}`}
                          options={POSITION_OPTIONS}
                          value={settings.position || String(POSITION_FRONT)}
                          onChange={(e) => handleImageSettingChange(i, 'position', e.target.value)}
                          className="mb-0"
                        />
                      </div>
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

      <div className="buttons flex justify-between gap-2 mt-4">
        <div className="flex gap-2">
          {allImagesHaveVariants && (
            <button
              type="button"
              onClick={handlePublish}
              disabled={saving || loading}
            >
              {published && hasSettingsChanged ? 'Publish Changes' : 'Publish'}
            </button>
          )}
          {published && (
            <button
              type="button"
              onClick={handleUnpublish}
              disabled={saving || loading}
              className="cancel"
            >
              Unpublish
            </button>
          )}
        </div>
        <div className="flex gap-2">
          <ButtonOutline className="cancel" onClick={onClose}>
            Cancel
          </ButtonOutline>
          <ButtonOutline onClick={handleSave} disabled={saving || loading}>
            {saving ? 'Saving...' : 'Save Changes'}
          </ButtonOutline>
        </div>
      </div>
    </Modal>
  );
}
