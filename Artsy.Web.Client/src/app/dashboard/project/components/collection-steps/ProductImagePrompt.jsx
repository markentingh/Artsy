import React, { useCallback, useMemo, useState, useRef, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Spinner from '@/components/ui/spinner';

export default function ProductImagePrompt() {
  const {
    productImagePrompt, setProductImagePrompt,
    selectedProductCombos,
    currentProductComboIndex,
    setStep, setMessage, STEPS, onClose,
    collectionId, api, projectId,
    setSelectedProductCombos, setCurrentProductComboIndex,
    collectionArtwork, blueprints,
    allProductImages,
    setArtworkPreview,
    mockups, printifyProducts, loadMockups,
  } = useCollection();

  const combo = selectedProductCombos[currentProductComboIndex];

  useEffect(() => {
    if (collectionId && mockups.length === 0) {
      loadMockups(collectionId);
    }
  }, [collectionId, mockups.length, loadMockups]);

  const mockupImages = useMemo(() => {
    if (!combo || !printifyProducts.length || !mockups.length) return [];
    const pp = printifyProducts.find(p => p.projectBlueprintId === combo.projectBlueprintId);
    if (!pp) return [];
    return mockups.filter(m => m.printifyProductId === pp.id).map(m => m.imageUrl);
  }, [combo, printifyProducts, mockups]);

  const [thumbRetried, setThumbRetried] = useState({});
  const [thumbFailed, setThumbFailed] = useState({});
  const retryRef = useRef({});
  const [tokenEstimate, setTokenEstimate] = useState(null);
  const [estimatingTokens, setEstimatingTokens] = useState(false);
  const estimateTimerRef = useRef(null);

  const placementItemId = useMemo(() => {
    if (!combo) return null;
    const bp = blueprints.find(b => b.id === combo.projectBlueprintId);
    if (!bp || !bp.placementJson) return null;
    try {
      const placementArr = JSON.parse(bp.placementJson);
      if (!placementArr || !Array.isArray(placementArr) || placementArr.length === 0) return null;
      const placement = placementArr[0];
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
      i.productImageId === combo.productImageId
    );
    if (!img || !img.accepted) return null;
    return `/api/projects/collection/${collectionId}/product-image/${img.id}`;
  }, [combo, allProductImages, collectionId]);

  const allImages = useMemo(() => {
    const productImg = existingProductImage ? [existingProductImage] : [];
    return [...productImg, ...mockupImages, ...artworkImages.map(a => a.thumbUrl)];
  }, [existingProductImage, mockupImages, artworkImages]);

  const fullSizeImages = useMemo(() => {
    const productImg = existingProductImage ? [existingProductImage] : [];
    return [...productImg, ...mockupImages, ...artworkImages.map(a => a.url)];
  }, [existingProductImage, mockupImages, artworkImages]);

  const handleImageError = useCallback(async (index) => {
    if (retryRef.current[index]) return;
    retryRef.current[index] = true;

    const productImgCount = existingProductImage ? 1 : 0;
    const mockupImgCount = mockupImages.length;
    const artworkIndex = index - productImgCount - mockupImgCount;
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
  }, [artworkImages, existingProductImage, mockupImages, collectionId, api]);

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

  useEffect(() => {
    if (!combo || !collectionId || !projectId) return;
    if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current);
    estimateTimerRef.current = setTimeout(async () => {
      setEstimatingTokens(true);
      try {
        const res = await api.estimateProductImageTokens({
          projectId,
          collectionId,
          projectBlueprintId: combo.projectBlueprintId,
          productImageId: combo.productImageId,
          prompt: productImagePrompt,
        });
        if (res.data.success) {
          setTokenEstimate(res.data.data);
        } else {
          setTokenEstimate(null);
        }
      } catch {
        setTokenEstimate(null);
      } finally {
        setEstimatingTokens(false);
      }
    }, 2000);
    return () => { if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current); };
  }, [combo, collectionId, projectId, api, productImagePrompt]);

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
      img.productImageId === combo.productImageId
    );
    if (!existing || !existing.accepted) {
      try {
        await api.deleteProductImage({
          collectionId,
          projectBlueprintId: combo.projectBlueprintId,
          productImageId: combo.productImageId,
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
      setStep(STEPS.PUBLISH_PRODUCTS);
    } else {
      setCurrentProductComboIndex(nextIndex + 1);
    }
  }, [currentProductComboIndex, selectedProductCombos.length, setStep, STEPS, setCurrentProductComboIndex]);

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-sm text-gray-600 dark:text-gray-400 mb-4">
        {selectedProductCombos.length} product image{selectedProductCombos.length !== 1 ? 's' : ''} to generate.
      </p>

      {combo && (
        <div className="flex flex-col items-center mb-4">
          <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            {combo.blueprintName} — {combo.title} - {combo.variantColor}
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
        {estimatingTokens ? (
          <div className="flex items-center gap-2 mt-2 text-sm text-gray-500 dark:text-gray-400">
            <Spinner className="text-sm" />
            <span>Estimating token cost...</span>
          </div>
        ) : tokenEstimate ? (
          <div className="mt-2 text-sm text-gray-500 dark:text-gray-400 space-y-1">
            <div>
              <span className="font-medium">{tokenEstimate.totalTokens.toLocaleString()}</span> tokens
              {tokenEstimate.estimatedCostUSD > 0 && (
                <span> · est. ${tokenEstimate.estimatedCostUSD.toFixed(4)} USD</span>
              )}
            </div>
            <div className="text-xs">
              {tokenEstimate.textInputTokens.toLocaleString()} text input · {tokenEstimate.imageInputTokens.toLocaleString()} image input · {tokenEstimate.imageOutputTokens.toLocaleString()} output · {tokenEstimate.inputImageCount} reference image{tokenEstimate.inputImageCount !== 1 ? 's' : ''}
            </div>
          </div>
        ) : null}
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
