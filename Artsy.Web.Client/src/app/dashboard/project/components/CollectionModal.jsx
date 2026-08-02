import React, { useEffect, useMemo, useState } from 'react';
import { CollectionProvider, useCollection, STEPS } from '@/context/collection';
import Modal from '@/components/ui/modal';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import Steps from '@/components/ui/steps';
import Icon from '@/components/ui/icon';
import ArtworkPreviewModal from './ProductImagePreview';
import ProjectQuestions from './collection-steps/ProjectQuestions';
import ArtworkQuestions from './collection-steps/ArtworkQuestions';
import ArtworkPreview from './collection-steps/ArtworkPreview';
import ReadyToGenerate from './collection-steps/ReadyToGenerate';
import PostSocialMedia from './collection-steps/PostSocialMedia';
import SummaryStep from './collection-steps/SummaryStep';
import CreateProducts from './collection-steps/CreateProducts';
import PublishProductsStep from './collection-steps/PublishProductsStep';
import ProductImagePrompt from './collection-steps/ProductImagePrompt';
import ProductImagePreview from './collection-steps/ProductImagePreview';
import CollectionSetupList from './CollectionSetupList';

const stepTitle = (step, title) => {
  const prefix = title || 'New Collection';
  switch (step) {
    case STEPS.PROJECT_QUESTIONS: return `${prefix} - Project Questions`;
    case STEPS.ARTWORK_QUESTIONS: return `${prefix} - Artwork Questions`;
    case STEPS.ARTWORK_PREVIEW: return `${prefix} - Artwork Preview`;
    case STEPS.READY_TO_GENERATE: return `${prefix} - Ready to Upscale`;
    case STEPS.PRODUCT_IMAGE_PROMPT: return `${prefix} - Product Image Prompt`;
    case STEPS.PRODUCT_IMAGE_PREVIEW: return `${prefix} - Product Images`;
    case STEPS.CREATE_PRODUCTS: return `${prefix} - Create Products`;
    case STEPS.PUBLISH_PRODUCTS: return `${prefix} - Publish Products`;
    case STEPS.SOCIAL_MEDIA: return `${prefix} - Social Media`;
    case STEPS.SUMMARY: return `${prefix} - Summary`;
    default: return prefix;
  }
};

function CollectionWizard() {
  const {
    step, message, setMessage,
    initialLoading, artworkPreview, setArtworkPreview,
    onClose, STEPS, wizardSteps, stepIndex, maxStepIndex,
    collectionTitle, setStep, reviewStep,
  } = useCollection();

  const [showChecklist, setShowChecklist] = useState(false);

  const stepFromIndex = useMemo(() => {
    const map = {};
    const order = [
      STEPS.PROJECT_QUESTIONS,
      STEPS.ARTWORK_QUESTIONS,
      STEPS.ARTWORK_PREVIEW,
      STEPS.READY_TO_GENERATE,
      STEPS.CREATE_PRODUCTS,
      STEPS.PRODUCT_IMAGE_PROMPT,
      STEPS.PRODUCT_IMAGE_PREVIEW,
      STEPS.PUBLISH_PRODUCTS,
      STEPS.SOCIAL_MEDIA,
      STEPS.SUMMARY,
    ];
    for (const s of order) {
      const idx = stepIndex[s];
      if (idx !== undefined && map[idx] === undefined) {
        map[idx] = s;
      }
    }
    return map;
  }, [stepIndex, STEPS]);

  const handleStepClick = (index) => {
    const targetStep = stepFromIndex[index];
    if (!targetStep) return;
    reviewStep(targetStep);
    setShowChecklist(true);
  };

  return (
    <Modal
      title={stepTitle(step, collectionTitle)}
      onClose={onClose}
      top
      className="min-w-[40em] max-w-full"
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      {initialLoading ? (
        <div className="flex items-center justify-center py-12">
          <Spinner className="text-4xl" />
        </div>
      ) : (
        <>
          <Steps steps={wizardSteps} currentIndex={stepIndex[step] ?? 0} maxIndex={maxStepIndex} onStepClick={handleStepClick} />
          <div className="flex items-center gap-2 mb-4">
            <hr className="flex-1 border-gray-200 dark:border-gray-700" />
            <button
              onClick={() => setShowChecklist(prev => !prev)}
              className="rounded-full p-3 pb-2 transition-colors hover:bg-gray-100 dark:hover:bg-gray-700"
              title={showChecklist ? 'Hide' : 'Show'}
            >
              <Icon
                name="expand_more"
                className={`text-lg leading-none text-gray-500 dark:text-gray-400 transition-transform duration-200`}
                style={{ display: 'block', transform: showChecklist ? 'rotate(180deg) translateY(4px)' : 'translateY(-2px)' }}
              />
            </button>
            <hr className="flex-1 border-gray-200 dark:border-gray-700" />
          </div>
          <div className={showChecklist ? "flex gap-4 items-stretch" : ""}>
            {showChecklist && (
              <div className="w-[450px] shrink-0 overflow-y-auto max-h-[60vh]">
                <CollectionSetupList />
              </div>
            )}
            <div className={showChecklist ? "flex-1 min-w-[600px] flex flex-col" : ""}>
              {step === STEPS.PROJECT_QUESTIONS && <ProjectQuestions />}
              {step === STEPS.ARTWORK_QUESTIONS && <ArtworkQuestions />}
              {step === STEPS.ARTWORK_PREVIEW && <ArtworkPreview />}
              {step === STEPS.READY_TO_GENERATE && <ReadyToGenerate />}
              {step === STEPS.PRODUCT_IMAGE_PROMPT && <ProductImagePrompt />}
              {step === STEPS.PRODUCT_IMAGE_PREVIEW && <ProductImagePreview />}
              {step === STEPS.CREATE_PRODUCTS && <CreateProducts />}
              {step === STEPS.PUBLISH_PRODUCTS && <PublishProductsStep />}
              {step === STEPS.SOCIAL_MEDIA && <PostSocialMedia />}
              {step === STEPS.SUMMARY && <SummaryStep />}
            </div>
          </div>
        </>
      )}

      <ArtworkPreviewModal
        show={!!artworkPreview}
        images={artworkPreview?.images || []}
        alt="Artwork Preview"
        defaultIndex={artworkPreview ? (artworkPreview._idx ?? artworkPreview.images.indexOf(artworkPreview.src)) : 0}
        onClose={() => setArtworkPreview(null)}
      />
    </Modal>
  );
}

