import React, { useCallback } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';

export default function ProjectQuestions() {
  const {
    projectQuestions, answers, setAnswers,
    ensureCollection, saveAnswers,
    collectionArtwork, aiItems, blueprintItemIds,
    setStep, setCurrentItemIndex, loadItemData,
    fetchEstimate, STEPS, onClose,
  } = useCollection();

  const handleAnswerChange = useCallback((questionId, value) => {
    setAnswers((prev) => ({ ...prev, [questionId]: value }));
  }, [setAnswers]);

  const handleNext = useCallback(async () => {
    const colId = await ensureCollection();
    if (!colId) return;
    await saveAnswers(colId);
    const acceptedItemIds = new Set(
      collectionArtwork.filter(a => a.accepted).map(a => String(a.itemId))
    );
    const firstBlueprintItemIndex = aiItems.findIndex(item =>
      blueprintItemIds.has(String(item.id)) &&
      !acceptedItemIds.has(String(item.id))
    );
    if (firstBlueprintItemIndex === -1) {
      setStep(STEPS.READY_TO_GENERATE);
      fetchEstimate();
    } else {
      setCurrentItemIndex(firstBlueprintItemIndex);
      loadItemData(firstBlueprintItemIndex);
    }
  }, [ensureCollection, saveAnswers, collectionArtwork, aiItems, blueprintItemIds, fetchEstimate, loadItemData, setStep, setCurrentItemIndex, STEPS]);

  return (
    <div>
      <div className="max-h-[50vh] overflow-y-auto space-y-4">
        {projectQuestions.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No project questions.</p>
        ) : (
          projectQuestions.map((question) => (
            <TextArea
              key={question.id}
              name={`answer-${question.id}`}
              label={question.question}
              value={answers[question.id] || ''}
              onChange={(e) => handleAnswerChange(question.id, e.target.value)}
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
