import React from 'react';
import { useProductBlueprint } from '@/context/productBlueprint';
import Icon from '@/components/ui/icon';
import { List, Item } from '@/components/ui/list';

export default function SetupChecklist() {
  const {
    productName,
    productDescription,
    selectedVariants,
    placementSettings,
    productBlueprintImages,
    variantPrices,
  } = useProductBlueprint();

  const hasTitleAndDescription = !!(productName?.trim() && productDescription?.trim());
  const hasVariants = selectedVariants.length > 0;
  const hasPricing = selectedVariants.length > 0 && selectedVariants.every(id => {
    const price = parseFloat(variantPrices[id]);
    return !isNaN(price) && price > 0;
  });
  const hasPlacements = Object.values(placementSettings).some(p => p && p.source);
  const hasProductImages = productBlueprintImages.length > 0 && productBlueprintImages.every(img => !!(img.prompt && img.prompt.trim()));

  const items = [
    { label: 'Fill out Title & Description', checked: hasTitleAndDescription },
    { label: 'Configure one or more Variants', checked: hasVariants },
    { label: 'Set up Pricing for all selected Variants', checked: hasPricing },
    { label: 'Configure one or more Image Placements', checked: hasPlacements },
    { label: 'Set up one or more Product Images per Variant', checked: hasProductImages },
  ];

  return (
    <div className="w-[400px] shrink-0">
      <h4 className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Setup Checklist</h4>
      <List inModal>
        {items.map((item, i) => (
          <Item key={i} className="justify-start gap-2 text-sm">
            <Icon
              name={item.checked ? 'check_circle' : 'radio_button_unchecked'}
              className={item.checked
                ? 'text-green-500'
                : 'text-gray-400 dark:text-gray-500'}
            />
            <span className={item.checked
              ? 'text-gray-500 dark:text-gray-400'
              : 'text-gray-700 dark:text-gray-300'}>
              {item.label}
            </span>
          </Item>
        ))}
      </List>
    </div>
  );
}
