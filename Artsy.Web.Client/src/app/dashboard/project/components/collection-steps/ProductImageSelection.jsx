import React, { useCallback, useMemo, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import ButtonOutline from '@/components/ui/button-outline';
import { List, Item } from '@/components/ui/list';

export default function ProductImageSelection() {
  const {
    productImageVariants, imageModels, selectedImageModel, setSelectedImageModel,
    selectedProductCombos, setSelectedProductCombos,
    setStep, setMessage, STEPS, onClose,
  } = useCollection();

  useEffect(() => {
  }, [productImageVariants]);

  const handleModelChange = useCallback((e) => {
    const model = imageModels.find(m => m.id === parseInt(e.target.value));
    setSelectedImageModel(model || null);
  }, [imageModels, setSelectedImageModel]);

  const toggleCombo = useCallback((bp, variant, combo) => {
    setSelectedProductCombos(prev => {
      const exists = prev.find(c =>
        c.projectBlueprintId === bp.projectBlueprintId &&
        c.variant === variant.variant &&
        c.placement === combo.placementIndex
      );
      if (exists) {
        return prev.filter(c => !(c.projectBlueprintId === bp.projectBlueprintId && c.variant === variant.variant && c.placement === combo.placementIndex));
      }
      return [...prev, {
        projectBlueprintId: bp.projectBlueprintId,
        variant: variant.variant,
        placement: combo.placementIndex,
        blueprintName: bp.blueprintName,
        variantTitle: variant.variantTitle,
        placementName: combo.placementName,
        tokens: combo.tokens,
      }];
    });
  }, [setSelectedProductCombos]);

  const isComboSelected = (bpId, variant, placement) => {
    return selectedProductCombos.some(c => c.projectBlueprintId === bpId && c.variant === variant && c.placement === placement);
  };

  const totalTokens = useMemo(() => {
    return selectedProductCombos.reduce((sum, c) => sum + (c.tokens || 0), 0);
  }, [selectedProductCombos]);

  const handleNext = useCallback(() => {
    if (selectedProductCombos.length === 0) {
      setMessage({ type: 'error', text: 'Select at least one variant/placement combination.' });
      return;
    }
    setStep(STEPS.PRODUCT_IMAGE_PROMPT);
  }, [selectedProductCombos, setStep, setMessage, STEPS]);

  const sortedVariants = useCallback((bp) => {
    return [...(bp.variants || [])].sort((a, b) => {
      const aTitle = a.variantTitle || `Variant ${a.variant}`;
      const bTitle = b.variantTitle || `Variant ${b.variant}`;
      return aTitle.localeCompare(bTitle);
    });
  }, []);

  return (
    <div>
      <p className="text-sm text-gray-600 dark:text-gray-400 text-center mb-4">
        Select the product variant placements you wish to generate images for.
      </p>
      <div className="flex justify-end mb-4">
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">
            Image Model
          </label>
          <select
            className="w-auto inline-block rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-3 py-2 text-sm text-gray-900 dark:text-gray-100"
            value={selectedImageModel?.id || ''}
            onChange={handleModelChange}
          >
            {imageModels.map(m => (
              <option key={m.id} value={m.id}>{m.name} ({m.model})</option>
            ))}
          </select>
        </div>
      </div>

      <div className="max-h-[40vh] overflow-y-auto space-y-4">
        {productImageVariants.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No variants or placements available.</p>
        ) : (
          productImageVariants.map((bp) => (
            <div key={bp.projectBlueprintId} className="space-y-2">
              <h3 className="text-base font-medium text-gray-700 dark:text-gray-300 border-b border-gray-200 dark:border-gray-600 pb-1">
                {bp.blueprintName}
              </h3>
              <List>
                {sortedVariants(bp).map((v) => (
                  (v.combos || []).map((combo) => (
                    <Item
                      key={`${v.variant}-${combo.placementIndex}`}
                      className="cursor-pointer text-sm"
                    >
                      <label className="flex items-center gap-2 w-full cursor-pointer">
                        <input
                          type="checkbox"
                          checked={isComboSelected(bp.projectBlueprintId, v.variant, combo.placementIndex)}
                          onChange={() => toggleCombo(bp, v, combo)}
                          className="rounded"
                        />
                        <span className="flex-1">{v.variantTitle} - {combo.placementName}</span>
                        {combo.hasArtwork && (
                          <span className="text-xs font-medium text-green-600 dark:text-green-400" title="This placement will print an artwork onto it">Artwork</span>
                        )}
                        <span className="text-xs text-gray-500 dark:text-gray-400">{combo.tokens} tokens</span>
                      </label>
                    </Item>
                  ))
                ))}
              </List>
            </div>
          ))
        )}
      </div>

      <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-600">
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
          {selectedProductCombos.length} combination{selectedProductCombos.length !== 1 ? 's' : ''} selected = <strong>{totalTokens} tokens</strong>
        </p>
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
