import React, { useEffect, useMemo, useRef, useState, lazy, Suspense, useCallback } from 'react';
import { useSession } from '@/context/session';
import { useDashboard } from '@/context/dashboard';
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
import Slider from '@/components/ui/slider';
import ColorPicker from '@/components/ui/ColorPicker';
import QuestionsAnswersModal from './QuestionsAnswersModal';
import CustomImageSelector from './CustomImageSelector';
import ArtworkSelector from './ArtworkSelector';
const ConfirmModal = lazy(() => import('@/components/ui/confirm-modal'));
const EditQuestionModal = lazy(() => import('./EditQuestionModal'));

export default function EditArtworkModal({ show, item, onClose, onChanged }) {
  const session = useSession();
  const { refreshTokens } = useDashboard();
  const {
    updateItemTitle, updateItemSocialMedia,
    getItemArtwork, updateItemPrompt, updateItemImageModel, updateItemArtworkType,
    getQuestions, getItemQuestions, createItemQuestion, updateItemQuestion, deleteItemQuestion,
    getItemPreviews, generateItemPreview, deleteItemPreview, getItemPreviewUrl,
    getItemReferences, uploadItemReference, deleteItemReference, addArtworkReference, addCustomImageReference, getItemReferenceUrl,
    estimateItemTokens, updateItemIgnoredQuestions, updateItemOpacity
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
  const selectedImageModelId = useMemo(() => imageModels.find(m => m.modelKey === imageModel)?.id, [imageModels, imageModel]);
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

  // Opacity Mask state — single stateful object
  const [opacitySettings, setOpacitySettings] = useState({ chromakeys: [], fuziness: 60, background: null, overlay: null });
  const [loadedOpacityJson, setLoadedOpacityJson] = useState('null');
  const [showColorPicker, setShowColorPicker] = useState(false);
  const [pendingColor, setPendingColor] = useState('#00ff00');
  const pendingColorRef = useRef('#00ff00');
  const [showBgColorPicker, setShowBgColorPicker] = useState(false);
  const [pendingBgColor, setPendingBgColor] = useState('#000000');
  const pendingBgColorRef = useRef('#000000');
  const [showOverlayColorPicker, setShowOverlayColorPicker] = useState(false);
  const [pendingOverlayColor, setPendingOverlayColor] = useState('#ffffff');
  const pendingOverlayColorRef = useRef('#ffffff');
  const [showOpacityArtworkSelector, setShowOpacityArtworkSelector] = useState(false);
  const [showOpacityCustomImageSelector, setShowOpacityCustomImageSelector] = useState(false);
  const [opacityBgPreview, setOpacityBgPreview] = useState(null);
  const [processedPreviewUrl, setProcessedPreviewUrl] = useState(null);
  const [eyeDropperMode, setEyeDropperMode] = useState(false);
  const [eyeDropperColor, setEyeDropperColor] = useState(null);
  const [eyeDropperPos, setEyeDropperPos] = useState(null);
  const eyeDropperCanvasRef = useRef(null);
  const eyeDropperImageRef = useRef(null);
  const previewContainerRef = useRef(null);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState(false);
  const panStartRef = useRef({ x: 0, y: 0, panX: 0, panY: 0 });

  // Derived values from the single state object
  const chromaKeys = opacitySettings.chromakeys;
  const fuzziness = opacitySettings.fuziness;
  const opacityBackground = opacitySettings.background;
  const opacityOverlay = opacitySettings.overlay;

  // Serialize current settings and compare to loaded JSON to determine dirty state
  const currentOpacityJson = useMemo(() => {
    const hasChromaKeys = chromaKeys && chromaKeys.length > 0;
    const hasBackground = !!opacityBackground;
    const hasOverlay = !!opacityOverlay;
    if (!hasChromaKeys && !hasBackground && !hasOverlay) return 'null';
    return JSON.stringify({ chromakeys: chromaKeys, fuziness: fuzziness, background: opacityBackground || undefined, overlay: opacityOverlay || undefined });
  }, [chromaKeys, fuzziness, opacityBackground, opacityOverlay]);
  const opacityDirty = currentOpacityJson !== loadedOpacityJson;

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
    setOpacitySettings({ chromakeys: [], fuziness: 60, background: null, overlay: null });
    setLoadedOpacityJson('null');
    setShowColorPicker(false);
    setPendingColor('#00ff00');
    setShowBgColorPicker(false);
    setPendingBgColor('#000000');
    setShowOverlayColorPicker(false);
    setPendingOverlayColor('#ffffff');
    setShowOpacityArtworkSelector(false);
    setShowOpacityCustomImageSelector(false);
    setOpacityBgPreview(null);
    setProcessedPreviewUrl(null);
    setEyeDropperMode(false);
    setEyeDropperColor(null);
    setEyeDropperPos(null);
    setZoom(1);
    setPan({ x: 0, y: 0 });
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
          try {
            const opacityJson = response.data.data?.opacityJson;
            if (opacityJson) {
              const parsed = JSON.parse(opacityJson);
              const loadedKeys = parsed.chromakeys || [];
              const loadedFuzz = parsed.fuziness ?? 60;
              const loadedBg = parsed.background || null;
              const loadedOverlay = parsed.overlay || null;
              const settings = { chromakeys: loadedKeys, fuziness: loadedFuzz, background: loadedBg, overlay: loadedOverlay };
              setOpacitySettings(settings);
              // Store the serialized form exactly as we would produce it, for dirty comparison
              setLoadedOpacityJson(loadedKeys.length > 0
                ? JSON.stringify({ chromakeys: loadedKeys, fuziness: loadedFuzz, background: loadedBg || undefined, overlay: loadedOverlay || undefined })
                : 'null');
              if (loadedOverlay && loadedOverlay.color) {
                setPendingOverlayColor(loadedOverlay.color);
                pendingOverlayColorRef.current = loadedOverlay.color;
              }
              if (parsed.background && parsed.background.type === 'color' && parsed.background.color) {
                setOpacityBgPreview(null);
                setPendingBgColor(parsed.background.color);
                pendingBgColorRef.current = parsed.background.color;
              } else if (parsed.background && parsed.background.id) {
                if (parsed.background.type === 'custom') {
                  setOpacityBgPreview(getCustomImageUrl(parsed.background.id, true));
                } else if (parsed.background.type === 'artwork') {
                  // Load artwork preview for background
                  try {
                    const previewResp = await getItemPreviews(parsed.background.id);
                    if (previewResp.data.success) {
                      const bgPreviews = previewResp.data.data || [];
                      if (bgPreviews.length > 0) {
                        setOpacityBgPreview(getItemPreviewUrl(parsed.background.id, bgPreviews[0].id, true));
                      }
                    }
                  } catch { /* ignore */ }
                }
              }
            }
          } catch {
            setOpacitySettings({ chromakeys: [], fuziness: 60, background: null, overlay: null });
            setLoadedOpacityJson('null');
            setOpacityBgPreview(null);
            setPendingBgColor('#000000');
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
    estimateItemTokens(item.id, 3840, 3840, selectedImageModelId).then(response => {
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
        modelId: selectedImageModelId,
        answers: answerList,
      });
      if (response.data.success) {
        const updated = await getItemPreviews(item.id);
        if (updated.data.success) {
          const list = updated.data.data || [];
          setPreviews(list);
          refreshTokens();
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
      {showQuestionModal && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <EditQuestionModal
            show={showQuestionModal}
            editingQuestionId={editingQuestionId}
            value={questionFormValue}
            onClose={handleCloseQuestionModal}
            onChange={setQuestionFormValue}
            onSave={handleSaveQuestion}
          />
        </Suspense>
      )}
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

  // Opacity Mask handlers
  const saveOpacity = useCallback(async () => {
    if (!item) return;
    const hasChromaKeys = chromaKeys && chromaKeys.length > 0;
    const hasBackground = !!opacityBackground;
    const hasOverlay = !!opacityOverlay;
    const opacityJson = (hasChromaKeys || hasBackground || hasOverlay)
      ? JSON.stringify({ chromakeys: chromaKeys, fuziness: fuzziness, background: opacityBackground || undefined, overlay: opacityOverlay || undefined })
      : null;
    try {
      const response = await updateItemOpacity({ itemId: item.id, opacityJson });
      if (response.data.success) {
        setLoadedOpacityJson(currentOpacityJson);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save opacity settings' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save opacity settings' });
    }
  }, [item, updateItemOpacity, chromaKeys, fuzziness, opacityBackground, opacityOverlay, currentOpacityJson, setMessage]);

  const handleSaveAllChanges = async () => {
    if (!item) return;
    const promises = [];
    if (promptDirty) promises.push(handleSavePrompt());
    if (imageModelDirty) promises.push(handleSaveImageModel());
    if (opacityDirty) promises.push(saveOpacity());
    await Promise.all(promises);
  };

  const handleColorPickerChange = (colorObj) => {
    const hex = typeof colorObj === 'string' ? colorObj : colorObj?.hex;
    if (hex) {
      pendingColorRef.current = hex;
    }
  };

  const handleColorPickerOk = (colorObj) => {
    const hex = typeof colorObj === 'string' ? colorObj : colorObj?.hex;
    if (!hex) return;
    pendingColorRef.current = hex;
    if (chromaKeys.length >= 32 * 32 - 1) return;
    setOpacitySettings(prev => ({ ...prev, chromakeys: [...prev.chromakeys, hex] }));
  };

  const handleColorPickerClose = () => {
    setShowColorPicker(false);
  };

  const handleAddChromaKey = (colorObj) => {
    handleColorPickerOk(colorObj);
  };

  const handleDeleteChromaKey = (index) => {
    setOpacitySettings(prev => ({ ...prev, chromakeys: prev.chromakeys.filter((_, i) => i !== index) }));
  };

  const handleFuzzinessChange = (value) => {
    setOpacitySettings(prev => ({ ...prev, fuziness: value }));
  };


  const handleSelectOpacityArtworkBackground = async (artworkItem) => {
    setShowOpacityArtworkSelector(false);
    const bg = { type: 'artwork', id: artworkItem.id };
    setOpacitySettings(prev => ({ ...prev, background: bg }));
    try {
      const previewResp = await getItemPreviews(artworkItem.id);
      if (previewResp.data.success) {
        const bgPreviews = previewResp.data.data || [];
        if (bgPreviews.length > 0) {
          setOpacityBgPreview(getItemPreviewUrl(artworkItem.id, bgPreviews[0].id, true));
        }
      }
    } catch { /* ignore */ }
  };

  const handleSelectOpacityCustomBackground = (img) => {
    setShowOpacityCustomImageSelector(false);
    const bg = { type: 'custom', id: img.id };
    setOpacitySettings(prev => ({ ...prev, background: bg }));
    setOpacityBgPreview(getCustomImageUrl(img.id, true));
  };

  const handleRemoveOpacityBackground = () => {
    setOpacitySettings(prev => ({ ...prev, background: null }));
    setOpacityBgPreview(null);
  };

  const handleBgColorPickerChange = (colorObj) => {
    const hex = typeof colorObj === 'string' ? colorObj : colorObj?.hex;
    if (hex) pendingBgColorRef.current = hex;
  };

  const handleBgColorPickerOk = (colorObj) => {
    const hex = typeof colorObj === 'string' ? colorObj : colorObj?.hex;
    if (!hex) return;
    pendingBgColorRef.current = hex;
    setPendingBgColor(hex);
    setOpacitySettings(prev => ({ ...prev, background: { type: 'color', color: hex } }));
    setOpacityBgPreview(null);
  };

  const handleBgColorPickerClose = () => {
    setShowBgColorPicker(false);
  };

  const handleOverlayColorPickerChange = (colorObj) => {
    const hex = typeof colorObj === 'string' ? colorObj : colorObj?.hex;
    if (hex) pendingOverlayColorRef.current = hex;
  };

  const handleOverlayColorPickerOk = (colorObj) => {
    const hex = typeof colorObj === 'string' ? colorObj : colorObj?.hex;
    if (!hex) return;
    pendingOverlayColorRef.current = hex;
    setPendingOverlayColor(hex);
    setOpacitySettings(prev => ({ ...prev, overlay: { color: hex } }));
  };

  const handleOverlayColorPickerClose = () => {
    setShowOverlayColorPicker(false);
  };

  const handleRemoveOverlayColor = () => {
    setOpacitySettings(prev => ({ ...prev, overlay: null }));
  };

  const latestPreviewThumbUrl = useMemo(() => {
    if (!item || previews.length === 0) return null;
    return getItemPreviewUrl(item.id, previews[0].id, true);
  }, [item, previews]);

  // Eye dropper: load image into a canvas for sampling
  const handleToggleEyeDropper = useCallback(() => {
    if (eyeDropperMode) {
      setEyeDropperMode(false);
      setEyeDropperColor(null);
      setEyeDropperPos(null);
      return;
    }
    if (!latestPreviewThumbUrl) return;
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = img.naturalWidth;
      canvas.height = img.naturalHeight;
      const ctx = canvas.getContext('2d');
      ctx.drawImage(img, 0, 0);
      eyeDropperCanvasRef.current = canvas;
      eyeDropperImageRef.current = img;
      setEyeDropperMode(true);
    };
    img.src = latestPreviewThumbUrl;
  }, [eyeDropperMode, latestPreviewThumbUrl]);

  const handlePreviewMouseMove = useCallback((e) => {
    if (!eyeDropperMode || !eyeDropperCanvasRef.current || !eyeDropperImageRef.current || !previewContainerRef.current) return;
    const containerRect = previewContainerRef.current.getBoundingClientRect();
    const mouseX = e.clientX - containerRect.left;
    const mouseY = e.clientY - containerRect.top;
    const img = eyeDropperImageRef.current;
    const containerW = containerRect.width;
    const containerH = containerRect.height;
    const baseScale = Math.min(containerW / img.naturalWidth, containerH / img.naturalHeight);
    const displayedW = img.naturalWidth * baseScale;
    const displayedH = img.naturalHeight * baseScale;
    const offsetX = (containerW - displayedW) / 2;
    const offsetY = (containerH - displayedH) / 2;
    // Inverse of: transformOrigin=center, scale(zoom), translate(pan.x/zoom, pan.y/zoom)
    // screen → element-relative: (mouse - center - pan) / zoom + center
    // element-relative → image pixel: (element - offset) / baseScale
    const elX = (mouseX - containerW / 2 - pan.x) / zoom + containerW / 2;
    const elY = (mouseY - containerH / 2 - pan.y) / zoom + containerH / 2;
    const imgX = Math.floor((elX - offsetX) / baseScale);
    const imgY = Math.floor((elY - offsetY) / baseScale);
    if (imgX < 0 || imgY < 0 || imgX >= img.naturalWidth || imgY >= img.naturalHeight) {
      setEyeDropperColor(null);
      setEyeDropperPos(null);
      return;
    }
    const ctx = eyeDropperCanvasRef.current.getContext('2d');
    try {
      const pixel = ctx.getImageData(imgX, imgY, 1, 1).data;
      const hex = '#' + [pixel[0], pixel[1], pixel[2]].map(v => v.toString(16).padStart(2, '0')).join('').toUpperCase();
      setEyeDropperColor(hex);
      setEyeDropperPos({ x: mouseX, y: mouseY });
    } catch { /* CORS */ }
  }, [eyeDropperMode, zoom, pan]);

  const handlePreviewMouseLeave = useCallback(() => {
    setEyeDropperColor(null);
    setEyeDropperPos(null);
  }, []);

  const handlePreviewClick = useCallback(() => {
    if (!eyeDropperMode || !eyeDropperColor) return;
    if (chromaKeys.length >= 32 * 32 - 1) return;
    setOpacitySettings(prev => ({ ...prev, chromakeys: [...prev.chromakeys, eyeDropperColor] }));
    setEyeDropperMode(false);
    setEyeDropperColor(null);
    setEyeDropperPos(null);
  }, [eyeDropperMode, eyeDropperColor, chromaKeys]);

  // Zoom & pan handlers
  const handlePreviewWheel = useCallback((e) => {
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.1 : 0.1;
    setZoom(prevZoom => {
      const newZoom = Math.max(1, Math.min(10, Math.round((prevZoom + delta) * 100) / 100));
      if (newZoom === prevZoom) return prevZoom;
      // Zoom relative to the center of the preview container:
      // The element point at the container center stays fixed.
      // pan_new = pan_old * newZoom / prevZoom
      setPan(prevPan => (newZoom === 1 ? { x: 0, y: 0 } : {
        x: prevPan.x * newZoom / prevZoom,
        y: prevPan.y * newZoom / prevZoom,
      }));
      return newZoom;
    });
  }, []);

  // Attach wheel listener non-passively (React onWheel is passive by default)
  useEffect(() => {
    const el = previewContainerRef.current;
    if (!el) return;
    el.addEventListener('wheel', handlePreviewWheel, { passive: false });
    return () => el.removeEventListener('wheel', handlePreviewWheel);
  }, [handlePreviewWheel]);

  const handleZoomSliderChange = useCallback((value) => {
    setZoom(value);
    if (value === 1) setPan({ x: 0, y: 0 });
  }, []);

  const handlePreviewMouseDown = useCallback((e) => {
    if (eyeDropperMode || zoom === 1) return;
    setIsPanning(true);
    panStartRef.current = { x: e.clientX, y: e.clientY, panX: pan.x, panY: pan.y };
  }, [eyeDropperMode, zoom, pan]);

  const handlePreviewPanMove = useCallback((e) => {
    if (!isPanning) return;
    const dx = e.clientX - panStartRef.current.x;
    const dy = e.clientY - panStartRef.current.y;
    setPan({ x: panStartRef.current.panX + dx, y: panStartRef.current.panY + dy });
  }, [isPanning]);

  const handlePreviewPanEnd = useCallback(() => {
    setIsPanning(false);
  }, []);

  // Client-side chroma key processing: apply chroma keys + fuzziness to the preview image
  useEffect(() => {
    if (!latestPreviewThumbUrl || chromaKeys.length === 0) {
      setProcessedPreviewUrl(latestPreviewThumbUrl);
      return;
    }

    let cancelled = false;
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      if (cancelled) return;
      const canvas = document.createElement('canvas');
      canvas.width = img.width;
      canvas.height = img.height;
      const ctx = canvas.getContext('2d');
      ctx.drawImage(img, 0, 0);
      try {
        const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const data = imageData.data;
        const maxDistance = fuzziness;
        const sourceColors = chromaKeys.map(hex => {
          const cleaned = hex.replace('#', '');
          return {
            r: parseInt(cleaned.substring(0, 2), 16),
            g: parseInt(cleaned.substring(2, 4), 16),
            b: parseInt(cleaned.substring(4, 6), 16),
          };
        });
        for (let i = 0; i < data.length; i += 4) {
          const r = data[i], g = data[i + 1], b = data[i + 2], a = data[i + 3];
          let bestMatch = 0;
          for (const sc of sourceColors) {
            const dr = r - sc.r, dg = g - sc.g, db = b - sc.b;
            const distance = Math.sqrt(dr * dr + dg * dg + db * db);
            if (distance <= maxDistance) {
              const match = 1 - (distance / maxDistance);
              if (match > bestMatch) bestMatch = match;
            }
          }
          if (bestMatch > 0) {
            // Opacity strength is 4x the color match — pixels in the inner quarter of the range go fully transparent
            const opacityStrength = Math.min(1, bestMatch * 4);
            data[i + 3] = Math.round(a * (1 - opacityStrength));
          }
        }
        // Apply overlay color to all non-transparent pixels
        if (opacityOverlay?.color) {
          const cleaned = opacityOverlay.color.replace('#', '');
          const or = parseInt(cleaned.substring(0, 2), 16);
          const og = parseInt(cleaned.substring(2, 4), 16);
          const ob = parseInt(cleaned.substring(4, 6), 16);
          for (let i = 0; i < data.length; i += 4) {
            if (data[i + 3] > 0) {
              data[i] = or;
              data[i + 1] = og;
              data[i + 2] = ob;
            }
          }
        }
        ctx.putImageData(imageData, 0, 0);
        if (!cancelled) setProcessedPreviewUrl(canvas.toDataURL('image/png'));
      } catch {
        // CORS or other error — fall back to raw image
        if (!cancelled) setProcessedPreviewUrl(latestPreviewThumbUrl);
      }
    };
    img.onerror = () => {
      if (!cancelled) setProcessedPreviewUrl(latestPreviewThumbUrl);
    };
    img.src = latestPreviewThumbUrl;
    return () => { cancelled = true; };
  }, [latestPreviewThumbUrl, chromaKeys, fuzziness, opacityOverlay]);

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
      {previewEstimatedCost != null && (
        <div className="mb-4">
          <span className="text-sm text-gray-600 dark:text-gray-400">
            Estimated Cost: {previewEstimatedCost} tokens
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
            <p className="text-sm text-gray-500 dark:text-gray-400 text-center py-12">No reference images uploaded.</p>
          )}
        </div>
      ),
    }] : []),
    ...(isAI ? [{
      id: 'opacity',
      label: 'Opacity Mask',
      content: (
        <div>
          <div className="flex gap-6 mb-4">
            <div className="flex-1">
              <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300 mb-4">Chroma Key Colors</h3>
              <div className="grid gap-1" style={{ gridTemplateColumns: 'repeat(auto-fill, 64px)' }}>
                {chromaKeys.map((color, index) => (
                  <div
                    key={index}
                    className="relative group rounded border border-gray-300 dark:border-gray-600 cursor-pointer"
                    style={{ backgroundColor: color, width: '64px', height: '64px' }}
                    onClick={() => handleDeleteChromaKey(index)}
                    title={`Remove ${color}`}
                  >
                    <div className="absolute top-0 right-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition bg-red-600 rounded-bl rounded-tr w-4 h-4">
                      <Icon name="close" className="text-white" style={{ fontSize: '12px', lineHeight: 1 }} />
                    </div>
                  </div>
                ))}
                {chromaKeys.length < 32 * 32 - 1 && (
                  <>
                    <div
                      className="rounded border-2 border-dashed border-gray-400 dark:border-gray-500 flex items-center justify-center cursor-pointer hover:border-primary-500 hover:bg-primary-500/5 transition"
                      style={{ width: '64px', height: '64px' }}
                      title="Add Chroma Key"
                      onClick={() => setShowColorPicker(true)}
                    >
                      <Icon name="add" className="text-gray-400 text-sm" />
                    </div>
                    {previews.length > 0 && (
                      <div
                        className={`rounded border-2 flex items-center justify-center cursor-pointer transition ${eyeDropperMode ? 'border-primary-500 bg-primary-500/10 text-primary-500' : 'border-gray-400 dark:border-gray-500 text-gray-400 hover:border-primary-500 hover:bg-primary-500/5'}`}
                        style={{ width: '64px', height: '64px' }}
                        title="Pick color from preview"
                        onClick={handleToggleEyeDropper}
                      >
                        <Icon name="colorize" className="text-lg" />
                      </div>
                    )}
                  </>
                )}
              </div>
            </div>
            <div className="shrink-0">
              <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300 mb-4">Overlay Color</h3>
              <div
                className={`relative group rounded border-2 flex items-center justify-center cursor-pointer transition ${
                  opacityOverlay?.color
                    ? 'border-gray-300 dark:border-gray-600'
                    : 'border-dashed border-gray-400 dark:border-gray-500 hover:border-primary-500 hover:bg-primary-500/5'
                }`}
                style={{
                  width: '48px',
                  height: '48px',
                  backgroundColor: opacityOverlay?.color || 'transparent',
                }}
                title={opacityOverlay?.color ? `Change overlay color (${opacityOverlay.color})` : 'Add overlay color'}
                onClick={() => {
                  setPendingOverlayColor(opacityOverlay?.color || '#ffffff');
                  pendingOverlayColorRef.current = opacityOverlay?.color || '#ffffff';
                  setShowOverlayColorPicker(true);
                }}
              >
                <Icon name="add" className="text-gray-400 text-sm" />
                {opacityOverlay?.color && (
                  <div
                    className="absolute top-0 right-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition bg-red-600 rounded-bl rounded-tr w-4 h-4"
                    onClick={(e) => { e.stopPropagation(); handleRemoveOverlayColor(); }}
                    title="Remove overlay color"
                  >
                    <Icon name="close" className="text-white" style={{ fontSize: '12px', lineHeight: 1 }} />
                  </div>
                )}
              </div>
            </div>
          </div>

          {showColorPicker && (
            <ColorPicker
              color={pendingColor}
              onChange={handleColorPickerChange}
              onOk={handleAddChromaKey}
              onClose={handleColorPickerClose}
            />
          )}

          {showBgColorPicker && (
            <ColorPicker
              color={pendingBgColor}
              onChange={handleBgColorPickerChange}
              onOk={handleBgColorPickerOk}
              onClose={handleBgColorPickerClose}
            />
          )}

          {showOverlayColorPicker && (
            <ColorPicker
              color={pendingOverlayColor}
              onChange={handleOverlayColorPickerChange}
              onOk={handleOverlayColorPickerOk}
              onClose={handleOverlayColorPickerClose}
            />
          )}

          {previews.length === 0 ? (
            <div className="w-[500px] py-8 flex items-center justify-center text-center mx-auto">
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Choose at least one chroma key color, then generate a preview artwork before continuing to set up your Opacity Mask.
              </p>
            </div>
          ) : (
            <>
              <div className="mb-4">
                <Slider
                  label="Fuzziness"
                  value={fuzziness}
                  onChange={handleFuzzinessChange}
                  min={1}
                  max={200}
                  step={1}
                />
              </div>

              {processedPreviewUrl && (
                <div className="mb-4 flex flex-col items-center">
                  <label className="text-sm font-medium text-gray-600 dark:text-gray-300 block mb-2">Preview</label>
                  <div className="relative">
                    <div
                      ref={previewContainerRef}
                      className={`w-[350px] h-[350px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 relative ${eyeDropperMode ? 'cursor-crosshair' : (zoom > 1 ? (isPanning ? 'cursor-grabbing' : 'cursor-grab') : '')}`}
                      style={{
                        backgroundColor: opacityBgPreview
                          ? 'transparent'
                          : opacityBackground?.type === 'color' && opacityBackground?.color
                            ? opacityBackground.color
                            : 'transparent',
                        backgroundImage: opacityBgPreview
                          ? 'none'
                          : opacityBackground?.type === 'color' && opacityBackground?.color
                            ? 'none'
                            : 'url(/checkerboard.png)',
                        backgroundSize: opacityBgPreview ? 'auto' : '20px 20px',
                        backgroundRepeat: opacityBgPreview ? 'no-repeat' : 'repeat',
                        backgroundPosition: 'center',
                      }}
                      onMouseMove={(e) => { handlePreviewMouseMove(e); handlePreviewPanMove(e); }}
                      onMouseLeave={() => { handlePreviewMouseLeave(); handlePreviewPanEnd(); }}
                      onMouseDown={handlePreviewMouseDown}
                      onMouseUp={handlePreviewPanEnd}
                      onClick={handlePreviewClick}
                    >
                      {opacityBgPreview && (
                        <img
                          src={opacityBgPreview}
                          alt="Background"
                          className="absolute inset-0 w-full h-full object-cover select-none"
                          style={{
                            transform: `scale(${zoom}) translate(${pan.x / zoom}px, ${pan.y / zoom}px)`,
                            transformOrigin: 'center',
                            transition: isPanning ? 'none' : 'transform 0.1s ease-out',
                            imageRendering: zoom > 1 ? 'pixelated' : 'auto',
                          }}
                          draggable={false}
                        />
                      )}
                      <img
                        src={processedPreviewUrl}
                        alt="Opacity preview"
                        className="w-full h-full object-contain relative z-10 select-none"
                        style={{
                          transform: `scale(${zoom}) translate(${pan.x / zoom}px, ${pan.y / zoom}px)`,
                          transformOrigin: 'center',
                          transition: isPanning ? 'none' : 'transform 0.1s ease-out',
                          imageRendering: zoom > 1 ? 'pixelated' : 'auto',
                        }}
                        draggable={false}
                      />
                      {eyeDropperMode && (
                        <div className="absolute top-2 left-2 z-20 pointer-events-none bg-black/60 text-white text-xs px-2 py-1 rounded">
                          Click to pick a color
                        </div>
                      )}
                    </div>
                    {eyeDropperMode && eyeDropperColor && eyeDropperPos && (
                      <div
                        className="absolute z-30 pointer-events-none rounded-full border-2 border-white shadow-lg"
                        style={{
                          left: `${eyeDropperPos.x + 10}px`,
                          top: `${eyeDropperPos.y + 10}px`,
                          width: '24px',
                          height: '24px',
                          backgroundColor: eyeDropperColor,
                          boxShadow: '0 0 0 1px rgba(0,0,0,0.5)',
                        }}
                      >
                        <span className="absolute -bottom-5 left-1/2 -translate-x-1/2 text-xs text-white bg-black/70 px-1 rounded whitespace-nowrap">
                          {eyeDropperColor}
                        </span>
                      </div>
                    )}
                  </div>
                  <div className="w-[350px] mt-2">
                    <Slider
                      label="Zoom"
                      value={zoom}
                      onChange={handleZoomSliderChange}
                      min={1}
                      max={10}
                      step={0.1}
                    />
                  </div>
                </div>
              )}

              <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
                <div className="flex items-center justify-between mb-3">
                  <div>
                    <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300">Background</h3>
                    <p className="text-xs text-gray-500 dark:text-gray-400">for Social Media Posts</p>
                  </div>
                  <div className="flex items-center gap-2">
                    {(!opacityBackground || opacityBackground.type === 'color') && (
                      <div
                        className={`rounded border-2 flex items-center justify-center cursor-pointer transition ${
                          opacityBackground?.type === 'color'
                            ? 'border-gray-300 dark:border-gray-600'
                            : 'border-dashed border-gray-400 dark:border-gray-500 hover:border-primary-500 hover:bg-primary-500/5'
                        }`}
                        style={{
                          width: '48px',
                          height: '48px',
                          backgroundColor: opacityBackground?.type === 'color' ? opacityBackground.color : 'transparent',
                        }}
                        title={opacityBackground?.type === 'color' ? `Change color (${opacityBackground.color})` : 'Add background color'}
                        onClick={() => {
                          setPendingBgColor(opacityBackground?.color || '#000000');
                          pendingBgColorRef.current = opacityBackground?.color || '#000000';
                          setShowBgColorPicker(true);
                        }}
                      >
                        <Icon name="add" className="text-gray-400 text-sm" />
                      </div>
                    )}
                    <ButtonOutline onClick={() => setShowOpacityArtworkSelector(true)}>
                      <span>Use Artwork</span>
                    </ButtonOutline>
                    <ButtonOutline onClick={() => setShowOpacityCustomImageSelector(true)}>
                      <span>Custom Image</span>
                    </ButtonOutline>
                    {opacityBackground && (
                      <ButtonOutline color="red" onClick={handleRemoveOpacityBackground}>
                        Remove
                      </ButtonOutline>
                    )}
                  </div>
                </div>
              </div>
            </>
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

      {(promptDirty || imageModelDirty || opacityDirty) && (
        <div className="flex justify-end pt-4 border-t border-gray-200 dark:border-gray-700 mt-4">
          <ButtonOutline onClick={handleSaveAllChanges}>Save Changes</ButtonOutline>
        </div>
      )}

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

      {showOpacityArtworkSelector && (
        <ArtworkSelector
          show={showOpacityArtworkSelector}
          projectId={item.projectId}
          currentIndex={item.index}
          onSelect={handleSelectOpacityArtworkBackground}
          onClose={() => setShowOpacityArtworkSelector(false)}
        />
      )}

      {showOpacityCustomImageSelector && (
        <CustomImageSelector
          show={showOpacityCustomImageSelector}
          onSelect={handleSelectOpacityCustomBackground}
          onClose={() => setShowOpacityCustomImageSelector(false)}
        />
      )}

      {deleteReferenceTarget && (
        <Modal
          title="Delete Reference Image"
          onClose={() => setDeleteReferenceTarget(null)}
        >
          <p className="text-sm">Do you really want to delete this reference image? This cannot be undone.</p>
          <div className="buttons mt-4 flex justify-end gap-2">
            <ButtonOutline color="gray" className="cancel" onClick={() => setDeleteReferenceTarget(null)}>
              Cancel
            </ButtonOutline>
            <ButtonOutline onClick={handleDeleteReference}>
              Delete
            </ButtonOutline>
          </div>
        </Modal>
      )}

      {deleteQuestionTargetId && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ConfirmModal
            show={!!deleteQuestionTargetId}
            title="Delete Question"
            message="Do you really want to delete this question? This cannot be undone."
            onConfirm={handleConfirmDeleteQuestion}
            onClose={() => setDeleteQuestionTargetId(null)}
          />
        </Suspense>
      )}

      {deletePreviewTarget && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ConfirmModal
            show={!!deletePreviewTarget}
            title="Delete Preview"
            message="Do you really want to delete this preview image? This cannot be undone."
            onConfirm={handleDeletePreview}
            onClose={() => setDeletePreviewTarget(null)}
          />
        </Suspense>
      )}
    </Modal>
  );
}
