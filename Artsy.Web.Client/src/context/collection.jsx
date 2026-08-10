import React, { createContext, useContext, useState, useRef, useMemo, useCallback, useEffect } from 'react';
import { useSession } from '@/context/session';
import { useDashboard } from '@/context/dashboard';
import { Projects } from '@/api/user/projects';
import { Instagram } from '@/api/user/instagram';
import { PrintifyProducts } from '@/api/user/printifyProducts';
import { ImageGeneration } from '@/api/user/imageGeneration';

const CollectionContext = createContext(null);

export const STEPS = {
  PROJECT_QUESTIONS: 'project_questions',
  ARTWORK_QUESTIONS: 'artwork_questions',
  ARTWORK_PREVIEW: 'artwork_preview',
  READY_TO_GENERATE: 'ready_to_generate',
  PRODUCT_IMAGE_PROMPT: 'product_image_prompt',
  PRODUCT_IMAGE_PREVIEW: 'product_image_preview',
  CREATE_PRODUCTS: 'create_products',
  PUBLISH_PRODUCTS: 'publish_products',
  SOCIAL_MEDIA: 'social_media',
  SUMMARY: 'summary',
};

export const WIZARD_STEPS = [
  'Project Questions',
  'Artwork Questions',
  'Ready to Upscale',
  'Create Products',
  'Product Images',
  'Publish Products',
  'Social Media',
  'Summary',
];

const PLACEMENT_NAMES = [
  'Front', 'Back', 'Left Sleeve', 'Right Sleeve', 'Left', 'Right',
  'Top', 'Bottom', 'Inside', 'Outside',
];
export function getPlacementName(num) {
  return PLACEMENT_NAMES[num] || `Placement ${num + 1}`;
}

export const STEP_INDEX = {
  project_questions: 0,
  artwork_questions: 1,
  artwork_preview: 1,
  ready_to_generate: 2,
  create_products: 3,
  product_image_prompt: 4,
  product_image_preview: 4,
  publish_products: 5,
  social_media: 6,
  summary: 7,
};

export function buildWizardSteps(hasProjectQuestions) {
  const steps = [];
  if (hasProjectQuestions) steps.push('Project Questions');
  steps.push('Artwork Questions', 'Ready to Upscale', 'Create Products', 'Product Images', 'Publish Products', 'Social Media', 'Summary');
  return steps;
}

export function buildStepIndex(hasProjectQuestions) {
  const offset = hasProjectQuestions ? 0 : -1;
  return {
    project_questions: 0,
    artwork_questions: 1 + offset,
    artwork_preview: 1 + offset,
    ready_to_generate: 2 + offset,
    create_products: 3 + offset,
    product_image_prompt: 4 + offset,
    product_image_preview: 4 + offset,
    publish_products: 5 + offset,
    social_media: 6 + offset,
    summary: 7 + offset,
  };
}

