import React, { createContext, useContext, useState, useRef, useMemo, useCallback } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';

const CollectionContext = createContext(null);

export const STEPS = {
  PROJECT_QUESTIONS: 'project_questions',
  ARTWORK_QUESTIONS: 'artwork_questions',
  ARTWORK_PREVIEW: 'artwork_preview',
  READY_TO_GENERATE: 'ready_to_generate',
  PRODUCT_IMAGE_PROMPT: 'product_image_prompt',
  PRODUCT_IMAGE_PREVIEW: 'product_image_preview',
  PRODUCT_IMAGE_DONE: 'product_image_done',
  NEXT_STEP: 'next_step',
};

export const WIZARD_STEPS = [
  'Project Questions',
  'Artwork Questions',
  'Artwork Preview',
  'Ready to Upscale',
  'Product Images',
  'Next Steps',
];

export const STEP_INDEX = {
  project_questions: 0,
  artwork_questions: 1,
  artwork_preview: 2,
  ready_to_generate: 3,
  product_image_prompt: 4,
  product_image_preview: 4,
  product_image_done: 4,
  next_step: 5,
};

export function CollectionProvider({ children, projectId, project, collectionId: initialCollectionId, onClose, onSaved }) {
  const session = useSession();
  const api = useMemo(() => Projects(session), [session]);

  const [step, setStep] = useState(STEPS.PROJECT_QUESTIONS);
  const [projectQuestions, setProjectQuestions] = useState([]);
  const [items, setItems] = useState([]);
  const [aiItems, setAiItems] = useState([]);
  const [currentItemIndex, setCurrentItemIndex] = useState(0);
  const [currentItemQuestions, setCurrentItemQuestions] = useState([]);
  const [currentArtwork, setCurrentArtwork] = useState(null);
  const [blueprints, setBlueprints] = useState([]);
  const [answers, setAnswers] = useState({});
  const [itemAnswers, setItemAnswers] = useState({});
  const [previewImageData, setPreviewImageData] = useState(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [showChanges, setShowChanges] = useState(false);
  const [requestedChanges, setRequestedChanges] = useState('');
  const [collectionId, setCollectionId] = useState(null);
  const [savedAnswers, setSavedAnswers] = useState({});
  const [collectionArtwork, setCollectionArtwork] = useState([]);
  const [resumeStep, setResumeStep] = useState(null);
  const [estimate, setEstimate] = useState(null);
  const [message, setMessage] = useState(null);
  const [generatingProgress, setGeneratingProgress] = useState(0);
  const [generatedArtworks, setGeneratedArtworks] = useState([]);
  const [isGeneratingAll, setIsGeneratingAll] = useState(false);
  const [generatingMessage, setGeneratingMessage] = useState('');
  const [currentGeneratingIndex, setCurrentGeneratingIndex] = useState(-1);
  const [generationError, setGenerationError] = useState(null);
  const [artworkPreview, setArtworkPreview] = useState(null);
  const [initialLoading, setInitialLoading] = useState(false);
  const cancelRef = useRef(false);

  // Product image state
  const [productImageVariants, setProductImageVariants] = useState([]);
  const [productImagePrompt, setProductImagePrompt] = useState('');
  const [selectedProductCombos, setSelectedProductCombos] = useState([]);
  const [currentProductComboIndex, setCurrentProductComboIndex] = useState(0);
  const [allProductImages, setAllProductImages] = useState([]);

  const blueprintItemIds = useMemo(() => {
    const ids = new Set();
    for (const bp of blueprints) {
      if (!bp.placementJson) continue;
      try {
        const placements = JSON.parse(bp.placementJson);
        if (!placements) continue;
        for (const p of Object.values(placements)) {
          if (p.source === 'item' && p.itemId) ids.add(String(p.itemId));
        }
      } catch { /* skip */ }
    }
    return ids;
  }, [blueprints]);

  const currentItem = aiItems[currentItemIndex];

  const ensureCollection = useCallback(async () => {
    if (collectionId) return collectionId;
    try {
      const colRes = await api.createCollection({ projectId, title: `Collection ${new Date().toISOString().split('T')[0]}` });
      if (colRes.data.success) {
        setCollectionId(colRes.data.data.id);
        return colRes.data.data.id;
      } else {
        setMessage({ type: 'error', text: colRes.data.message || 'Failed to create collection' });
        return null;
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to create collection' });
      return null;
    }
  }, [collectionId, projectId, api]);

  const buildProjectAnswers = useCallback(() => {
    return Object.entries(answers)
      .filter(([_, value]) => value && value.trim())
      .map(([questionId, answer]) => ({ questionId, itemId: null, answer: answer.trim() }));
  }, [answers]);

  const buildAllAnswers = useCallback(() => {
    const allAnswers = [];
    for (const [questionId, answer] of Object.entries(answers)) {
      if (answer && answer.trim()) {
        allAnswers.push({ questionId, itemId: null, answer: answer.trim() });
      }
    }
    for (const [questionId, answer] of Object.entries(itemAnswers)) {
      if (answer && answer.trim()) {
        allAnswers.push({ questionId, itemId: currentItem?.id, answer: answer.trim() });
      }
    }
    return allAnswers;
  }, [answers, itemAnswers, currentItem]);

  const saveAnswers = useCallback(async (colId) => {
    try {
      await api.saveCollectionDraft({
        projectId,
        collectionId: colId,
        answers: buildAllAnswers(),
      });
    } catch (error) {
      console.error('saveAnswers error:', error?.response?.data || error);
    }
  }, [projectId, api, buildAllAnswers]);

  const fetchEstimate = useCallback(async () => {
    try {
      const res = await api.estimateCollectionTokens({ projectId });
      if (res.data.success) {
        setEstimate(res.data.data);
      }
    } catch (error) {
      // non-critical
    }
  }, [projectId, api]);

  const doGeneratePreview = useCallback(async (colId) => {
    const item = aiItems[currentItemIndex];
    if (!item) return;

    setIsGenerating(true);
    setMessage(null);
    try {
      const answerList = [
        ...buildProjectAnswers(),
        ...Object.entries(itemAnswers || {})
          .filter(([_, value]) => value && value.trim())
          .map(([questionId, answer]) => ({ questionId, answer })),
      ];

      const res = await api.generateCollectionArtwork({
        projectId,
        collectionId: colId,
        itemId: item.id,
        width: 2048,
        height: 2048,
        answers: answerList,
        requestedChanges: showChanges ? requestedChanges : null,
      });

      if (res.data.success) {
        const artwork = res.data.data;
        const url = api.getCollectionArtworkImageUrl(colId, item.id, artwork.id, false, Date.now());
        setCurrentArtwork(artwork);
        setPreviewImageData(url);
        setShowChanges(false);
        setRequestedChanges('');
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to generate preview' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to generate preview' });
    } finally {
      setIsGenerating(false);
    }
  }, [aiItems, currentItemIndex, itemAnswers, showChanges, requestedChanges, projectId, api, buildProjectAnswers]);

  const loadItemData = useCallback(async (index) => {
    const item = aiItems[index];
    if (!item) {
      setStep(STEPS.READY_TO_GENERATE);
      fetchEstimate();
      return;
    }

    try {
      const [qRes, artRes] = await Promise.all([
        api.getItemQuestions(item.id),
        api.getItemArtwork(item.id),
      ]);

      const questions = qRes.data.success ? (qRes.data.data || []) : [];
      setCurrentItemQuestions(questions);
      const art = artRes.data.success ? artRes.data.data : null;
      setCurrentArtwork(art);

      const restoredItemAnswers = {};
      if (collectionId) {
        for (const q of questions) {
          const key = `${item.id}:${q.id}`;
          if (savedAnswers[key]) {
            restoredItemAnswers[q.id] = savedAnswers[key];
          }
        }
      }
      setItemAnswers(restoredItemAnswers);
      setPreviewImageData(null);
      setShowChanges(false);
      setRequestedChanges('');

      if (questions.length > 0) {
        setStep(STEPS.ARTWORK_QUESTIONS);
      } else {
        setStep(STEPS.ARTWORK_PREVIEW);
        const colId = await ensureCollection();
        if (colId) await doGeneratePreview(colId);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load artwork data' });
    }
  }, [aiItems, collectionId, savedAnswers, api, ensureCollection, doGeneratePreview, fetchEstimate]);

  const advanceToNextItem = useCallback((fromIndex = currentItemIndex) => {
    const acceptedItemIds = new Set(
      collectionArtwork.filter(a => a.accepted).map(a => String(a.itemId))
    );
    const nextIndex = aiItems.findIndex((item, idx) =>
      idx > fromIndex &&
      blueprintItemIds.has(String(item.id)) &&
      !acceptedItemIds.has(String(item.id))
    );
    if (nextIndex === -1) {
      setStep(STEPS.READY_TO_GENERATE);
      fetchEstimate();
    } else {
      setCurrentItemIndex(nextIndex);
      loadItemData(nextIndex);
    }
  }, [currentItemIndex, collectionArtwork, aiItems, blueprintItemIds, fetchEstimate, loadItemData]);

  const handleSaveDraft = useCallback(async () => {
    if (!collectionId) {
      try {
        const colRes = await api.createCollection({ projectId, title: `Collection ${new Date().toISOString().split('T')[0]}` });
        if (colRes.data.success) {
          setCollectionId(colRes.data.data.id);
          const res = await api.saveCollectionDraft({
            projectId,
            collectionId: colRes.data.data.id,
            answers: buildAllAnswers(),
          });
          if (res.data.success) {
            if (onSaved) onSaved();
            onClose();
          } else {
            setMessage({ type: 'error', text: res.data.message || 'Failed to save draft' });
          }
        } else {
          setMessage({ type: 'error', text: colRes.data.message || 'Failed to create collection' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to create collection' });
      }
    } else {
      try {
        const res = await api.saveCollectionDraft({
          projectId,
          collectionId,
          answers: buildAllAnswers(),
        });
        if (res.data.success) {
          if (onSaved) onSaved();
          onClose();
        } else {
          setMessage({ type: 'error', text: res.data.message || 'Failed to save draft' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save draft' });
      }
    }
  }, [collectionId, projectId, api, buildAllAnswers, onSaved, onClose]);

  const loadProductImageVariants = useCallback(async () => {
    if (!collectionId) return;
    try {
      const res = await api.getProductImageVariants(projectId, collectionId);
      if (res.data.success) {
        const variants = res.data.data || [];
        setProductImageVariants(variants);

        let defaultPrompt = '';
        for (const bp of variants) {
          const pb = blueprints.find(b => b.id === bp.projectBlueprintId);
          if (pb?.prompt) {
            defaultPrompt = pb.prompt;
            break;
          }
        }
        setProductImagePrompt(defaultPrompt);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load product image variants' });
    }
  }, [collectionId, projectId, api, blueprints]);

  const doGenerateAll = useCallback(async (colId) => {
    if (!estimate || estimate.generations.length === 0) return;

    setIsGeneratingAll(true);
    setGeneratingProgress(0);
    setGeneratedArtworks([]);
    setCurrentGeneratingIndex(0);
    setGenerationError(null);
    setGeneratingMessage(`Generating artwork 1 of ${estimate.generations.length}...`);
    cancelRef.current = false;

    const results = [];
    for (let i = 0; i < estimate.generations.length; i++) {
      if (cancelRef.current) break;

      const gen = estimate.generations[i];
      const item = aiItems.find(a => a.id === gen.itemId);
      setCurrentGeneratingIndex(i);
      setGeneratingMessage(`Generating artwork ${i + 1} of ${estimate.generations.length}: ${item?.title || 'Untitled'} (${gen.width}x${gen.height})...`);

      try {
        const answerList = [...buildProjectAnswers()];
        const res = await api.upscaleArtwork({
          projectId,
          collectionId: colId,
          itemId: gen.itemId,
        });

        if (res.data.success) {
          const artwork = res.data.data;
          const url = api.getCollectionArtworkImageUrl(colId, gen.itemId, artwork.id, true, Date.now());
          results.push({ itemId: gen.itemId, artworkId: artwork.id, url, width: gen.width, height: gen.height });
          setGeneratedArtworks([...results]);
        } else {
          setGenerationError(res.data.message || 'Failed to generate artwork');
          setIsGeneratingAll(false);
          return;
        }
      } catch (error) {
        setGenerationError(error?.response?.data?.message || error?.message || 'Failed to generate artwork');
        setIsGeneratingAll(false);
        return;
      }

      setGeneratingProgress(Math.round(((i + 1) / estimate.generations.length) * 100));
    }

    setIsGeneratingAll(false);
    setCurrentGeneratingIndex(-1);
    if (!cancelRef.current) {
      await loadProductImageVariants();
      setStep(STEPS.PRODUCT_IMAGE_PROMPT);
    }
  }, [estimate, aiItems, projectId, api, buildProjectAnswers, loadProductImageVariants]);

  const reset = useCallback(() => {
    setStep(STEPS.PROJECT_QUESTIONS);
    setProjectQuestions([]);
    setItems([]);
    setAiItems([]);
    setCurrentItemIndex(0);
    setCurrentItemQuestions([]);
    setCurrentArtwork(null);
    setBlueprints([]);
    setAnswers({});
    setItemAnswers({});
    setPreviewImageData(null);
    setIsGenerating(false);
    setShowChanges(false);
    setRequestedChanges('');
    setCollectionId(initialCollectionId || null);
    setSavedAnswers({});
    setCollectionArtwork([]);
    setResumeStep(null);
    setEstimate(null);
    setMessage(null);
    setGeneratingProgress(0);
    setGeneratedArtworks([]);
    setIsGeneratingAll(false);
    setGeneratingMessage('');
    setCurrentGeneratingIndex(-1);
    setGenerationError(null);
    setArtworkPreview(null);
    setInitialLoading(true);
    cancelRef.current = false;
    setProductImageVariants([]);
    setProductImagePrompt('');
    setSelectedProductCombos([]);
    setCurrentProductComboIndex(0);
    setAllProductImages([]);
  }, [initialCollectionId]);

  const loadData = useCallback(async (existingCollectionId) => {
    try {
      const [qRes, itemsRes, bpRes] = await Promise.all([
        api.getQuestions(projectId),
        api.getItems(projectId),
        api.getBlueprints(projectId),
      ]);

      if (qRes.data.success) setProjectQuestions(qRes.data.data || []);
      if (itemsRes.data.success) {
        const allItems = itemsRes.data.data || [];
        setItems(allItems);
      }
      if (bpRes.data.success) {
        const allBps = bpRes.data.data || [];
        const completeBps = allBps.filter(bp => {
          if (!bp.placementJson) return false;
          try {
            const placements = JSON.parse(bp.placementJson);
            if (!placements || Object.keys(placements).length === 0) return false;
            return Object.values(placements).some(p => {
              if (!p.source) return false;
              if (p.source === 'item' && p.itemId) return true;
              if (p.source === 'custom' && p.customImageId) return true;
              return false;
            });
          } catch { return false; }
        });
        setBlueprints(completeBps);
      }

      if (existingCollectionId) {
        let savedAnsMap = {};
        let artworkList = [];

        const [ansRes, artRes] = await Promise.all([
          api.getCollectionAnswers(existingCollectionId),
          api.getCollectionArtwork(existingCollectionId),
        ]);

        if (ansRes.data.success) {
          savedAnsMap = {};
          for (const a of (ansRes.data.data || [])) {
            const key = a.itemId ? `${a.itemId}:${a.questionId}` : `project:${a.questionId}`;
            savedAnsMap[key] = a.answer;
            if (a.itemId) {
              setItemAnswers(prev => ({ ...prev, [a.questionId]: a.answer }));
            } else {
              setAnswers(prev => ({ ...prev, [a.questionId]: a.answer }));
            }
          }
          setSavedAnswers(savedAnsMap);
        }

        if (artRes.data.success) {
          artworkList = artRes.data.data || [];
          setCollectionArtwork(artworkList);
        }

        const questions = qRes.data.success ? (qRes.data.data || []) : [];
        const allProjectQuestionsAnswered = questions.length === 0 || questions.every(q => savedAnsMap[`project:${q.id}`]);
        if (!allProjectQuestionsAnswered) {
          setResumeStep(STEPS.PROJECT_QUESTIONS);
        } else {
          setResumeStep('artwork_resume');
        }
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load data' });
      setInitialLoading(false);
    }
  }, [projectId, api]);

  const value = {
    // props
    projectId, project, onClose, onSaved, api,
    // step
    step, setStep, STEPS, WIZARD_STEPS, STEP_INDEX,
    // data
    projectQuestions, items, aiItems, setAiItems, blueprints, blueprintItemIds,
    currentItemIndex, setCurrentItemIndex, currentItem,
    currentItemQuestions, currentArtwork, setCurrentArtwork,
    collectionId, setCollectionId, collectionArtwork, setCollectionArtwork,
    savedAnswers, estimate, setEstimate,
    // form state
    answers, setAnswers, itemAnswers, setItemAnswers,
    previewImageData, setPreviewImageData,
    isGenerating, setIsGenerating,
    showChanges, setShowChanges,
    requestedChanges, setRequestedChanges,
    // generation state
    isGeneratingAll, setIsGeneratingAll,
    generatingProgress, setGeneratingProgress,
    generatedArtworks, setGeneratedArtworks,
    generatingMessage, setGeneratingMessage,
    currentGeneratingIndex, setCurrentGeneratingIndex,
    generationError, setGenerationError,
    artworkPreview, setArtworkPreview,
    initialLoading, setInitialLoading,
    message, setMessage,
    resumeStep, setResumeStep,
    cancelRef,
    // helpers
    ensureCollection, buildProjectAnswers, buildAllAnswers, saveAnswers,
    fetchEstimate, doGeneratePreview, loadItemData, advanceToNextItem,
    handleSaveDraft, doGenerateAll,
    // product image
    productImageVariants, productImagePrompt, setProductImagePrompt,
    selectedProductCombos, setSelectedProductCombos,
    currentProductComboIndex, setCurrentProductComboIndex,
    allProductImages, setAllProductImages,
    loadProductImageVariants,
    reset, loadData,
  };

  return (
    <CollectionContext.Provider value={value}>
      {children}
    </CollectionContext.Provider>
  );
}

export function useCollection() {
  const context = useContext(CollectionContext);
  if (!context) {
    throw new Error('useCollection must be used within a CollectionProvider');
  }
  return context;
}
