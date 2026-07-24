import React, { useCallback } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';

export default function ProductImagePrompt() {
  const {
    productImageVariants, productImagePrompt, setProductImagePrompt,
    selectedProductCombos, setSelectedProductCombos,
    setCurrentProductComboIndex,
    setStep, setMessage, STEPS, onClose,
  } = useCollection();

  const toggleCombo = useCallback((bp, variant, placement) => {
    setSelectedProductCombos(prev => {
      const exists = prev.find(c =>
        c.projectBlueprintId === bp.projectBlueprintId && c.variant === variant && c.placement === placement
      );
      if (exists) {
        return prev.filter(c => !(c.projectBlueprintId === bp.projectBlueprintId && c.variant === variant && c.placement === placement));
      }
      return [...prev, { projectBlueprintId: bp.projectBlueprintId, variant, placement, blueprintName: bp.blueprintName }];
    });
  }, [setSelectedProductCombos]);

  const isComboSelected = (bpId, variant, placement) => {
    return selectedProductCombos.some(c => c.projectBlueprintId === bpId && c.variant === variant && c.placement === placement);
  };

  const handleNext = useCallback(() => {
    if (selectedProductCombos.length === 0) {
      setMessage({ type: 'error', text: 'Select at least one variant/placement combination.' });
      return;
    }
    setCurrentProductComboIndex(0);
    setStep(STEPS.PRODUCT_IMAGE_PREVIEW);
  }, [selectedProductCombos, setCurrentProductComboIndex, setStep, setMessage, STEPS]);

  return (
    <div>
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

      <div className="max-h-[40vh] overflow-y-auto space-y-4">
        {productImageVariants.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No variants or placements available.</p>
        ) : (
          productImageVariants.map((bp) => (
            <div key={bp.projectBlueprintId} className="space-y-2">
              <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300">{bp.blueprintName}</h4>
              {bp.placements.map((placement) => (
                <div key={placement.placement} className="pl-4 space-y-1">
                  <p className="text-xs text-gray-500 dark:text-gray-400">Placement: {placement.placement}</p>
                  {bp.variants.map((v) => (
                    <label key={v.variant} className="flex items-center gap-2 cursor-pointer text-sm py-1">
                      <input
                        type="checkbox"
                        checked={isComboSelected(bp.projectBlueprintId, v.variant, placement.placement)}
                        onChange={() => toggleCombo(bp, v.variant, placement.placement)}
                        className="rounded"
                      />
                      <span>Variant {v.variant}</span>
                    </label>
                  ))}
                </div>
              ))}
            </div>
          ))
        )}
      </div>

      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext} disabled={selectedProductCombos.length === 0}>
          Next ({selectedProductCombos.length} selected)
        </ButtonOutline>
      </div>
    </div>
  );
}