export function CollectionProvider({ children, projectId, project, collectionId: initialCollectionId, collectionTitle, onClose, onSaved }) {
  const session = useSession();
  const { refreshTokens } = useDashboard();
  const api = useMemo(() => Projects(session), [session]);
  const instagramApi = useMemo(() => Instagram(session), [session]);
  const printifyProductsApi = useMemo(() => PrintifyProducts(session), [session]);
  const imageGenerationApi = useMemo(() => ImageGeneration(session), [session]);

  const [step, setStep] = useState(STEPS.PROJECT_QUESTIONS);
  const [maxStepIndex, setMaxStepIndex] = useState(0);
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
  const [currentGeneratingItemId, setCurrentGeneratingItemId] = useState(null);
  const [generationError, setGenerationError] = useState(null);
  const [artworkPreview, setArtworkPreview] = useState(null);
  const [initialLoading, setInitialLoading] = useState(false);
  const cancelRef = useRef(false);
  const advanceToNextItemRef = useRef(null);

  // Product image state
  const [productImageVariants, setProductImageVariants] = useState([]);
  const [productImagePrompt, setProductImagePrompt] = useState('');
  const [selectedProductCombos, setSelectedProductCombos] = useState([]);
  const [currentProductComboIndex, setCurrentProductComboIndex] = useState(0);
  const [allProductImages, setAllProductImages] = useState([]);
  const [imageModels, setImageModels] = useState([]);
  const [selectedImageModel, setSelectedImageModel] = useState(null);
  const [selectedProductImageModel, setSelectedProductImageModel] = useState(null);
  const [upscaleComplete, setUpscaleComplete] = useState(false);
  const [productBlueprintImages, setProductBlueprintImages] = useState([]);
  const [printifyImageIndexByColor, setPrintifyImageIndexByColor] = useState({});
  const [currentProductImageIndex, setCurrentProductImageIndex] = useState(0);
  const [printifyProducts, setPrintifyProducts] = useState([]);
  const [mockups, setMockups] = useState([]);

  const [socialMediaImageOrder, setSocialMediaImageOrder] = useState([]);
  const [socialMediaSelectedImages, setSocialMediaSelectedImages] = useState({});
  const [instagramPosted, setInstagramPosted] = useState(false);
  const [instagramPost, setInstagramPost] = useState(null);

  const blueprintItemIds = useMemo(() => {
    const ids = new Set();
    for (const bp of blueprints) {
      if (!bp.placementJson) continue;
      try {
        const placements = JSON.parse(bp.placementJson);
        if (!placements || !Array.isArray(placements)) continue;
        for (const p of placements) {
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
        modelId: selectedImageModel?.id,
      });

      if (res.data.success) {
        const artwork = res.data.data;
        const url = api.getCollectionArtworkImageUrl(colId, item.id, artwork.id, false, Date.now());
        setCurrentArtwork(artwork);
        setPreviewImageData(url);
        setShowChanges(false);
        setRequestedChanges('');
        refreshTokens();
        if (onSaved) onSaved();
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to generate preview' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to generate preview' });
    } finally {
      setIsGenerating(false);
    }
  }, [aiItems, currentItemIndex, itemAnswers, showChanges, requestedChanges, projectId, api, buildProjectAnswers, selectedImageModel, refreshTokens, onSaved]);

  const loadItemData = useCallback(async (index) => {
    setCurrentItemIndex(index);
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

      if (art && art.artworkType === 'custom') {
        const colId = await ensureCollection();
        if (colId) {
          try {
            await api.autoAcceptCustomArtwork({ projectId, collectionId: colId, itemId: item.id });
            const artRes2 = await api.getCollectionArtwork(colId);
            if (artRes2.data.success) {
              const updatedArtwork = artRes2.data.data || [];
              setCollectionArtwork(updatedArtwork);
              advanceToNextItemRef.current(index, updatedArtwork);
              return;
            }
          } catch (e) {
            console.error('autoAcceptCustomArtwork error:', e?.response?.data || e);
          }
        }
        advanceToNextItemRef.current(index);
        return;
      }

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
      setShowChanges(false);
      setRequestedChanges('');

      const existingArt = collectionArtwork.find(a => String(a.itemId) === String(item.id) && a.active);
      if (existingArt) {
        setPreviewImageData(api.getCollectionArtworkImageUrl(collectionId, item.id, existingArt.id, false, Date.now()));
        setStep(STEPS.ARTWORK_PREVIEW);
      } else if (questions.length > 0) {
        setPreviewImageData(null);
        setStep(STEPS.ARTWORK_QUESTIONS);
      } else {
        setPreviewImageData(null);
        setStep(STEPS.ARTWORK_PREVIEW);
        const colId = await ensureCollection();
        if (colId) await doGeneratePreview(colId);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load artwork data' });
    }
  }, [aiItems, collectionId, savedAnswers, api, ensureCollection, doGeneratePreview, fetchEstimate, projectId]);

  const loadMockups = useCallback(async (colId) => {
    const id = colId || collectionId;
    if (!id) return [];
    try {
      const res = await printifyProductsApi.getMockups(id);
      if (res.data.success) {
        const data = res.data.data || [];
        setMockups(data);
        return data;
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load mockups' });
    }
    return [];
  }, [collectionId, api, setMessage]);

  const loadProductImageVariants = useCallback(async (colId) => {
    const id = colId || collectionId;
    if (!id) {
      console.warn('loadProductImageVariants: no collectionId');
      return [];
    }
    try {
      const res = await api.getProductImageVariants(projectId, id);
      if (res.data.success) {
        const variants = res.data.data || [];
        setProductImageVariants(variants);

        let defaultPrompt = '';
        for (const bp of variants) {
          if (bp.prompt) {
            defaultPrompt = bp.prompt;
            break;
          }
        }
        setProductImagePrompt(defaultPrompt);
        return variants;
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load product image variants' });
    }
    return [];
  }, [collectionId, projectId, api, blueprints]);

  const loadImageModels = useCallback(async () => {
    try {
      const res = await imageGenerationApi.getActiveModels();
      if (res.data.success) {
        const models = res.data.data || [];
        setImageModels(models);
        if (models.length > 0 && !selectedImageModel) {
          setSelectedImageModel(models[0]);
        }
        if (models.length > 0 && !selectedProductImageModel) {
          setSelectedProductImageModel(models[0]);
        }
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load image models' });
    }
  }, [api, selectedImageModel, selectedProductImageModel]);

  const advanceToNextItem = useCallback((fromIndex = currentItemIndex, artworkOverride = null) => {
    const artwork = artworkOverride || collectionArtwork;
    const acceptedItemIds = new Set(
      artwork.filter(a => a.accepted).map(a => String(a.itemId))
    );
    const nextIndex = aiItems.findIndex((item, idx) =>
      idx > fromIndex &&
      (blueprintItemIds.has(String(item.id)) || item.socialMedia) &&
      !acceptedItemIds.has(String(item.id))
    );
    if (nextIndex === -1) {
      const allFullSize = artwork.length > 0 &&
        artwork.filter(a => a.accepted).every(a => a.fullSize);
      if (allFullSize) {
        (async () => {
          const colId = collectionId || await ensureCollection();
          if (colId) {
            await loadImageModels();
            try {
              const pbImgRes = await api.getAllProductBlueprintImages(projectId);
              const allPbImages = pbImgRes.data.success ? (pbImgRes.data.data || []) : [];
              setProductBlueprintImages(allPbImages);

              // Load existing product images
              const imgRes = await api.getProductImages(colId);
              if (imgRes.data.success) {
                const allImages = (imgRes.data.data || []).filter(img => img.active);
                setAllProductImages(allImages);

                // Filter out product blueprint images that already have accepted product images
                const acceptedProductImageIds = new Set(
                  allImages.filter(img => img.accepted).map(img => img.productImageId)
                );

                const missing = allPbImages.filter(pbi => !acceptedProductImageIds.has(pbi.id));

                if (allPbImages.length === 0) {
                  setStep(STEPS.CREATE_PRODUCTS);
                  return;
                }

                if (missing.length === 0) {
                  try {
                    await printifyProductsApi.ensureProducts({ collectionId: colId });
                  } catch (e) { /* non-critical */ }
                  setStep(STEPS.CREATE_PRODUCTS);
                  return;
                }

                // Set up combos for product image prompt step
                const combos = missing.map(pbi => {
                  const bp = blueprints.find(b => b.id === pbi.projectBlueprintId);
                  const colorMap = printifyImageIndexByColor[pbi.projectBlueprintId] || {};
                  const imageIndex = colorMap[pbi.variantColor];
                  const printifyImageUrl = (bp && imageIndex !== undefined)
                    ? `/api/printify/blueprint-image?blueprintId=${bp.blueprintId}&index=${imageIndex}&thumb=true`
                    : null;
                  return {
                    productImageId: pbi.id,
                    projectBlueprintId: pbi.projectBlueprintId,
                    blueprintName: pbi.blueprintName,
                    title: pbi.title,
                    variantColor: pbi.variantColor,
                    prompt: pbi.prompt || '',
                    printifyImageUrl,
                  };
                });
                setSelectedProductCombos(combos);
                setCurrentProductComboIndex(0);
                setProductImagePrompt(combos[0]?.prompt || '');
                setStep(STEPS.PRODUCT_IMAGE_PROMPT);
                return;
              }
            } catch (e) {  }
            setStep(STEPS.PRODUCT_IMAGE_PROMPT);
          } else {
            setStep(STEPS.READY_TO_GENERATE);
            fetchEstimate();
          }
        })();
      } else {
        setStep(STEPS.READY_TO_GENERATE);
        fetchEstimate();
      }
    } else {
      setCurrentItemIndex(nextIndex);
      loadItemData(nextIndex);
    }
  }, [currentItemIndex, collectionArtwork, aiItems, blueprintItemIds, fetchEstimate, loadItemData, collectionId, ensureCollection, loadImageModels, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex, blueprints, api, printifyImageIndexByColor]);
  advanceToNextItemRef.current = advanceToNextItem;

  const doGenerateAll = useCallback(async (colId) => {
    if (!estimate || estimate.generations.length === 0) return;

    const pendingGenerations = estimate.generations.filter(gen =>
      !collectionArtwork.some(a => String(a.itemId) === String(gen.itemId) && a.fullSize)
    );

    if (pendingGenerations.length === 0) {
      setUpscaleComplete(true);
      return;
    }

    setIsGeneratingAll(true);
    setGeneratingProgress(0);
    setGeneratedArtworks([]);
    setCurrentGeneratingIndex(0);
    setCurrentGeneratingItemId(null);
    setGenerationError(null);
    setGeneratingMessage(`Generating artwork 1 of ${pendingGenerations.length}...`);
    cancelRef.current = false;

    const results = [];
    for (let i = 0; i < pendingGenerations.length; i++) {
      if (cancelRef.current) break;

      const gen = pendingGenerations[i];
      const item = aiItems.find(a => a.id === gen.itemId);
      setCurrentGeneratingIndex(i);
      setCurrentGeneratingItemId(gen.itemId);
      setGeneratingMessage(`Generating artwork ${i + 1} of ${pendingGenerations.length}: ${item?.title || 'Untitled'} (${gen.width}x${gen.height})...`);

      try {
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
          setCollectionArtwork(prev => prev.map(a =>
            String(a.itemId) === String(gen.itemId)
              ? { ...a, fullSize: true }
              : a
          ));
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

      setGeneratingProgress(Math.round(((i + 1) / pendingGenerations.length) * 100));
    }

    setIsGeneratingAll(false);
    setCurrentGeneratingIndex(-1);
    setCurrentGeneratingItemId(null);
    if (!cancelRef.current) {
      setUpscaleComplete(true);
      if (onSaved) onSaved();
    }
    refreshTokens();
  }, [estimate, aiItems, projectId, api, buildProjectAnswers, collectionArtwork, refreshTokens, onSaved]);

  const reset = useCallback(() => {
    setStep(STEPS.PROJECT_QUESTIONS);
    setMaxStepIndex(0);
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
    setCurrentGeneratingItemId(null);
    setGenerationError(null);
    setArtworkPreview(null);
    setInitialLoading(true);
    cancelRef.current = false;
    setProductImageVariants([]);
    setProductImagePrompt('');
    setSelectedProductCombos([]);
    setCurrentProductComboIndex(0);
    setAllProductImages([]);
    setImageModels([]);
    setSelectedImageModel(null);
    setUpscaleComplete(false);
    setProductBlueprintImages([]);
    setCurrentProductImageIndex(0);
    setPrintifyProducts([]);
    setMockups([]);
    setSocialMediaImageOrder([]);
    setSocialMediaSelectedImages({});
  }, [initialCollectionId]);

  const loadData = useCallback(async (existingCollectionId) => {
    try {
      const [qRes, itemsRes, bpRes] = await Promise.all([
        api.getQuestions(projectId),
        api.getItems(projectId),
        api.getBlueprints(projectId),
      ]);

      if (qRes.data.success) {
        setProjectQuestions(qRes.data.data || []);
      }
      if (itemsRes.data.success) {
        const allItems = itemsRes.data.data || [];
        setItems(allItems);
      }
      if (bpRes.data.success) {
        const allBps = bpRes.data.data || [];
        const completeBps = allBps.filter(bp => bp.configured === true);
        setBlueprints(completeBps);

        const colorMap = {};
        for (const bp of allBps) {
          const idxMap = {};
          for (const img of (bp.printifyImages || [])) {
            if (img.variantColors && img.imageIndex !== undefined) {
              for (const color of img.variantColors) {
                idxMap[color] = img.imageIndex;
              }
            }
          }
          colorMap[bp.id] = idxMap;
        }
        setPrintifyImageIndexByColor(colorMap);
      }

      if (existingCollectionId) {
        let savedAnsMap = {};
        let artworkList = [];

        const [ansRes, artRes, ppRes, mkRes, igRes, igPostRes] = await Promise.all([
          api.getCollectionAnswers(existingCollectionId),
          api.getCollectionArtwork(existingCollectionId),
          printifyProductsApi.getByCollection(existingCollectionId),
          printifyProductsApi.getMockups(existingCollectionId),
          instagramApi.checkPosted(existingCollectionId),
          instagramApi.getPost(existingCollectionId),
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

        if (ppRes.data.success) {
          setPrintifyProducts(ppRes.data.data || []);
        }

        if (mkRes.data.success) {
          setMockups(mkRes.data.data || []);
        }

        if (igRes.data.success) {
          setInstagramPosted(igRes.data.data?.posted || false);
        }

        if (igPostRes.data.success) {
          setInstagramPost(igPostRes.data.data || null);
        }

        const questions = qRes.data.success ? (qRes.data.data || []) : [];
        const allProjectQuestionsAnswered = questions.length === 0 || questions.every(q => savedAnsMap[`project:${q.id}`]);
        if (!allProjectQuestionsAnswered) {
          setResumeStep(STEPS.PROJECT_QUESTIONS);
        } else {
          setResumeStep('artwork_resume');
        }
        setInitialLoading(false);
      } else {
        setInitialLoading(false);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load data' });
      setInitialLoading(false);
    }
  }, [projectId, api]);

  const hasProjectQuestions = projectQuestions.length > 0;
  const wizardSteps = useMemo(() => buildWizardSteps(hasProjectQuestions), [hasProjectQuestions]);
  const stepIndex = useMemo(() => buildStepIndex(hasProjectQuestions), [hasProjectQuestions]);

  useEffect(() => {
    const idx = stepIndex[step] ?? 0;
    setMaxStepIndex(prev => Math.max(prev, idx));
  }, [step, stepIndex]);

  const reviewStep = useCallback((targetStep, substep) => {
    if ((targetStep === STEPS.ARTWORK_QUESTIONS || targetStep === STEPS.ARTWORK_PREVIEW) && aiItems.length > 0) {
      if (substep) {
        const itemIndex = aiItems.findIndex(a => String(a.id) === String(substep));
        if (itemIndex !== -1) {
          loadItemData(itemIndex);
          return;
        }
      }
      loadItemData(0);
      return;
    }

    if (targetStep === STEPS.PRODUCT_IMAGE_PROMPT && productBlueprintImages.length > 0) {
      let combo;
      if (substep) {
        combo = substep;
      } else {
        const pbi = productBlueprintImages[0];
        combo = {
          productImageId: pbi.id,
          projectBlueprintId: pbi.projectBlueprintId,
          blueprintName: pbi.blueprintName,
          title: pbi.title,
          variantColor: pbi.variantColor,
        };
      }
      const existingImg = allProductImages.find(img =>
        img.projectBlueprintId === combo.projectBlueprintId &&
        img.productImageId === combo.productImageId
      );
      if (existingImg?.prompt) {
        setProductImagePrompt(existingImg.prompt);
      } else {
        setProductImagePrompt(combo.prompt || '');
      }
      const comboIndex = selectedProductCombos.findIndex(c =>
        c.projectBlueprintId === combo.projectBlueprintId &&
        c.productImageId === combo.productImageId
      );
      if (comboIndex !== -1) {
        setCurrentProductComboIndex(comboIndex);
      } else {
        setSelectedProductCombos(prev => {
          const next = [...prev, combo];
          setCurrentProductComboIndex(next.length - 1);
          return next;
        });
      }
      setStep(STEPS.PRODUCT_IMAGE_PROMPT);
      return;
    }

    setStep(targetStep);
  }, [aiItems, loadItemData, productBlueprintImages, allProductImages, selectedProductCombos, setProductImagePrompt, setSelectedProductCombos, setCurrentProductComboIndex, setStep, STEPS]);

  const value = {
    // props
    projectId, project, collectionTitle, onClose, onSaved, api,
    // step
    step, setStep, STEPS, wizardSteps, stepIndex, maxStepIndex,
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
    currentGeneratingItemId, setCurrentGeneratingItemId,
    generationError, setGenerationError,
    artworkPreview, setArtworkPreview,
    initialLoading, setInitialLoading,
    message, setMessage,
    resumeStep, setResumeStep,
    cancelRef,
    // helpers
    ensureCollection, buildProjectAnswers, buildAllAnswers, saveAnswers,
    fetchEstimate, doGeneratePreview, loadItemData, advanceToNextItem,
    doGenerateAll, reviewStep,
    // product image
    productImageVariants, productImagePrompt, setProductImagePrompt,
    selectedProductCombos, setSelectedProductCombos,
    currentProductComboIndex, setCurrentProductComboIndex,
    allProductImages, setAllProductImages,
    loadProductImageVariants, loadImageModels,
    imageModels, selectedImageModel, setSelectedImageModel,
    selectedProductImageModel, setSelectedProductImageModel,
    upscaleComplete, setUpscaleComplete,
    productBlueprintImages, setProductBlueprintImages,
    printifyImageIndexByColor,
    currentProductImageIndex, setCurrentProductImageIndex,
    printifyProducts, setPrintifyProducts,
    mockups, setMockups, loadMockups,
    socialMediaImageOrder, setSocialMediaImageOrder,
    socialMediaSelectedImages, setSocialMediaSelectedImages,
    instagramPosted, setInstagramPosted,
    instagramPost, setInstagramPost,
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
