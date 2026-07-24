import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import { ImageGeneration } from '@/api/user/imageGeneration';
import Modal from '@/components/ui/modal';
import Tabs from '@/components/ui/tabs';
import Input from '@/components/forms/input';
import TextArea from '@/components/forms/textarea';
import Select from '@/components/forms/select';
import Checkbox from '@/components/forms/checkbox';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import EditQuestionModal from './EditQuestionModal';
import QuestionsAnswersModal from './QuestionsAnswersModal';
import CustomImageSelector from './CustomImageSelector';
import ConfirmModal from '@/components/ui/confirm-modal';

export default function EditArtworkModal({ show, item, onClose, onChanged }) {
  const session = useSession();
  const {
    updateItemTitle, updateItemSocialMedia,
    getItemArtwork, updateItemPrompt, updateItemImageModel, updateItemArtworkType,
    getQuestions, getItemQuestions, createItemQuestion, updateItemQuestion, deleteItemQuestion,
    getItemPreviews, generateItemPreview, getItemPreviewUrl,
    getItemReferences, uploadItemReference, deleteItemReference, getItemReferenceUrl,
    estimateItemTokens
  } = Projects(session);

  const [title, setTitle] = useState('');
  const [initialTitle, setInitialTitle] = useState('');
  const [socialMedia, setSocialMedia] = useState(false);
  const [prompt, setPrompt] = useState('');
  const [initialPrompt, setInitialPrompt] = useState('');
  const [imageModel, setImageModel] = useState('');
  const [initialImageModel, setInitialImageModel] = useState('');
  const [artworkType, setArtworkType] = useState('ai');
  const [customImageId, setCustomImageId] = useState(null);
  const [showCustomImageSelector, setShowCustomImageSelector] = useState(false);

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

  const [imageModels, setImageModels] = useState([]);
  const [estimatedCost, setEstimatedCost] = useState(null);
  const [estimating, setEstimating] = useState(false);
  const estimateTimerRef = useRef(null);
  const [previewEstimatedCost, setPreviewEstimatedCost] = useState(null);

  const [references, setReferences] = useState([]);
  const [uploadingReference, setUploadingReference] = useState(false);
  const [deleteReferenceTarget, setDeleteReferenceTarget] = useState(null);
  const [deleteQuestionTargetId, setDeleteQuestionTargetId] = useState(null);
  const fileInputRef = useRef(null);

  const reset = () => {
    const itemTitle = item?.title || '';
    setTitle(itemTitle);
    setInitialTitle(itemTitle);
    const itemSocialMedia = item?.socialMedia || false;
    setSocialMedia(itemSocialMedia);
    setPrompt('');
    setInitialPrompt('');
    setImageModel('');
    setInitialImageModel('');
    setArtworkType('ai');
    setCustomImageId(null);
    setShowCustomImageSelector(false);
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
    setReferences([]);
    setUploadingReference(false);
    setDeleteReferenceTarget(null);
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
          setArtworkType(response.data.data?.artworkType || 'ai');
          setCustomImageId(response.data.data?.customImageId || null);
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

    const fetchReferences = async () => {
      try {
        const response = await getItemReferences(item.id);
        if (response.data.success) {
          setReferences(response.data.data || []);
        }
      } catch (error) {
        // Ignore load errors for optional references
      }
    };

    const fetchImageModels = async () => {
      try {
        const { getActiveModels } = ImageGeneration(session);
        const response = await getActiveModels();
        if (response.data.success) {
          setImageModels(response.data.data || []);
        }
      } catch (error) {
        // Ignore load errors for optional image models
      }
    };

    fetchArtwork();
    fetchQuestions();
    fetchProjectQuestions();
    fetchPreviews();
    fetchReferences();
    fetchImageModels();
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

  const handleSocialMediaChange = async (e) => {
    const checked = e.target.checked;
    setSocialMedia(checked);
    if (!item) return;
    try {
      const response = await updateItemSocialMedia({ id: item.id, socialMedia: checked });
      if (response.data.success) {
        setMessage(null);
        if (onChanged) onChanged(item.id);
      } else {
        setSocialMedia(!checked);
        setMessage({ type: 'error', text: response.data.message || 'Failed to update social media setting' });
      }
    } catch (error) {
      setSocialMedia(!checked);
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update social media setting' });
    }
  };

  const handleImageModelChange = (value) => {
    setImageModel(value);
  };

  const estimateTokens = () => {
    if (!imageModel || !item || artworkType !== 'ai') {
      setEstimatedCost(null);
      return;
    }
    setEstimating(true);
    estimateItemTokens(item.id, 3840, 3840).then(response => {
      if (response.data.success) {
        setEstimatedCost(response.data.data);
      } else {
        setEstimatedCost(null);
      }
    }).catch(() => {
      setEstimatedCost(null);
    }).finally(() => {
      setEstimating(false);
    });
  };

  useEffect(() => {
    if (!imageModel || artworkType !== 'ai') {
      setEstimatedCost(null);
      return;
    }
    if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current);
    estimateTimerRef.current = setTimeout(() => {
      estimateTokens();
    }, 500);
    return () => { if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [imageModel, prompt, references, artworkType]);

  useEffect(() => {
    if (!imageModel || !item || artworkType !== 'ai') {
      setPreviewEstimatedCost(null);
      return;
    }
    estimateItemTokens(item.id, 512, 512).then(response => {
      if (response.data.success) {
        setPreviewEstimatedCost(response.data.data);
      } else {
        setPreviewEstimatedCost(null);
      }
    }).catch(() => {
      setPreviewEstimatedCost(null);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [imageModel, prompt, references, artworkType]);

  const handleSaveImageModel = async () => {
    if (!item) return;
    try {
      const response = await updateItemImageModel({ itemId: item.id, imageModel });
      if (response.data.success) {
        setMessage(null);
        setInitialImageModel(imageModel);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save image model' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save image model' });
    }
  };

  const handleArtworkTypeChange = async (value) => {
    setArtworkType(value);
    if (!item) return;
    try {
      const response = await updateItemArtworkType({ itemId: item.id, artworkType: value, customImageId: value === 'custom' ? customImageId : null });
      if (response.data.success) {
        setMessage(null);
        if (value !== 'custom') {
          setCustomImageId(null);
        }
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save artwork type' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save artwork type' });
    }
  };

  const handleSelectCustomImage = async (img) => {
    setCustomImageId(img.id);
    setShowCustomImageSelector(false);
    if (!item) return;
    try {
      const response = await updateItemArtworkType({ itemId: item.id, artworkType: 'custom', customImageId: img.id });
      if (response.data.success) {
        setMessage(null);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save custom image' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save custom image' });
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

  const handleFileSelect = async (e) => {
    const files = Array.from(e.target.files || []);
    if (files.length === 0) return;
    setUploadingReference(true);
    setMessage(null);
    try {
      for (const file of files) {
        const response = await uploadItemReference(item.id, file);
        if (response.data.success) {
          setReferences((prev) => [...prev, response.data.data]);
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to upload reference' });
        }
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to upload reference' });
    } finally {
      setUploadingReference(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const handleDeleteReference = async () => {
    if (!deleteReferenceTarget) return;
    try {
      const response = await deleteItemReference({ id: deleteReferenceTarget.id });
      if (response.data.success) {
        setReferences((prev) => prev.filter((r) => r.id !== deleteReferenceTarget.id));
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete reference' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete reference' });
    } finally {
      setDeleteReferenceTarget(null);
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
      const answerList = Object.entries(answers || {})
        .filter(([_, value]) => value && value.trim())
        .map(([questionId, answer]) => ({ questionId, answer }));

      const response = await generateItemPreview({
        itemId: item.id,
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

  const handleDeleteQuestion = (id) => {
    setDeleteQuestionTargetId(id);
  };

  const handleConfirmDeleteQuestion = async () => {
    if (!deleteQuestionTargetId) return;
    try {
      const response = await deleteItemQuestion({ id: deleteQuestionTargetId });
      if (response.data.success) {
        setQuestions((prev) => prev.filter((q) => q.id !== deleteQuestionTargetId));
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete question' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete question' });
    } finally {
      setDeleteQuestionTargetId(null);
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
            name="artworkType"
            label="Artwork Type"
            options={[
              { value: 'ai', label: 'AI Artwork' },
              { value: 'custom', label: 'Custom Image' }
            ]}
            value={artworkType}
            onChange={(e) => handleArtworkTypeChange(e.target.value)}
          />
        </div>
      </div>
      {artworkType === 'custom' && (
        <div className="mt-4 flex items-center justify-between">
          <Checkbox
            name="socialMedia"
            label="Publish to Social Media"
            checked={socialMedia}
            onChange={handleSocialMediaChange}
          />
          <ButtonOutline onClick={() => setShowCustomImageSelector(true)}>
            <Icon name="image" className="mr-2" />
            <span>Select Image</span>
          </ButtonOutline>
        </div>
      )}
      {artworkType === 'custom' && customImageId && (
        <div className="mt-4">
          <img
            src={getItemReferenceUrl(item.id, customImageId, true)}
            alt="Custom image"
            className="w-full rounded-lg object-cover border border-gray-300 dark:border-gray-600"
          />
        </div>
      )}
      {artworkType !== 'custom' && (
        <div className="mt-4 flex items-center justify-between">
          <Checkbox
            name="socialMedia"
            label="Publish to Social Media"
            checked={socialMedia}
            onChange={handleSocialMediaChange}
          />
          {estimatedCost && (
            <span className="text-sm text-gray-600 dark:text-gray-400">
              {estimating ? 'Estimating...' : `Estimated Cost: ${estimatedCost.textInputTokens + estimatedCost.imageInputTokens + estimatedCost.imageOutputTokens} tokens`}
            </span>
          )}
        </div>
      )}
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
                <ButtonIcon name="delete" color="red" onClick={() => handleDeleteQuestion(question.id)} title="Delete question" />
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
        <div className="flex items-center gap-4">
          <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300">Preview</h3>
          {previewEstimatedCost && (
            <span className="text-sm text-gray-600 dark:text-gray-400">
              Estimated Cost: {previewEstimatedCost.textInputTokens + previewEstimatedCost.imageInputTokens + previewEstimatedCost.imageOutputTokens} tokens
            </span>
          )}
        </div>
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

  const isAI = artworkType === 'ai';

  const tabs = [
    { id: 'info', label: 'Info', content: infoTabContent },
    ...(isAI ? [{
      id: 'prompt',
      label: 'Prompt',
      content: (
        <div>
          <div className="mb-4">
            <div className="flex items-end gap-4">
              <Select
                name="imageModel"
                label="Image Model"
                options={imageModels.map(m => ({ value: m.modelKey, label: m.name }))}
                value={imageModel}
                onChange={(e) => handleImageModelChange(e.target.value)}
                className="flex-1"
              />
            </div>
            {imageModelDirty && (
              <ButtonOutline onClick={handleSaveImageModel}>
                Save Changes
              </ButtonOutline>
            )}
          </div>
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
    }] : []),
    ...(isAI ? [{
      id: 'references',
      label: 'References',
      content: (
        <div>
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300">Reference Images</h3>
            <div>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/jpeg,image/png"
                multiple
                onChange={handleFileSelect}
                className="hidden"
              />
              <ButtonOutline onClick={() => fileInputRef.current?.click()}>
                {uploadingReference ? (
                  <Icon name="progress_activity" spin className="mr-2" />
                ) : (
                  <Icon name="add" className="mr-2" />
                )}
                <span>Upload Images</span>
              </ButtonOutline>
            </div>
          </div>
          {references.length > 0 ? (
            <div className="grid grid-cols-[repeat(auto-fill,150px)] gap-2">
              {references.map((ref) => (
                <div
                  key={ref.id}
                  className="relative group rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600"
                >
                  <img
                    src={getItemReferenceUrl(item.id, ref.id, true)}
                    alt={ref.fileName}
                    className="w-[150px] h-[150px] object-cover"
                  />
                  <button
                    type="button"
                    onClick={() => setDeleteReferenceTarget(ref)}
                    className="absolute top-1 right-1 w-6 h-6 flex items-center justify-center bg-black/60 text-white rounded opacity-0 group-hover:opacity-100 transition"
                    title="Remove reference"
                  >
                    <Icon name="close" />
                  </button>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-gray-500 dark:text-gray-400">No reference images uploaded.</p>
          )}
        </div>
      ),
    }] : []),
    ...(isAI ? [
      { id: 'questions', label: 'Questions', content: questionTabContent },
      { id: 'preview', label: 'Preview', content: previewTabContent },
    ] : []),
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

      {showCustomImageSelector && (
        <CustomImageSelector
          show={showCustomImageSelector}
          itemId={item.id}
          projectId={item.projectId}
          selectedImageId={customImageId}
          onSelect={handleSelectCustomImage}
          onClose={() => setShowCustomImageSelector(false)}
        />
      )}

      {deleteReferenceTarget && (
        <Modal
          title="Delete Reference Image"
          onClose={() => setDeleteReferenceTarget(null)}
        >
          <p className="text-sm">Do you really want to delete this reference image? This cannot be undone.</p>
          <div className="buttons mt-4 flex justify-end gap-2">
            <ButtonOutline className="cancel" onClick={() => setDeleteReferenceTarget(null)}>
              Cancel
            </ButtonOutline>
            <ButtonOutline onClick={handleDeleteReference}>
              Delete
            </ButtonOutline>
          </div>
        </Modal>
      )}

      <ConfirmModal
        show={!!deleteQuestionTargetId}
        title="Delete Question"
        message="Do you really want to delete this question? This cannot be undone."
        onConfirm={handleConfirmDeleteQuestion}
        onClose={() => setDeleteQuestionTargetId(null)}
      />
    </Modal>
  );
}
