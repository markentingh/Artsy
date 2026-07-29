import React from 'react';
import Carousel from '@/components/ui/carousel';
import Tooltip from '@/components/ui/tooltip';
import Select from '@/components/forms/select';
import SelectChecklist from '@/components/ui/select-checklist';
import { useProductBlueprint } from '@/context/productBlueprint';

export default function VariantsTab() {
  const {
    blueprint,
    detail,
    variants,
    setVariants,
    selectedProvider,
    setSelectedProvider,
    selectedVariants,
    setSelectedVariants,
    outOfStockIds,
    setOutOfStockIds,
    variantsByColor,
    imagesByColor,
    providerOptions,
    loadVariants,
    setMessage,
    setPreviewImage,
    setPreviewIndex,
    getBlueprintImageUrl,
  } = useProductBlueprint();

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

  const handleColorVariantsChange = (color, values) => {
    setSelectedVariants((prev) => {
      const colorVariantIds = variantsByColor.find((g) => g.color === color)?.variants.map((v) => String(v.id)) || [];
      const otherIds = prev.filter((id) => !colorVariantIds.includes(String(id)));
      return [...otherIds, ...values.map(Number)];
    });
  };

  if (variantsByColor.length === 0) {
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400">No variants available. Select a print provider to load variants.</p>
    );
  }

  return (
    <div>
      <div className="max-w-xs mb-4">
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

      <div className="flex items-center gap-1 mb-2">
        <label className="block text-sm font-medium">Variants</label>
        <Tooltip marginTop={2} text="Choose which sizes and colors of this product you want to offer. Only selected variants will be available for sale. Out-of-stock variants can be selected but will not be available for sale in your online shop until they are back in stock." />
      </div>
      <div className="grid grid-cols-[repeat(auto-fill,250px)] gap-4">
        {variantsByColor.filter(group => (imagesByColor.get(group.color) || []).length > 0).map((group) => {
          const options = group.variants.map((v) => {
            const size = v.size || v.color;
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
  );
}
