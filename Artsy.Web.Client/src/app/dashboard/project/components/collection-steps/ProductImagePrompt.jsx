import React, { useCallback, useMemo, useState, useRef } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';

export default function ProductImagePrompt() {
  const {
    productImageVariants, productImagePrompt, setProductImagePrompt,
    selectedProductCombos,
    currentProductComboIndex,
    setStep, setMessage, STEPS, onClose,
    collectionId, api, projectId,
    setSelectedProductCombos, setCurrentProductComboIndex,
    collectionArtwork, blueprints,
    allProductImages,
    setArtworkPreview,
  } = useCollection();

  const combo = selectedProductCombos[currentProductComboIndex];
  const [thumbRetried, setThumbRetried] = useState({});
  const [thumbFailed, setThumbFailed] = useState({});
  const retryRef = useRef({});

  const variantImages = useMemo(() => {
    if (!combo) return [];
    const bp = productImageVariants.find(b => b.projectBlueprintId === combo.projectBlueprintId);
    if (!bp || !bp.variants) return [];
    const variant = bp.variants.find(v => v.variant === combo.variant);
    if (!variant) return [];
    return [variant.imageUrl].filter(Boolean);
  }, [combo, productImageVariants]);

  const placementItemId = useMemo(() => {
    if (!combo) return null;
    const bp = blueprints.find(b => b.id === combo.projectBlueprintId);
    if (!bp || !bp.placementJson) return null;
    try {
      const placementDict = JSON.parse(bp.placementJson);
      if (!placementDict) return null;
      const placementKeys = Object.keys(placementDict);
      if (combo.placement < 0 || combo.placement >= placementKeys.length) return null;
      const placementKey = placementKeys[combo.placement];
      const placement = placementDict[placementKey];
      if (placement && placement.source === 'item' && placement.itemId) return String(placement.itemId);
    } catch { /* skip */ }
    return null;
  }, [combo, blueprints]);

  const artworkImages = useMemo(() => {
    if (!collectionId || !placementItemId) return [];
    return collectionArtwork
      .filter(a => a.active && String(a.itemId) === placementItemId)
      .map(a => ({
        itemId: a.itemId,
        artworkId: a.id,
        url: api.getCollectionArtworkImageUrl(collectionId, a.itemId, a.id, true),
        thumbUrl: api.getCollectionArtworkThumbUrl(collectionId, a.itemId, a.id),
      }));
  }, [collectionId, collectionArtwork, api, placementItemId]);

  const existingProductImage = useMemo(() => {
    if (!combo || !collectionId) return null;
    const img = allProductImages.find(i =>
      i.projectBlueprintId === combo.projectBlueprintId &&
      i.variant === combo.variant &&
      i.placement === combo.placement
    );
    if (!img || !img.accepted) return null;
    return `/api/projects/collection/${collectionId}/product-image/${img.id}`;
  }, [combo, allProductImages, collectionId]);

  const allImages = useMemo(() => {
    const productImg = existingProductImage ? [existingProductImage] : [];
    return [...productImg, ...variantImages, ...artworkImages.map(a => a.thumbUrl)];
  }, [existingProductImage, artworkImages, variantImages]);

  const fullSizeImages = useMemo(() => {
    const productImg = existingProductImage ? [existingProductImage] : [];
    return [...productImg, ...variantImages, ...artworkImages.map(a => a.url)];
  }, [existingProductImage, artworkImages, variantImages]);

  const handleImageError = useCallback(async (index) => {
    if (retryRef.current[index]) return;
    retryRef.current[index] = true;

    const productImgCount = existingProductImage ? 1 : 0;
    const artworkIndex = index - productImgCount - variantImages.length;
    const artwork = artworkImages[artworkIndex];
    if (!artwork || !collectionId) {
      setThumbFailed(prev => ({ ...prev, [index]: true }));
      return;
    }

    try {
      const res = await api.generateArtworkThumbnail({ collectionId, itemId: artwork.itemId });
      if (res.data.success) {
        setThumbRetried(prev => ({ ...prev, [index]: Date.now() }));
      } else {
        setThumbFailed(prev => ({ ...prev, [index]: true }));
      }
    } catch {
      setThumbFailed(prev => ({ ...prev, [index]: true }));
    }
  }, [artworkImages, variantImages, existingProductImage, collectionId, api]);

  const imagesWithRetry = useMemo(() => {
    return allImages.map((url, i) => {
      if (thumbFailed[i]) return null;
      if (thumbRetried[i]) return `${url}&r=${thumbRetried[i]}`;
      return url;
    }).filter(Boolean);
  }, [allImages, thumbRetried, thumbFailed]);

  const failedCount = Object.keys(thumbFailed).length;
  const displayImages = failedCount > 0 && imagesWithRetry.length === 0
    ? []
    : imagesWithRetry;

  const handleNext = useCallback(() => {
    if (!productImagePrompt.trim()) {
      setMessage({ type: 'error', text: 'Enter a product image prompt.' });
      return;
    }
    setStep(STEPS.PRODUCT_IMAGE_PREVIEW);
  }, [productImagePrompt, setStep, setMessage, STEPS]);

  const handleSkip = useCallback(async () => {
    if (!combo || !collectionId) {
      moveToNextCombo();
      return;
    }
    const existing = allProductImages.find(img =>
      img.projectBlueprintId === combo.projectBlueprintId &&
      img.variant === combo.variant &&
      img.placement === combo.placement
    );
    if (!existing || !existing.accepted) {
      try {
        await api.deleteProductImage({
          collectionId,
          projectBlueprintId: combo.projectBlueprintId,
          variant: combo.variant,
          placement: combo.placement,
        });
      } catch (e) {
        console.error('deleteProductImage error:', e?.response?.data || e);
      }
    }
    setSelectedProductCombos(prev => prev.filter((_, idx) => idx !== currentProductComboIndex));
    moveToNextCombo();
  }, [combo, collectionId, api, allProductImages, currentProductComboIndex, setSelectedProductCombos]);

  const moveToNextCombo = useCallback(() => {
    const nextIndex = currentProductComboIndex >= selectedProductCombos.length - 1
      ? selectedProductCombos.length - 1
      : currentProductComboIndex;
    if (nextIndex >= selectedProductCombos.length - 1) {
      setStep(STEPS.CREATE_PRODUCTS);
    } else {
      setCurrentProductComboIndex(nextIndex + 1);
    }
  }, [currentProductComboIndex, selectedProductCombos.length, setStep, STEPS, setCurrentProductComboIndex]);

  const tokenCost = combo ? (combo.tokens || 2) : 0;

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-sm text-gray-600 dark:text-gray-400 mb-4">
        {selectedProductCombos.length} combination{selectedProductCombos.length !== 1 ? 's' : ''} selected for product image generation.
      </p>

      {combo && (
        <div className="flex flex-col items-center mb-4">
          <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            {combo.blueprintName} — {combo.variantTitle} - {combo.placementName}
          </h4>
          {displayImages.length > 0 && (
            <div className="w-full max-w-[300px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-2">
              <Carousel
                images={displayImages}
                alt="Artwork & Product"
                singleImage
                infiniteScroll
                imageClassName="!max-h-none w-full h-full object-contain"
                onImageError={handleImageError}
                onImageClick={(src) => setArtworkPreview({ images: fullSizeImages, src, alt: 'Image Preview' })}
                placeholder="No Thumbnail"
              />
            </div>
          )}
          {displayImages.length === 0 && (
            <div className="w-full max-w-[300px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-2 p-8 text-center">
              <span className="text-sm text-gray-500 dark:text-gray-400">No Thumbnail</span>
            </div>
          )}
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Token cost: {tokenCost}
          </p>
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

      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleSkip}>
          Skip
        </ButtonOutline>
        <ButtonOutline onClick={handleNext} disabled={!productImagePrompt.trim()}>
          Generate Image
        </ButtonOutline>
      </div>
    </div>
  );
}
