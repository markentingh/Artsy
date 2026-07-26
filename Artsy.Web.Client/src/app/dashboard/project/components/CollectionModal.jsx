import React, { useEffect, useState } from 'react';
import { CollectionProvider, useCollection, STEPS } from '@/context/collection';
import Modal from '@/components/ui/modal';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import Steps from '@/components/ui/steps';
import ArtworkPreviewModal from './ProductImagePreview';
import ProjectQuestions from './collection-steps/ProjectQuestions';
import ArtworkQuestions from './collection-steps/ArtworkQuestions';
import ArtworkPreview from './collection-steps/ArtworkPreview';
import ReadyToGenerate from './collection-steps/ReadyToGenerate';
import PublishProducts from './collection-steps/PublishProducts';
import ProductImageSelection from './collection-steps/ProductImageSelection';
import ProductImagePrompt from './collection-steps/ProductImagePrompt';
import ProductImagePreview from './collection-steps/ProductImagePreview';
import CollectionSetupList from './CollectionSetupList';

const stepTitle = (step) => {
  switch (step) {
    case STEPS.PROJECT_QUESTIONS: return 'New Collection - Project Questions';
    case STEPS.ARTWORK_QUESTIONS: return 'New Collection - Artwork Questions';
    case STEPS.ARTWORK_PREVIEW: return 'New Collection - Artwork Preview';
    case STEPS.READY_TO_GENERATE: return 'New Collection - Ready to Upscale';
    case STEPS.PRODUCT_IMAGE_SELECTION: return 'New Collection - Product Image Selection';
    case STEPS.PRODUCT_IMAGE_PROMPT: return 'New Collection - Product Image Prompt';
    case STEPS.PRODUCT_IMAGE_PREVIEW: return 'New Collection - Product Images';
    case STEPS.PUBLISH: return 'New Collection - Publish';
    case STEPS.SOCIAL_MEDIA: return 'New Collection - Social Media';
    default: return 'New Collection';
  }
};

function CollectionWizard() {
  const {
    step, message, setMessage,
    initialLoading, artworkPreview, setArtworkPreview,
    onClose, STEPS, wizardSteps, stepIndex,
  } = useCollection();

  console.log('[CollectionWizard] wizardSteps:', wizardSteps, 'stepIndex:', stepIndex, 'step:', step);

  return (
    <Modal
      title={stepTitle(step)}
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
          <Steps steps={wizardSteps} currentIndex={stepIndex[step] ?? 0} />
          <div className="px-2">
            <CollectionSetupList />
          </div>
          {step === STEPS.PROJECT_QUESTIONS && <ProjectQuestions />}
          {step === STEPS.ARTWORK_QUESTIONS && <ArtworkQuestions />}
          {step === STEPS.ARTWORK_PREVIEW && <ArtworkPreview />}
          {step === STEPS.READY_TO_GENERATE && <ReadyToGenerate />}
          {step === STEPS.PRODUCT_IMAGE_SELECTION && <ProductImageSelection />}
          {step === STEPS.PRODUCT_IMAGE_PROMPT && <ProductImagePrompt />}
          {step === STEPS.PRODUCT_IMAGE_PREVIEW && <ProductImagePreview />}
          {step === STEPS.PUBLISH && <PublishProducts />}
          {step === STEPS.SOCIAL_MEDIA && <PublishProducts />}
        </>
      )}

      <ArtworkPreviewModal
        show={!!artworkPreview}
        images={artworkPreview?.images || []}
        alt="Artwork Preview"
        defaultIndex={artworkPreview ? artworkPreview.images.indexOf(artworkPreview.src) : 0}
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
    loadProductImageVariants, loadImageModels, ensureCollection,
    api, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex,
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
    const ai = items.filter(i => blueprintItemIds.has(String(i.id)));
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
          blueprintItemIds.has(String(item.id)) &&
          unacceptedArtworkItemIds.includes(String(item.id))
        );

        if (firstUnacceptedIndex !== -1) {
          setCurrentItemIndex(firstUnacceptedIndex);
          loadItemData(firstUnacceptedIndex);
        } else {
          const allFullSize = collectionArtwork.length > 0 &&
            collectionArtwork.filter(a => a.accepted).every(a => a.fullSize);
          if (allFullSize) {
            (async () => {
              const colId = initialCollectionId || await ensureCollection();
              if (colId) {
                const [variants,] = await Promise.all([loadProductImageVariants(colId), loadImageModels()]);
                try {
                  const imgRes = await api.getProductImages(colId);
                  console.log('[ResumeManager] getProductImages response:', imgRes.data);
                  if (imgRes.data.success) {
                    const allImages = (imgRes.data.data || []).filter(img => img.active);
                    const accepted = allImages.filter(img => img.accepted);
                    const acceptedKeys = new Set(accepted.map(img => `${img.projectBlueprintId}:${img.variant}:${img.placement}`));
                    console.log('[ResumeManager] existing:', allImages.length, 'accepted:', accepted.length, accepted);

                    const activeKeys = new Set(allImages.map(img => `${img.projectBlueprintId}:${img.variant}:${img.placement}`));

                    const allCombos = [];
                    for (const bp of variants) {
                      for (const v of (bp.variants || [])) {
                        for (const c of (v.combos || [])) {
                          if (c.hasArtwork) {
                            const key = `${bp.projectBlueprintId}:${v.variant}:${c.placementIndex}`;
                            if (activeKeys.has(key)) {
                              allCombos.push({
                                projectBlueprintId: bp.projectBlueprintId,
                                blueprintName: bp.blueprintName,
                                variant: v.variant,
                                variantTitle: v.variantTitle,
                                placement: c.placementIndex,
                                placementName: c.placementName,
                                tokens: c.tokens,
                              });
                            }
                          }
                        }
                      }
                    }

                    const missingCombos = allCombos.filter(c => !acceptedKeys.has(`${c.projectBlueprintId}:${c.variant}:${c.placement}`));
                    console.log('[ResumeManager] allCombos:', allCombos.length, 'missing:', missingCombos.length);

                    if (allCombos.length === 0) {
                      setAllProductImages(allImages);
                      setStep(STEPS.PRODUCT_IMAGE_SELECTION);
                      setInitialLoading(false);
                      return;
                    }

                    if (missingCombos.length === 0) {
                      setAllProductImages(allImages);
                      setStep(STEPS.PUBLISH);
                      setInitialLoading(false);
                      return;
                    }

                    if (missingCombos.length < allCombos.length) {
                      setSelectedProductCombos(missingCombos);
                      setCurrentProductComboIndex(0);
                      setAllProductImages(allImages);
                      setStep(STEPS.PRODUCT_IMAGE_PROMPT);
                      setInitialLoading(false);
                      return;
                    }
                  }
                } catch (e) { console.log('[ResumeManager] getProductImages error:', e); }
                console.log('[ResumeManager] falling through to PRODUCT_IMAGE_SELECTION');
                setStep(STEPS.PRODUCT_IMAGE_SELECTION);
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

export default function CollectionModal({ show, projectId, project, collectionId: initialCollectionId, onClose, onSaved }) {
  if (!show) return null;

  return (
    <CollectionProvider
      projectId={projectId}
      project={project}
      collectionId={initialCollectionId}
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
