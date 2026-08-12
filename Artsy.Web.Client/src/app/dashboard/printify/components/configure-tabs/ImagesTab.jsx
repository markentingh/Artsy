import React, { useMemo, useEffect, useRef } from 'react';
import { usePrintifyBlueprint, TYPE_OPTIONS, POSITION_OPTIONS, POSITION_FRONT } from '@/context/printifyBlueprint';
import Select from '@/components/forms/select';
import SelectChecklist from '@/components/ui/select-checklist';

export default function ImagesTab() {
  const {
    detail, blueprint, imageSettings, handleImageSettingChange,
    variants, outOfStockIds, api,
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
    return Array.from(colorMap.values()).sort((a, b) =>
      a.label.localeCompare(b.label, undefined, { numeric: true, sensitivity: 'base' })
    );
  }, [variants, outOfStockIds]);

  useEffect(() => {
    if (hasAutoChecked.current || variantColorOptions.length !== 1 || !detail?.imageCount) return;
    hasAutoChecked.current = true;
    const color = variantColorOptions[0].value;
    for (let i = 0; i < detail.imageCount; i++) {
      const settings = imageSettings[i] || {};
      if (!settings.variantColors || settings.variantColors.length === 0) {
        handleImageSettingChange(i, 'variantColors', [color]);
      }
    }
  }, [variantColorOptions, detail?.imageCount, imageSettings, handleImageSettingChange]);

  if (!detail || detail.imageCount === 0) return null;

  const handleUncheckAll = () => {
    for (let i = 0; i < detail.imageCount; i++) {
      handleImageSettingChange(i, 'variantColors', []);
    }
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <label className="block text-sm font-medium">Images</label>
        <button
          type="button"
          onClick={handleUncheckAll}
          className="text-sm text-primary-600 dark:text-primary-400 hover:underline"
        >
          Uncheck All
        </button>
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
