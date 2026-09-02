import React, { createContext, useContext, useState, useRef, useMemo, useCallback, useEffect } from 'react';
import { useSession } from '@/context/session';
import { useDashboard } from '@/context/dashboard';
import { Projects } from '@/api/user/projects';
import { Instagram } from '@/api/user/instagram';
import { artworkImageUrl } from '@/utils/artworkUrls';
import { PrintifyProducts } from '@/api/user/printifyProducts';
import { ImageGeneration } from '@/api/user/imageGeneration';

const CollectionContext = createContext(null);

export const STEPS = {
  SELECT_PRODUCTS: 'select_products',
  PROJECT_QUESTIONS: 'project_questions',
  ARTWORK_QUESTIONS: 'artwork_questions',
  ARTWORK_PREVIEW: 'artwork_preview',
  READY_TO_GENERATE: 'ready_to_generate',
  PRODUCT_IMAGE_PROMPT: 'product_image_prompt',
  PRODUCT_IMAGE_PREVIEW: 'product_image_preview',
  GENERATE_PRODUCT_IMAGES: 'generate_product_images',
  CREATE_PRODUCTS: 'create_products',
  PUBLISH_PRODUCTS: 'publish_products',
};

export const WIZARD_STEPS = [
  'Select Products',
  'Project Questions',
  'Artwork Generation',
  'Ready to Upscale',
  'Create Products',
  'Product Images',
  'Publish Products',
];

const STEP_ORDER = [
  STEPS.SELECT_PRODUCTS,
  STEPS.PROJECT_QUESTIONS,
  STEPS.ARTWORK_QUESTIONS,
  STEPS.ARTWORK_PREVIEW,
  STEPS.READY_TO_GENERATE,
  STEPS.CREATE_PRODUCTS,
  STEPS.GENERATE_PRODUCT_IMAGES,
  STEPS.PUBLISH_PRODUCTS,
];

const PLACEMENT_NAMES = [
  'Front', 'Back', 'Left Sleeve', 'Right Sleeve', 'Left', 'Right',
  'Top', 'Bottom', 'Inside', 'Outside',
];
export function getPlacementName(num) {
  return PLACEMENT_NAMES[num] || `Placement ${num + 1}`;
}

export const STEP_INDEX = {
  select_products: 0,
  project_questions: 1,
  artwork_questions: 2,
  artwork_preview: 2,
  ready_to_generate: 3,
  create_products: 4,
  product_image_prompt: 5,
  product_image_preview: 5,
  publish_products: 6,
};

export function buildWizardSteps(hasProjectQuestions) {
  const steps = ['Select Products'];
  if (hasProjectQuestions) steps.push('Project Questions');
  steps.push('Artwork Generation', 'Ready to Upscale', 'Create Products', 'Product Images', 'Publish Products');
  return steps;
}

export function buildStepIndex(hasProjectQuestions) {
  const offset = hasProjectQuestions ? 0 : -1;
  return {
    select_products: 0,
    project_questions: 1,
    artwork_questions: 2 + offset,
    artwork_preview: 2 + offset,
    ready_to_generate: 3 + offset,
    create_products: 4 + offset,
    generate_product_images: 5 + offset,
    publish_products: 6 + offset,
  };
}

