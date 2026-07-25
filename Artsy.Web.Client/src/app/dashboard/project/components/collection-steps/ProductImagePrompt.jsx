import React, { useCallback, useMemo } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';

export default function ProductImagePrompt() {
  const {
    productImageVariants, productImagePrompt, setProductImagePrompt,
    selectedProductCombos,
    setCurrentProductComboIndex,
    setStep, setMessage, STEPS, onClose,
  } = useCollection();

  const firstCombo = selectedProductCombos[0];

  const variantImages = useMemo(() => {
    if (!firstCombo) return [];
    const bp = productImageVariants.find(b => b.projectBlueprintId === firstCombo.projectBlueprintId);
    if (!bp || !bp.variants) return [];
    const variant = bp.variants.find(v => v.variant === firstCombo.variant);
    if (!variant) return [];
    return [variant.imageUrl].filter(Boolean);
  }, [firstCombo, productImageVariants]);

  const handleNext = useCallback(() => {
    if (!productImagePrompt.trim()) {
      setMessage({ type: 'error', text: 'Enter a product image prompt.' });
      return;
    }
    setCurrentProductComboIndex(0);
    setStep(STEPS.PRODUCT_IMAGE_PREVIEW);
  }, [productImagePrompt, setCurrentProductComboIndex, setStep, setMessage, STEPS]);

  return (
    <div>
      <p className="text-center text-sm text-gray-600 dark:text-gray-400 mb-4">
        {selectedProductCombos.length} combination{selectedProductCombos.length !== 1 ? 's' : ''} selected for product image generation.
      </p>

      {firstCombo && (
        <div className="flex flex-col items-center mb-4">
          <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            {firstCombo.blueprintName} — {firstCombo.variantTitle}
          </h4>
          {variantImages.length > 0 && (
            <div className="w-full max-w-[300px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-4">
              <Carousel
                images={variantImages}
                alt={`${firstCombo.blueprintName} - ${firstCombo.variantTitle}`}
                singleImage
                infiniteScroll
                imageClassName="!max-h-none w-full h-full object-contain"
              />
            </div>
          )}
        </div>
      )}

      <div className="mb-4">
        <TextArea
          name="productImagePrompt"
          label="Product Image Prompt"
          value={productImagePrompt}
          onChange={(e) => setProductImagePrompt(e.target.value)}
          placeholder="Describe how the product should be presented..."
          rows={4}
        />
      </div>

      <div className="buttons flex justify-end gap-2">
        <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext} disabled={!productImagePrompt.trim()}>
          Next
        </ButtonOutline>
      </div>
    </div>
  );
}