function ResumeManager({ show, projectId, initialCollectionId }) {
  const {
    items, setAiItems, aiItems,
    resumeStep, setResumeStep, blueprintItemIds,
    collectionArtwork, savedAnswers,
    setStep, setCurrentItemIndex, loadItemData,
    fetchEstimate, setInitialLoading,
    STEPS, reset, loadData,
    loadImageModels, ensureCollection,
    api, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex,
    blueprints, setProductBlueprintImages, setProductImagePrompt,
    printifyImageIndexByColor, printifyProducts,
    instagramPosted, setInstagramPost,
  } = useCollection();

  const [aiItemsLoaded, setAiItemsLoaded] = useState(false);

  useEffect(() => {
    if (!show || !projectId) return;
    setAiItemsLoaded(false);
    reset();
    loadData(initialCollectionId || null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [show, projectId]);

  useEffect(() => {
    if (!items || items.length === 0) return;
    const ai = items.filter(i =>
      i.artworkType !== 'custom' &&
      (blueprintItemIds.has(String(i.id)) || i.socialMedia)
    );
    setAiItems(ai);
    setAiItemsLoaded(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items, blueprintItemIds]);

  useEffect(() => {
    if (!resumeStep) return;
    if (resumeStep === STEPS.PROJECT_QUESTIONS) {
      setResumeStep(null);
      setInitialLoading(false);
      return;
    }
    if (resumeStep === 'artwork_resume') {
      if (!aiItemsLoaded) return;
      if (initialCollectionId && collectionArtwork.length === 0) return;
      setResumeStep(null);

      const artworkItemIds = new Set(
        collectionArtwork.map(a => String(a.itemId))
      );

      const unacceptedArtworkItemIds = collectionArtwork
        .filter(a => !a.accepted)
        .map(a => String(a.itemId));

      const itemsWithAnswers = new Set();
      for (const key of Object.keys(savedAnswers)) {
        const sepIdx = key.indexOf(':');
        if (sepIdx > 0) {
          const itemId = key.substring(0, sepIdx);
          if (itemId !== 'project') itemsWithAnswers.add(itemId);
        }
      }

      const firstMissingIndex = aiItems.findIndex(item =>
        !artworkItemIds.has(String(item.id))
      );

      if (firstMissingIndex !== -1) {
        setCurrentItemIndex(firstMissingIndex);
        loadItemData(firstMissingIndex);
      } else {
        const firstUnacceptedIndex = aiItems.findIndex(item =>
          (blueprintItemIds.has(String(item.id)) || item.socialMedia) &&
          unacceptedArtworkItemIds.includes(String(item.id))
        );

        if (firstUnacceptedIndex !== -1) {
          setCurrentItemIndex(firstUnacceptedIndex);
          loadItemData(firstUnacceptedIndex);
        } else {
          const customItemIds = new Set(collectionArtwork.filter(a => a.imageModel === 'custom').map(a => String(a.itemId)));
          const aiBlueprintItems = aiItems.filter(item => !customItemIds.has(String(item.id)));
          const allFullSize = aiBlueprintItems.length > 0 && aiBlueprintItems.every(item => {
            const art = collectionArtwork.find(a => String(a.itemId) === String(item.id));
            return art && art.accepted && art.fullSize;
          });
          if (allFullSize) {
            (async () => {
              const colId = initialCollectionId || await ensureCollection();
              if (colId) {
                await loadImageModels();
                try {
                  const pbImgRes = await api.getAllProductBlueprintImages(projectId);
                  const allPbImages = pbImgRes.data.success ? (pbImgRes.data.data || []) : [];
                  setProductBlueprintImages(allPbImages);

                  const imgRes = await api.getProductImages(colId);
                  if (imgRes.data.success) {
                    const allImages = (imgRes.data.data || []).filter(img => img.active);
                    setAllProductImages(allImages);

                    const acceptedProductImageIds = new Set(
                      allImages.filter(img => img.accepted).map(img => img.productImageId)
                    );

                    const missing = allPbImages.filter(pbi => !acceptedProductImageIds.has(pbi.id));

                    if (allPbImages.length === 0) {
                      const createdBpIds = new Set(
                        printifyProducts.filter(pp => pp.printifyProductId && pp.projectBlueprintId && pp.mockupsDownloaded)
                          .map(pp => pp.projectBlueprintId)
                      );
                      const allProductsCreated = blueprints.length > 0 && blueprints.every(bp => createdBpIds.has(bp.id));

                      if (allProductsCreated) {
                        const allPublished = printifyProducts.length > 0 && printifyProducts.every(pp => pp.published);
                        setStep(allPublished ? (instagramPosted ? STEPS.SUMMARY : STEPS.SOCIAL_MEDIA) : STEPS.PUBLISH_PRODUCTS);
                      } else {
                        setStep(STEPS.CREATE_PRODUCTS);
                      }
                      setInitialLoading(false);
                      return;
                    }

                    if (missing.length === 0) {
                      try {
                        await api.ensurePrintifyProducts({ collectionId: colId });
                      } catch (e) { /* non-critical */ }

                      const createdBpIds = new Set(
                        printifyProducts.filter(pp => pp.printifyProductId && pp.projectBlueprintId && pp.mockupsDownloaded)
                          .map(pp => pp.projectBlueprintId)
                      );
                      const allProductsCreated = blueprints.length > 0 && blueprints.every(bp => createdBpIds.has(bp.id));

                      if (allProductsCreated) {
                        const allPublished = printifyProducts.length > 0 && printifyProducts.every(pp => pp.published);
                        setStep(allPublished ? (instagramPosted ? STEPS.SUMMARY : STEPS.SOCIAL_MEDIA) : STEPS.PUBLISH_PRODUCTS);
                      } else {
                        setStep(STEPS.CREATE_PRODUCTS);
                      }
                      setInitialLoading(false);
                      return;
                    }

                    const createdBpIds = new Set(
                      printifyProducts.filter(pp => pp.printifyProductId && pp.projectBlueprintId && pp.mockupsDownloaded)
                        .map(pp => pp.projectBlueprintId)
                    );
                    const allProductsCreated = blueprints.length > 0 && blueprints.every(bp => createdBpIds.has(bp.id));

                    if (!allProductsCreated) {
                      setStep(STEPS.CREATE_PRODUCTS);
                      setInitialLoading(false);
                      return;
                    }

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
                    setInitialLoading(false);
                    return;
                  }
                } catch (e) {
                  const createdBpIds = new Set(
                    printifyProducts.filter(pp => pp.printifyProductId && pp.projectBlueprintId && pp.mockupsDownloaded)
                      .map(pp => pp.projectBlueprintId)
                  );
                  const allProductsCreated = blueprints.length > 0 && blueprints.every(bp => createdBpIds.has(bp.id));
                  if (!allProductsCreated) {
                    setStep(STEPS.CREATE_PRODUCTS);
                  } else {
                    setStep(STEPS.PRODUCT_IMAGE_PROMPT);
                  }
                }
                setInitialLoading(false);
              } else {
                setStep(STEPS.READY_TO_GENERATE);
                fetchEstimate();
              }
            })();
          } else {
            setStep(STEPS.READY_TO_GENERATE);
            fetchEstimate();
          }
        }
      }
      setInitialLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aiItems, aiItemsLoaded, resumeStep, blueprintItemIds, collectionArtwork, initialCollectionId]);

  return null;
}

export default function CollectionModal({ show, projectId, project, collectionId: initialCollectionId, collectionTitle, onClose, onSaved }) {
  if (!show) return null;

  return (
    <CollectionProvider
      projectId={projectId}
      project={project}
      collectionId={initialCollectionId}
      collectionTitle={collectionTitle}
      onClose={onClose}
      onSaved={onSaved}
    >
      <ResumeManager
        show={show}
        projectId={projectId}
        initialCollectionId={initialCollectionId}
      />
      <CollectionWizard />
    </CollectionProvider>
  );
}
