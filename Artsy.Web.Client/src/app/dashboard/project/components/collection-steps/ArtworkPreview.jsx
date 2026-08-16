import React, { useCallback, useMemo } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Spinner from '@/components/ui/spinner';
import Tooltip from '@/components/ui/tooltip';

export default function ArtworkPreview() {
  const {
    aiItems, currentItemIndex, currentItem,
    isGenerating, previewImageData, currentArtwork,
    showChanges, setShowChanges,
    requestedChanges, setRequestedChanges,
    collectionId, ensureCollection,
    doGeneratePreview, advanceToNextItem,
    setCollectionArtwork,
    api, onClose, onSaved, setArtworkPreview, setStep, STEPS, goBack,
  } = useCollection();

  const hasOpacity = currentArtwork?.opacity === true;

  const carouselImages = useMemo(() => {
    if (!previewImageData || !currentArtwork || !collectionId) return null;
    if (hasOpacity) {
      const pngUrl = previewImageData;
      const jpgWithBgUrl = api.getCollectionArtworkJpgWithBgUrl(collectionId, currentItem.id, currentArtwork.id, false, Date.now());
      return [pngUrl, jpgWithBgUrl];
    }
    return null;
  }, [previewImageData, currentArtwork, hasOpacity, collectionId, currentItem, api]);

  const handleTryAgain = useCallback(() => {
    setStep(STEPS.ARTWORK_QUESTIONS);
  }, [setStep, STEPS]);

  const handleMakeChanges = useCallback(() => {
    setShowChanges(true);
  }, [setShowChanges]);

  const handleSubmitChanges = useCallback(() => {
    if (!requestedChanges.trim()) return;
    setShowChanges(false);
    if (collectionId) {
      doGeneratePreview(collectionId);
    } else {
      ensureCollection().then((colId) => {
        if (colId) doGeneratePreview(colId);
      });
    }
  }, [requestedChanges, collectionId, doGeneratePreview, ensureCollection, setShowChanges]);

  const handleAccept = useCallback(async () => {
    const colId = await ensureCollection();
    if (colId) {
      const item = aiItems[currentItemIndex];
      if (item) {
        try {
          await api.acceptCollectionArtwork({ collectionId: colId, itemId: item.id });
          const artRes = await api.getCollectionArtwork(colId);
          if (artRes.data.success) {
            const updatedArtwork = artRes.data.data || [];
            setCollectionArtwork(updatedArtwork);
            if (onSaved) onSaved();
            advanceToNextItem(undefined, updatedArtwork);
            return;
          }
        } catch (error) {
          console.error('acceptCollectionArtwork error:', error?.response?.data || error);
        }
      }
    }
    advanceToNextItem();
  }, [ensureCollection, aiItems, currentItemIndex, api, advanceToNextItem, setCollectionArtwork, onSaved]);

  return (
    <div className="flex flex-col h-full">
      <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">
        Artwork {currentItemIndex + 1} of {aiItems.length}: {currentItem?.title || 'Untitled'}
      </h3>
      <div className="flex flex-col items-center gap-4">
        <div className="w-[512px] h-[512px] max-w-full flex items-center justify-center rounded-lg bg-gray-100 dark:bg-gray-700 overflow-hidden">
          {isGenerating ? (
            <Spinner className="text-3xl" />
          ) : hasOpacity && carouselImages ? (
            <div
              className="w-full h-full"
              style={{
                backgroundImage: 'url(/checkerboard.png)',
                backgroundSize: '20px 20px',
                backgroundRepeat: 'repeat',
              }}
            >
              <Carousel
                images={carouselImages}
                alt="Artwork preview"
                singleImage
                infiniteScroll
                imageClassName="!max-h-none w-full h-[512px] object-contain"
                onImageClick={(src) => setArtworkPreview({ images: carouselImages, src })}
              />
            </div>
          ) : previewImageData ? (
            <img
              src={previewImageData}
              alt="Preview"
              className="w-full h-full object-contain cursor-pointer"
              onClick={() => setArtworkPreview({ images: [previewImageData], src: previewImageData })}
            />
          ) : (
            <span className="text-sm text-gray-500 dark:text-gray-400">No preview generated yet.</span>
          )}
        </div>

        {showChanges && !isGenerating && (
          <div className="w-full max-w-[512px]">
            <TextArea
              name="requestedChanges"
              label="Requested Changes"
              value={requestedChanges}
              onChange={(e) => setRequestedChanges(e.target.value)}
              placeholder="Describe the changes you want..."
              rows={4}
            />
          </div>
        )}
      </div>
      <div className="buttons flex justify-end gap-2 items-center pt-4 mt-auto">
        {!showChanges && !isGenerating && previewImageData && (
          <>
            <Tooltip text="Either make changes to the generated artwork using a prompt to edit the artwork, accept the currently generated artwork, or try again by changing the original prompt text." className="pr-8" />
            <ButtonOutline color="gray" onClick={handleMakeChanges}>Make Changes</ButtonOutline>
            <ButtonOutline onClick={handleAccept}>Accept</ButtonOutline>
            <ButtonOutline color="red" onClick={handleTryAgain}>Try Again</ButtonOutline>
          </>
        )}
        {showChanges && !isGenerating && (
          <ButtonOutline onClick={handleSubmitChanges} disabled={!requestedChanges.trim()}>
            Regenerate
          </ButtonOutline>
        )}
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
      </div>
    </div>
  );
}
