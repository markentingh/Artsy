import React, { useCallback, useState, useRef, useMemo, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import { useDashboard } from '@/context/dashboard';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Checked from '@/components/ui/checked';
import Spinner from '@/components/ui/spinner';

export default function ReadyToGenerate() {
  const {
    collectionId, setCollectionId, collectionArtwork, blueprints, estimate,
    isGeneratingAll, generatingProgress, generatingMessage,
    generationError, setGenerationError,
    generatedArtworks, currentGeneratingIndex, currentGeneratingItemId,
    doGenerateAll,
    setArtworkPreview, onClose, onSaved, api,
    projectId, cancelRef, STEPS,
    upscaleComplete, setUpscaleComplete,
    setStep, loadImageModels,
    ensureCollection, goBack,
  } = useCollection();
  const { refreshTokens } = useDashboard();

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
        refreshTokens();
      } else {
        setThumbFailed(prev => ({ ...prev, [index]: true }));
      }
    } catch {
      setThumbFailed(prev => ({ ...prev, [index]: true }));
    }
  }, [acceptedArtworks, collectionId, api, refreshTokens]);

  const displayImages = useMemo(() => {
    return artworkImages.map((url, i) => {
      if (thumbFailed[i]) return null;
      if (thumbRetried[i]) return `${url}&r=${thumbRetried[i]}`;
      return url;
    }).filter(Boolean);
  }, [artworkImages, thumbRetried, thumbFailed]);

  const pendingCount = useMemo(() => {
    if (!estimate?.generations) return 0;
    return estimate.generations.filter(gen =>
      !collectionArtwork.some(a => String(a.itemId) === String(gen.itemId) && a.fullSize)
    ).length;
  }, [estimate, collectionArtwork]);

  const pendingTokens = pendingCount * 2;

  useEffect(() => {
    if (!isGeneratingAll && pendingCount === 0) {
      setUpscaleComplete(true);
    }
  }, [isGeneratingAll, pendingCount, setUpscaleComplete]);

  const currentItemGenId = currentGeneratingItemId;

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
    await loadImageModels();
    setStep(STEPS.CREATE_PRODUCTS);
  }, [collectionId, ensureCollection, loadImageModels, setStep, STEPS]);

  const renderOverlay = (i) => {
    const artwork = acceptedArtworks[i];
    if (!artwork) return null;

    const isAlreadyUpscaled = artwork.fullSize;
    const isCurrent = isGeneratingAll && artwork?.itemId === currentItemGenId;
    const isDone = generatedArtworks.some(g => g.itemId === artwork?.itemId);

    if (isCurrent && !isDone && !isAlreadyUpscaled) {
      return (
        <div className="absolute inset-0 flex items-center justify-center bg-black/40 rounded-lg">
          <Spinner className="text-2xl text-white" />
        </div>
      );
    }
    if (isDone || isAlreadyUpscaled) {
      return (
        <div className="absolute inset-0 flex items-center justify-center">
          <Checked checked={true} />
        </div>
      );
    }
    return null;
  };

  return (
    <div className="flex flex-col h-full">
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
                <ButtonOutline color="gray" className="cancel" onClick={handleCancelGeneration}>Cancel</ButtonOutline>
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
                <ButtonOutline color="gray" className="cancel" onClick={handleCancelGeneration}>Cancel</ButtonOutline>
              </div>
            </>
          )}
        </div>
      ) : upscaleComplete ? (
        <>
          <p className="text-center text-lg mb-4">
            Upscaling complete! {generatedArtworks.length} artwork{generatedArtworks.length !== 1 ? 's' : ''} upscaled to full size.
          </p>
          <div className="buttons flex justify-end gap-2 mt-auto">
            <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
            <ButtonOutline color="gray" className="cancel" onClick={onClose}>Close</ButtonOutline>
            <ButtonOutline onClick={handleNext}>Next</ButtonOutline>
          </div>
        </>
      ) : (
        <>
          {pendingCount === 0 ? (
            <p className="text-center text-lg mb-4">
              All artworks have been upscaled.
            </p>
          ) : (
            <>
              <p className="text-center text-lg mb-2">
                Ready to upscale {pendingCount} artwork{pendingCount !== 1 ? 's' : ''} to full size.
              </p>
              <p className="text-center text-sm text-gray-600 dark:text-gray-400 mb-6">
                This will cost {pendingTokens} tokens.
              </p>
            </>
          )}
          <div className="buttons flex justify-end gap-2 mt-auto">
            <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
            <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
            {pendingCount > 0 && (
              <ButtonOutline onClick={handleGenerateArtworks}>Upscale Artworks</ButtonOutline>
            )}
            {pendingCount === 0 && (
              <ButtonOutline onClick={handleNext}>Next</ButtonOutline>
            )}
          </div>
        </>
      )}
    </div>
  );
}
