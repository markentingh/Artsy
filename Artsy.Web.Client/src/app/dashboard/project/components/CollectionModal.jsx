import React, { useEffect, useMemo, useState, lazy, Suspense } from 'react';
import { CollectionProvider, useCollection, STEPS } from '@/context/collection';
import Modal from '@/components/ui/modal';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import Steps from '@/components/ui/steps';
import Icon from '@/components/ui/icon';
import ArtworkPreviewModal from './ProductImagePreview';
import CollectionSetupList from './CollectionSetupList';
const SelectProductsStep = lazy(() => import('./collection-steps/SelectProductsStep'));
const ProjectQuestions = lazy(() => import('./collection-steps/ProjectQuestions'));
const ArtworkQuestions = lazy(() => import('./collection-steps/ArtworkQuestions'));
const ArtworkPreview = lazy(() => import('./collection-steps/ArtworkPreview'));
const ReadyToGenerate = lazy(() => import('./collection-steps/ReadyToGenerate'));
const CreateProducts = lazy(() => import('./collection-steps/CreateProducts'));
const PublishProductsStep = lazy(() => import('./collection-steps/PublishProductsStep'));
const GenerateProductImages = lazy(() => import('./collection-steps/GenerateProductImages'));
const ProductImagePrompt = lazy(() => import('./collection-steps/ProductImagePrompt'));
const ProductImagePreview = lazy(() => import('./collection-steps/ProductImagePreview'));

