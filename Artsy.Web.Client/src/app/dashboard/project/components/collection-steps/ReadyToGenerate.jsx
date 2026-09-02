import React, { useCallback, useState, useRef, useMemo, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import { useDashboard } from '@/context/dashboard';
import { artworkThumbUrl } from '@/utils/artworkUrls';
import Button from '@/components/ui/button';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Checked from '@/components/ui/checked';
import Spinner from '@/components/ui/spinner';

export default function ReadyToGenerate() {
  const {
    collectionId, setCollectionId, collectionArtwork, setCollectionArtwork, blueprints,
    isGeneratingAll, generatingProgress, generatingMessage,
    generationError, setGenerationError,
    generatedArtworks, currentGeneratingIndex, currentGeneratingItemId,
    doGenerateAll,
    setArtworkPreview, onClose, onSaved, api,
    projectId, cancelRef, cancelAll, STEPS,
    upscaleComplete, setUpscaleComplete,
    setStep, loadImageModels,
    ensureCollection, goBack,
  } = useCollection();
  const { refreshTokens } = useDashboard();

  const acceptedArtworks = useMemo(() => {
    return collectionArtwork.filter(a =>
      a.active && a.imageModel !== 'custom'
    );
  }, [collectionArtwork]);
  const [thumbRetried, setThumbRetried] = useState({});
  const [thumbFailed, setThumbFailed] = useState({});
  const retryRef = useRef({});

  const artworkImages = acceptedArtworks.flatMap(a => {
    const thumbs = [];
    const cacheBust = Math.floor(Math.random() * 1000000);

    // For group artworks, show one image per group (the combined image before cutting)
    if (a.hasGroups && a.groupPlacements) {
      for (const grp of a.groupPlacements) {
        const groupFullSize = grp.placements.every(p => p.fullSize);
        thumbs.push({
          url: artworkThumbUrl(collectionId, a.itemId, a.id, { cacheBust }),
          artwork: a,
          placementIndex: -1,
          groupId: grp.groupId,
          fullSize: groupFullSize,
        });
      }
    }

    // Show non-group placement thumbnails
    const nonGroupPlacements = (a.placements || []).filter(p => !p.groupId);
    for (const p of nonGroupPlacements) {
      thumbs.push({
        url: artworkThumbUrl(collectionId, a.itemId, a.id, { placementIndex: p.index, cacheBust }),
        artwork: a,
        placementIndex: p.index,
        fullSize: p.fullSize, // per-placement fullSize
      });
    }

    // If no placements at all, show the base artwork
    if (thumbs.length === 0) {
      thumbs.push({
        url: artworkThumbUrl(collectionId, a.itemId, a.id, { cacheBust }),
        artwork: a,
        placementIndex: -1,
        fullSize: a.fullSize,
      });
    }

    return thumbs;
  });

  const handleImageError = useCallback(async (index) => {
    if (retryRef.current[index]) return;
    retryRef.current[index] = true;

    const artwork = artworkImages[index]?.artwork;
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
  }, [artworkImages, collectionId, api, refreshTokens]);

  const displayImages = useMemo(() => {
    return artworkImages.map((img, i) => {
      if (thumbFailed[i]) return null;
      if (thumbRetried[i]) return `${img.url}${img.url.includes('?') ? '&' : '?'}r=${thumbRetried[i]}`;
      return img.url;
    }).filter(Boolean);
  }, [artworkImages, thumbRetried, thumbFailed]);

  const pendingCount = useMemo(() => {
    // Count the number of placement images that need upscaling (per-placement fullSize)
    return artworkImages.filter(img => !img.fullSize).length;
  }, [artworkImages]);

  const pendingTokens = pendingCount * 2;

  useEffect(() => {
    if (!isGeneratingAll && pendingCount === 0) {
      setUpscaleComplete(true);
    } else if (pendingCount > 0) {
      setUpscaleComplete(false);
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
    cancelAll();
    onClose();
  }, [cancelAll, onClose]);

  const [upscalingAgain, setUpscalingAgain] = useState({});
  const [hoveredIdx, setHoveredIdx] = useState(null);

  const handleUpscaleAgain = useCallback(async (img) => {
    if (!collectionId || !projectId) return;
    const key = `${img.artwork.itemId}-${img.groupId || ''}-${img.placementIndex ?? ''}`;
    setUpscalingAgain(prev => ({ ...prev, [key]: true }));
    try {
      const res = await api.upscaleArtwork({
        projectId,
        collectionId,
        itemId: img.artwork.itemId,
        force: true,
        groupId: img.groupId || undefined,
        placementIndex: img.placementIndex != null && img.placementIndex >= 0 ? img.placementIndex : undefined,
      });
      if (res.data.success) {
        // Refresh artwork data from server to get updated fullSize
        const artRes = await api.getCollectionArtwork(collectionId);
        if (artRes.data.success) {
          setCollectionArtwork(artRes.data.data || []);
        }
        refreshTokens();
      }
    } catch (error) {
      console.error('Upscale again failed:', error);
    } finally {
      setUpscalingAgain(prev => ({ ...prev, [key]: false }));
    }
  }, [collectionId, projectId, api, setCollectionArtwork, refreshTokens]);

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
    const img = artworkImages[i];
    const artwork = img?.artwork;
    if (!artwork || !img) return null;

    const isAlreadyUpscaled = img.fullSize; // per-placement fullSize
    const isCurrent = isGeneratingAll && artwork?.itemId === currentItemGenId;
    // Match completed tasks by itemId + groupId or placementIndex
    const isDone = generatedArtworks.some(g =>
      g.itemId === artwork?.itemId &&
      (g.groupId ? g.groupId === img.groupId : !g.groupId && !img.groupId) &&
      (g.placementIndex != null ? g.placementIndex === img.placementIndex : g.placementIndex == null && img.placementIndex == null)
    );
    const isUpscalingAgain = upscalingAgain[`${artwork.itemId}-${img.groupId || ''}-${img.placementIndex ?? ''}`];

    // Show spinner on pending images for the current item being upscaled
    if (isCurrent && !isDone && !isAlreadyUpscaled) {
      return (
        <div className="absolute inset-0 flex items-center justify-center bg-black/40 rounded-lg">
          <Spinner className="text-2xl text-white" />
        </div>
      );
    }
    if (isUpscalingAgain) {
      return (
        <div className="absolute inset-0 flex items-center justify-center bg-black/40 rounded-lg">
          <Spinner className="text-2xl text-white" />
        </div>
      );
    }
    if (isDone || isAlreadyUpscaled) {
      return (
        <div
          className="absolute inset-0 flex flex-col items-center justify-between pt-4 pb-2"
          onMouseEnter={() => setHoveredIdx(i)}
          onMouseLeave={() => setHoveredIdx(null)}
        >
          <Checked checked={true} />
          {isAlreadyUpscaled && hoveredIdx === i && (
            <Button
              onClick={(e) => {
                e.stopPropagation();
                handleUpscaleAgain(img);
              }}
              size="small"
              className="!text-xs"
            >
              Upscale Again
            </Button>
          )}
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
              imageWidth="150px"
              imageHeight="150px"
              imageClassName="!max-h-none w-full h-full object-contain rounded-lg"
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
            Upscaling complete! {artworkImages.filter(img => img.fullSize).length} image{artworkImages.filter(img => img.fullSize).length !== 1 ? 's' : ''} upscaled to full size.
          </p>
          <div className="buttons flex justify-end gap-2 mt-auto">
            <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
            <ButtonOutline color="gray" className="cancel" onClick={onClose}>Close</ButtonOutline>
            <ButtonOutline onClick={handleNext}>Next</ButtonOutline>
          </div>
        </>
      ) : (
        <>
          {collectionArtwork.some(a => a.needsRegeneration) && (
            <p className="text-center text-sm text-yellow-600 dark:text-yellow-400 mb-4">
              Blueprint placements have changed. Some artworks need to be regenerated.
            </p>
          )}
          {pendingCount === 0 ? (
            <p className="text-center text-lg mb-4">
              All artworks have been upscaled.
            </p>
          ) : (
            <>
              <p className="text-center text-lg mb-2">
                Ready to upscale {pendingCount} artwork{pendingCount !== 1 ? 's' : ''} at full size.
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
