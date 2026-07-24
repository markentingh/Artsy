import React, { useCallback, useEffect, useState } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';

export default function ProductImagePreview() {
  const {
    selectedProductCombos, currentProductComboIndex,
    collectionId, projectId, api, productImagePrompt,
    setStep, setMessage, STEPS, onClose,
    allProductImages, setAllProductImages,
    setCurrentProductComboIndex,
  } = useCollection();

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
      const res = await api.generateProductImage({
        projectId,
        collectionId,
        projectBlueprintId: comboArg.projectBlueprintId,
        variant: comboArg.variant,
        placement: comboArg.placement,
        prompt: productImagePrompt,
        requestedChanges: changes,
      });
      if (res.data.success) {
        setCurrentProductImage(res.data.data);
        setShowProductImageChanges(false);
        setProductImageChanges('');
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to generate product image' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to generate product image' });
    } finally {
      setIsGeneratingProductImage(false);
    }
  }, [projectId, collectionId, api, productImagePrompt, setIsGeneratingProductImage, setCurrentProductImage, setShowProductImageChanges, setProductImageChanges, setMessage]);

  useEffect(() => {
    if (!currentProductImage && combo && !isGeneratingProductImage) {
      doGenerateProductImage(combo);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentProductComboIndex]);

  const handleAccept = useCallback(async () => {
    if (!currentProductImage) return;
    try {
      await api.acceptProductImage({ collectionId, productImageId: currentProductImage.id });
      setAllProductImages(prev => [...prev, currentProductImage]);
    } catch (error) {
      console.error('acceptProductImage error:', error?.response?.data || error);
    }

    const nextIndex = currentProductComboIndex + 1;
    if (nextIndex >= selectedProductCombos.length) {
      setStep(STEPS.PRODUCT_IMAGE_DONE);
    } else {
      setCurrentProductComboIndex(nextIndex);
      setCurrentProductImage(null);
      setShowProductImageChanges(false);
      setProductImageChanges('');
      doGenerateProductImage(selectedProductCombos[nextIndex]);
    }
  }, [currentProductImage, collectionId, api, currentProductComboIndex, selectedProductCombos, setStep, STEPS, setCurrentProductComboIndex, setCurrentProductImage, setShowProductImageChanges, setProductImageChanges, doGenerateProductImage, setAllProductImages]);

  const handleMakeChanges = useCallback(() => {
    setShowProductImageChanges(true);
  }, [setShowProductImageChanges]);

  const handleSubmitChanges = useCallback(() => {
    if (!productImageChanges.trim()) return;
    setShowProductImageChanges(false);
    doGenerateProductImage(selectedProductCombos[currentProductComboIndex], productImageChanges);
  }, [productImageChanges, selectedProductCombos, currentProductComboIndex, doGenerateProductImage, setShowProductImageChanges]);

  const imageUrl = currentProductImage
    ? api.getProductImageUrl(collectionId, currentProductImage.id)
    : null;

  return (
    <div>
      <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">
        Product Image {currentProductComboIndex + 1} of {selectedProductCombos.length}
        {combo && ` — ${combo.blueprintName} (Variant ${combo.variant}, Placement ${combo.placement})`}
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

        {!showProductImageChanges && !isGeneratingProductImage && currentProductImage && (
          <div className="buttons flex gap-2">
            <ButtonOutline onClick={handleMakeChanges}>Make Changes</ButtonOutline>
            <ButtonOutline onClick={handleAccept}>Accept</ButtonOutline>
          </div>
        )}

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
      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
      </div>
    </div>
  );
}
