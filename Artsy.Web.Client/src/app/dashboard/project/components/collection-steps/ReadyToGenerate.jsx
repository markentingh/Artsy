import React, { useCallback } from 'react';
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
  } = useCollection();

  const acceptedArtworks = collectionArtwork.filter(a => a.active);
  const artworkImages = acceptedArtworks.map(a =>
    api.getCollectionArtworkImageUrl(collectionId, a.itemId, a.id, false, a.updatedAt || a.id)
  );

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
    if (collectionId) doGenerateAll(collectionId);
  }, [collectionId, doGenerateAll, setGenerationError]);

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
      {artworkImages.length > 0 && (
        <div className="flex justify-center mb-4">
          <div className="w-full">
            <Carousel
              images={artworkImages}
              alt="Accepted artwork"
              infiniteScroll
              onImageClick={(src) => {
                if (isGeneratingAll) return;
                const idx = artworkImages.indexOf(src);
                setArtworkPreview({
                  src: artworkImages[idx] || src,
                  images: artworkImages,
                });
              }}
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
