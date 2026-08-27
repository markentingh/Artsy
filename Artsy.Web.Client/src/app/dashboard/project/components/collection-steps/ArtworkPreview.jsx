import React, { useCallback, useMemo, useState } from 'react';
import { useCollection } from '@/context/collection';
import { useDashboard } from '@/context/dashboard';
import { artworkImageUrl, artworkJpgWithBgUrl } from '@/utils/artworkUrls';
import TextArea from '@/components/forms/textarea';
import Select from '@/components/forms/select';
import ButtonOutline from '@/components/ui/button-outline';
import Button from '@/components/ui/button';
import Spinner from '@/components/ui/spinner';
import Tooltip from '@/components/ui/tooltip';

export default function ArtworkPreview() {
  const {
    aiItems, currentItemIndex, currentItem,
    isGenerating, previewImageData, currentArtwork, setCurrentArtwork,
    showChanges, setShowChanges,
    requestedChanges, setRequestedChanges,
    collectionId, ensureCollection, projectId,
    doGeneratePreview, advanceToNextItem,
    setCollectionArtwork,
    api, onClose, onSaved, setArtworkPreview, setStep, STEPS, goBack,
    previewGenerationIndex, previewGenerationTotal, generatingMessage,
    previewGenerationThumbs,
    itemAnswers, buildProjectAnswers, selectedImageModel,
  } = useCollection();

  const { refreshTokens } = useDashboard();

  const [changeMode, setChangeMode] = useState('regenerate');
  const [isFixing, setIsFixing] = useState(false);
  const [regeneratingIndex, setRegeneratingIndex] = useState(null);
  const [regeneratedCacheBust, setRegeneratedCacheBust] = useState({});

  const stripThumb = (url) => (url || '').replace('thumb=true&', '').replace('&thumb=true', '').replace('?thumb=true', '?').replace(/\?$/, '');

  const rnd = () => Math.floor(Math.random() * 100000);

  const hasOpacity = currentArtwork?.opacity === true;
  const totalPlacements = currentArtwork?.totalPlacements || 0;
  const hasVariants = totalPlacements > 0 && !currentArtwork?.hasGroups;
  const isGroupArtwork = !!currentArtwork?.hasGroups;

  const carouselImages = useMemo(() => {
    if (!previewImageData || !currentArtwork || !collectionId) return null;

    // For seamless group artworks, show the main combined image (not individual cut-ups)
    if (isGroupArtwork) {
      if (hasOpacity) {
        const pngUrl = previewImageData;
        const jpgWithBgUrl = artworkJpgWithBgUrl(collectionId, currentItem.id, currentArtwork.id, { thumb: true, cacheBust: rnd() });
        return [pngUrl, jpgWithBgUrl];
      }
      return null; // Single image, no carousel needed
    }

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
  }, [previewImageData, currentArtwork, hasOpacity, hasVariants, isGroupArtwork, totalPlacements, collectionId, currentItem]);

  // Full-size image URLs for the click-to-enlarge preview modal
  const fullSizeImages = useMemo(() => {
    if (!currentArtwork || !collectionId) return null;

    // For group artworks, show the main image
    if (isGroupArtwork) {
      if (hasOpacity) {
        return [
          artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust: rnd() }),
          artworkJpgWithBgUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust: rnd() }),
        ];
      }
      return [artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust: rnd() })];
    }

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

  // Build the grid of placement thumbnail images (same pattern as ReadyToGenerate step)
  const placementGridImages = useMemo(() => {
    if (!currentArtwork || !collectionId || !currentItem) return [];
    const images = [];
    const cacheBust = rnd();

    // For group artworks, show the base combined image (not individual segments)
    if (currentArtwork.hasGroups) {
      images.push({
        index: 0,
        thumb: artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { thumb: true, cacheBust }),
        full: artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust }),
      });
    }

    // Show non-group placement thumbnails
    const nonGroupPlacements = (currentArtwork.placements || []).filter(p => !p.groupId);
    for (const p of nonGroupPlacements) {
      images.push({
        index: p.index,
        thumb: artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { thumb: true, cacheBust, placementIndex: p.index }),
        full: artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust, placementIndex: p.index }),
      });
    }

    // If no placements at all, show the base artwork
    if (images.length === 0) {
      images.push({
        index: 0,
        thumb: previewImageData || artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { thumb: true, cacheBust }),
        full: artworkImageUrl(collectionId, currentItem.id, currentArtwork.id, { cacheBust }),
      });
    }

    return images;
  }, [currentArtwork, collectionId, currentItem, previewImageData]);

  const handleRegeneratePlacement = useCallback(async (placementIndex) => {
    if (!currentItem || regeneratingIndex !== null) return;
    setRegeneratingIndex(placementIndex);
    try {
      const colId = collectionId || await ensureCollection();
      if (!colId) { setRegeneratingIndex(null); return; }

      const answerList = [
        ...buildProjectAnswers(),
        ...Object.entries(itemAnswers || {})
          .filter(([_, value]) => value && value.trim())
          .map(([questionId, answer]) => ({ questionId, answer })),
      ];

      const res = await api.generateCollectionArtwork({
        projectId,
        collectionId: colId,
        itemId: currentItem.id,
        width: 2048,
        height: 2048,
        answers: answerList,
        requestedChanges: null,
        modelId: selectedImageModel?.id,
        generationIndex: placementIndex,
      });

      if (res.data.success) {
        // The API returns the artwork entity without the placements array,
        // so we only update the cache bust for this specific placement to refresh its image
        setRegeneratedCacheBust(prev => ({ ...prev, [placementIndex]: Math.floor(Math.random() * 1000000) }));
        refreshTokens();
      }
    } catch (error) {
      console.error('Regenerate placement error:', error?.response?.data || error);
    } finally {
      setRegeneratingIndex(null);
    }
  }, [currentItem, regeneratingIndex, collectionId, ensureCollection, buildProjectAnswers, itemAnswers, api, projectId, selectedImageModel, refreshTokens]);

  const handleTryAgain = useCallback(() => {
    setStep(STEPS.ARTWORK_QUESTIONS);
  }, [setStep, STEPS]);

  const handleMakeChanges = useCallback(() => {
    setShowChanges(true);
  }, [setShowChanges]);

  const handleSubmitChanges = useCallback(() => {
    if (changeMode === 'fix') {
      // Fix seamless placements: re-cut the existing artwork image
      setIsFixing(true);
      const doFix = async () => {
        const colId = collectionId || await ensureCollection();
        if (!colId) { setIsFixing(false); return; }
        try {
          const resp = await api.fixSeamlessPlacements({
            projectId,
            collectionId: colId,
            itemId: currentItem.id,
          });
          if (resp.data.success) {
            // Refresh artwork data to get updated group images
            const artRes = await api.getCollectionArtwork(colId);
            if (artRes.data.success) {
              setCollectionArtwork(artRes.data.data || []);
            }
            setShowChanges(false);
            setChangeMode('regenerate');
          }
        } catch (error) {
          console.error('fixSeamlessPlacements error:', error?.response?.data || error);
        } finally {
          setIsFixing(false);
        }
      };
      doFix();
      return;
    }
    if (!requestedChanges.trim()) return;
    setShowChanges(false);
    if (collectionId) {
      doGeneratePreview(collectionId);
    } else {
      ensureCollection().then((colId) => {
        if (colId) doGeneratePreview(colId);
      });
    }
  }, [changeMode, requestedChanges, collectionId, doGeneratePreview, ensureCollection, setShowChanges, api, currentItem, setCollectionArtwork, projectId]);

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
        <div className="min-h-[100px] flex items-center justify-center">
          {isGenerating && previewGenerationThumbs.length === 0 ? (
            <Spinner className="text-3xl my-16" />
          ) : isGenerating && previewGenerationThumbs.length > 0 ? (
            <div className="flex flex-wrap justify-center gap-2 max-w-[400px]">
              {previewGenerationThumbs.map((thumb) => (
                <img
                  key={thumb.index}
                  src={thumb.url}
                  alt={`Generation ${thumb.index + 1}`}
                  className="w-[120px] h-[120px] object-contain rounded border border-gray-300 dark:border-gray-600 cursor-pointer bg-gray-100 dark:bg-gray-700"
                  onClick={() => setArtworkPreview({ images: previewGenerationThumbs.map(t => stripThumb(t.url)), src: stripThumb(thumb.url), _idx: thumb.index })}
                />
              ))}
              {isGenerating && (
                <div className="w-[120px] h-[120px] flex items-center justify-center rounded border border-gray-300 dark:border-gray-600 bg-gray-100 dark:bg-gray-700">
                  <Spinner className="text-2xl" />
                </div>
              )}
            </div>
          ) : placementGridImages.length > 0 ? (
            <div className="flex flex-wrap justify-center gap-3 max-w-[500px]">
              {placementGridImages.map((img) => {
                const isRegenerating = regeneratingIndex === img.index;
                const r = regeneratedCacheBust[img.index];
                const thumbSrc = r
                  ? `${img.thumb}${img.thumb.includes('?') ? '&' : '?'}r=${r}`
                  : img.thumb;
                return (
                  <div
                    key={img.index}
                    className="group relative w-[150px] h-[150px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 cursor-pointer bg-gray-100 dark:bg-gray-700"
                    onClick={() => setArtworkPreview({ images: placementGridImages.map(p => p.full), src: img.full, alt: 'Artwork Preview' })}
                  >
                    {isRegenerating ? (
                      <div className="w-full h-full flex items-center justify-center">
                        <Spinner className="text-2xl" />
                      </div>
                    ) : (
                      <>
                        <img
                          src={thumbSrc}
                          alt={`Placement ${img.index + 1}`}
                          className="w-full h-full object-contain"
                        />
                        <div className="absolute inset-x-0 bottom-0 p-1 opacity-0 group-hover:opacity-100 transition">
                          <Button
                            color="green"
                            size="small"
                            className="!w-full !text-xs"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleRegeneratePlacement(img.index);
                            }}
                          >
                            Regenerate
                          </Button>
                        </div>
                      </>
                    )}
                  </div>
                );
              })}
            </div>
          ) : previewImageData ? (
            <img
              src={previewImageData}
              alt="Preview"
              className="!max-w-[350px] !max-h-[350px] object-contain cursor-pointer"
              onClick={() => setArtworkPreview({ images: (fullSizeImages || [previewImageData]).map(stripThumb), src: stripThumb(fullSizeImages?.[0] || previewImageData) })}
            />
          ) : (
            <span className="text-sm text-gray-500 dark:text-gray-400 my-16">No preview generated yet.</span>
          )}
        </div>

        {isGenerating && previewGenerationTotal > 0 && (
          <div className="w-full max-w-[350px]">
            <p className="text-center text-sm text-gray-500 dark:text-gray-400 mb-2">
              {generatingMessage || `Generating artwork ${previewGenerationIndex + 1} of ${previewGenerationTotal}`}
            </p>
            <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2 overflow-hidden">
              <div
                className="bg-blue-500 h-full rounded-full transition-all duration-300"
                style={{ width: `${Math.round(((previewGenerationIndex + 1) / previewGenerationTotal) * 100)}%` }}
              />
            </div>
          </div>
        )}

        {showChanges && !isGenerating && (
          <div className="w-full max-w-[512px]">
            {isGroupArtwork && (
              <Select
                name="changeMode"
                label="Action"
                value={changeMode}
                onChange={(e) => setChangeMode(e.target.value)}
                options={[
                  { value: 'regenerate', label: 'Regenerate Image' },
                  { value: 'fix', label: 'Fix Seamless Placements' },
                ]}
                className="mb-3"
              />
            )}
            {changeMode === 'regenerate' && (
              <TextArea
                name="requestedChanges"
                label="Requested Changes"
                value={requestedChanges}
                onChange={(e) => setRequestedChanges(e.target.value)}
                placeholder="Describe the changes you want..."
                rows={4}
              />
            )}
          </div>
        )}
      </div>
      <div className="buttons flex justify-end gap-2 items-center pt-4 mt-auto">
        {!showChanges && !isGenerating && (previewImageData || placementGridImages.length > 0) && (
          <>
            <Tooltip text="Either make changes to the generated artwork using a prompt to edit the artwork, accept the currently generated artwork, or try again by changing the original prompt text." className="pr-8" />
            <ButtonOutline color="gray" onClick={handleMakeChanges}>Make Changes</ButtonOutline>
            <ButtonOutline onClick={handleAccept}>Accept</ButtonOutline>
            <ButtonOutline color="red" onClick={handleTryAgain}>Try Again</ButtonOutline>
          </>
        )}
        {showChanges && !isGenerating && (
          <ButtonOutline
            onClick={handleSubmitChanges}
            disabled={changeMode === 'regenerate' ? !requestedChanges.trim() : isFixing}
          >
            {isFixing ? <Spinner className="text-base" /> : changeMode === 'fix' ? 'Fix Placements' : 'Regenerate'}
          </ButtonOutline>
        )}
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
      </div>
    </div>
  );
}
