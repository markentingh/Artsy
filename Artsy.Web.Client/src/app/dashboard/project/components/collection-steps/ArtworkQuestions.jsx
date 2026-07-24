import React, { useCallback } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';

export default function ArtworkQuestions() {
  const {
    aiItems, currentItemIndex, currentItem,
    currentItemQuestions, itemAnswers, setItemAnswers,
    ensureCollection, saveAnswers,
    setStep, doGeneratePreview, STEPS, onClose,
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

  return (
    <div>
      <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">
        Artwork {currentItemIndex + 1} of {aiItems.length}: {currentItem?.title || 'Untitled'}
      </h3>
      <div className="max-h-[40vh] overflow-y-auto space-y-4">
        {currentItemQuestions.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No questions for this artwork.</p>
        ) : (
          currentItemQuestions.map((question) => (
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
          ))
        )}
      </div>
      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext}>Next</ButtonOutline>
      </div>
    </div>
  );
}
