import React, { useEffect, useMemo, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Modal from '@/components/ui/modal';
import Tabs from '@/components/ui/tabs';
import Input from '@/components/forms/input';
import TextArea from '@/components/forms/textarea';
import Select from '@/components/forms/select';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import EditQuestionModal from './EditQuestionModal';
import QuestionsAnswersModal from './QuestionsAnswersModal';

export default function EditArtworkModal({ show, item, onClose, onChanged }) {
  const session = useSession();
  const {
    updateItemTitle,
    getItemArtwork, updateItemPrompt, updateItemImageModel,
    getQuestions, getItemQuestions, createItemQuestion, updateItemQuestion, deleteItemQuestion,
    getItemPreviews, generateItemPreview, getItemPreviewUrl
  } = Projects(session);

  const [title, setTitle] = useState('');
  const [initialTitle, setInitialTitle] = useState('');
  const [prompt, setPrompt] = useState('');
  const [initialPrompt, setInitialPrompt] = useState('');
  const [imageModel, setImageModel] = useState('');
  const [initialImageModel, setInitialImageModel] = useState('');
  const [imageModelJson, setImageModelJson] = useState('');
  const [initialImageModelJson, setInitialImageModelJson] = useState('');

  const [questions, setQuestions] = useState([]);
  const [projectQuestions, setProjectQuestions] = useState([]);
  const [previews, setPreviews] = useState([]);
  const [isGenerating, setIsGenerating] = useState(false);
  const [enlargedPreview, setEnlargedPreview] = useState(null);
  const [showQuestionModal, setShowQuestionModal] = useState(false);
  const [showAnswersModal, setShowAnswersModal] = useState(false);
  const [editingQuestionId, setEditingQuestionId] = useState(null);
  const [questionFormValue, setQuestionFormValue] = useState('');

  const [message, setMessage] = useState(null);

  const reset = () => {
    const itemTitle = item?.title || '';
    setTitle(itemTitle);
    setInitialTitle(itemTitle);
    setPrompt('');
    setInitialPrompt('');
    setImageModel('');
    setInitialImageModel('');
    setImageModelJson('');
    setInitialImageModelJson('');
    setQuestions([]);
    setProjectQuestions([]);
    setPreviews([]);
    setIsGenerating(false);
    setEnlargedPreview(null);
    setShowQuestionModal(false);
    setShowAnswersModal(false);
    setEditingQuestionId(null);
    setQuestionFormValue('');
    setMessage(null);
  };

  useEffect(() => {
    if (!show || !item) return;
    reset();

    const fetchArtwork = async () => {
      try {
        const response = await getItemArtwork(item.id);
        if (response.data.success) {
          const artworkPrompt = response.data.data?.prompt || '';
          setPrompt(artworkPrompt);
          setInitialPrompt(artworkPrompt);
          setImageModel(response.data.data?.imageModel || '');
          setInitialImageModel(response.data.data?.imageModel || '');
          setImageModelJson(response.data.data?.imageModelJson || '');
          setInitialImageModelJson(response.data.data?.imageModelJson || '');
        }
      } catch (error) {
        // Ignore load errors for optional prompt
      }
    };

    const fetchQuestions = async () => {
      try {
        const response = await getItemQuestions(item.id);
        if (response.data.success) {
          setQuestions(response.data.data || []);
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to load questions' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load questions' });
      }
    };

    const fetchProjectQuestions = async () => {
      try {
        const response = await getQuestions(item.projectId);
        if (response.data.success) {
          setProjectQuestions(response.data.data || []);
        }
      } catch (error) {
        // Ignore load errors for optional project questions
      }
    };

    const fetchPreviews = async () => {
      try {
        const response = await getItemPreviews(item.id);
        if (response.data.success) {
          const list = response.data.data || [];
          setPreviews(list);
        }
      } catch (error) {
        // Ignore load errors for optional previews
      }
    };

    fetchArtwork();
    fetchQuestions();
    fetchProjectQuestions();
    fetchPreviews();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [show, item]);

  const handleSaveTitle = async () => {
    if (!item) return;
    try {
      const response = await updateItemTitle({ id: item.id, title: title.trim() });
      if (response.data.success) {
        setMessage(null);
        setInitialTitle(title.trim());
        if (onChanged) onChanged(item.id);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save title' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save title' });
    }
  };

  const handleImageModelChange = (value) => {
    setImageModel(value);
  };

  const handleSaveImageModel = async () => {
    if (!item) return;
    try {
      const response = await updateItemImageModel({ itemId: item.id, imageModel, imageModelJson });
      if (response.data.success) {
        setMessage(null);
        setInitialImageModel(imageModel);
        setInitialImageModelJson(imageModelJson);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save image model' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save image model' });
    }
  };

  const handleSavePrompt = async () => {
    if (!item) return;
    try {
      const response = await updateItemPrompt({ itemId: item.id, prompt });
      if (response.data.success) {
        setMessage(null);
        setInitialPrompt(prompt);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save prompt' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save prompt' });
    }
  };

  const previewQuestions = useMemo(() => [
    ...projectQuestions.map((q) => ({ ...q, source: 'Project' })),
    ...questions.map((q) => ({ ...q, source: 'Item' })),
  ], [projectQuestions, questions]);

  const runGenerate = async (answers) => {
    if (!item || isGenerating) return;

    setIsGenerating(true);
    setShowAnswersModal(false);
    setMessage(null);
    try {
      let modelRequest = {};
      try {
        modelRequest = JSON.parse(imageModelJson || '{}');
      } catch {
        modelRequest = {};
      }
      modelRequest.prompt = prompt;
      modelRequest.size = '1024x1024';
      modelRequest.quality = 'medium';

      const answerList = Object.entries(answers || {})
        .filter(([_, value]) => value && value.trim())
        .map(([questionId, answer]) => ({ questionId, answer }));

      const response = await generateItemPreview({
        projectId: item.projectId,
        itemId: item.id,
        imageModel,
        imageModelJson: JSON.stringify(modelRequest),
        answers: answerList,
      });
      if (response.data.success) {
        const updated = await getItemPreviews(item.id);
        if (updated.data.success) {
          const list = updated.data.data || [];
          setPreviews(list);
        }
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to generate preview' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to generate preview' });
    } finally {
      setIsGenerating(false);
    }
  };

  const handleGeneratePreview = () => {
    if (!item || isGenerating) return;

    if (!imageModel) {
      setMessage({ type: 'error', text: 'Image model is not configured for this artwork.' });
      return;
    }

    if (!prompt.trim()) {
      setMessage({ type: 'error', text: 'Prompt is required to generate a preview.' });
      return;
    }

    if (previewQuestions.length > 0) {
      setShowAnswersModal(true);
    } else {
      runGenerate({});
    }
  };

  const handleOpenNewQuestion = () => {
    setEditingQuestionId(null);
    setQuestionFormValue('');
    setShowQuestionModal(true);
  };

  const handleOpenEditQuestion = (question) => {
    setEditingQuestionId(question.id);
    setQuestionFormValue(question.question);
    setShowQuestionModal(true);
  };

  const handleCloseQuestionModal = () => {
    setShowQuestionModal(false);
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
        response = await updateItemQuestion({ id: editingQuestionId, question: trimmed });
      } else if (item) {
        response = await createItemQuestion({ itemId: item.id, projectId: item.projectId, question: trimmed, index: questions.length });
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
      const response = await deleteItemQuestion({ id });
      if (response.data.success) {
        setQuestions((prev) => prev.filter((q) => q.id !== id));
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete question' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete question' });
    }
  };

  if (!show || !item) return null;

  const titleDirty = title !== initialTitle;
  const imageModelDirty = imageModel !== initialImageModel;

  const infoTabContent = (
    <div>
      <div className="flex gap-4 items-start">
        <div className="w-2/3">
          <Input
            name="title"
            label="Title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Enter artwork title"
          />
          {titleDirty && (
            <ButtonOutline onClick={handleSaveTitle}>
              Save Changes
            </ButtonOutline>
          )}
        </div>
        <div className="w-1/3">
          <Select
            name="imageModel"
            label="Image Model"
            options={[{ value: 'openai', label: 'OpenAI' }]}
            value={imageModel}
            onChange={(e) => handleImageModelChange(e.target.value)}
          />
          {imageModelDirty && (
            <ButtonOutline onClick={handleSaveImageModel}>
              Save Changes
            </ButtonOutline>
          )}
        </div>
      </div>
    </div>
  );

  const questionTabContent = (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300">Artwork Questions</h3>
        <ButtonOutline onClick={handleOpenNewQuestion}>
          New Question
        </ButtonOutline>
      </div>
      {questions.length === 0 ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">No questions added for this artwork.</p>
      ) : (
        <div className="space-y-2">
          {questions.map((question) => (
            <div
              key={question.id}
              className="relative bg-gray-100 dark:bg-gray-700 rounded px-3 py-2 pr-10"
            >
              <span>{question.question}</span>
              <div className="absolute top-1 right-1 flex gap-1">
                <ButtonIcon name="edit" onClick={() => handleOpenEditQuestion(question)} title="Edit question" />
                <ButtonIcon name="delete" onClick={() => handleDeleteQuestion(question.id)} title="Delete question" />
              </div>
            </div>
          ))}
        </div>
      )}
      <EditQuestionModal
        show={showQuestionModal}
        editingQuestionId={editingQuestionId}
        value={questionFormValue}
        onClose={handleCloseQuestionModal}
        onChange={setQuestionFormValue}
        onSave={handleSaveQuestion}
      />
    </div>
  );

  const promptDirty = prompt !== initialPrompt;

  const previewTabContent = (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300">Preview</h3>
        <ButtonOutline onClick={handleGeneratePreview}>
          {isGenerating ? (
            <Icon name="progress_activity" spin className="mr-2" />
          ) : (
            <Icon name="add" className="mr-2" />
          )}
          <span>Generate Preview</span>
        </ButtonOutline>
      </div>
      {previews.length > 0 || isGenerating ? (
        <div className="grid grid-cols-[repeat(auto-fill,150px)] gap-2">
          {isGenerating && (
            <div className="flex items-center justify-center rounded-lg bg-gray-100 dark:bg-gray-700 w-[200px] h-[200px]">
              <Spinner className="text-2xl" />
            </div>
          )}
          {previews.map((preview) => (
            <img
              key={preview.id}
              src={getItemPreviewUrl(item.id, preview.id, true)}
              alt="Preview"
              className="w-[150px] h-[200px] rounded-lg object-cover cursor-pointer"
              onClick={() => setEnlargedPreview(preview)}
            />
          ))}
        </div>
      ) : (
        <p className="text-sm text-gray-500 dark:text-gray-400">No preview generated yet.</p>
      )}
    </div>
  );

  const tabs = [
    { id: 'info', label: 'Info', content: infoTabContent },
    {
      id: 'prompt',
      label: 'Prompt',
      content: (
        <div>
          <TextArea
            name="prompt"
            label="Image Prompt"
            rows={20}
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            className="w-full"
          />
          {promptDirty && (
            <ButtonOutline onClick={handleSavePrompt}>
              Save Changes
            </ButtonOutline>
          )}
        </div>
      ),
    },
    { id: 'questions', label: 'Questions', content: questionTabContent },
    { id: 'preview', label: 'Preview', content: previewTabContent },
    { id: 'products', label: 'Products', content: <div className="text-sm text-gray-500 dark:text-gray-400">Products coming soon.</div> },
  ];

  return (
    <Modal
      title={title || item.title || 'Edit Artwork'}
      onClose={onClose}
      top
      className="min-w-[40em] max-w-full"
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <Tabs tabs={tabs} defaultTab="info" />

      <QuestionsAnswersModal
        show={showAnswersModal}
        projectId={item.projectId}
        itemId={item.id}
        questions={previewQuestions}
        isGenerating={isGenerating}
        onSubmit={runGenerate}
        onClose={() => setShowAnswersModal(false)}
      />

      {enlargedPreview && (
        <Modal
          title="Preview"
          onClose={() => setEnlargedPreview(null)}
          className="min-w-[40em] max-w-full"
        >
          <div className="max-h-[80vh] overflow-y-auto">
            <img
              src={getItemPreviewUrl(item.id, enlargedPreview.id)}
              alt="Preview"
              className="w-full rounded-lg"
            />
          </div>
        </Modal>
      )}
    </Modal>
  );
}
