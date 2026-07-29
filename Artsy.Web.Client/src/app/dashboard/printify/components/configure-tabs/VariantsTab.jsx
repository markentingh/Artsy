import React, { useMemo } from 'react';
import { usePrintifyBlueprint } from '@/context/printifyBlueprint';
import SelectChecklist from '@/components/ui/select-checklist';

export default function VariantsTab() {
  const { variants, outOfStockIds } = usePrintifyBlueprint();

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

  if (variantsByColor.length === 0) return null;

  return (
    <div>
      <label className="block text-sm font-medium mb-2">Variants</label>
      <div className="grid grid-cols-3 gap-4">
        {variantsByColor.map((group) => {
          const options = group.variants.map((v) => {
            const size = v.size || v.color;
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
  );
}