export function CollectionProvider({ children, projectId, project, collectionId: initialCollectionId, collectionTitle, onClose, onSaved }) {
  const session = useSession();
  const { refreshTokens } = useDashboard();
  const api = useMemo(() => Projects(session), [session]);
  const instagramApi = useMemo(() => Instagram(session), [session]);
  const printifyProductsApi = useMemo(() => PrintifyProducts(session), [session]);
  const imageGenerationApi = useMemo(() => ImageGeneration(session), [session]);

  const [step, setStep] = useState(STEPS.SELECT_PRODUCTS);
  const [maxStepIndex, setMaxStepIndex] = useState(0);
  const [projectQuestions, setProjectQuestions] = useState([]);
  const [items, setItems] = useState([]);
  const [aiItems, setAiItems] = useState([]);
  const [currentItemIndex, setCurrentItemIndex] = useState(0);
  const [currentItemQuestions, setCurrentItemQuestions] = useState([]);
  const [currentArtwork, setCurrentArtwork] = useState(null);
  const [blueprints, setBlueprints] = useState([]);
  const [answers, setAnswers] = useState({});
  const [editableCollectionTitle, setEditableCollectionTitle] = useState(collectionTitle || '');
  const [itemAnswers, setItemAnswers] = useState({});
  const [previewImageData, setPreviewImageData] = useState(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [showChanges, setShowChanges] = useState(false);
  const [requestedChanges, setRequestedChanges] = useState('');
  const [collectionId, setCollectionId] = useState(null);
  const [savedAnswers, setSavedAnswers] = useState({});
  const [collectionArtwork, setCollectionArtwork] = useState([]);
  const [resumeStep, setResumeStep] = useState(null);
  const [message, setMessage] = useState(null);
  const [generatingProgress, setGeneratingProgress] = useState(0);
  const [generatedArtworks, setGeneratedArtworks] = useState([]);
  const [isGeneratingAll, setIsGeneratingAll] = useState(false);
  const [generatingMessage, setGeneratingMessage] = useState('');
  const [previewGenerationThumbs, setPreviewGenerationThumbs] = useState([]);
  const [previewGenerationIndex, setPreviewGenerationIndex] = useState(0);
  const [previewGenerationTotal, setPreviewGenerationTotal] = useState(0);
  const [currentGeneratingIndex, setCurrentGeneratingIndex] = useState(-1);
  const [currentGeneratingItemId, setCurrentGeneratingItemId] = useState(null);
  const [generationError, setGenerationError] = useState(null);
  const [artworkPreview, setArtworkPreview] = useState(null);
  const [initialLoading, setInitialLoading] = useState(true);
  const cancelRef = useRef(false);
  const advanceToNextItemRef = useRef(null);

  // Product image state
  const [productImageVariants, setProductImageVariants] = useState([]);
  const [productImagePrompt, setProductImagePrompt] = useState('');
  const [productImageGenerateTrigger, setProductImageGenerateTrigger] = useState(0);
  const [selectedProductCombos, setSelectedProductCombos] = useState([]);
  const [currentProductComboIndex, setCurrentProductComboIndex] = useState(0);
  const [allProductImages, setAllProductImages] = useState([]);
  const [imageModels, setImageModels] = useState([]);
  const [selectedImageModel, setSelectedImageModel] = useState(null);
  const [selectedProductImageModel, setSelectedProductImageModel] = useState(null);
  const [design, setDesign] = useState('artwork');
  const [optionalPrompt, setOptionalPrompt] = useState('');
  const [patternSettings, setPatternSettings] = useState({ spacingX: 1, spacingY: 1, angle: 0, offset: 0, scale: 0.5 });
  const [upscaleComplete, setUpscaleComplete] = useState(false);
  const [productBlueprintImages, setProductBlueprintImages] = useState([]);
  const [printifyImageIndexByColor, setPrintifyImageIndexByColor] = useState({});
  const [currentProductImageIndex, setCurrentProductImageIndex] = useState(0);
  const [printifyProducts, setPrintifyProducts] = useState([]);
  const [mockups, setMockups] = useState([]);
  const [collectionProducts, setCollectionProducts] = useState([]);

  const [socialMediaImageOrder, setSocialMediaImageOrder] = useState([]);
  const [socialMediaSelectedImages, setSocialMediaSelectedImages] = useState({});
  const [instagramPosted, setInstagramPosted] = useState(false);
  const [instagramPost, setInstagramPost] = useState(null);

  // Sync editable title when the prop changes (e.g. opening an existing collection)
  useEffect(() => {
    setEditableCollectionTitle(collectionTitle || '');
  }, [collectionTitle]);

  const blueprintItemIds = useMemo(() => {
    const activeBlueprintIds = new Set(
      collectionProducts.filter(cp => cp.active).map(cp => cp.projectBlueprintId)
    );
    const ids = new Set();
    for (const bp of blueprints) {
      // Only include items from active products (or all if no collection products exist yet)
      if (collectionProducts.length > 0 && !activeBlueprintIds.has(bp.id)) continue;
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
  }, [blueprints, collectionProducts]);

  const currentItem = aiItems[currentItemIndex];
  const creatingCollectionRef = useRef(null);

  const ensureCollection = useCallback(async () => {
    if (collectionId) return collectionId;
    if (creatingCollectionRef.current) return creatingCollectionRef.current;

    const promise = (async () => {
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
    })();

    creatingCollectionRef.current = promise;
    promise.finally(() => { creatingCollectionRef.current = null; });
    return promise;
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

  const refreshCollectionArtwork = useCallback(async () => {
    if (!collectionId) return;
    try {
      const res = await api.getCollectionArtwork(collectionId);
      if (res.data.success) {
        setCollectionArtwork(res.data.data || []);
      }
    } catch (error) {
      // non-critical
    }
  }, [collectionId, api]);

  const doGeneratePreview = useCallback(async (colId) => {
    const item = aiItems[currentItemIndex];
    if (!item) return;

    setIsGenerating(true);
    setMessage(null);
    cancelRef.current = false;
    try {
      // Save optional prompt to collection artwork before generating
      if (colId) {
        await api.updateCollectionArtworkOptionalPrompt({ collectionId: colId, itemId: item.id, optionalPrompt });
      }

      const answerList = [
        ...buildProjectAnswers(),
        ...Object.entries(itemAnswers || {})
          .filter(([_, value]) => value && value.trim())
          .map(([questionId, answer]) => ({ questionId, answer })),
      ];

      // Get the number of generations from the estimate
      let totalGenerations = 1;
      let generations = [];
      try {
        const estRes = await api.estimateItemTokens(item.id, selectedImageModel?.id, colId, design);
        if (estRes.data.success) {
          const data = estRes.data.data;
          if (typeof data === 'number') {
            totalGenerations = 1;
          } else {
            generations = data.generations || [];
            totalGenerations = generations.length || 1;
          }
        }
      } catch { /* non-critical */ }

      setPreviewGenerationTotal(totalGenerations);
      setPreviewGenerationIndex(0);
      setPreviewGenerationThumbs([]);

      let lastArtwork = null;
      for (let genIdx = 0; genIdx < totalGenerations; genIdx++) {
        if (cancelRef.current) break;
        setPreviewGenerationIndex(genIdx);
        const dims = generations[genIdx];
        const dimStr = dims ? ` (${dims.width}x${dims.height})` : '';
        setGeneratingMessage(`Generating artwork ${genIdx + 1} of ${totalGenerations}${dimStr}...`);

        const res = await api.generateCollectionArtwork({
          projectId,
          collectionId: colId,
          itemId: item.id,
          width: 2048,
          height: 2048,
          answers: answerList,
          requestedChanges: showChanges ? requestedChanges : null,
          modelId: selectedImageModel?.id,
          generationIndex: genIdx,
          design,
          patternJson: design === 'pattern' ? JSON.stringify(patternSettings) : null,
          optionalPrompt,
        });

        if (res.data.success) {
          lastArtwork = res.data.data;
          // Update preview image after each generation
          const placementIndex = lastArtwork.totalPlacements > 0 ? genIdx : null;
          const cacheBust = Math.floor(Math.random() * 100000);
          const url = artworkImageUrl(colId, item.id, lastArtwork.id, { thumb: true, cacheBust, placementIndex });
          setCurrentArtwork(lastArtwork);
          setPreviewImageData(url);
          // Add this generation's thumbnail to the grid
          setPreviewGenerationThumbs(prev => [...prev, { index: genIdx, url, width: dims?.width, height: dims?.height }]);
          setCollectionArtwork(prev => prev.filter(a => String(a.itemId) !== String(item.id)).concat(lastArtwork));
          refreshTokens();
        } else {
          setMessage({ type: 'error', text: res.data.message || 'Failed to generate preview' });
          setIsGenerating(false);
          setPreviewGenerationIndex(0);
          setPreviewGenerationTotal(0);
          return;
        }
      }

      setShowChanges(false);
      setRequestedChanges('');
      if (onSaved) onSaved();
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to generate preview' });
    } finally {
      setIsGenerating(false);
      setPreviewGenerationIndex(0);
      setPreviewGenerationTotal(0);
    }
  }, [aiItems, currentItemIndex, itemAnswers, showChanges, requestedChanges, projectId, api, buildProjectAnswers, selectedImageModel, refreshTokens, onSaved, currentArtwork, optionalPrompt, design]);

  const loadItemData = useCallback(async (index, forceQuestions = false) => {
    setCurrentItemIndex(index);
    const item = aiItems[index];
    if (!item) {
      setStep(STEPS.READY_TO_GENERATE);
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

      // Load design from item artwork config (default for new generations)
      if (art?.design) {
        setDesign(art.design);
      } else {
        setDesign('artwork');
      }
      // Reload collection artwork to get the latest optionalPrompt and patternJson
      let latestCollectionArtwork = collectionArtwork;
      if (collectionId) {
        try {
          const artRes2 = await api.getCollectionArtwork(collectionId);
          if (artRes2.data.success) {
            latestCollectionArtwork = artRes2.data.data || [];
            setCollectionArtwork(latestCollectionArtwork);
          }
        } catch { /* non-critical */ }
      }

      // Load pattern settings from existing collection artwork if available
      const existingCollectionArt = latestCollectionArtwork.find(a => String(a.itemId) === String(item.id) && a.active);
      setOptionalPrompt(existingCollectionArt?.optionalPrompt || '');
      if (existingCollectionArt?.patternJson) {
        try {
          const parsed = JSON.parse(existingCollectionArt.patternJson);
          setPatternSettings({
            spacingX: parsed.spacingX ?? 0,
            spacingY: parsed.spacingY ?? 0,
            angle: parsed.angle ?? 0,
            offset: parsed.offset ?? 0,
            scale: parsed.scale ?? 0.5,
          });
        } catch { setPatternSettings({ spacingX: 1, spacingY: 1, angle: 0, offset: 0, scale: 0.5 }); }
      } else {
        setPatternSettings({ spacingX: 1, spacingY: 1, angle: 0, offset: 0, scale: 0.5 });
      }

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
      if (existingArt && !forceQuestions) {
        const placementIndex = existingArt.totalPlacements > 0 ? 0 : null;
        setPreviewImageData(artworkImageUrl(collectionId, item.id, existingArt.id, { thumb: true, cacheBust: Math.floor(Math.random() * 100000), placementIndex }));
        setCurrentArtwork(existingArt);
        // Load design from existing collection artwork
        if (existingArt.design) setDesign(existingArt.design);
        if (existingArt.patternJson) {
          try {
            const parsed = JSON.parse(existingArt.patternJson);
            setPatternSettings({
              spacingX: parsed.spacingX ?? 0,
              spacingY: parsed.spacingY ?? 0,
              angle: parsed.angle ?? 0,
              offset: parsed.offset ?? 0,
              scale: parsed.scale ?? 0.5,
            });
          } catch { setPatternSettings({ spacingX: 1, spacingY: 1, angle: 0, offset: 0, scale: 0.5 }); }
        }
        setStep(STEPS.ARTWORK_PREVIEW);
      } else if (questions.length > 0) {
        setPreviewImageData(null);
        setStep(STEPS.ARTWORK_QUESTIONS);
      } else {
        setPreviewImageData(null);
        setStep(STEPS.ARTWORK_QUESTIONS);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load artwork data' });
    } finally {
      setInitialLoading(false);
    }
  }, [aiItems, collectionId, savedAnswers, api, ensureCollection, doGeneratePreview, projectId]);

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

  const selectedImageModelRef = useRef(selectedImageModel);
  selectedImageModelRef.current = selectedImageModel;
  const selectedProductImageModelRef = useRef(selectedProductImageModel);
  selectedProductImageModelRef.current = selectedProductImageModel;

  const loadImageModels = useCallback(async () => {
    try {
      const res = await imageGenerationApi.getActiveModels();
      if (res.data.success) {
        const models = res.data.data || [];
        setImageModels(models);
        if (models.length > 0 && !selectedImageModelRef.current) {
          setSelectedImageModel(models[0]);
        }
        if (models.length > 0 && !selectedProductImageModelRef.current) {
          setSelectedProductImageModel(models[0]);
        }
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load image models' });
    }
  }, [api]);

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
                setStep(STEPS.GENERATE_PRODUCT_IMAGES);
                return;
              }
            } catch (e) {  }
            setStep(STEPS.GENERATE_PRODUCT_IMAGES);
          } else {
            setStep(STEPS.READY_TO_GENERATE);
          }
        })();
      } else {
        setStep(STEPS.READY_TO_GENERATE);
      }
    } else {
      setCurrentItemIndex(nextIndex);
      loadItemData(nextIndex);
    }
  }, [currentItemIndex, collectionArtwork, aiItems, blueprintItemIds, loadItemData, collectionId, ensureCollection, loadImageModels, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex, blueprints, api, printifyImageIndexByColor]);
  advanceToNextItemRef.current = advanceToNextItem;

  // Check if an artwork has any placements that still need upscaling
  const hasPendingPlacements = (a) => {
    if (a.hasGroups) {
      // Group image needs upscaling if any group placement isn't fullSize
      const groupPlacements = (a.placements || []).filter(p => p.groupId);
      if (groupPlacements.length > 0 && groupPlacements.some(p => !p.fullSize)) return true;
      if (groupPlacements.length === 0 && !a.fullSize) return true;
    }
    const pending = (a.placements || []).filter(p => !p.groupId && !p.fullSize);
    if (pending.length > 0) return true;
    if (!a.fullSize && (!a.placements || a.placements.length === 0)) return true;
    return false;
  };

  const doGenerateAll = useCallback(async (colId) => {
    // Build a flat list of individual upscale tasks (one per group, placement, or base artwork)
    const tasks = [];
    for (const a of collectionArtwork) {
      if (!aiItems.some(item => String(item.id) === String(a.itemId))) continue;
      if (!hasPendingPlacements(a)) continue;

      const item = aiItems.find(i => String(i.id) === String(a.itemId));

      // Group tasks — one per group
      if (a.hasGroups && a.groupPlacements) {
        for (const grp of a.groupPlacements) {
          const groupPlacements = (a.placements || []).filter(p => p.groupId === grp.groupId);
          const needsUpscale = groupPlacements.length > 0 ? groupPlacements.some(p => !p.fullSize) : !a.fullSize;
          if (needsUpscale) {
            tasks.push({
              artwork: a, item, groupId: grp.groupId,
              label: `${item?.title || 'Untitled'} (Group)`,
            });
          }
        }
      }

      // Non-group placement tasks — one per placement index (deduplicated)
      const seenIndices = new Set();
      const nonGroupPlacements = (a.placements || []).filter(p => !p.groupId && !p.fullSize);
      for (const p of nonGroupPlacements) {
        if (seenIndices.has(p.index)) continue;
        seenIndices.add(p.index);
        tasks.push({
          artwork: a, item, placementIndex: p.index,
          label: `${item?.title || 'Untitled'} (Placement ${p.index + 1})`,
        });
      }

      // Base artwork task (no placements at all)
      if ((!a.placements || a.placements.length === 0) && !a.fullSize) {
        tasks.push({
          artwork: a, item,
          label: `${item?.title || 'Untitled'}`,
        });
      }
    }

    if (tasks.length === 0) {
      setUpscaleComplete(true);
      return;
    }

    const totalImageCount = tasks.length;
    setIsGeneratingAll(true);
    setGeneratingProgress(0);
    setGeneratedArtworks([]);
    setCurrentGeneratingIndex(0);
    setCurrentGeneratingItemId(null);
    setGenerationError(null);
    setGeneratingMessage(`Upscaling artwork 1 of ${totalImageCount}...`);
    cancelRef.current = false;

    const completedItemIds = new Set();
    let imagesProcessed = 0;

    for (let i = 0; i < tasks.length; i++) {
      if (cancelRef.current) break;

      const task = tasks[i];
      const { artwork: art, item } = task;
      setCurrentGeneratingIndex(i);
      setCurrentGeneratingItemId(art.itemId);
      setGeneratingMessage(`Upscaling artwork ${imagesProcessed + 1} of ${totalImageCount}: ${task.label} (${art.width}x${art.height})...`);

      try {
        const res = await api.upscaleArtwork({
          projectId,
          collectionId: colId,
          itemId: art.itemId,
          groupId: task.groupId || undefined,
          placementIndex: task.placementIndex != null ? task.placementIndex : undefined,
        });

        if (res.data.success) {
          completedItemIds.add(art.itemId);
          // Refresh from server to get accurate per-placement fullSize data after each task
          const artRes = await api.getCollectionArtwork(colId);
          if (artRes.data.success) {
            setCollectionArtwork(artRes.data.data || []);
          }
          // Track completed tasks for the overlay checkmarks
          const completedTask = {
            itemId: art.itemId,
            groupId: task.groupId,
            placementIndex: task.placementIndex,
          };
          setGeneratedArtworks(prev => [...prev, completedTask]);
        } else {
          setGenerationError(res.data.message || 'Failed to upscale artwork');
          setIsGeneratingAll(false);
          return;
        }
      } catch (error) {
        setGenerationError(error?.response?.data?.message || error?.message || 'Failed to upscale artwork');
        setIsGeneratingAll(false);
        return;
      }

      imagesProcessed++;
      setGeneratingProgress(Math.round((imagesProcessed / totalImageCount) * 100));
    }

    setIsGeneratingAll(false);
    setCurrentGeneratingIndex(-1);
    setCurrentGeneratingItemId(null);
    if (!cancelRef.current) {
      setUpscaleComplete(true);
      if (onSaved) onSaved();
    }
    refreshTokens();
  }, [aiItems, projectId, api, collectionArtwork, refreshTokens, onSaved]);

  const cancelAll = useCallback(() => {
    cancelRef.current = true;
    setIsGenerating(false);
    setIsGeneratingAll(false);
    setPreviewGenerationIndex(0);
    setPreviewGenerationTotal(0);
    setPreviewGenerationThumbs([]);
    setGeneratingProgress(0);
    setCurrentGeneratingIndex(-1);
    setCurrentGeneratingItemId(null);
    setGeneratingMessage('');
  }, []);

  const reset = useCallback(() => {
    setStep(STEPS.SELECT_PRODUCTS);
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
    setMessage(null);
    setGeneratingProgress(0);
    setGeneratedArtworks([]);
    setIsGeneratingAll(false);
    setGeneratingMessage('');
    setCurrentGeneratingIndex(-1);
    setCurrentGeneratingItemId(null);
    setGenerationError(null);
    setPreviewGenerationIndex(0);
    setPreviewGenerationTotal(0);
    setPreviewGenerationThumbs([]);
    setArtworkPreview(null);
    setInitialLoading(true);
    cancelRef.current = false;
    setProductImageVariants([]);
    setProductImagePrompt('');
    setOptionalPrompt('');
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
      const res = await api.loadCollectionWizard(projectId, existingCollectionId || null);
      if (!res.data.success) {
        setMessage({ type: 'error', text: res.data.message || 'Failed to load data' });
        setInitialLoading(false);
        return;
      }

      const d = res.data.data;

      // Questions
      setProjectQuestions(d.questions || []);

      // Items
      const allItems = d.items || [];
      setItems(allItems);

      // Blueprints
      const allBps = d.blueprints || [];
      const completeBps = allBps.filter(bp => bp.configured === true);
      setBlueprints(completeBps);
      setPrintifyImageIndexByColor(d.printifyImageIndexByColor || {});

      // Item references
      const refs = (d.itemReferences || []).map(r => ({
        ...r,
        itemId: r.itemId ?? r.ItemId,
        artworkId: r.artworkId ?? r.ArtworkId,
      }));

      // Collection-specific data
      if (existingCollectionId && d.answers) {
        let savedAnsMap = {};
        for (const a of d.answers) {
          const key = a.itemId ? `${a.itemId}:${a.questionId}` : `project:${a.questionId}`;
          savedAnsMap[key] = a.answer;
          if (a.itemId) {
            setItemAnswers(prev => ({ ...prev, [a.questionId]: a.answer }));
          } else {
            setAnswers(prev => ({ ...prev, [a.questionId]: a.answer }));
          }
        }
        setSavedAnswers(savedAnsMap);

        const artworkList = d.artwork || [];
        setCollectionArtwork(artworkList);

        const hasArtwork = artworkList.length > 0;
        const allUpscaled = hasArtwork && artworkList.every(a => a.fullSize);
        setUpscaleComplete(allUpscaled);

        setPrintifyProducts(d.printifyProducts || []);
        setMockups(d.mockups || []);
        setCollectionProducts(d.collectionProducts || []);
        setProductBlueprintImages(d.productBlueprintImages || []);
        setAllProductImages((d.productImages || []).filter(img => img.active));
        setInstagramPosted(d.instagramPosted || false);
        setInstagramPost(d.instagramPost || null);

        // Determine resume step
        const questions = d.questions || [];
        const collectionProductsList = d.collectionProducts || [];
        const allProjectQuestionsAnswered = questions.length === 0 || questions.every(q => savedAnsMap[`project:${q.id}`]);
        const loadedBlueprints = completeBps;
        const allBlueprintsHaveProducts = loadedBlueprints.length > 0 &&
          loadedBlueprints.every(bp => collectionProductsList.some(cp => String(cp.projectBlueprintId) === String(bp.id)));

        // Compute aiItems so ResumeManager has everything immediately
        const activeBlueprintIds = new Set(
          collectionProductsList.filter(cp => cp.active).map(cp => cp.projectBlueprintId)
        );
        const computedBlueprintItemIds = new Set();
        for (const bp of loadedBlueprints) {
          if (collectionProductsList.length > 0 && !activeBlueprintIds.has(bp.id)) continue;
          if (!bp.placementJson) continue;
          try {
            const placements = JSON.parse(bp.placementJson);
            if (!placements || !Array.isArray(placements)) continue;
            for (const p of placements) {
              if (p.source === 'item' && p.itemId) computedBlueprintItemIds.add(String(p.itemId));
            }
          } catch { /* ignore */ }
        }
        const referencedArtworkIds = new Set();
        for (const ref of refs) {
          if (ref.artworkId) referencedArtworkIds.add(String(ref.artworkId));
        }
        for (const item of allItems) {
          if (!item.opacityJson) continue;
          try {
            const parsed = JSON.parse(item.opacityJson);
            if (parsed?.background?.type === 'artwork' && parsed.background.id) {
              referencedArtworkIds.add(String(parsed.background.id));
            }
          } catch { /* ignore */ }
        }
        const computedAiItems = allItems.filter(i =>
          i.artworkType !== 'custom' &&
          (computedBlueprintItemIds.has(String(i.id)) || i.socialMedia || referencedArtworkIds.has(String(i.id)))
        );
        setAiItems(computedAiItems);

        console.log('[ResumeCheck]', {
          blueprints: loadedBlueprints.map(bp => ({ id: bp.id, configured: bp.configured })),
          collectionProducts: collectionProductsList.map(cp => ({ projectBlueprintId: cp.projectBlueprintId, active: cp.active })),
          allBlueprintsHaveProducts,
          allProjectQuestionsAnswered,
          questionsCount: questions.length,
        });
        if (!allBlueprintsHaveProducts) {
          setResumeStep(STEPS.SELECT_PRODUCTS);
        } else if (!allProjectQuestionsAnswered) {
          setResumeStep(STEPS.PROJECT_QUESTIONS);
        } else {
          setResumeStep('artwork_resume');
        }
        // Don't set initialLoading=false here — ResumeManager will set it
        // after navigating to the correct step.
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

    if (targetStep === STEPS.GENERATE_PRODUCT_IMAGES) {
      setStep(STEPS.GENERATE_PRODUCT_IMAGES);
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
          variantIds: pbi.variantIds || [],
          prompt: pbi.prompt || '',
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

  const goBack = useCallback(() => {
    setResumeStep(null);

    if ((step === STEPS.ARTWORK_PREVIEW || step === STEPS.ARTWORK_QUESTIONS) && aiItems.length > 0) {
      if (currentItemIndex > 0) {
        loadItemData(currentItemIndex - 1);
      } else {
        setStep(STEPS.PROJECT_QUESTIONS);
      }
      return;
    }

    if (step === STEPS.GENERATE_PRODUCT_IMAGES) {
      setStep(STEPS.CREATE_PRODUCTS);
      return;
    }

    if (step === STEPS.PRODUCT_IMAGE_PROMPT && selectedProductCombos.length > 0) {
      if (currentProductComboIndex > 0) {
        const prevIndex = currentProductComboIndex - 1;
        const prevCombo = selectedProductCombos[prevIndex];
        setCurrentProductComboIndex(prevIndex);
        const existing = allProductImages.find(img =>
          img.projectBlueprintId === prevCombo.projectBlueprintId &&
          img.productImageId === prevCombo.productImageId
        );
        setProductImagePrompt(existing?.prompt || prevCombo.prompt || '');
        setProductImageGenerateTrigger(0);
        setStep(STEPS.PRODUCT_IMAGE_PREVIEW);
      } else {
        setStep(STEPS.CREATE_PRODUCTS);
      }
      return;
    }

    if (step === STEPS.PRODUCT_IMAGE_PREVIEW && selectedProductCombos.length > 0) {
      const currCombo = selectedProductCombos[currentProductComboIndex];
      const existing = allProductImages.find(img =>
        img.projectBlueprintId === currCombo.projectBlueprintId &&
        img.productImageId === currCombo.productImageId
      );
      setProductImagePrompt(existing?.prompt || currCombo.prompt || '');
      setStep(STEPS.PRODUCT_IMAGE_PROMPT);
      return;
    }

    const index = STEP_ORDER.indexOf(step);
    if (index <= 0) return;

    const prevStep = STEP_ORDER[index - 1];

    if (prevStep === STEPS.ARTWORK_PREVIEW && aiItems.length > 0) {
      const lastItem = aiItems[aiItems.length - 1];
      reviewStep(STEPS.ARTWORK_PREVIEW, lastItem.id);
      return;
    }

    setStep(prevStep);
  }, [step, currentItemIndex, currentProductComboIndex, aiItems, selectedProductCombos, allProductImages, loadItemData, reviewStep, setCurrentProductComboIndex, setProductImagePrompt, setStep, setResumeStep, STEPS]);

  const value = {
    // props
    projectId, project, collectionTitle, editableCollectionTitle, setEditableCollectionTitle, onClose, onSaved, api,
    // step
    step, setStep, STEPS, wizardSteps, stepIndex, maxStepIndex, goBack,
    // data
    projectQuestions, items, aiItems, setAiItems, blueprints, blueprintItemIds,
    currentItemIndex, setCurrentItemIndex, currentItem,
    currentItemQuestions, currentArtwork, setCurrentArtwork,
    collectionId, setCollectionId, collectionArtwork, setCollectionArtwork,
    savedAnswers,
    // form state
    answers, setAnswers, itemAnswers, setItemAnswers,
    previewImageData, setPreviewImageData,
    isGenerating, setIsGenerating,
    showChanges, setShowChanges,
    requestedChanges, setRequestedChanges,
    // generation state
    previewGenerationIndex, previewGenerationTotal, previewGenerationThumbs,
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
    cancelAll,
    // helpers
    ensureCollection, buildProjectAnswers, buildAllAnswers, saveAnswers,
    refreshCollectionArtwork, doGeneratePreview, loadItemData, advanceToNextItem,
    doGenerateAll, reviewStep,
    // product image
    productImageVariants, productImagePrompt, setProductImagePrompt,
    productImageGenerateTrigger, setProductImageGenerateTrigger,
    selectedProductCombos, setSelectedProductCombos,
    currentProductComboIndex, setCurrentProductComboIndex,
    allProductImages, setAllProductImages,
    loadProductImageVariants, loadImageModels,
    imageModels, selectedImageModel, setSelectedImageModel,
    selectedProductImageModel, setSelectedProductImageModel,
    design, setDesign, optionalPrompt, setOptionalPrompt, patternSettings, setPatternSettings,
    upscaleComplete, setUpscaleComplete,
    productBlueprintImages, setProductBlueprintImages,
    printifyImageIndexByColor,
    currentProductImageIndex, setCurrentProductImageIndex,
    printifyProducts, setPrintifyProducts,
    collectionProducts, setCollectionProducts,
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
