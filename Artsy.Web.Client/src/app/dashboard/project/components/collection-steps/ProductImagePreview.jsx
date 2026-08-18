import React, { useCallback, useEffect, useState, useMemo } from 'react';
import { useCollection } from '@/context/collection';
import { useDashboard } from '@/context/dashboard';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';

export default function ProductImagePreview() {
  const {
    selectedProductCombos, currentProductComboIndex,
    collectionId, projectId, api, productImagePrompt,
    setStep, setMessage, STEPS, onClose, onSaved, goBack,
    allProductImages, setAllProductImages,
    setCurrentProductComboIndex,
    selectedProductImageModel,
    collectionProducts,
  } = useCollection();
  const { refreshTokens } = useDashboard();

  const [currentProductImage, setCurrentProductImage] = useState(null);
  const [isGeneratingProductImage, setIsGeneratingProductImage] = useState(false);
  const [productImageChanges, setProductImageChanges] = useState('');
  const [showProductImageChanges, setShowProductImageChanges] = useState(false);

  const combo = selectedProductCombos[currentProductComboIndex];

  const doGenerateProductImage = useCallback(async (comboArg, changes = null) => {
    if (!comboArg) return;
    setIsGeneratingProductImage(true);
    setMessage(null);
    try {
      const cp = collectionProducts.find(p => p.projectBlueprintId === comboArg.projectBlueprintId);
      const res = await api.generateProductImage({
        projectId,
        collectionId,
        projectBlueprintId: comboArg.projectBlueprintId,
        productImageId: comboArg.productImageId,
        modelId: selectedProductImageModel?.id,
        prompt: productImagePrompt,
        variantColor: comboArg.variantColor || '',
        requestedChanges: changes,
        productName: cp?.name || undefined,
        mockupImageIds: comboArg.selectedMockupImageIds || [],
      });
      if (res.data.success) {
        setCurrentProductImage(res.data.data);
        setShowProductImageChanges(false);
        setProductImageChanges('');
        refreshTokens();
        if (onSaved) onSaved();
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to generate product image' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to generate product image' });
    } finally {
      setIsGeneratingProductImage(false);
    }
  }, [projectId, collectionId, api, productImagePrompt, setIsGeneratingProductImage, setCurrentProductImage, setShowProductImageChanges, setProductImageChanges, setMessage, refreshTokens, selectedProductImageModel, onSaved, collectionProducts]);

  useEffect(() => {
    if (!combo) return;
    setCurrentProductImage(null);
    doGenerateProductImage(combo);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentProductComboIndex]);

  const handleAccept = useCallback(async () => {
    if (!currentProductImage) return;
    try {
      await api.acceptProductImage({ collectionId, productImageId: currentProductImage.id });
      setAllProductImages(prev => {
        const idx = prev.findIndex(img =>
          img.projectBlueprintId === currentProductImage.projectBlueprintId &&
          img.productImageId === currentProductImage.productImageId
        );
        if (idx !== -1) {
          const next = [...prev];
          next[idx] = { ...currentProductImage, accepted: true };
          return next;
        }
        return [...prev, { ...currentProductImage, accepted: true }];
      });
      if (onSaved) onSaved();
    } catch (error) {
      console.error('acceptProductImage error:', error?.response?.data || error);
    }

    const nextIndex = currentProductComboIndex + 1;
    if (nextIndex >= selectedProductCombos.length) {
      setStep(STEPS.PUBLISH_PRODUCTS);
    } else {
      setCurrentProductComboIndex(nextIndex);
      setCurrentProductImage(null);
      setShowProductImageChanges(false);
      setProductImageChanges('');
      setStep(STEPS.PRODUCT_IMAGE_PROMPT);
    }
  }, [currentProductImage, collectionId, api, currentProductComboIndex, selectedProductCombos, setStep, STEPS, setCurrentProductComboIndex, setCurrentProductImage, setShowProductImageChanges, setProductImageChanges, doGenerateProductImage, setAllProductImages, onSaved]);

  const handleMakeChanges = useCallback(() => {
    setShowProductImageChanges(true);
  }, [setShowProductImageChanges]);

  const handleSubmitChanges = useCallback(() => {
    if (!productImageChanges.trim()) return;
    setShowProductImageChanges(false);
    doGenerateProductImage(selectedProductCombos[currentProductComboIndex], productImageChanges);
  }, [productImageChanges, selectedProductCombos, currentProductComboIndex, doGenerateProductImage, setShowProductImageChanges]);

  const imageUrl = useMemo(() => {
    if (!currentProductImage?.imageUrl) return null;
    const u = new URL(currentProductImage.imageUrl, window.location.href);
    u.searchParams.delete('thumb');
    u.searchParams.set('r', Math.floor(Math.random() * 100000).toString());
    return u.toString();
  }, [currentProductImage?.imageUrl]);

  return (
    <div className="flex flex-col h-full">
      <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">
        Product Image {currentProductComboIndex + 1} of {selectedProductCombos.length}
        {combo && ` — ${combo.blueprintName} - ${combo.title} - ${combo.variantColor}`}
      </h3>
      <div className="flex flex-col items-center gap-4">
        <div className="w-[512px] h-[512px] max-w-full flex items-center justify-center rounded-lg bg-gray-100 dark:bg-gray-700 overflow-hidden">
          {isGeneratingProductImage ? (
            <Spinner className="text-3xl" />
          ) : imageUrl ? (
            <img
              src={imageUrl}
              alt="Product preview"
              className="w-full h-full object-contain"
            />
          ) : (
            <span className="text-sm text-gray-500 dark:text-gray-400">Generating product image...</span>
          )}
        </div>

        {showProductImageChanges && !isGeneratingProductImage && (
          <div className="w-full max-w-[512px]">
            <TextArea
              name="productImageChanges"
              label="Requested Changes"
              value={productImageChanges}
              onChange={(e) => setProductImageChanges(e.target.value)}
              placeholder="Describe the changes you want..."
              rows={4}
            />
            <div className="buttons flex justify-end gap-2">
              <ButtonOutline onClick={handleSubmitChanges} disabled={!productImageChanges.trim()}>
                Regenerate
              </ButtonOutline>
            </div>
          </div>
        )}
      </div>
      <div className="buttons flex justify-end gap-2 mt-8 mt-auto">
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        {!showProductImageChanges && !isGeneratingProductImage && currentProductImage && (
          <>
            <ButtonOutline onClick={handleMakeChanges}>Make Changes</ButtonOutline>
            <ButtonOutline onClick={handleAccept}>Accept</ButtonOutline>
          </>
        )}
      </div>
    </div>
  );
}
