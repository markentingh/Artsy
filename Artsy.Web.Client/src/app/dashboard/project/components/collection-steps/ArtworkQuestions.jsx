import React, { useCallback, useMemo, useEffect, useState, useRef } from 'react';
import { useSession } from '@/context/session';
import { useCollection } from '@/context/collection';
import { CustomImages } from '@/api/user/customImages';
import TextArea from '@/components/forms/textarea';
import Select from '@/components/forms/select';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';

export default function ArtworkQuestions() {
  const session = useSession();
  const { getCustomImageUrl } = CustomImages(session);
  const {
    aiItems, currentItemIndex, currentItem,
    currentItemQuestions, itemAnswers, setItemAnswers,
    ensureCollection, saveAnswers,
    setStep, doGeneratePreview, STEPS, onClose,
    collectionArtwork, collectionId, api, setArtworkPreview,
    currentArtwork,
    imageModels, selectedImageModel, setSelectedImageModel, loadImageModels,
  } = useCollection();

  const [previews, setPreviews] = useState([]);
  const [referencePreviews, setReferencePreviews] = useState([]);
  const [customImageRefs, setCustomImageRefs] = useState([]);
  const [calculatedTokens, setCalculatedTokens] = useState(null);
  const [estimatingTokens, setEstimatingTokens] = useState(false);
  const estimateTimerRef = useRef(null);

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
                thumb: api.getCollectionArtworkThumbUrl(cId, r.artworkId, a.id),
                full: api.getCollectionArtworkImageUrl(cId, r.artworkId, a.id, !!a.fullSize),
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

    return () => { cancelled = true; };
  }, [currentItem]);

  const handleItemAnswerChange = useCallback((questionId, value) => {
    setItemAnswers((prev) => ({ ...prev, [questionId]: value }));
  }, [setItemAnswers]);

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
        thumbUrl: api.getCollectionArtworkThumbUrl(collectionId, currentItem.id, a.id),
        fullUrl: api.getCollectionArtworkImageUrl(collectionId, currentItem.id, a.id, !!a.fullSize),
      }));
  }, [currentItem, collectionArtwork, collectionId, api]);

  const thumbImages = useMemo(() => existingArtworks.map(a => a.thumbUrl), [existingArtworks]);
  const fullImages = useMemo(() => existingArtworks.map(a => a.fullUrl), [existingArtworks]);

  const previewThumbImages = useMemo(() => {
    if (!currentItem) return [];
    const refThumbs = referencePreviews.map(r => r.thumb);
    const customThumbs = customImageRefs.map(r => r.thumb);
    const ownThumbs = previews.map(p => api.getItemPreviewUrl(currentItem.id, p.id, true));
    return [...refThumbs, ...customThumbs, ...ownThumbs];
  }, [currentItem, previews, referencePreviews, customImageRefs, api]);

  useEffect(() => {
    loadImageModels();
  }, [loadImageModels]);

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
        const res = await api.estimateItemTokens(currentItem.id, 2048, 2048, selectedImageModel.id);
        if (res.data.success) {
          setCalculatedTokens(res.data.data);
        } else {
          setCalculatedTokens(null);
        }
      } catch {
        setCalculatedTokens(null);
      } finally {
        setEstimatingTokens(false);
      }
    }, 500);
    return () => { if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current); };
  }, [currentItem, selectedImageModel, api]);

  const modelOptions = useMemo(() =>
    (imageModels || []).map((m) => ({ value: m.id, label: m.name })),
  [imageModels]);

  const handleModelChange = useCallback((e) => {
    const id = parseInt(e.target.value);
    const model = (imageModels || []).find((m) => m.id === id);
    setSelectedImageModel(model || null);
  }, [imageModels, setSelectedImageModel]);

  const previewFullImages = useMemo(() => {
    if (!currentItem) return [];
    const refFulls = referencePreviews.map(r => r.full);
    const customFulls = customImageRefs.map(r => r.full);
    const ownFulls = previews.map(p => api.getItemPreviewUrl(currentItem.id, p.id, false));
    return [...refFulls, ...customFulls, ...ownFulls];
  }, [currentItem, previews, referencePreviews, customImageRefs, api]);

  return (
    <div className="flex flex-col h-full">
      <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">
        Artwork {currentItemIndex + 1} of {aiItems.length}: {currentItem?.title || 'Untitled'}
      </h3>
      {thumbImages.length > 0 && (
        <div className="w-full max-w-[300px] mx-auto rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-4">
          <Carousel
            images={thumbImages}
            alt="Generated Artwork"
            singleImage
            infiniteScroll
            imageClassName="!max-h-none w-full h-full object-contain"
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
            imageClassName="!max-h-none w-full h-full object-contain"
            onImageClick={(_src, index) => setArtworkPreview({ images: previewFullImages, src: previewFullImages[index], alt: 'Artwork Preview' })}
            placeholder="No Previews"
          />
        </div>
      )}
      <div className="flex flex-wrap items-end gap-4 justify-between mb-4">
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
        {estimatingTokens ? (
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-2">Estimating tokens...</p>
        ) : calculatedTokens !== null ? (
          <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
            Calculated Tokens: {calculatedTokens}
          </p>
        ) : null}
      </div>
      <div className="max-h-[40vh] overflow-y-auto">
        {currentItemQuestions.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No questions for this artwork.</p>
        ) : (
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
        )}
      </div>
      <div className="buttons flex justify-end gap-2 mt-4 mt-auto">
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext}>Generate Artwork</ButtonOutline>
      </div>
    </div>
  );
}
