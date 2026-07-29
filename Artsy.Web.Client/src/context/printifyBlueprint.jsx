import React, { createContext, useContext, useState, useMemo, useCallback, useEffect, useRef } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/admin/printify';
import { Printify as PrintifyPublic } from '@/api/user/printify';

const PrintifyBlueprintContext = createContext(null);

export const IMAGE_TYPE_NONE = 0;
export const IMAGE_TYPE_BEFORE = 1;
export const IMAGE_TYPE_AFTER = 2;
export const IMAGE_TYPE_FOR_PRODUCT_IMAGE = 3;

export const TYPE_OPTIONS = [
  { value: String(IMAGE_TYPE_NONE), label: 'None' },
  { value: String(IMAGE_TYPE_BEFORE), label: 'Before Artwork Applied' },
  { value: String(IMAGE_TYPE_AFTER), label: 'After Artwork Applied' },
  { value: String(IMAGE_TYPE_FOR_PRODUCT_IMAGE), label: 'For Product Image' },
];

export const POSITION_NONE = 0;
export const POSITION_FRONT = 1;
export const POSITION_BACK = 2;
export const POSITION_TOP = 3;
export const POSITION_BOTTOM = 4;
export const POSITION_LEFT = 5;
export const POSITION_RIGHT = 6;

export const POSITION_OPTIONS = [
  { value: String(POSITION_NONE), label: 'None' },
  { value: String(POSITION_FRONT), label: 'Front' },
  { value: String(POSITION_BACK), label: 'Back' },
  { value: String(POSITION_TOP), label: 'Top' },
  { value: String(POSITION_BOTTOM), label: 'Bottom' },
  { value: String(POSITION_LEFT), label: 'Left Side' },
  { value: String(POSITION_RIGHT), label: 'Right Side' },
];

