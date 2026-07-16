import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import EditQuestionModal from './EditQuestionModal';
import Message from '@/components/ui/message';

export default function QuestionsSection({ projectId }) {
  const session = useSession();
  const { getQuestions, createQuestion, updateQuestion, deleteQuestion } = Projects(session);
  const [questions, setQuestions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingQuestionId, setEditingQuestionId] = useState(null);
  const [questionFormValue, setQuestionFormValue] = useState('');
  const [message, setMessage] = useState(null);

  useEffect(() => {
    const fetchQuestions = async () => {
      setLoading(true);
      try {
        const response = await getQuestions(projectId);
        if (response.data.success) {
          setQuestions(response.data.data || []);
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to load questions' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load questions' });
      } finally {
        setLoading(false);
      }
    };
    fetchQuestions();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  const handleOpenNewQuestion = () => {
    setEditingQuestionId(null);
    setQuestionFormValue('');
    setShowModal(true);
  };

  const handleOpenEditQuestion = (id, currentText) => {
    setEditingQuestionId(id);
    setQuestionFormValue(currentText);
    setShowModal(true);
  };

  const handleCloseQuestionModal = () => {
    setShowModal(false);
    setEditingQuestionId(null);
    setQuestionFormValue('');
  };

  const handleSaveQuestion = async () => {
    const trimmed = questionFormValue.trim();
    if (!trimmed) {
      setMessage({ type: 'error', text: 'Question is required.' });
      return;
    }
    try {
      let response;
      if (editingQuestionId) {
        response = await updateQuestion({ id: editingQuestionId, question: trimmed });
      } else {
        response = await createQuestion({ projectId, question: trimmed, index: questions.length });
      }
      if (response.data.success) {
        if (editingQuestionId) {
          setQuestions((prev) => prev.map((q) => (q.id === editingQuestionId ? { ...q, question: response.data.data.question } : q)));
        } else {
          setQuestions((prev) => [...prev, response.data.data]);
        }
        handleCloseQuestionModal();
        setMessage(null);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save question' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save question' });
    }
  };

  const handleDeleteQuestion = async (id) => {
    if (!window.confirm('Delete this question?')) return;
    try {
      const response = await deleteQuestion({ id });
      if (response.data.success) {
        setQuestions((prev) => prev.filter((q) => q.id !== id));
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete question' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete question' });
    }
  };

  return (
    <div className="mb-8">
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold">Questions</h2>
        <ButtonOutline onClick={handleOpenNewQuestion}>
          <Icon name="add" />
          <span className="ml-2">New Question</span>
        </ButtonOutline>
      </div>
      {loading ? (
        <div className="p-8 text-center">
          <Icon name="progress_activity" spin className="w-6 h-6 mx-auto mb-2" />
          Loading questions...
        </div>
      ) : questions.length === 0 ? (
        <div className="p-12 text-center text-gray-600 dark:text-gray-400">
          No Questions exist for this project
        </div>
      ) : (
        <div className="space-y-3">
          {questions.map((question) => (
            <div
              key={question.id}
              className="relative bg-white dark:bg-gray-800 rounded-lg shadow p-4 pr-14"
            >
              <p className="pr-2">{question.question}</p>
              <div className="absolute top-2 right-2 flex flex-col gap-1">
                <ButtonIcon name="edit" onClick={() => handleOpenEditQuestion(question.id, question.question)} title="Edit question" />
                <ButtonIcon name="delete" onClick={() => handleDeleteQuestion(question.id)} title="Delete question" />
              </div>
            </div>
          ))}
        </div>
      )}

      <EditQuestionModal
        show={showModal}
        editingQuestionId={editingQuestionId}
        value={questionFormValue}
        onClose={handleCloseQuestionModal}
        onChange={setQuestionFormValue}
        onSave={handleSaveQuestion}
      />
    </div>
  );
}
