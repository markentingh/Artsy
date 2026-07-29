import React from 'react';
import Tooltip from '@/components/ui/tooltip';
import { List, Item } from '@/components/ui/list';
import { useProductBlueprint } from '@/context/productBlueprint';

export default function PricingTab() {
  const { variants, selectedVariants, variantPrices, setVariantPrices } = useProductBlueprint();

  if (variants.length === 0) {
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400">No variants available. Select a print provider to load variants.</p>
    );
  }

  return (
    <div className="pb-4">
      <div className="flex items-center gap-1 mb-1">
        <label className="block text-sm font-medium">Variant Pricing</label>
        <Tooltip marginTop={2} text="Each variant can be configured to have its own price. Take into account that these prices do not include shipping costs. If you provide free shipping within your online shop, you may want to include shipping costs within the variant price itself." />
      </div>

      <List inModal>
        <Item bg={false} hover={false}>
          <div className="flex items-center justify-between w-full">
            <span className="text-sm font-medium"></span>
            <div className="flex items-center gap-1">
              <span className="text-sm text-gray-500 mr-5">Change All Variants</span>
              <span className="text-sm text-gray-500">$</span>
              <input
                type="number"
                min="0"
                step="0.01"
                placeholder="0.00"
                className="w-24 px-2 py-1 text-right border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-primary-500"
                onChange={(e) => {
                  const val = e.target.value;
                  const newPrices = {};
                  variants.forEach(v => { newPrices[v.id] = val; });
                  setVariantPrices(newPrices);
                }}
              />
            </div>
          </div>
        </Item>
        {[...variants]
          .filter(v => selectedVariants.includes(v.id))
          .sort((a, b) => {
            const aColor = a.title || 'Default';
            const bColor = b.title || 'Default';
            if (aColor !== bColor) return aColor.localeCompare(bColor);
            const sizeOrder = ['XS', 'S', 'M', 'L', 'XL', '2XL', '3XL', '4XL', '5XL'];
            const aSize = a.size || '';
            const bSize = b.size || '';
            const aIdx = sizeOrder.indexOf(aSize);
            const bIdx = sizeOrder.indexOf(bSize);
            if (aIdx !== -1 && bIdx !== -1) return aIdx - bIdx;
            if (aIdx !== -1) return -1;
            if (bIdx !== -1) return 1;
            return aSize.localeCompare(bSize);
          })
          .map((v) => {
            const color = v.color || 'Default';
            const size = v.size || v.color;
            return (
              <Item key={v.id}>
                <div className="flex items-center justify-between w-full">
                  <span className="text-sm">{color} - {size}</span>
                  <div className="flex items-center gap-1">
                    <span className="text-sm text-gray-500">$</span>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      placeholder="0.00"
                      value={variantPrices[v.id] || ''}
                      onChange={(e) => {
                        setVariantPrices(prev => ({ ...prev, [v.id]: e.target.value }));
                      }}
                      className="w-24 px-2 py-1 text-right border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-primary-500"
                    />
                  </div>
                </div>
              </Item>
            );
          })}
      </List>
    </div>
  );
}
