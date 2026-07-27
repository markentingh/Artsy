import React, { useCallback, useState, useRef, useMemo } from 'react';
import { useCollection } from '@/context/collection';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Checked from '@/components/ui/checked';
import Spinner from '@/components/ui/spinner';

export default function ReadyToGenerate() {
  const {
    collectionId, setCollectionId, collectionArtwork, blueprints, estimate,
    isGeneratingAll, generatingProgress, generatingMessage,
    generationError, setGenerationError,
    generatedArtworks, currentGeneratingIndex,
    doGenerateAll, handleSaveDraft,
    setArtworkPreview, onClose, api,
    projectId, cancelRef, STEPS,
    upscaleComplete, setUpscaleComplete,
    setStep, loadProductImageVariants, loadImageModels,
    ensureCollection, setAllProductImages,
    setSelectedProductCombos, setCurrentProductComboIndex,
  } = useCollection();

  const acceptedArtworks = collectionArtwork.filter(a => a.active && a.imageModel !== 'custom');
  const [thumbRetried, setThumbRetried] = useState({});
  const [thumbFailed, setThumbFailed] = useState({});
  const retryRef = useRef({});

  const artworkImages = acceptedArtworks.map(a =>
    api.getCollectionArtworkThumbUrl(collectionId, a.itemId, a.id, a.updatedAt || a.id)
  );

  const handleImageError = useCallback(async (index) => {
    if (retryRef.current[index]) return;
    retryRef.current[index] = true;

    const artwork = acceptedArtworks[index];
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
  }, [acceptedArtworks, collectionId, api]);

  const displayImages = useMemo(() => {
    return artworkImages.map((url, i) => {
      if (thumbFailed[i]) return null;
      if (thumbRetried[i]) return `${url}&r=${thumbRetried[i]}`;
      return url;
    }).filter(Boolean);
  }, [artworkImages, thumbRetried, thumbFailed]);

  const currentItemGenId = currentGeneratingIndex >= 0 && estimate?.generations?.[currentGeneratingIndex]?.itemId;

  const handleGenerateArtworks = useCallback(async () => {
    if (!collectionId) {
      try {
        const colRes = await api.createCollection({ projectId, title: `Collection ${new Date().toISOString().split('T')[0]}` });
        if (colRes.data.success) {
          setCollectionId(colRes.data.data.id);
          await doGenerateAll(colRes.data.data.id);
        }
      } catch (error) {
        setGenerationError(error?.response?.data?.message || 'Failed to create collection');
      }
    } else {
      await doGenerateAll(collectionId);
    }
  }, [collectionId, projectId, api, setCollectionId, doGenerateAll, setGenerationError]);

  const handleCancelGeneration = useCallback(() => {
    cancelRef.current = true;
    onClose();
  }, [cancelRef, onClose]);

  const handleTryAgain = useCallback(() => {
    setGenerationError(null);
    setUpscaleComplete(false);
    if (collectionId) doGenerateAll(collectionId);
  }, [collectionId, doGenerateAll, setGenerationError, setUpscaleComplete]);

  const handleNext = useCallback(async () => {
    const colId = collectionId || await ensureCollection();
    if (!colId) return;
    const [variants,] = await Promise.all([loadProductImageVariants(colId), loadImageModels()]);

    try {
      const imgRes = await api.getProductImages(colId);
      if (imgRes.data.success) {
        const allImages = (imgRes.data.data || []).filter(img => img.active);
        const accepted = allImages.filter(img => img.accepted);
        const acceptedKeys = new Set(accepted.map(img => `${img.projectBlueprintId}:${img.variant}:${img.placement}`));

        const activeKeys = new Set(allImages.map(img => `${img.projectBlueprintId}:${img.variant}:${img.placement}`));

        const allCombos = [];
        for (const bp of variants) {
          for (const v of (bp.variants || [])) {
            for (const c of (v.combos || [])) {
              if (c.hasArtwork) {
                const key = `${bp.projectBlueprintId}:${v.variant}:${c.placementIndex}`;
                if (activeKeys.has(key)) {
                  allCombos.push({
                    projectBlueprintId: bp.projectBlueprintId,
                    blueprintName: bp.blueprintName,
                    variant: v.variant,
                    variantTitle: v.variantTitle,
                    placement: c.placementIndex,
                    placementName: c.placementName,
                    tokens: c.tokens,
                  });
                }
              }
            }
          }
        }

        const missingCombos = allCombos.filter(c => !acceptedKeys.has(`${c.projectBlueprintId}:${c.variant}:${c.placement}`));

        if (allCombos.length === 0) {
          setAllProductImages(allImages);
          setStep(STEPS.PRODUCT_IMAGE_SELECTION);
          return;
        }

        if (missingCombos.length === 0) {
          setAllProductImages(allImages);
          setStep(STEPS.CREATE_PRODUCTS);
          return;
        }

        if (missingCombos.length < allCombos.length) {
          setSelectedProductCombos(missingCombos);
          setCurrentProductComboIndex(0);
          setAllProductImages(allImages);
          setStep(STEPS.PRODUCT_IMAGE_PROMPT);
          return;
        }
      }
    } catch (e) { }

    setStep(STEPS.PRODUCT_IMAGE_SELECTION);
  }, [collectionId, ensureCollection, loadProductImageVariants, loadImageModels, setStep, STEPS, api, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex]);

  const renderOverlay = (i) => {
    if (!isGeneratingAll) return null;
    const artwork = acceptedArtworks[i];
    const isCurrent = artwork?.itemId === currentItemGenId;
    const isDone = generatedArtworks.some(g => g.itemId === artwork?.itemId);

    if (isCurrent && !isDone) {
      return (
        <div className="absolute inset-0 flex items-center justify-center bg-black/40 rounded-lg">
          <Spinner className="text-2xl text-white" />
        </div>
      );
    }
    if (isDone) {
      return (
        <div className="absolute top-1 right-1">
          <Checked checked={true} />
        </div>
      );
    }
    return null;
  };

  return (
    <div>
      {displayImages.length > 0 && (
        <div className="flex justify-center mb-4">
          <div className="w-full">
            <Carousel
              images={displayImages}
              alt="Accepted artwork"
              infiniteScroll
              onImageClick={(src) => {
                if (isGeneratingAll) return;
                const idx = displayImages.indexOf(src);
                setArtworkPreview({
                  src: displayImages[idx] || src,
                  images: displayImages,
                });
              }}
              onImageError={handleImageError}
              imageClassName="!max-h-none w-[150px] h-[150px] object-contain rounded-lg"
              overlayRender={renderOverlay}
            />
          </div>
        </div>
      )}
      {isGeneratingAll || generationError ? (
        <div className="w-full max-w-[500px] mx-auto">
          {generationError ? (
            <>
              <p className="text-center text-sm text-red-600 dark:text-red-400 mb-2">
                {generationError}
              </p>
              <div className="buttons flex justify-center gap-2">
                <ButtonOutline className="cancel" onClick={handleCancelGeneration}>Cancel</ButtonOutline>
                <ButtonOutline onClick={handleTryAgain}>Try Again</ButtonOutline>
              </div>
            </>
          ) : (
            <>
              <p className="text-center text-sm text-gray-600 dark:text-gray-400 mb-2">
                {generatingMessage}
              </p>
              <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-4 mb-2">
                <div
                  className="bg-primary-500 h-4 rounded-full transition-all"
                  style={{ width: `${generatingProgress}%` }}
                />
              </div>
              <p className="text-center text-sm text-gray-600 dark:text-gray-400 mb-4">
                {generatingProgress}% complete
              </p>
              <div className="buttons flex justify-center">
                <ButtonOutline className="cancel" onClick={handleCancelGeneration}>Cancel</ButtonOutline>
              </div>
            </>
          )}
        </div>
      ) : upscaleComplete ? (
        <>
          <p className="text-center text-lg mb-4">
            Upscaling complete! {generatedArtworks.length} artwork{generatedArtworks.length !== 1 ? 's' : ''} upscaled to full size.
          </p>
          <div className="buttons flex justify-end gap-2">
            <ButtonOutline className="cancel" onClick={onClose}>Close</ButtonOutline>
            <ButtonOutline onClick={handleNext}>Next</ButtonOutline>
          </div>
        </>
      ) : (
        <>
          <p className="text-center text-lg mb-2">
            Ready to upscale {estimate?.artworkCount || 0} preview artworks to full size for printing onto your {blueprints.length} product{blueprints.length !== 1 ? 's' : ''}.
          </p>
          <p className="text-center text-sm text-gray-600 dark:text-gray-400 mb-6">
            This will cost {estimate?.totalTokens || 0} tokens.
          </p>
          <div className="buttons flex justify-end gap-2">
            <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
            <ButtonOutline onClick={handleSaveDraft}>Save Draft</ButtonOutline>
            <ButtonOutline onClick={handleGenerateArtworks}>Upscale Artworks</ButtonOutline>
          </div>
        </>
      )}
    </div>
  );
}
