import React, { useEffect, useMemo, useState } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/admin/printify';
import Modal from '@/components/ui/modal';
import Carousel from '@/components/ui/carousel';
import Select from '@/components/forms/select';
import Checkbox from '@/components/forms/checkbox';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';
import ProductImagePreview from './ProductImagePreview';

export default function ConfigureProductBlueprint({
  show,
  blueprint,
  existingConfig,
  onSave,
  onClose,
}) {
  const session = useSession();
  const { getBlueprintDetail, getBlueprintVariants, getBlueprintImageUrl } = Printify(session);

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

    (async () => {
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
        setVariants(resp.data.data.variants || []);
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
      blueprintId: blueprint.id,
      blueprintTitle: detail?.title || blueprint.title,
      printProviderId: parseInt(selectedProvider),
      variantIds: selectedVariants,
    };

    if (onSave) {
      onSave({
        blueprintId: blueprint.id,
        name: detail?.title || blueprint.title,
        blueprintJson: JSON.stringify(config),
      });
    }
    setSaving(false);
  };

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
              <div
                className="text-sm text-gray-500 dark:text-gray-400"
                dangerouslySetInnerHTML={{ __html: detail.description }}
              />
            )}
          </div>

          <Select
            name="printProvider"
            label="Print Provider"
            placeholder="Select a print provider"
            options={providerOptions}
            value={selectedProvider}
            onChange={handleProviderChange}
          />

          {variants.length > 0 && (
            <div>
              <label className="block text-sm font-medium mb-2">Variants</label>
              <div className="grid grid-cols-4 gap-2">
                {sortedVariants.map((variant) => {
                  const color = variant.options?.color || '';
                  const size = variant.options?.size || '';
                  const label = color || size
                    ? [color, size].filter(Boolean).join(' · ')
                    : variant.title;
                  return (
                    <div
                      key={variant.id}
                      className={`flex items-center p-2 rounded border ${
                        selectedVariants.includes(variant.id)
                          ? 'border-primary-500 bg-primary-50 dark:bg-primary-900/20'
                          : 'border-gray-300 dark:border-gray-600'
                      }`}
                    >
                      <Checkbox
                        name={`variant-${variant.id}`}
                        label={label}
                        checked={selectedVariants.includes(variant.id)}
                        onChange={() => handleVariantToggle(variant.id)}
                        className="mb-0"
                      />
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
    </Modal>
  );
}
