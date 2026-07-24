import React, { useEffect, useState } from 'react';
import { CollectionProvider, useCollection, STEPS, WIZARD_STEPS, STEP_INDEX } from '@/context/collection';
import Modal from '@/components/ui/modal';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import Steps from '@/components/ui/steps';
import ArtworkPreviewModal from './ProductImagePreview';
import ProjectQuestions from './collection-steps/ProjectQuestions';
import ArtworkQuestions from './collection-steps/ArtworkQuestions';
import ArtworkPreview from './collection-steps/ArtworkPreview';
import ReadyToGenerate from './collection-steps/ReadyToGenerate';
import NextStep from './collection-steps/NextStep';
import ProductImagePrompt from './collection-steps/ProductImagePrompt';
import ProductImagePreview from './collection-steps/ProductImagePreview';
import ProductImageDone from './collection-steps/ProductImageDone';

const stepTitle = (step) => {
  switch (step) {
    case STEPS.PROJECT_QUESTIONS: return 'New Collection - Project Questions';
    case STEPS.ARTWORK_QUESTIONS: return 'New Collection - Artwork Questions';
    case STEPS.ARTWORK_PREVIEW: return 'New Collection - Artwork Preview';
    case STEPS.READY_TO_GENERATE: return 'New Collection - Ready to Upscale';
    case STEPS.PRODUCT_IMAGE_PROMPT: return 'New Collection - Product Images';
    case STEPS.PRODUCT_IMAGE_PREVIEW: return 'New Collection - Product Images';
    case STEPS.PRODUCT_IMAGE_DONE: return 'New Collection - Product Images';
    case STEPS.NEXT_STEP: return 'New Collection - Next Step';
    default: return 'New Collection';
  }
};

function CollectionWizard() {
  const {
    step, message, setMessage,
    initialLoading, artworkPreview, setArtworkPreview,
    onClose,
  } = useCollection();

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
          <Steps steps={WIZARD_STEPS} currentIndex={STEP_INDEX[step] ?? 0} />

          {step === STEPS.PROJECT_QUESTIONS && <ProjectQuestions />}
          {step === STEPS.ARTWORK_QUESTIONS && <ArtworkQuestions />}
          {step === STEPS.ARTWORK_PREVIEW && <ArtworkPreview />}
          {step === STEPS.READY_TO_GENERATE && <ReadyToGenerate />}
          {step === STEPS.PRODUCT_IMAGE_PROMPT && <ProductImagePrompt />}
          {step === STEPS.PRODUCT_IMAGE_PREVIEW && <ProductImagePreview />}
          {step === STEPS.PRODUCT_IMAGE_DONE && <ProductImageDone />}
          {step === STEPS.NEXT_STEP && <NextStep />}
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
      setResumeStep(null);

      const artworkItemIds = new Set(
        collectionArtwork.map(a => String(a.itemId))
      );

      const acceptedItemIds = new Set(
        collectionArtwork.filter(a => a.accepted).map(a => String(a.itemId))
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
          setStep(STEPS.READY_TO_GENERATE);
          fetchEstimate();
        }
      }
      setInitialLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aiItems, aiItemsLoaded, resumeStep, blueprintItemIds]);

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