export function PrintifyBlueprintProvider({ children, show, blueprint, onClose, onSave }) {
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
  const [imagePrompt, setImagePrompt] = useState('');
  const [initialImagePrompt, setInitialImagePrompt] = useState('');
  const scrollRef = useRef(null);
  const [scrollMaxHeight, setScrollMaxHeight] = useState('none');

  const api = useMemo(() => ({
    getBlueprintDetail, getBlueprintVariants, getBlueprintImageUrl,
    getBlueprintImages, saveBlueprintImages, getVariantAvailability,
  }), [getBlueprintDetail, getBlueprintVariants, getBlueprintImageUrl, getBlueprintImages, saveBlueprintImages, getVariantAvailability]);

  const loadVariants = useCallback(async (blueprintId, printProviderId) => {
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

        return variantList;
      }
      return [];
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to load variants' });
      return [];
    }
  }, [getBlueprintVariants, getVariantAvailability]);

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
    setImagePrompt('');
    setInitialImagePrompt('');

    (async () => {
      try {
        const resp = await getBlueprintDetail(blueprint.id);
        if (resp.data.success) {
          const data = resp.data.data;
          setDetail(data.blueprint);
          setPublished(data.blueprint.published || false);
          setImagePrompt(data.blueprint.imagePrompt || '');
          setInitialImagePrompt(data.blueprint.imagePrompt || '');
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
            const idx = img.imageIndex;
            if (!settings[idx]) {
              settings[idx] = {
                variantColors: [],
                type: String(img.type),
                position: String(img.position ?? POSITION_FRONT),
              };
            }
            if (img.variantColors) {
              settings[idx].variantColors.push(...img.variantColors);
            }
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

  const handleProviderChange = useCallback(async (providerId) => {
    setSelectedProvider(String(providerId));
    setVariants([]);
    setOutOfStockIds(new Set());
    if (providerId && blueprint) {
      await loadVariants(blueprint.id, parseInt(providerId));
    }
  }, [blueprint, loadVariants]);

  const handleImageSettingChange = useCallback((index, field, value) => {
    setImageSettings((prev) => ({
      ...prev,
      [index]: {
        variantColors: field === 'variantColors' ? value : (prev[index]?.variantColors || []),
        type: field === 'type' ? value : (prev[index]?.type || '0'),
        position: field === 'position' ? value : (prev[index]?.position || String(POSITION_FRONT)),
      },
    }));
  }, []);

  const hasSettingsChanged = useMemo(() => {
    if (!detail?.imageCount || detail.imageCount === 0) return false;
    for (let i = 0; i < detail.imageCount; i++) {
      const current = imageSettings[i] || { variantColors: [], type: '0', position: String(POSITION_FRONT) };
      const initial = initialImageSettings[i] || { variantColors: [], type: '0', position: String(POSITION_FRONT) };
      const currentColors = [...(current.variantColors || [])].sort().join(',');
      const initialColors = [...(initial.variantColors || [])].sort().join(',');
      if (currentColors !== initialColors) return true;
      if ((current.type || '0') !== (initial.type || '0')) return true;
      if ((current.position || String(POSITION_FRONT)) !== (initial.position || String(POSITION_FRONT))) return true;
    }
    if ((imagePrompt || '') !== (initialImagePrompt || '')) return true;
    return false;
  }, [detail, imageSettings, initialImageSettings, imagePrompt, initialImagePrompt]);

  const allImagesHaveVariants = useMemo(() => {
    if (!detail?.imageCount || detail.imageCount === 0) return false;
    for (let i = 0; i < detail.imageCount; i++) {
      const settings = imageSettings[i];
      if (!settings || !settings.variantColors || settings.variantColors.length === 0) return false;
    }
    return true;
  }, [detail, imageSettings]);

  const buildImagesPayload = useCallback(() => {
    const images = [];
    if (detail?.imageCount > 0) {
      for (let i = 0; i < detail.imageCount; i++) {
        const settings = imageSettings[i] || { variantColors: [], type: '0', position: String(POSITION_FRONT) };
        images.push({
          imageIndex: i,
          variantColors: settings.variantColors || [],
          type: parseInt(settings.type) || 0,
          position: parseInt(settings.position) || 0,
        });
      }
    }
    return images;
  }, [detail, imageSettings]);

  const handleSave = useCallback(async () => {
    setSaving(true);
    setMessage(null);
    try {
      const images = buildImagesPayload();
      await saveBlueprintImages(blueprint.id, { images, published, imagePrompt });
      setInitialImageSettings(JSON.parse(JSON.stringify(imageSettings)));
      setInitialImagePrompt(imagePrompt);
      if (onSave) onSave();
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save' });
    } finally {
      setSaving(false);
    }
  }, [blueprint, buildImagesPayload, saveBlueprintImages, published, imagePrompt, imageSettings, onSave]);

  const handlePublish = useCallback(async () => {
    setSaving(true);
    setMessage(null);
    try {
      const images = buildImagesPayload();
      await saveBlueprintImages(blueprint.id, { images, published: true, imagePrompt });
      setPublished(true);
      setInitialImageSettings(JSON.parse(JSON.stringify(imageSettings)));
      setInitialImagePrompt(imagePrompt);
      if (onSave) onSave();
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to publish' });
    } finally {
      setSaving(false);
    }
  }, [blueprint, buildImagesPayload, saveBlueprintImages, imagePrompt, imageSettings, onSave]);

  const handleUnpublish = useCallback(async () => {
    setSaving(true);
    setMessage(null);
    try {
      const images = buildImagesPayload();
      await saveBlueprintImages(blueprint.id, { images, published: false, imagePrompt });
      setPublished(false);
      if (onSave) onSave();
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to unpublish' });
    } finally {
      setSaving(false);
    }
  }, [blueprint, buildImagesPayload, saveBlueprintImages, imagePrompt, onSave]);

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

  const value = {
    show, blueprint, onClose, onSave,
    detail, printProviders, variants, selectedProvider,
    loading, saving, message, setMessage,
    published, imageSettings, initialImageSettings,
    descriptionExpanded, setDescriptionExpanded,
    outOfStockIds, scrollRef, scrollMaxHeight,
    imagePrompt, setImagePrompt, initialImagePrompt,
    api,
    handleProviderChange, handleImageSettingChange,
    hasSettingsChanged, allImagesHaveVariants,
    handleSave, handlePublish, handleUnpublish,
  };

  return (
    <PrintifyBlueprintContext.Provider value={value}>
      {children}
    </PrintifyBlueprintContext.Provider>
  );
}

export function usePrintifyBlueprint() {
  const ctx = useContext(PrintifyBlueprintContext);
  if (!ctx) throw new Error('usePrintifyBlueprint must be used within PrintifyBlueprintProvider');
  return ctx;
}
