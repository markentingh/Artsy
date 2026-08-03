import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import { CustomImages } from '@/api/user/customImages';
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
import ArtworkSelector from './ArtworkSelector';
import ConfirmModal from '@/components/ui/confirm-modal';

export default function EditArtworkModal({ show, item, onClose, onChanged }) {
  const session = useSession();
  const {
    updateItemTitle, updateItemSocialMedia,
    getItemArtwork, updateItemPrompt, updateItemImageModel, updateItemArtworkType,
    getQuestions, getItemQuestions, createItemQuestion, updateItemQuestion, deleteItemQuestion,
    getItemPreviews, generateItemPreview, deleteItemPreview, getItemPreviewUrl,
    getItemReferences, uploadItemReference, deleteItemReference, addArtworkReference, addCustomImageReference, getItemReferenceUrl,
    estimateItemTokens, updateItemIgnoredQuestions
  } = Projects(session);
  const { getCustomImageUrl } = CustomImages(session);

  const [title, setTitle] = useState('');
  const [initialTitle, setInitialTitle] = useState('');
  const [socialMedia, setSocialMedia] = useState(false);
  const [initialSocialMedia, setInitialSocialMedia] = useState(false);
  const [prompt, setPrompt] = useState('');
  const [initialPrompt, setInitialPrompt] = useState('');
  const [imageModel, setImageModel] = useState('');
  const [initialImageModel, setInitialImageModel] = useState('');
  const [artworkType, setArtworkType] = useState('ai');
  const [customImageId, setCustomImageId] = useState(null);
  const [showCustomImageSelector, setShowCustomImageSelector] = useState(false);
  const [showReferenceCustomImageSelector, setShowReferenceCustomImageSelector] = useState(false);

  const [questions, setQuestions] = useState([]);
  const [projectQuestions, setProjectQuestions] = useState([]);
  const [ignoredQuestions, setIgnoredQuestions] = useState([]);
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
  const previewEstimateTimerRef = useRef(null);
  const [previewEstimatedCost, setPreviewEstimatedCost] = useState(null);

  const [references, setReferences] = useState([]);
  const [artworkRefPreviews, setArtworkRefPreviews] = useState({});
  const [uploadingReference, setUploadingReference] = useState(false);
  const [deleteReferenceTarget, setDeleteReferenceTarget] = useState(null);
  const [deletePreviewTarget, setDeletePreviewTarget] = useState(null);
  const [deleteQuestionTargetId, setDeleteQuestionTargetId] = useState(null);
  const [showArtworkSelector, setShowArtworkSelector] = useState(false);
  const [activeTab, setActiveTab] = useState('info');
  const fileInputRef = useRef(null);

  const reset = () => {
    const itemTitle = item?.title || '';
    setTitle(itemTitle);
    setInitialTitle(itemTitle);
    const itemSocialMedia = item?.socialMedia || false;
    setSocialMedia(itemSocialMedia);
    setInitialSocialMedia(itemSocialMedia);
    setPrompt('');
    setInitialPrompt('');
    setImageModel('');
    setInitialImageModel('');
    setArtworkType('ai');
    setCustomImageId(null);
    setShowCustomImageSelector(false);
    setQuestions([]);
    setProjectQuestions([]);
    setIgnoredQuestions([]);
    setPreviews([]);
    setIsGenerating(false);
    setEnlargedPreview(null);
    setShowQuestionModal(false);
    setShowAnswersModal(false);
    setEditingQuestionId(null);
    setQuestionFormValue('');
    setMessage(null);
    setReferences([]);
    setArtworkRefPreviews({});
    setUploadingReference(false);
    setDeleteReferenceTarget(null);
    setShowArtworkSelector(false);
    setActiveTab('info');
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
          try {
            const ignored = response.data.data?.ignoredQuestions;
            setIgnoredQuestions(ignored ? JSON.parse(ignored) : []);
          } catch {
            setIgnoredQuestions([]);
          }
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
          const refs = response.data.data || [];
          setReferences(refs);

          const artworkRefs = refs.filter((r) => r.artworkId);
          if (artworkRefs.length > 0) {
            const previewMap = {};
            await Promise.all(artworkRefs.map(async (r) => {
              try {
                const previewResp = await getItemPreviews(r.artworkId);
                if (previewResp.data.success) {
                  const previews = previewResp.data.data || [];
                  if (previews.length > 0) {
                    previewMap[r.id] = getItemPreviewUrl(r.artworkId, previews[0].id, true);
                  }
                }
              } catch {
                // ignore preview fetch errors
              }
            }));
            setArtworkRefPreviews(previewMap);
          }
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

  const handleSaveChanges = async () => {
    if (!item) return;
    try {
      if (titleDirty) {
        const response = await updateItemTitle({ id: item.id, title: title.trim() });
        if (!response.data.success) {
          setMessage({ type: 'error', text: response.data.message || 'Failed to save title' });
          return;
        }
        setInitialTitle(title.trim());
      }
      if (socialMedia !== initialSocialMedia) {
        const response = await updateItemSocialMedia({ id: item.id, socialMedia });
        if (!response.data.success) {
          setMessage({ type: 'error', text: response.data.message || 'Failed to update social media setting' });
          return;
        }
        setInitialSocialMedia(socialMedia);
      }
      setMessage(null);
      if (onChanged) onChanged(item.id);
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save changes' });
    }
  };

  const handleSocialMediaChange = (e) => {
    setSocialMedia(e.target.checked);
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
    if (!imageModel || artworkType !== 'ai' || activeTab !== 'preview') {
      setEstimatedCost(null);
      return;
    }
    if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current);
    estimateTimerRef.current = setTimeout(() => {
      estimateTokens();
    }, 2000);
    return () => { if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [imageModel, prompt, references, artworkType, activeTab]);

  useEffect(() => {
    if (!imageModel || !item || artworkType !== 'ai' || activeTab !== 'preview') {
      setPreviewEstimatedCost(null);
      return;
    }
    if (previewEstimateTimerRef.current) clearTimeout(previewEstimateTimerRef.current);
    previewEstimateTimerRef.current = setTimeout(() => {
      estimateItemTokens(item.id, 512, 512).then(response => {
        if (response.data.success) {
          setPreviewEstimatedCost(response.data.data);
        } else {
          setPreviewEstimatedCost(null);
        }
      }).catch(() => {
        setPreviewEstimatedCost(null);
      });
    }, 2000);
    return () => { if (previewEstimateTimerRef.current) clearTimeout(previewEstimateTimerRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [imageModel, prompt, references, artworkType, activeTab]);

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

  const handleSelectReferenceCustomImage = async (img) => {
    setShowReferenceCustomImageSelector(false);
    try {
      const response = await addCustomImageReference({ itemId: item.id, customImageId: img.id });
      if (response.data.success) {
        setReferences((prev) => [...prev, response.data.data]);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to add reference' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to add reference' });
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

  const handleSelectArtworkReference = async (artworkItem) => {
    setShowArtworkSelector(false);
    if (!item) return;
    try {
      const response = await addArtworkReference({ itemId: item.id, artworkId: artworkItem.id });
      if (response.data.success) {
        const newRef = response.data.data;
        setReferences((prev) => [...prev, newRef]);
        try {
          const previewResp = await getItemPreviews(newRef.artworkId);
          if (previewResp.data.success) {
            const previews = previewResp.data.data || [];
            if (previews.length > 0) {
              setArtworkRefPreviews((prev) => ({
                ...prev,
                [newRef.id]: getItemPreviewUrl(newRef.artworkId, previews[0].id, true)
              }));
            }
          }
        } catch {
          // ignore preview fetch errors
        }
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to add artwork reference' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to add artwork reference' });
    }
  };

  const previewQuestions = useMemo(() => [
    ...projectQuestions.filter((q) => !ignoredQuestions.includes(q.id)).map((q) => ({ ...q, source: 'Project' })),
    ...questions.map((q) => ({ ...q, source: 'Item' })),
  ], [projectQuestions, questions, ignoredQuestions]);

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
            src={getCustomImageUrl(customImageId, true)}
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
        </div>
      )}
      {(titleDirty || socialMedia !== initialSocialMedia) && (
        <div className="flex justify-end mt-4">
          <ButtonOutline onClick={handleSaveChanges}>
            Save Changes
          </ButtonOutline>
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
      {projectQuestions.length > 0 && (
        <div className="mt-6">
          <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300 mb-3">Ignored Project Questions</h3>
          <div className="space-y-2">
            {projectQuestions.map((question) => (
              <label
                key={question.id}
                className="flex items-center gap-2 bg-gray-100 dark:bg-gray-700 rounded px-3 py-2 cursor-pointer"
              >
                <Checkbox
                  checked={ignoredQuestions.includes(question.id)}
                  onChange={() => handleToggleIgnoredQuestion(question.id)}
                />
                <span>{question.question}</span>
              </label>
            ))}
          </div>
        </div>
      )}
    </div>
  );

  const promptDirty = prompt !== initialPrompt;

  const handleDeletePreview = async () => {
    if (!deletePreviewTarget) return;
    try {
      const response = await deleteItemPreview({ id: deletePreviewTarget.id });
      if (response.data.success) {
        setPreviews((prev) => prev.filter((p) => p.id !== deletePreviewTarget.id));
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete preview' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete preview' });
    } finally {
      setDeletePreviewTarget(null);
    }
  };

  const handleToggleIgnoredQuestion = async (questionId) => {
    const newIgnored = ignoredQuestions.includes(questionId)
      ? ignoredQuestions.filter((id) => id !== questionId)
      : [...ignoredQuestions, questionId];
    setIgnoredQuestions(newIgnored);
    try {
      await updateItemIgnoredQuestions({ itemId: item.id, ignoredQuestionIds: newIgnored });
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save ignored questions' });
    }
  };

  const previewTabContent = (
    <div>
      <div className="flex items-center gap-4 mb-4">
        <Select
          name="imageModel"
          label="Image Model"
          options={imageModels.map(m => ({ value: m.modelKey, label: m.name }))}
          value={imageModel}
          onChange={(e) => handleImageModelChange(e.target.value)}
          className="flex-1"
        />
        <ButtonOutline onClick={handleGeneratePreview}>
          {isGenerating ? (
            <Icon name="progress_activity" spin className="mr-2" />
          ) : (
            <Icon name="add" className="mr-2" />
          )}
          <span>Generate Preview</span>
        </ButtonOutline>
      </div>
      {imageModelDirty && (
        <div className="mb-4">
          <ButtonOutline onClick={handleSaveImageModel}>
            Save Changes
          </ButtonOutline>
        </div>
      )}
      {previewEstimatedCost && (
        <div className="mb-4">
          <span className="text-sm text-gray-600 dark:text-gray-400">
            Estimated Cost: {previewEstimatedCost.textInputTokens + previewEstimatedCost.imageInputTokens + previewEstimatedCost.imageOutputTokens} tokens
          </span>
        </div>
      )}
      {previews.length > 0 || isGenerating ? (
        <div className="grid grid-cols-[repeat(auto-fill,200px)] gap-2">
          {isGenerating && (
            <div className="flex items-center justify-center rounded-lg bg-gray-100 dark:bg-gray-700 w-[200px] h-[200px]">
              <Spinner className="text-2xl" />
            </div>
          )}
          {previews.map((preview) => (
            <div
              key={preview.id}
              className="relative group rounded-lg overflow-hidden"
            >
              <img
                src={getItemPreviewUrl(item.id, preview.id, true)}
                alt="Preview"
                className="w-[200px] h-[200px] rounded-lg object-cover cursor-pointer"
                onClick={() => setEnlargedPreview(preview)}
              />
              <button
                type="button"
                onClick={() => setDeletePreviewTarget(preview)}
                className="absolute bottom-1 right-1 w-7 h-7 flex items-center justify-center bg-black/60 text-white rounded hover:bg-red-600 transition"
                title="Delete preview"
              >
                <Icon name="delete" />
              </button>
            </div>
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
            <div className="flex gap-2">
              <ButtonOutline onClick={() => setShowArtworkSelector(true)}>
                <Icon name="add" className="mr-2" />
                <span>Artwork Reference</span>
              </ButtonOutline>
              <ButtonOutline onClick={() => setShowReferenceCustomImageSelector(true)}>
                <Icon name="add" className="mr-2" />
                <span>Custom Image</span>
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
                  {ref.artworkId ? (
                    artworkRefPreviews[ref.id] ? (
                      <img
                        src={artworkRefPreviews[ref.id]}
                        alt="Artwork reference"
                        className="w-[150px] h-[150px] object-cover"
                      />
                    ) : (
                      <div className="w-[150px] h-[150px] flex items-center justify-center bg-gray-100 dark:bg-gray-700">
                        <Icon name="image" className="text-4xl text-gray-400" />
                      </div>
                    )
                  ) : (
                    <img
                      src={getCustomImageUrl(ref.customImageId, true)}
                      alt=""
                      className="w-[150px] h-[150px] object-cover"
                    />
                  )}
                  {ref.artworkId && (
                    <div className="absolute bottom-0 left-0 right-0 bg-black/60 text-white text-xs px-2 py-1 truncate">
                      Artwork Reference
                    </div>
                  )}
                  <button
                    type="button"
                    onClick={() => setDeleteReferenceTarget(ref)}
                    className="absolute bottom-1 right-1 w-7 h-7 flex items-center justify-center bg-black/60 text-white rounded hover:bg-red-600 transition"
                    title="Remove reference"
                  >
                    <Icon name="delete" />
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

      <Tabs tabs={tabs} defaultTab="info" onTabChange={setActiveTab} />

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
          selectedImageId={customImageId}
          onSelect={handleSelectCustomImage}
          onClose={() => setShowCustomImageSelector(false)}
        />
      )}

      {showReferenceCustomImageSelector && (
        <CustomImageSelector
          show={showReferenceCustomImageSelector}
          onSelect={handleSelectReferenceCustomImage}
          onClose={() => setShowReferenceCustomImageSelector(false)}
        />
      )}

      {showArtworkSelector && (
        <ArtworkSelector
          show={showArtworkSelector}
          projectId={item.projectId}
          currentIndex={item.index}
          onSelect={handleSelectArtworkReference}
          onClose={() => setShowArtworkSelector(false)}
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

      <ConfirmModal
        show={!!deletePreviewTarget}
        title="Delete Preview"
        message="Do you really want to delete this preview image? This cannot be undone."
        onConfirm={handleDeletePreview}
        onClose={() => setDeletePreviewTarget(null)}
      />
    </Modal>
  );
}
