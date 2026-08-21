import React, { useCallback, useMemo } from 'react';
import { useCollection } from '@/context/collection';
import { artworkImageUrl, artworkJpgWithBgUrl } from '@/utils/artworkUrls';
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

  const rnd = () => Math.floor(Math.random() * 100000);

  const hasOpacity = currentArtwork?.opacity === true;
  const totalPlacements = currentArtwork?.totalPlacements || 0;
  const hasVariants = totalPlacements > 0;

  const carouselImages = useMemo(() => {
    if (!previewImageData || !currentArtwork || !collectionId) return null;

    // When the artwork has placement variants, show each variant in the carousel
    if (hasVariants) {
      const images = [];
      for (let i = 0; i < totalPlacements; i++) {
        const url = artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { thumb: true, cacheBust: rnd(), placementIndex: i });
        images.push(url);
      }
      // For opacity artworks, also add the JPG-with-bg version of the first variant
      if (hasOpacity) {
        const jpgWithBgUrl = artworkJpgWithBgUrl(collectionId, currentItem.id, currentArtwork.id, { thumb: true, cacheBust: rnd(), placementIndex: 0 });
        images.push(jpgWithBgUrl);
      }
      return images;
    }

    // Single artwork with opacity: show PNG + JPG with background
    if (hasOpacity) {
      const pngUrl = previewImageData;
      const jpgWithBgUrl = artworkJpgWithBgUrl(collectionId, currentItem.id, currentArtwork.id, { thumb: true, cacheBust: rnd() });
      return [pngUrl, jpgWithBgUrl];
    }
    return null;
  }, [previewImageData, currentArtwork, hasOpacity, hasVariants, totalPlacements, collectionId, currentItem]);

  // Full-size image URLs for the click-to-enlarge preview modal
  const fullSizeImages = useMemo(() => {
    if (!currentArtwork || !collectionId) return null;

    if (hasVariants) {
      const images = [];
      for (let i = 0; i < totalPlacements; i++) {
        images.push(artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust: rnd(), placementIndex: i }));
      }
      if (hasOpacity) {
        images.push(artworkJpgWithBgUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust: rnd(), placementIndex: 0 }));
      }
      return images;
    }

    if (hasOpacity) {
      return [
        artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust: rnd() }),
        artworkJpgWithBgUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust: rnd() }),
      ];
    }

    return [artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust: rnd() })];
  }, [currentArtwork, hasOpacity, hasVariants, totalPlacements, collectionId, currentItem]);

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
        <div className="w-[350px] max-w-full min-h-[100px] flex items-center justify-center rounded-lg bg-gray-100 dark:bg-gray-700 overflow-hidden">
          {isGenerating ? (
            <Spinner className="text-3xl my-16" />
          ) : (hasOpacity || hasVariants) && carouselImages ? (
            <div
              className="w-full"
              style={hasOpacity ? {
                backgroundImage: 'url(/checkerboard.png)',
                backgroundSize: '20px 20px',
                backgroundRepeat: 'repeat',
              } : undefined}
            >
              <Carousel
                images={carouselImages}
                alt="Artwork preview"
                singleImage
                infiniteScroll
                imageClassName="!max-w-[350px] !max-h-[350px] object-contain"
                onImageClick={(_src, index) => setArtworkPreview({ images: fullSizeImages || carouselImages, src: fullSizeImages?.[index] || _src, _idx: index })}
              />
            </div>
          ) : previewImageData ? (
            <img
              src={previewImageData}
              alt="Preview"
              className="!max-w-[350px] !max-h-[350px] object-contain cursor-pointer"
              onClick={() => setArtworkPreview({ images: fullSizeImages || [previewImageData], src: fullSizeImages?.[0] || previewImageData })}
            />
          ) : (
            <span className="text-sm text-gray-500 dark:text-gray-400 my-16">No preview generated yet.</span>
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
