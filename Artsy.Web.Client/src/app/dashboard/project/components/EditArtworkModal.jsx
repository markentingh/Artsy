import React, { useEffect, useMemo, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import { Printify } from '@/api/user/printify';
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
import ConfigureProductBlueprint from './ConfigureProductBlueprint';

export default function EditArtworkModal({ show, item, onClose, onChanged }) {
  const session = useSession();
  const {
    updateItemTitle,
    getItemArtwork, updateItemPrompt, updateItemImageModel,
    getQuestions, getItemQuestions, createItemQuestion, updateItemQuestion, deleteItemQuestion,
    getItemPreviews, generateItemPreview, getItemPreviewUrl,
    getItemBlueprints, createItemBlueprint, deleteItemBlueprint, updateItemBlueprint
  } = Projects(session);
  const { getBlueprints, getBrands, getBlueprintImageUrl } = Printify(session);

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

  const [blueprints, setBlueprints] = useState([]);
  const [blueprintSearch, setBlueprintSearch] = useState('');
  const [blueprintBrand, setBlueprintBrand] = useState('all');
  const [blueprintBrands, setBlueprintBrands] = useState([]);
  const [printifyResults, setPrintifyResults] = useState([]);
  const [searchingBlueprints, setSearchingBlueprints] = useState(false);
  const [loadingMoreBlueprints, setLoadingMoreBlueprints] = useState(false);
  const [hasMoreBlueprints, setHasMoreBlueprints] = useState(false);
  const [configBlueprint, setConfigBlueprint] = useState(null);
  const [editingBlueprint, setEditingBlueprint] = useState(null);
  const [debounceTimer, setDebounceTimer] = useState(null);
  const [blueprintSearchInitiated, setBlueprintSearchInitiated] = useState(false);

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
    setBlueprints([]);
    setBlueprintSearch('');
    setBlueprintBrand('all');
    setBlueprintBrands([]);
    setPrintifyResults([]);
    setSearchingBlueprints(false);
    setLoadingMoreBlueprints(false);
    setHasMoreBlueprints(false);
    setConfigBlueprint(null);
    setEditingBlueprint(null);
    setBlueprintSearchInitiated(false);
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

    const fetchBlueprints = async () => {
      try {
        const response = await getItemBlueprints(item.id);
        if (response.data.success) {
          setBlueprints(response.data.data || []);
        }
      } catch (error) {
        // Ignore load errors for optional blueprints
      }
    };

    fetchArtwork();
    fetchQuestions();
    fetchProjectQuestions();
    fetchPreviews();
    fetchBlueprints();
    handleSearchBlueprints('', 'all');
    getBrands()
      .then((resp) => {
        if (resp.data.success) {
          setBlueprintBrands(resp.data.data.brands || []);
        }
      })
      .catch(() => {});
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

  const handleSearchBlueprints = (keyword, brand, append = false) => {
    if (!append) {
      setSearchingBlueprints(true);
      setBlueprintSearchInitiated(true);
    } else {
      setLoadingMoreBlueprints(true);
    }
    setMessage(null);
    const start = append ? printifyResults.length : 0;
    getBlueprints(keyword, brand, start, 20)
      .then((resp) => {
        if (resp.data.success) {
          const newResults = resp.data.data.blueprints || [];
          if (append) {
            setPrintifyResults((prev) => [...prev, ...newResults]);
          } else {
            setPrintifyResults(newResults);
          }
          setHasMoreBlueprints(resp.data.data.hasMore || false);
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to search blueprints' });
        }
      })
      .catch((error) => {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to search blueprints' });
      })
      .finally(() => {
        setSearchingBlueprints(false);
        setLoadingMoreBlueprints(false);
      });
  };

  const handleBlueprintScroll = (e) => {
    const el = e.target;
    if (el.scrollTop + el.clientHeight >= el.scrollHeight - 50 && hasMoreBlueprints && !loadingMoreBlueprints) {
      handleSearchBlueprints(blueprintSearch, blueprintBrand, true);
    }
  };

  const handleBlueprintSearchInput = (e) => {
    const value = e.target.value;
    setBlueprintSearch(value);
    if (debounceTimer) clearTimeout(debounceTimer);
    const timer = setTimeout(() => {
      handleSearchBlueprints(value, blueprintBrand);
    }, 400);
    setDebounceTimer(timer);
  };

  const handleBlueprintBrandChange = (e) => {
    const value = e.target.value;
    setBlueprintBrand(value);
    handleSearchBlueprints(blueprintSearch, value);
  };

  const handleBlueprintResultClick = (bp) => {
    setConfigBlueprint(bp);
    setEditingBlueprint(null);
  };

  const handleEditBlueprint = (bp) => {
    setEditingBlueprint(bp);
    setConfigBlueprint({ id: bp.blueprintId, title: bp.name });
  };

  const handleDeleteBlueprint = async (bp) => {
    try {
      const resp = await deleteItemBlueprint({ id: bp.id });
      if (resp.data.success) {
        const updated = await getItemBlueprints(item.id);
        if (updated.data.success) {
          setBlueprints(updated.data.data || []);
        }
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to delete blueprint' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete blueprint' });
    }
  };

  const handleSaveBlueprintConfig = async (config) => {
    if (editingBlueprint) {
      try {
        const resp = await updateItemBlueprint({
          id: editingBlueprint.id,
          blueprintId: config.blueprintId,
          name: config.name,
          blueprintJson: config.blueprintJson,
        });
        if (resp.data.success) {
          const updated = await getItemBlueprints(item.id);
          if (updated.data.success) {
            setBlueprints(updated.data.data || []);
          }
          setConfigBlueprint(null);
          setEditingBlueprint(null);
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to update blueprint' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update blueprint' });
      }
    } else {
      try {
        const resp = await createItemBlueprint({
          itemId: item.id,
          blueprintId: config.blueprintId,
          name: config.name,
          blueprintJson: config.blueprintJson,
        });
        if (resp.data.success) {
          const updated = await getItemBlueprints(item.id);
          if (updated.data.success) {
            setBlueprints(updated.data.data || []);
          }
          setConfigBlueprint(null);
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to save blueprint' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save blueprint' });
      }
    }
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
    { id: 'products', label: 'Products', content: (
      <div className="space-y-4">
        {blueprints.length > 0 && (
          <div>
            <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300 mb-2">Saved Blueprints</h3>
            <div className="space-y-1">
              {blueprints.map((bp) => (
                <div
                  key={bp.id}
                  className="flex items-center justify-between px-3 py-2 rounded border border-gray-300 dark:border-gray-600 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-700"
                  onClick={() => handleEditBlueprint(bp)}
                >
                  <span className="text-sm">{bp.name}</span>
                  <ButtonIcon
                    name="delete"
                    title="Remove blueprint"
                    onClick={(e) => { e.stopPropagation(); handleDeleteBlueprint(bp); }}
                  />
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="filters tool-bar">
          <div className="flex items-center gap-2 flex-1">
            <Icon name="search" className="text-gray-400" />
            <Input
              name="blueprintSearch"
              value={blueprintSearch}
              onChange={handleBlueprintSearchInput}
              placeholder="Search Printify blueprints..."
              className="flex-1 mb-0"
            />
          </div>
          <div className="right-side">
            <Select
              name="blueprintBrand"
              placeholder="All brands"
              options={blueprintBrands.map((b) => ({ value: b, label: b }))}
              value={blueprintBrand === 'all' ? '' : blueprintBrand}
              onChange={handleBlueprintBrandChange}
              className="mb-0 min-w-[10em]"
            />
          </div>
        </div>

        <div className="max-h-[50vh] overflow-y-auto" onScroll={handleBlueprintScroll}>
          {searchingBlueprints ? (
            <div className="flex items-center justify-center py-12">
              <Spinner className="text-4xl" />
            </div>
          ) : printifyResults.length > 0 ? (
            <>
              <div className="grid grid-cols-4 gap-2">
                {printifyResults.map((bp) => (
                  <div
                    key={bp.id}
                    className="cursor-pointer rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 hover:border-primary-500"
                    onClick={() => handleBlueprintResultClick(bp)}
                  >
                    {bp.imageCount > 0 ? (
                      <img
                        src={getBlueprintImageUrl(bp.id, 0, true)}
                        alt={bp.title}
                        className="w-full aspect-square object-cover"
                      />
                    ) : (
                      <div className="w-full aspect-square flex items-center justify-center bg-gray-100 dark:bg-gray-700">
                        <span className="text-xs text-gray-400">No image</span>
                      </div>
                    )}
                    <div className="p-2">
                      <p className="text-xs font-medium truncate">{bp.title}</p>
                      <p className="text-xs text-gray-500 dark:text-gray-400 truncate">{bp.brand}</p>
                    </div>
                  </div>
                ))}
              </div>
              {loadingMoreBlueprints && (
                <div className="flex items-center justify-center py-4">
                  <Spinner className="text-2xl" />
                </div>
              )}
            </>
          ) : blueprintSearchInitiated ? (
            <p className="text-sm text-gray-500 dark:text-gray-400">No blueprints found.</p>
          ) : (
            <p className="text-sm text-gray-500 dark:text-gray-400">Search for product blueprints from Printify.</p>
          )}
        </div>

        {configBlueprint && (
          <ConfigureProductBlueprint
            show={!!configBlueprint}
            blueprint={configBlueprint}
            existingConfig={editingBlueprint}
            onSave={handleSaveBlueprintConfig}
            onClose={() => { setConfigBlueprint(null); setEditingBlueprint(null); }}
          />
        )}
      </div>
    ) },
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
