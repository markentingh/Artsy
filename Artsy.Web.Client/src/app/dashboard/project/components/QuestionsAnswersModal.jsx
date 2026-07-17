import React, { useEffect, useState } from 'react';
import Modal from '@/components/ui/modal';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';

export default function QuestionsAnswersModal({
  show,
  projectId,
  itemId,
  questions = [],
  isGenerating = false,
  onSubmit,
  onClose,
}) {
  const [answers, setAnswers] = useState({});

  const storageKey = `answers:${projectId}:${itemId}`;

  useEffect(() => {
    if (!show || !projectId || !itemId) return;

    const saved = localStorage.getItem(storageKey);
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        if (parsed && typeof parsed === 'object') {
          setAnswers(parsed);
          return;
        }
      } catch {
        // ignore invalid stored data
      }
    }

    setAnswers({});
  }, [show, projectId, itemId, storageKey]);

  useEffect(() => {
    if (!show || !projectId || !itemId) return;
    localStorage.setItem(storageKey, JSON.stringify(answers));
  }, [answers, show, projectId, itemId, storageKey]);

  const handleAnswerChange = (questionId, value) => {
    setAnswers((prev) => ({ ...prev, [questionId]: value }));
  };

  const handleSubmit = () => {
    if (onSubmit) onSubmit(answers);
  };

  if (!show) return null;

  return (
    <Modal
      title="Image Generation Questions"
      onClose={onClose}
      className="min-w-[30em] max-w-full"
    >
      <div className="max-h-[60vh] overflow-y-auto space-y-4">
        {questions.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No questions available.</p>
        ) : (
          questions.map((question) => (
            <TextArea
              key={question.id}
              name={`answer-${question.id}`}
              label={question.source ? `${question.source}: ${question.question}` : question.question}
              value={answers[question.id] || ''}
              onChange={(e) => handleAnswerChange(question.id, e.target.value)}
              placeholder="Enter an answer"
              rows={3}
            />
          ))
        )}
      </div>
      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>
          Cancel
        </ButtonOutline>
        <ButtonOutline onClick={handleSubmit} disabled={isGenerating}>
          {isGenerating ? 'Generating...' : 'Generate Preview'}
        </ButtonOutline>
      </div>
    </Modal>
  );
}
