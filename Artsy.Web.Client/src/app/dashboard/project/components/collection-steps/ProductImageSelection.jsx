import React, { useCallback, useMemo, useEffect, useState } from 'react';
import { useCollection } from '@/context/collection';
import ButtonOutline from '@/components/ui/button-outline';
import Checkbox from '@/components/forms/checkbox';
import { List, Item } from '@/components/ui/list';

export default function ProductImageSelection() {
  const {
    productImageVariants, imageModels, selectedImageModel, setSelectedImageModel,
    selectedProductCombos, setSelectedProductCombos,
    setStep, setMessage, STEPS, onClose,
    allProductImages, collectionId, api, setAllProductImages,
    setCurrentProductComboIndex, projectId,
  } = useCollection();

  const [checkedCombos, setCheckedCombos] = useState({});
  const [imagesLoaded, setImagesLoaded] = useState(false);

  useEffect(() => {
    if (collectionId) {
      setImagesLoaded(false);
      api.getProductImages(collectionId).then(res => {
        if (res.data.success) {
          setAllProductImages((res.data.data || []).filter(img => img.active));
        }
        setImagesLoaded(true);
      }).catch(e => {
        console.error('getProductImages error:', e);
        setImagesLoaded(true);
      });
    } else {
      setImagesLoaded(true);
    }
  }, [collectionId, api, setAllProductImages]);

  useEffect(() => {
    const initial = {};
    for (const img of allProductImages) {
      initial[`${img.projectBlueprintId}:${img.variant}:${img.placement}`] = true;
    }
    setCheckedCombos(initial);
  }, [allProductImages]);

  const handleModelChange = useCallback((e) => {
    const model = imageModels.find(m => m.id === parseInt(e.target.value));
    setSelectedImageModel(model || null);
  }, [imageModels, setSelectedImageModel]);

  const toggleCombo = useCallback((bp, variant, combo) => {
    const key = `${bp.projectBlueprintId}:${variant.variantColor}:${combo.placementIndex}`;
    setCheckedCombos(prev => {
      const next = { ...prev };
      if (next[key]) {
        delete next[key];
      } else {
        next[key] = true;
      }
      return next;
    });
  }, []);

  const checkedComboList = useMemo(() => {
    const result = [];
    for (const bp of productImageVariants) {
      for (const v of (bp.variants || [])) {
        for (const c of (v.combos || [])) {
          if (c.hasArtwork && checkedCombos[`${bp.projectBlueprintId}:${v.variantColor}:${c.placementIndex}`]) {
            result.push({
              projectBlueprintId: bp.projectBlueprintId,
              variantColor: v.variantColor,
              placement: c.placementIndex,
              blueprintName: bp.blueprintName,
              placementName: c.placementName,
              tokens: c.tokens,
            });
          }
        }
      }
    }
    return result;
  }, [productImageVariants, checkedCombos]);

  const totalTokens = useMemo(() => {
    return checkedComboList.reduce((sum, c) => sum + (c.tokens || 0), 0);
  }, [checkedComboList]);

  const handleNext = useCallback(async () => {
    if (checkedComboList.length === 0) {
      setMessage({ type: 'error', text: 'Select at least one variant/placement combination.' });
      return;
    }

    const selectedCombos = checkedComboList.map(c => ({
      projectBlueprintId: c.projectBlueprintId,
      variantColor: c.variantColor,
      placement: c.placement,
    }));

    let syncedImages = allProductImages;
    if (collectionId && projectId) {
      try {
        const res = await api.syncProductImageSelections({
          collectionId,
          projectId,
          selectedCombos,
        });
        if (res.data.success) {
          syncedImages = res.data.data || [];
          setAllProductImages(syncedImages);
        } else {
          setMessage({ type: 'error', text: res.data.message || 'Failed to sync product image selections' });
          return;
        }
      } catch (e) {
        const errMsg = e?.response?.data?.message || e?.message || 'Failed to sync product image selections';
        console.error('syncProductImageSelections error:', e?.response?.data || e);
        setMessage({ type: 'error', text: errMsg });
        return;
      }
    }

    const acceptedKeys = new Set(syncedImages.filter(img => img.accepted).map(img =>
      `${img.projectBlueprintId}:${img.variant}:${img.placement}`
    ));
    const missingCombos = checkedComboList.filter(c =>
      !acceptedKeys.has(`${c.projectBlueprintId}:${c.variantColor}:${c.placement}`)
    );

    if (missingCombos.length === 0) {
      setStep(STEPS.PUBLISH_PRODUCTS);
      return;
    }

    setSelectedProductCombos(missingCombos);
    setCurrentProductComboIndex(0);
    setStep(STEPS.PRODUCT_IMAGE_PROMPT);
  }, [checkedComboList, allProductImages, collectionId, projectId, api, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex, setStep, setMessage, STEPS]);

  const sortedVariants = useCallback((bp) => {
    return [...(bp.variants || [])].sort((a, b) => {
      const aColor = a.variantColor || 'Default';
      const bColor = b.variantColor || 'Default';
      return aColor.localeCompare(bColor);
    });
  }, []);

  return (
    <div className="flex flex-col h-full">
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
                  (v.combos || []).filter(c => c.hasArtwork).map((combo) => (
                    <Item
                      key={`${v.variantColor}-${combo.placementIndex}`}
                      className="cursor-pointer text-sm"
                    >
                      <Checkbox
                        checked={!!checkedCombos[`${bp.projectBlueprintId}:${v.variantColor}:${combo.placementIndex}`]}
                        onChange={() => toggleCombo(bp, v, combo)}
                        label={
                          <span className="flex items-center gap-2 flex-1">
                            <span>{v.variantColor} - {combo.placementName}</span>
                            <span className="text-xs text-gray-500 dark:text-gray-400">{combo.tokens} tokens</span>
                          </span>
                        }
                      />
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
          {checkedComboList.length} combination{checkedComboList.length !== 1 ? 's' : ''} selected = <strong>{totalTokens} tokens</strong>
        </p>
      </div>

      <div className="buttons flex justify-end gap-2 mt-4 mt-auto">
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        {checkedComboList.length > 0 && (
          <ButtonOutline onClick={handleNext} disabled={!imagesLoaded}>
            Next ({checkedComboList.length} selected)
          </ButtonOutline>
        )}
      </div>
    </div>
  );
}
