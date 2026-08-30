import React, { useCallback, useMemo, useEffect, useState, useRef, lazy, Suspense } from 'react';
import { useSession } from '@/context/session';
import { useCollection } from '@/context/collection';
import { useDashboard } from '@/context/dashboard';
import { CustomImages } from '@/api/user/customImages';
import { artworkImageUrl, artworkThumbUrl } from '@/utils/artworkUrls';
import TextArea from '@/components/forms/textarea';
import Select from '@/components/forms/select';
import SelectGrid from '@/components/ui/select-grid';
import { aspectRatioOptions } from '@/components/ui/aspect-ratio-icons';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Spinner from '@/components/ui/spinner';
import Icon from '@/components/ui/icon';
const TokenCostBreakdownModal = lazy(() => import('../TokenCostBreakdownModal'));
const CustomImageSelector = lazy(() => import('../CustomImageSelector'));
import PatternSettings from './PatternSettings';
import PatternPreview from './PatternPreview';

export default function ArtworkQuestions() {
  const session = useSession();
  const { getCustomImageUrl } = CustomImages(session);
  const { refreshTokens } = useDashboard();
  const {
    aiItems, currentItemIndex, currentItem,
    currentItemQuestions, itemAnswers, setItemAnswers,
    ensureCollection, saveAnswers, buildProjectAnswers,
    setStep, doGeneratePreview, STEPS, onClose, goBack,
    collectionArtwork, collectionId, api, setArtworkPreview,
    currentArtwork,
    imageModels, selectedImageModel, setSelectedImageModel, loadImageModels,
    design, setDesign, patternSettings, setPatternSettings,
    optionalPrompt, setOptionalPrompt, cancelAll,
  } = useCollection();

  const [previews, setPreviews] = useState([]);
  const [referencePreviews, setReferencePreviews] = useState([]);
  const [customImageRefs, setCustomImageRefs] = useState([]);
  const [collectionRefs, setCollectionRefs] = useState([]);
  const [showImageSelector, setShowImageSelector] = useState(false);
  const [deletingRefId, setDeletingRefId] = useState(null);
  const [calculatedTokens, setCalculatedTokens] = useState(null);
  const [estimateGenerations, setEstimateGenerations] = useState(null);
  const [estimatingTokens, setEstimatingTokens] = useState(false);
  const [showCostBreakdown, setShowCostBreakdown] = useState(false);
  const estimateTimerRef = useRef(null);
  const [patternAspectRatio, setPatternAspectRatio] = useState('1:1');
  const [isGeneratingPreview, setIsGeneratingPreview] = useState(false);
  const [previewImageOverride, setPreviewImageOverride] = useState(null);
  const optionalPromptTimerRef = useRef(null);
  const optionalPromptRef = useRef(null);
  const loadedOptionalPromptRef = useRef('');

  const autoResizeTextarea = useCallback((el) => {
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }, []);

  // Auto-resize on value change (e.g. when loading from saved data)
  useEffect(() => {
    if (optionalPromptRef.current) {
      autoResizeTextarea(optionalPromptRef.current);
    }
  }, [optionalPrompt, autoResizeTextarea]);

  // Track the loaded value so auto-save only fires on user edits, not on load
  useEffect(() => {
    loadedOptionalPromptRef.current = optionalPrompt;
  }, [currentItem]); // only update when item changes, not on every keystroke

  // Debounced auto-save of optional prompt to collection artwork (2 second delay)
  // Only fires if the value differs from what was loaded (i.e. user actually edited it)
  useEffect(() => {
    if (!currentItem || !collectionId) return;
    if (optionalPrompt === loadedOptionalPromptRef.current) return;
    if (optionalPromptTimerRef.current) clearTimeout(optionalPromptTimerRef.current);
    optionalPromptTimerRef.current = setTimeout(async () => {
      try {
        await api.updateCollectionArtworkOptionalPrompt({ collectionId, itemId: currentItem.id, optionalPrompt });
        loadedOptionalPromptRef.current = optionalPrompt;
      } catch (error) {
        console.error('Failed to auto-save optional prompt:', error?.response?.data || error);
      }
    }, 2000);
    return () => { if (optionalPromptTimerRef.current) clearTimeout(optionalPromptTimerRef.current); };
  }, [optionalPrompt, currentItem, api, collectionId]);

  const latestRef = useRef({});
  latestRef.current = { collectionArtwork, collectionId, getCustomImageUrl };

  useEffect(() => {
    if (!currentItem || !api) return;
    let cancelled = false;

    api.getItemPreviews(currentItem.id).then((res) => {
      if (!cancelled && res.data.success) {
        setPreviews(res.data.data || []);
      }
    }).catch(() => {});

    api.getItemReferences(currentItem.id).then(async (res) => {
      if (!cancelled && res.data.success) {
        const refs = res.data.data || [];
        const artworkRefs = refs.filter(r => r.artworkId);
        const customRefs = refs.filter(r => r.customImageId);
        const { collectionArtwork: ca, collectionId: cId, getCustomImageUrl: getUrl } = latestRef.current;

        if (!cancelled) {
          const refArtworks = [];
          for (const r of artworkRefs) {
            const generated = ca.filter(a => a.active && String(a.itemId) === String(r.artworkId));
            for (const a of generated) {
              refArtworks.push({
                thumb: artworkThumbUrl(cId, r.artworkId, a.id),
                full: artworkImageUrl(cId, r.artworkId, a.id, { fullSize: !!a.fullSize }),
              });
            }
          }
          setReferencePreviews(refArtworks);
          setCustomImageRefs(customRefs.map(r => ({
            thumb: getUrl(r.customImageId, true),
            full: getUrl(r.customImageId, false),
          })));
        }
      }
    }).catch(() => {});

    // Load collection artwork references
    if (collectionId) {
      api.getCollectionArtworkReferences(collectionId, currentItem.id).then((res) => {
        if (!cancelled && res.data.success) {
          setCollectionRefs(res.data.data || []);
        }
      }).catch(() => {});
    } else {
      setCollectionRefs([]);
    }

    return () => { cancelled = true; };
  }, [currentItem, collectionId, api]);

  const handleItemAnswerChange = useCallback((questionId, value) => {
    setItemAnswers((prev) => ({ ...prev, [questionId]: value }));
  }, [setItemAnswers]);

  const handleAddReference = useCallback(async (customImage) => {
    if (!customImage || !collectionId || !currentItem) return;
    try {
      const colId = await ensureCollection();
      if (!colId) return;
      const res = await api.addCollectionArtworkReference({
        collectionId: colId,
        itemId: currentItem.id,
        customImageId: customImage.id,
      });
      if (res.data.success) {
        setCollectionRefs(prev => [...prev, res.data.data]);
      }
    } catch (error) {
      console.error('Failed to add reference:', error);
    }
  }, [collectionId, currentItem, api, ensureCollection]);

  const handleDeleteReference = useCallback(async (refId) => {
    setDeletingRefId(refId);
    try {
      const res = await api.deleteCollectionArtworkReference({ id: refId });
      if (res.data.success) {
        setCollectionRefs(prev => prev.filter(r => r.id !== refId));
      }
    } catch (error) {
      console.error('Failed to delete reference:', error);
    } finally {
      setDeletingRefId(null);
    }
  }, [api]);

  const handleNext = useCallback(async () => {
    const colId = await ensureCollection();
    if (!colId) return;
    await saveAnswers(colId);
    setStep(STEPS.ARTWORK_PREVIEW);
    await doGeneratePreview(colId);
  }, [ensureCollection, saveAnswers, doGeneratePreview, setStep, STEPS]);

  const existingArtworks = useMemo(() => {
    if (!currentItem || !collectionId) return [];
    return collectionArtwork
      .filter(a => a.active && String(a.itemId) === String(currentItem.id))
      .map(a => ({
        id: a.id,
        thumbUrl: artworkThumbUrl(collectionId, currentItem.id, a.id),
        fullUrl: artworkImageUrl(collectionId, currentItem.id, a.id, { fullSize: !!a.fullSize }),
      }));
  }, [currentItem, collectionArtwork, collectionId]);

  const thumbImages = useMemo(() => existingArtworks.map(a => a.thumbUrl), [existingArtworks]);
  const fullImages = useMemo(() => existingArtworks.map(a => a.fullUrl), [existingArtworks]);

  const previewThumbImages = useMemo(() => {
    if (!currentItem) return [];
    const refThumbs = referencePreviews.map(r => r.thumb);
    const customThumbs = customImageRefs.map(r => r.thumb);
    const ownThumbs = previews.map(p => api.getItemPreviewUrl(currentItem.id, p.id, true));
    return [...refThumbs, ...customThumbs, ...ownThumbs];
  }, [currentItem, previews, referencePreviews, customImageRefs, api]);

  // Collection reference images for the carousel
  const collectionRefImages = useMemo(() => {
    return collectionRefs.map(r => ({
      id: r.id,
      thumb: getCustomImageUrl(r.customImageId, true),
      full: getCustomImageUrl(r.customImageId, false),
      fileName: r.fileName,
    }));
  }, [collectionRefs, getCustomImageUrl]);

  const collectionRefThumbs = useMemo(() => collectionRefImages.map(r => r.thumb), [collectionRefImages]);
  const collectionRefFulls = useMemo(() => collectionRefImages.map(r => r.full), [collectionRefImages]);

  useEffect(() => {
    loadImageModels();
  }, [loadImageModels]);

  useEffect(() => {
    if (currentArtwork?.aspectRatio) {
      setPatternAspectRatio(currentArtwork.aspectRatio);
    }
  }, [currentArtwork]);

  useEffect(() => {
    if (imageModels.length && currentArtwork?.imageModel) {
      const found = imageModels.find(m => m.model === currentArtwork.imageModel);
      if (found) setSelectedImageModel(found);
    }
  }, [currentArtwork, imageModels, setSelectedImageModel]);

  useEffect(() => {
    if (!currentItem || !selectedImageModel) {
      setCalculatedTokens(null);
      return;
    }
    if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current);
    setEstimatingTokens(true);
    estimateTimerRef.current = setTimeout(async () => {
      try {
        const res = await api.estimateItemTokens(currentItem.id, selectedImageModel.id, collectionId, design);        if (res.data.success) {
          const data = res.data.data;
          const total = typeof data === 'number' ? data : data.totalTokens;
          setCalculatedTokens(total);
          setEstimateGenerations(data?.generations || null);
          if (data?.generations) {
            console.log(`[EstimateItemTokens] Item ${currentItem.id}:`, JSON.stringify(data, null, 2));
          }
        } else {
          setCalculatedTokens(null);
          setEstimateGenerations(null);
        }
      } catch {
        setCalculatedTokens(null);
      } finally {
        setEstimatingTokens(false);
      }
    }, 500);
    return () => { if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current); };
  }, [currentItem, selectedImageModel, currentArtwork, api, collectionId, collectionRefs, design]);

  const modelOptions = useMemo(() =>
    (imageModels || []).map((m) => ({ value: m.id, label: m.name })),
  [imageModels]);

  const handleModelChange = useCallback((e) => {
    const id = parseInt(e.target.value);
    const model = (imageModels || []).find((m) => m.id === id);
    setSelectedImageModel(model || null);
  }, [imageModels, setSelectedImageModel]);

  const handlePatternAspectRatioChange = useCallback(async (value) => {
    setPatternAspectRatio(value);
    if (!currentItem) return;
    try {
      await api.updateItemAspectRatio({ itemId: currentItem.id, aspectRatio: value, design });
    } catch (error) {
      console.error('Failed to save aspect ratio:', error?.response?.data || error);
    }
  }, [currentItem, api, design]);

  const handleGeneratePreview = useCallback(async () => {
    if (!currentItem || isGeneratingPreview) return;
    if (!selectedImageModel) return;

    setIsGeneratingPreview(true);
    setPreviewImageOverride(null);
    try {
      // Save form changes before generating preview
      // 1. Save AI image model
      if (currentArtwork?.imageModel !== selectedImageModel.model) {
        await api.updateItemImageModel({ itemId: currentItem.id, imageModel: selectedImageModel.model });
      }
      // 2. Save design + aspect ratio (pattern mode)
      if (design === 'pattern') {
        await api.updateItemAspectRatio({ itemId: currentItem.id, aspectRatio: patternAspectRatio, design });
      } else if (currentArtwork?.design !== design) {
        await api.updateItemAspectRatio({ itemId: currentItem.id, aspectRatio: currentArtwork?.aspectRatio || '1:1', design });
      }
      // 3. Save optional prompt to collection artwork
      if (collectionId) {
        await api.updateCollectionArtworkOptionalPrompt({ collectionId, itemId: currentItem.id, optionalPrompt });
      }

      const answerList = [
        ...buildProjectAnswers(),
        ...Object.entries(itemAnswers || {})
          .filter(([_, value]) => value && value.trim())
          .map(([questionId, answer]) => ({ questionId, answer })),
      ];

      const response = await api.generateItemPreview({
        itemId: currentItem.id,
        modelId: selectedImageModel.id,
        answers: answerList,
        design,
        collectionId,
      });

      if (response.data.success) {
        // Refresh previews list
        const updated = await api.getItemPreviews(currentItem.id);
        if (updated.data.success) {
          setPreviews(updated.data.data || []);
        }
        refreshTokens();
        // Set the preview image override to the newest preview
        const newestPreview = (updated.data.data || [])[0];
        if (newestPreview) {
          setPreviewImageOverride(api.getItemPreviewUrl(currentItem.id, newestPreview.id, true));
        }
      }
    } catch (error) {
      console.error('Preview generation error:', error?.response?.data || error);
    } finally {
      setIsGeneratingPreview(false);
    }
  }, [currentItem, isGeneratingPreview, selectedImageModel, api, design, buildProjectAnswers, itemAnswers, refreshTokens, collectionId, currentArtwork, optionalPrompt, patternAspectRatio]);

  const previewFullImages = useMemo(() => {
    if (!currentItem) return [];
    const refFulls = referencePreviews.map(r => r.full);
    const customFulls = customImageRefs.map(r => r.full);
    const ownFulls = previews.map(p => api.getItemPreviewUrl(currentItem.id, p.id, false));
    return [...refFulls, ...customFulls, ...ownFulls];
  }, [currentItem, previews, referencePreviews, customImageRefs, api]);

  const renderReferenceOverlay = useCallback((i) => {
    const ref = collectionRefImages[i];
    if (!ref) return null;
    return (
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          handleDeleteReference(ref.id);
        }}
        disabled={deletingRefId === ref.id}
        className="absolute bottom-1 right-1 w-7 h-7 flex items-center justify-center bg-black/60 text-white rounded hover:bg-red-600/80 transition"
        title="Remove reference"
      >
        {deletingRefId === ref.id ? (
          <Spinner className="text-sm" />
        ) : (
          <Icon name="delete" />
        )}
      </button>
    );
  }, [collectionRefImages, deletingRefId, handleDeleteReference]);

  return (
    <div className="flex flex-col h-full">
      <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">
        Artwork {currentItemIndex + 1} of {aiItems.length}: {currentItem?.title || 'Untitled'}
      </h3>
      {currentArtwork?.needsRegeneration && (
        <p className="text-center text-sm text-yellow-600 dark:text-yellow-400 mb-4">
          Product selections have changed. Please regenerate this artwork to include placements for all selected products.
        </p>
      )}
      {isGeneratingPreview ? (
        <div className="w-full max-w-[500px] mx-auto rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-4">
          <div className="w-full h-[400px] flex items-center justify-center bg-gray-100 dark:bg-gray-700">
            <Spinner className="text-3xl" />
          </div>
        </div>
      ) : design === 'pattern' ? (
        <PatternPreview
          patternSettings={patternSettings}
          previewImage={previewImageOverride || (thumbImages.length > 0 ? thumbImages[0] : (previewThumbImages.length > 0 ? previewThumbImages[0] : null))}
        />
      ) : (
        <>
          {previewImageOverride ? (
            <div className="w-full max-w-[300px] mx-auto rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-4">
              <img
                src={previewImageOverride}
                alt="Preview"
                className="!max-w-[300px] !max-h-[300px] object-contain"
              />
            </div>
          ) : (
            <>
              {thumbImages.length > 0 && (
                <div className="w-full max-w-[300px] mx-auto rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-4">
                  <Carousel
                    images={thumbImages}
                    alt="Generated Artwork"
                    singleImage
                    infiniteScroll
                    imageClassName="!max-w-[300px] !max-h-[300px] object-contain"
                    onImageClick={(_src, index) => setArtworkPreview({ images: fullImages, src: fullImages[index], alt: 'Artwork Preview' })}
                    placeholder="No Thumbnail"
                  />
                </div>
              )}
              {thumbImages.length === 0 && previewThumbImages.length > 0 && (
                <div className="w-full max-w-[300px] mx-auto rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-4">
                  <Carousel
                    images={previewThumbImages}
                    alt="Artwork Previews"
                    singleImage
                    infiniteScroll
                    imageClassName="!max-w-[300px] !max-h-[300px] object-contain"
                    onImageClick={(_src, index) => setArtworkPreview({ images: previewFullImages, src: previewFullImages[index], alt: 'Artwork Preview' })}
                    placeholder="No Previews"
                  />
                </div>
              )}
            </>
          )}
        </>
      )}
      <div className="flex flex-wrap items-end gap-4 justify-between mb-4">
        <div className="flex items-start gap-4">
          <div className="min-w-[200px]">
            <Select
              label="AI Image Model"
              name="imageModel"
              value={selectedImageModel?.id || ''}
              onChange={handleModelChange}
              options={modelOptions}
              fitContent
            />
          </div>
          <div className="min-w-[140px]">
            <Select
              label="Design"
              name="design"
              value={design}
              onChange={(e) => setDesign(e.target.value)}
              options={[
                { value: 'artwork', label: 'Artwork' },
                { value: 'pattern', label: 'Pattern' },
              ]}
              fitContent
            />
          </div>
          {design === 'pattern' && (
            <div style={{ width: '10em' }}>
              <SelectGrid
                name="patternAspectRatio"
                label="Aspect Ratio"
                options={aspectRatioOptions}
                value={patternAspectRatio}
                onChange={handlePatternAspectRatioChange}
                columns={6}
                buttonWidth={160}
                dropdownWidth={400}
                placeholder="Select aspect ratio..."
              />
            </div>
          )}
        </div>
        <div className="flex flex-col items-end gap-1" style={{ marginBottom: '2em' }}>
          {estimatingTokens ? (
            <div className="flex items-center gap-1 text-sm text-gray-500 dark:text-gray-400">
              <Spinner className="text-sm" />
              <span>Estimating...</span>
            </div>
          ) : calculatedTokens !== null ? (
            <>
              <div className="text-sm text-gray-500 dark:text-gray-400">
                <span className="font-medium">Token Cost: <span className="text-white font-bold">{calculatedTokens.toLocaleString()}</span></span>
              </div>
              {estimateGenerations && estimateGenerations.length > 0 && (
                <ButtonOutline color="gray" size="small" onClick={() => setShowCostBreakdown(true)}>
                  Cost Breakdown
                </ButtonOutline>
              )}
            </>
          ) : null}
        </div>
      </div>
      {design === 'pattern' && (
        <PatternSettings
          patternSettings={patternSettings}
          setPatternSettings={setPatternSettings}
        />
      )}
      <div className="mb-4">
        <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Prompt (optional)</label>
        <textarea
          ref={optionalPromptRef}
          name="optionalPrompt"
          value={optionalPrompt}
          onChange={(e) => {
            setOptionalPrompt(e.target.value);
            autoResizeTextarea(e.target);
          }}
          onInput={(e) => autoResizeTextarea(e.target)}
          placeholder="Additional prompt instructions appended to the generated prompt..."
          rows={1}
          className="w-full px-3 py-2 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 resize-none overflow-hidden"
          style={{ minHeight: '2.25em' }}
        />
      </div>
      {currentItemQuestions.length > 0 && (
        <div className="max-h-[40vh] overflow-y-auto">
          <div className="space-y-4">
            {currentItemQuestions.map((question) => (
              <TextArea
                key={question.id}
                name={`item-answer-${question.id}`}
                label={question.question}
                value={itemAnswers[question.id] || ''}
                onChange={(e) => handleItemAnswerChange(question.id, e.target.value)}
                placeholder="Enter an answer"
                rows={3}
                maxLength={255}
              />
            ))}
          </div>
        </div>
      )}

      {/* References carousel */}
      {collectionRefThumbs.length > 0 && (
        <div className="mt-4 mb-8">
          <h4 className="text-sm font-medium text-gray-600 dark:text-gray-300 mb-2">References</h4>
          <Carousel
            images={collectionRefThumbs}
            alt="Reference Images"
            imageWidth="120px"
            imageHeight="120px"
            imageClassName="object-contain"
            onImageClick={(_src, index) => setArtworkPreview({ images: collectionRefFulls, src: collectionRefFulls[index], alt: 'Reference Preview' })}
            overlayRender={renderReferenceOverlay}
          />
        </div>
      )}

      <div className="buttons flex justify-end gap-2 mt-4 mt-auto">
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={() => { cancelAll(); onClose(); }}>Cancel</ButtonOutline>
        <ButtonOutline onClick={() => setShowImageSelector(true)}>+ Image Reference</ButtonOutline>
        <ButtonOutline
          onClick={handleGeneratePreview}
          disabled={isGeneratingPreview || !selectedImageModel}
        >
          {isGeneratingPreview ? <Spinner className="text-sm" /> : 'Preview'}
        </ButtonOutline>
        <ButtonOutline color="green" onClick={handleNext}>Generate Artwork</ButtonOutline>
      </div>
      {showCostBreakdown && (
        <Suspense fallback={null}>
          <TokenCostBreakdownModal
            show={showCostBreakdown}
            onClose={() => setShowCostBreakdown(false)}
            generations={estimateGenerations}
            design={design}
          />
        </Suspense>
      )}
      {showImageSelector && (
        <Suspense fallback={null}>
          <CustomImageSelector
            show={showImageSelector}
            onSelect={(img) => {
              handleAddReference(img);
              setShowImageSelector(false);
            }}
            onClose={() => setShowImageSelector(false)}
          />
        </Suspense>
      )}
    </div>
  );
}