const stepTitle = (step, title) => {
  const prefix = title || 'New Collection';
  switch (step) {
    case STEPS.SELECT_PRODUCTS: return `${prefix} - Select Products`;
    case STEPS.PROJECT_QUESTIONS: return `${prefix} - Project Questions`;
    case STEPS.ARTWORK_QUESTIONS: return `${prefix} - Artwork Generation`;
    case STEPS.ARTWORK_PREVIEW: return `${prefix} - Artwork Preview`;
    case STEPS.READY_TO_GENERATE: return `${prefix} - Ready to Upscale`;
    case STEPS.PRODUCT_IMAGE_PROMPT: return `${prefix} - Product Image Prompt`;
    case STEPS.PRODUCT_IMAGE_PREVIEW: return `${prefix} - Product Images`;
    case STEPS.GENERATE_PRODUCT_IMAGES: return `${prefix} - Product Images`;
    case STEPS.CREATE_PRODUCTS: return `${prefix} - Create Products`;
    case STEPS.PUBLISH_PRODUCTS: return `${prefix} - Publish Products`;
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
      STEPS.SELECT_PRODUCTS,
      STEPS.PROJECT_QUESTIONS,
      STEPS.ARTWORK_QUESTIONS,
      STEPS.ARTWORK_PREVIEW,
      STEPS.READY_TO_GENERATE,
      STEPS.CREATE_PRODUCTS,
      STEPS.GENERATE_PRODUCT_IMAGES,
      STEPS.PUBLISH_PRODUCTS,
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
      className="min-w-[40em] max-w-[95vw] w-[1200px]"
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
              <Suspense fallback={<div className="flex items-center justify-center py-12"><Spinner className="text-4xl" /></div>}>
                {step === STEPS.SELECT_PRODUCTS && <SelectProductsStep />}
                {step === STEPS.PROJECT_QUESTIONS && <ProjectQuestions />}
                {step === STEPS.ARTWORK_QUESTIONS && <ArtworkQuestions />}
                {step === STEPS.ARTWORK_PREVIEW && <ArtworkPreview />}
                {step === STEPS.READY_TO_GENERATE && <ReadyToGenerate />}
                {step === STEPS.GENERATE_PRODUCT_IMAGES && <GenerateProductImages />}
                {step === STEPS.PRODUCT_IMAGE_PROMPT && <ProductImagePrompt />}
                {step === STEPS.PRODUCT_IMAGE_PREVIEW && <ProductImagePreview />}
                {step === STEPS.CREATE_PRODUCTS && <CreateProducts />}
                {step === STEPS.PUBLISH_PRODUCTS && <PublishProductsStep />}
              </Suspense>
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
    collectionArtwork, savedAnswers, upscaleComplete,
    setStep, setCurrentItemIndex, loadItemData,
    refreshCollectionArtwork, setInitialLoading,
    STEPS, reset, loadData,
    loadImageModels, ensureCollection,
    api, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex,
    blueprints, setProductBlueprintImages, setProductImagePrompt,
    printifyImageIndexByColor, printifyProducts,
    collectionProducts,
  } = useCollection();

  const [aiItemsLoaded, setAiItemsLoaded] = useState(false);
  const [itemReferences, setItemReferences] = useState([]);

  useEffect(() => {
    if (!show || !projectId) return;
    setAiItemsLoaded(false);
    reset();
    loadData(initialCollectionId || null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [show, projectId]);

  useEffect(() => {
    if (!show || !projectId) return;
    api.getAllItemReferences(projectId)
      .then(res => {
        const refs = res.data?.success ? res.data.data || [] : [];
        setItemReferences(refs.map(r => ({
          ...r,
          itemId: r.itemId ?? r.ItemId,
          artworkId: r.artworkId ?? r.ArtworkId,
        })));
      })
      .catch(() => setItemReferences([]));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [show, projectId]);

  useEffect(() => {
    if (!items || items.length === 0) return;

    // Include any AI artwork that is referenced by another artwork
    const referencedArtworkIds = new Set();
    for (const ref of itemReferences) {
      if (ref.artworkId) referencedArtworkIds.add(String(ref.artworkId));
    }

    // Also include any AI artwork used as an opacity background by another artwork
    for (const item of items) {
      if (!item.opacityJson) continue;
      try {
        const parsed = JSON.parse(item.opacityJson);
        if (parsed?.background?.type === 'artwork' && parsed.background.id) {
          referencedArtworkIds.add(String(parsed.background.id));
        }
      } catch { /* ignore malformed opacity json */ }
    }

    const ai = items.filter(i =>
      i.artworkType !== 'custom' &&
      (blueprintItemIds.has(String(i.id)) || i.socialMedia || referencedArtworkIds.has(String(i.id)))
    );
    setAiItems(ai);
    setAiItemsLoaded(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items, blueprintItemIds, itemReferences]);

  useEffect(() => {
    if (!resumeStep) return;
    if (resumeStep === STEPS.SELECT_PRODUCTS) {
      setStep(STEPS.SELECT_PRODUCTS);
      setResumeStep(null);
      setInitialLoading(false);
      return;
    }
    if (resumeStep === STEPS.PROJECT_QUESTIONS) {
      setStep(STEPS.PROJECT_QUESTIONS);
      setResumeStep(null);
      setInitialLoading(false);
      return;
    }
    if (resumeStep === 'artwork_resume') {
      if (!aiItemsLoaded) return;
      setResumeStep(null);

      // No artwork yet — start at the first artwork item
      if (collectionArtwork.length === 0) {
        const firstBlueprintItemIndex = aiItems.findIndex(item =>
          blueprintItemIds.has(String(item.id))
        );
        if (firstBlueprintItemIndex !== -1) {
          setCurrentItemIndex(firstBlueprintItemIndex);
          loadItemData(firstBlueprintItemIndex);
        } else {
          setStep(STEPS.READY_TO_GENERATE);
        }
        setInitialLoading(false);
        return;
      }

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
            return !art || (art.accepted && art.fullSize);
          });

          // If blueprint placements changed (e.g. new products added with different placements), force regeneration
          const regenArtwork = collectionArtwork.filter(a => a.needsRegeneration);
          if (regenArtwork.length > 0) {
            const regenItemIds = new Set(regenArtwork.map(a => String(a.itemId)));
            const firstRegenIndex = aiItems.findIndex(item =>
              regenItemIds.has(String(item.id))
            );
            if (firstRegenIndex !== -1) {
              setCurrentItemIndex(firstRegenIndex);
              loadItemData(firstRegenIndex, true);
            } else {
              setStep(STEPS.ARTWORK_QUESTIONS);
            }
            setInitialLoading(false);
            return;
          }

          if (allFullSize || upscaleComplete) {
            (async () => {
              const colId = initialCollectionId || await ensureCollection();
              if (colId) {
                await loadImageModels();
                try {
                  const pbImgRes = await api.getAllProductBlueprintImages(projectId);
                  const rawPbImages = pbImgRes.data.success ? (pbImgRes.data.data || []) : [];
                  // Filter to only product images for active products
                  const activeBpIds = new Set(
                    (collectionProducts || []).filter(cp => cp.active).map(cp => cp.projectBlueprintId)
                  );
                  const allPbImages = activeBpIds.size > 0
                    ? rawPbImages.filter(pbi => activeBpIds.has(pbi.projectBlueprintId))
                    : rawPbImages;
                  setProductBlueprintImages(allPbImages);

                  const imgRes = await api.getProductImages(colId);
                  if (imgRes.data.success) {
                    const allImages = (imgRes.data.data || []).filter(img => img.active);
                    setAllProductImages(allImages);

                    const acceptedProductImageIds = new Set(
                      allImages.filter(img => img.accepted).map(img => img.productImageId)
                    );

                    const missing = allPbImages.filter(pbi => !acceptedProductImageIds.has(pbi.id));

                    const activeBpsForCheck = activeBpIds.size > 0
                      ? blueprints.filter(bp => activeBpIds.has(bp.id))
                      : blueprints;

                    // If all product images have been generated, skip to publish
                    if (allImages.length > 0 && allImages.every(img => img.generated)) {
                      try {
                        await api.ensurePrintifyProducts({ collectionId: colId });
                      } catch (e) { /* non-critical */ }

                      const createdBpIds = new Set(
                        printifyProducts.filter(pp => pp.printifyProductId && pp.projectBlueprintId && pp.mockupsDownloaded)
                          .map(pp => pp.projectBlueprintId)
                      );
                      const allProductsCreated = activeBpsForCheck.length > 0 && activeBpsForCheck.every(bp => createdBpIds.has(bp.id));

                      if (allProductsCreated) {
                        setStep(STEPS.PUBLISH_PRODUCTS);
                      } else {
                        setStep(STEPS.CREATE_PRODUCTS);
                      }
                      setInitialLoading(false);
                      return;
                    }

                    if (allPbImages.length === 0) {
                      const createdBpIds = new Set(
                        printifyProducts.filter(pp => pp.printifyProductId && pp.projectBlueprintId && pp.mockupsDownloaded)
                          .map(pp => pp.projectBlueprintId)
                      );
                      const allProductsCreated = activeBpsForCheck.length > 0 && activeBpsForCheck.every(bp => createdBpIds.has(bp.id));

                      if (allProductsCreated) {
                        setStep(STEPS.PUBLISH_PRODUCTS);
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
                      const allProductsCreated = activeBpsForCheck.length > 0 && activeBpsForCheck.every(bp => createdBpIds.has(bp.id));

                      if (allProductsCreated) {
                        setStep(STEPS.PUBLISH_PRODUCTS);
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
                    const allProductsCreated = activeBpsForCheck.length > 0 && activeBpsForCheck.every(bp => createdBpIds.has(bp.id));

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
                        variantIds: pbi.variantIds || [],
                        prompt: pbi.prompt || '',
                        printifyImageUrl,
                      };
                    });
                    setSelectedProductCombos(combos);
                    setCurrentProductComboIndex(0);
                    setProductImagePrompt(combos[0]?.prompt || '');
                    setStep(STEPS.GENERATE_PRODUCT_IMAGES);
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
                    setStep(STEPS.GENERATE_PRODUCT_IMAGES);
                  }
                }
                setInitialLoading(false);
              } else {
                setStep(STEPS.READY_TO_GENERATE);
              }
            })();
          } else {
            setStep(STEPS.READY_TO_GENERATE);
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
