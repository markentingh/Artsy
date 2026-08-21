import React, { useMemo, useEffect, useRef, useState } from 'react';
import { usePrintifyBlueprint, TYPE_OPTIONS, POSITION_OPTIONS, POSITION_FRONT } from '@/context/printifyBlueprint';
import Select from '@/components/forms/select';
import SelectChecklist from '@/components/ui/select-checklist';
import ButtonOutline from '@/components/ui/button-outline';
import PrintifyColorsWizard from '@/app/dashboard/printify/components/PrintifyColorsWizard';

export default function ImagesTab() {
  const {
    detail, blueprint, imageSettings, handleImageSettingChange,
    variants, outOfStockIds, api, selectedProvider, handleProviderChange,
  } = usePrintifyBlueprint();

  const hasAutoChecked = useRef(false);

  const variantColorOptions = useMemo(() => {
    const colorMap = new Map();
    for (const v of variants) {
      const color = v.color || 'Default';
      if (!colorMap.has(color)) {
        const allOutOfStock = variants
          .filter(va => (va.color || 'Default') === color)
          .every(va => outOfStockIds.has(va.id));
        colorMap.set(color, {
          value: color,
          label: color,
          hexColor: v.hexColor,
          note: allOutOfStock ? { text: 'Out of Stock', type: 'red' } : null,
        });
      }
    }
    // also include colors already applied to any image so they appear in the dropdown
    for (const idx of Object.keys(imageSettings)) {
      const colors = imageSettings[idx]?.variantColors;
      if (!Array.isArray(colors)) continue;
      for (const color of colors) {
        if (!color || typeof color !== 'string' || colorMap.has(color)) continue;
        colorMap.set(color, {
          value: color,
          label: color,
          hexColor: '',
          note: null,
        });
      }
    }
    return Array.from(colorMap.values()).sort((a, b) =>
      a.label.localeCompare(b.label, undefined, { numeric: true, sensitivity: 'base' })
    );
  }, [variants, outOfStockIds, imageSettings]);

  useEffect(() => {
    if (hasAutoChecked.current) return;
    hasAutoChecked.current = true;
    if (variantColorOptions.length !== 1 || !detail?.imageCount) return;
    const color = variantColorOptions[0].value;
    for (let i = 0; i < detail.imageCount; i++) {
      const settings = imageSettings[i] || {};
      if (!settings.variantColors || settings.variantColors.length === 0) {
        handleImageSettingChange(i, 'variantColors', [color]);
      }
    }
  }, [variantColorOptions, detail?.imageCount, imageSettings, handleImageSettingChange]);

  const [showColorsWizard, setShowColorsWizard] = useState(false);

  if (!detail || detail.imageCount === 0) return null;

  const allColorsChecked = useMemo(() => {
    if (!detail?.imageCount || variantColorOptions.length === 0) return false;
    const allValues = new Set(variantColorOptions.map((o) => o.value));
    for (let i = 0; i < detail.imageCount; i++) {
      const selected = imageSettings[i]?.variantColors || [];
      if (selected.length < allValues.size || !selected.every((v) => allValues.has(v))) return false;
    }
    return true;
  }, [detail?.imageCount, variantColorOptions, imageSettings]);

  const handleToggleAllColors = () => {
    if (allColorsChecked) {
      for (let i = 0; i < detail.imageCount; i++) {
        handleImageSettingChange(i, 'variantColors', []);
      }
    } else {
      const allValues = variantColorOptions.map((o) => o.value);
      for (let i = 0; i < detail.imageCount; i++) {
        handleImageSettingChange(i, 'variantColors', allValues);
      }
    }
  };

  const refreshImages = async () => {
    try {
      const resp = await api.getBlueprintImages(blueprint.id);
      if (resp.data.success) {
        for (const img of resp.data.data || []) {
          const i = img.imageIndex;
          handleImageSettingChange(i, 'variantColors', img.variantColors || []);
          handleImageSettingChange(i, 'type', String(img.type ?? 0));
          handleImageSettingChange(i, 'position', String(img.position ?? POSITION_FRONT));
        }
      }
    } catch (error) {
      // ignore
    }
  };

  const handleColorsComplete = async () => {
    await refreshImages();
    if (selectedProvider) await handleProviderChange(selectedProvider);
    setShowColorsWizard(false);
  };

  if (showColorsWizard) {
    return (
      <PrintifyColorsWizard
        blueprintId={blueprint.id}
        onComplete={handleColorsComplete}
        onCancel={() => setShowColorsWizard(false)}
      />
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <label className="block text-sm font-medium">Images</label>
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={handleToggleAllColors}
            className="text-sm text-primary-600 dark:text-primary-400 hover:underline"
          >
            {allColorsChecked ? 'Uncheck All' : 'Check All'}
          </button>
          <ButtonOutline
            onClick={() => setShowColorsWizard(true)}
            size="small"
          >
            Refresh Colors
          </ButtonOutline>
        </div>
      </div>
      <div className="grid grid-cols-[repeat(auto-fill,300px)] gap-4">
        {Array.from({ length: detail.imageCount }, (_, i) => {
          const settings = imageSettings[i] || { variantColors: [], type: '0', position: String(POSITION_FRONT) };
          return (
            <div key={i} className="rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
              <img
                src={api.getBlueprintImageUrl(blueprint.id, i)}
                alt={`${detail.title} ${i + 1}`}
                className="w-full aspect-square object-cover"
              />
              <div className="p-2 space-y-2">
                <SelectChecklist
                  name={`img-variantColors-${i}`}
                  options={variantColorOptions}
                  values={settings.variantColors || []}
                  onChange={(vals) => handleImageSettingChange(i, 'variantColors', vals)}
                  placeholder="Variant Colors"
                  checkboxes={true}
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
  );
}
