import React, { useCallback, useMemo } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';

export default function ArtworkQuestions() {
  const {
    aiItems, currentItemIndex, currentItem,
    currentItemQuestions, itemAnswers, setItemAnswers,
    ensureCollection, saveAnswers,
    setStep, doGeneratePreview, STEPS, onClose,
    collectionArtwork, collectionId, api, setArtworkPreview,
  } = useCollection();

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

  return (
    <div>
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
      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext}>Next</ButtonOutline>
      </div>
    </div>
  );
}
