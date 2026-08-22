import React, { useState, useMemo, useEffect, useCallback } from 'react';
import Modal from '@/components/ui/modal';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import Tooltip from '@/components/ui/tooltip';
import ButtonOutline from '@/components/ui/button-outline';
import Steps from '@/components/ui/steps';
import Select from '@/components/forms/select';
import Carousel from '@/components/ui/carousel';
import TextArea from '@/components/forms/textarea';
import { PersonalizeOrderItemProvider, usePersonalizeOrderItem } from '@/context/personalizeOrderItem';
import PersonalizeSetupList from './PersonalizeSetupList';

function cacheBustUrl(url) {
  if (!url) return url;
  const sep = url.includes('?') ? '&' : '?';
  return `${url}${sep}r=${Math.floor(Math.random() * 100000)}`;
}

function ArtworkCarousel({ images, defaultIndex = 0 }) {
  if (!images.length) {
    return (
      <div className="w-full h-64 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center text-gray-500 dark:text-gray-400 text-sm">
        No artwork selected for this order item
      </div>
    );
  }

  return (
    <div className="w-full h-64">
      <Carousel images={images} singleImage imageClassName="h-64" defaultIndex={defaultIndex} alt="Reference artwork" />
    </div>
  );
}

function Chevron({ showChecklist, setShowChecklist }) {
  return (
    <div className="flex items-center gap-2 mb-4">
      <hr className="flex-1 border-gray-200 dark:border-gray-700" />
      <button
        onClick={() => setShowChecklist((prev) => !prev)}
        className="rounded-full p-3 pb-2 transition-colors hover:bg-gray-100 dark:hover:bg-gray-700"
        title={showChecklist ? 'Hide' : 'Show'}
      >
        <Icon
          name="expand_more"
          className="text-lg leading-none text-gray-500 dark:text-gray-400 transition-transform duration-200"
          style={{ display: 'block', transform: showChecklist ? 'rotate(180deg) translateY(4px)' : 'translateY(-2px)' }}
        />
      </button>
      <hr className="flex-1 border-gray-200 dark:border-gray-700" />
    </div>
  );
}

function QuestionsStep() {
  const {
    projectQuestions,
    answers,
    setAnswers,
    saveAnswers,
    loadingQuestions,
    savingAnswers,
    setStep,
    STEPS,
    onClose,
  } = usePersonalizeOrderItem();

  const handleChange = useCallback((questionId, value) => {
    setAnswers((prev) => ({ ...prev, [questionId]: value }));
  }, [setAnswers]);

  const handleNext = useCallback(async () => {
    const saved = await saveAnswers();
    if (saved) setStep(STEPS.GENERATE);
  }, [saveAnswers, setStep, STEPS]);

  if (loadingQuestions) {
    return (
      <div className="flex items-center justify-center py-12">
        <Spinner className="text-4xl" />
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full space-y-4">
      <div className="max-h-[50vh] overflow-y-auto">
        {projectQuestions.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No project questions.</p>
        ) : (
          <div className="space-y-4">
            {projectQuestions.map((question) => (
              <TextArea
                key={question.id}
                name={`answer-${question.id}`}
                label={question.question}
                value={answers[question.id] || ''}
                onChange={(e) => handleChange(question.id, e.target.value)}
                placeholder="Enter an answer"
                rows={3}
                maxLength={255}
              />
            ))}
          </div>
        )}
      </div>
      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext} disabled={savingAnswers}>
          {savingAnswers ? 'Saving...' : 'Next'}
        </ButtonOutline>
      </div>
    </div>
  );
}

function GenerateStep() {
  const {
    order,
    orderItem,
    usedArtworks,
    personalizeApi,
    requestText,
    setRequestText,
    imageModels,
    selectedImageModel,
    setSelectedImageModel,
    generating,
    setGenerating,
    artworks,
    currentArtworkIndex,
    setCurrentArtworkIndex,
    setStep,
    setArtworks,
    STEPS,
    onClose,
    goBack,
  } = usePersonalizeOrderItem();

  const currentArtwork = artworks[currentArtworkIndex];
  const currentUsedArtwork = usedArtworks[currentArtworkIndex] || null;
  const shopId = order?.order?.printifyShopId;
  const printifyOrderId = order?.order?.orderId;

  const [view, setView] = useState(currentArtwork ? 'preview' : 'form');
  const [requestedChanges, setRequestedChanges] = useState('');

  useEffect(() => {
    setView(currentArtwork ? 'preview' : 'form');
    setRequestedChanges('');
  }, [currentArtworkIndex, currentArtwork]);

  const modelOptions = useMemo(() => imageModels.map((m) => ({ value: m.id, label: m.name })), [imageModels]);
  const [tokenCost, setTokenCost] = useState(null);

  useEffect(() => {
    if (!selectedImageModel?.id || !currentUsedArtwork?.artworkItemId || !order?.order?.id || !orderItem?.id) {
      setTokenCost(null);
      return;
    }
    let cancelled = false;
    personalizeApi.estimateOrderItemToken(order.order.id, orderItem.id, currentUsedArtwork.artworkItemId, selectedImageModel.id)
      .then((res) => {
        if (cancelled) return;
        const cost = res.data?.data;
        setTokenCost(typeof cost === 'number' ? Math.round(cost) : null);
      })
      .catch(() => { if (!cancelled) setTokenCost(null); });
    return () => { cancelled = true; };
  }, [selectedImageModel, currentUsedArtwork, order, orderItem, personalizeApi]);

  const handleGenerate = async (changes = '') => {
    if (!selectedImageModel || !currentUsedArtwork || (!requestText.trim() && !changes.trim()) || !order?.order?.id || !orderItem?.id) return;
    setGenerating(true);
    try {
      const generationText = [requestText, changes].filter(Boolean).join('\n');
      const res = await personalizeApi.generateOrderItemArtwork(order.order.id, orderItem.id, currentUsedArtwork.artworkItemId, selectedImageModel.id, generationText);
      if (res.data?.success) {
        const artworksList = res.data.data.artworks || [res.data.data.artwork];
        setArtworks((prev) => {
          const next = [...prev];
          next[currentArtworkIndex] = {
            id: artworksList[0].id,
            url: artworksList[0].url,
            prompt: artworksList[0].prompt,
            width: artworksList[0].width,
            height: artworksList[0].height,
            variants: artworksList,
            status: 'done',
          };
          return next;
        });
        setView('preview');
      }
    } finally {
      setGenerating(false);
    }
  };

  const handleTryAgain = useCallback(() => {
    setArtworks((prev) => {
      const next = [...prev];
      next[currentArtworkIndex] = undefined;
      return next;
    });
    setView('form');
  }, [currentArtworkIndex, setArtworks]);

  const handleMakeChanges = useCallback(() => {
    setView('changes');
  }, []);

  const handleSubmitChanges = useCallback(() => {
    if (!requestedChanges.trim()) return;
    handleGenerate(requestedChanges);
  }, [requestedChanges, handleGenerate]);

  const handleAccept = useCallback(async () => {
    if (currentArtwork?.id && order?.order?.id && orderItem?.id) {
      try {
        // Accept all variant artworks for this item
        const variants = currentArtwork.variants || [currentArtwork];
        for (const v of variants) {
          const res = await personalizeApi.acceptOrderItemArtwork(order.order.id, orderItem.id, v.id);
          if (!res.data?.success) return;
        }
        setArtworks((prev) => {
          const next = [...prev];
          next[currentArtworkIndex] = { ...next[currentArtworkIndex], status: 'accepted' };
          return next;
        });
      } catch {
        return;
      }
    }
    if (currentArtworkIndex < (usedArtworks.length || 1) - 1) {
      setCurrentArtworkIndex(currentArtworkIndex + 1);
    } else {
      setStep(STEPS.DOWNLOAD);
    }
  }, [currentArtwork, currentArtworkIndex, order, orderItem, personalizeApi, setArtworks, usedArtworks.length, setCurrentArtworkIndex, setStep, STEPS]);

  const currentImages = useMemo(() => {
    const generated = currentArtwork?.url;
    const source = currentUsedArtwork?.sourceImageUrl;
    return [generated, source].filter(Boolean);
  }, [currentArtwork, currentUsedArtwork]);

  const title = currentUsedArtwork
    ? `Artwork ${currentArtworkIndex + 1} of ${usedArtworks.length}: ${currentUsedArtwork.artworkItemTitle || 'Untitled'}`
    : 'Generate Personalized Artwork';

  return (
    <div className="flex flex-col h-full">
      <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">
        {title}
      </h3>

      {view === 'form' && !generating && (
        <div className="space-y-4 flex-1">
          {shopId && printifyOrderId && (
            <a
              href={`https://printify.com/app/store/${shopId}/order/${printifyOrderId}`}
              target="_blank"
              rel="noopener noreferrer"
              className="text-blue-600 dark:text-blue-400 hover:underline text-sm block"
            >
              View Order on Printify
            </a>
          )}

          <ArtworkCarousel images={currentImages} />

          <Select
            label="AI Image Model"
            name="personalizeImageModel"
            value={selectedImageModel?.id || ''}
            onChange={(value) => {
              const model = imageModels.find((m) => m.id === value);
              setSelectedImageModel(model || null);
            }}
            options={modelOptions}
            fitContent
            note={tokenCost != null ? `Estimated token cost: ${tokenCost.toLocaleString()}` : selectedImageModel ? 'Token cost not available' : ''}
          />

          <TextArea
            label="Copy/Paste Customer Personalization Request"
            name="requestText"
            value={requestText}
            onChange={(e) => setRequestText(e.target.value)}
            rows={4}
            placeholder="Enter the customer's request..."
          />
        </div>
      )}

      {(view === 'preview' || view === 'changes') && !generating && (
        <div className="space-y-4 flex-1">
          {view === 'preview' && (
            <div className="flex flex-col items-center gap-2">
              {currentArtwork ? (
                <>
                  <div className="w-full max-w-[512px] flex items-center justify-center rounded-lg bg-gray-100 dark:bg-gray-700 overflow-hidden mx-auto">
                    <img
                      src={cacheBustUrl(currentArtwork.url)}
                      alt="Generated artwork"
                      className="!max-w-[512px] !max-h-[512px] object-contain cursor-pointer"
                    />
                  </div>
                  {currentArtwork.variants && currentArtwork.variants.length > 1 && (
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      {currentArtwork.variants.length} variants generated for different product placements
                    </p>
                  )}
                </>
              ) : (
                <span className="text-sm text-gray-500 dark:text-gray-400">No preview generated yet.</span>
              )}
            </div>
          )}

          {view === 'changes' && (
            <TextArea
              name="requestedChanges"
              label="Requested Changes"
              value={requestedChanges}
              onChange={(e) => setRequestedChanges(e.target.value)}
              placeholder="Describe the changes you want..."
              rows={4}
            />
          )}
        </div>
      )}

      {generating && (
        <div className="w-full h-64 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center flex-1">
          <Spinner className="text-4xl" />
        </div>
      )}

      <div className="buttons flex justify-end gap-2 items-center pt-4 mt-auto">
        {view === 'form' && !generating && (
          <>
            <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
            <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
            <ButtonOutline onClick={() => handleGenerate('')} disabled={!selectedImageModel || !requestText || !currentUsedArtwork}>
              Generate Artwork
            </ButtonOutline>
          </>
        )}

        {view === 'preview' && !generating && currentArtwork && (
          <>
            <Tooltip text="Either make changes to the generated artwork using a prompt to edit the artwork, accept the currently generated artwork, or try again by changing the original prompt text." className="pr-8" />
            <ButtonOutline color="gray" onClick={handleMakeChanges}>Make Changes</ButtonOutline>
            <ButtonOutline onClick={handleAccept}>Accept</ButtonOutline>
            <ButtonOutline color="red" onClick={handleTryAgain}>Try Again</ButtonOutline>
            <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
            <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
          </>
        )}

        {view === 'changes' && !generating && (
          <>
            <ButtonOutline color="gray" onClick={() => setView('preview')}>Back</ButtonOutline>
            <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
            <ButtonOutline onClick={handleSubmitChanges} disabled={!requestedChanges.trim()}>
              Regenerate
            </ButtonOutline>
          </>
        )}
      </div>
    </div>
  );
}

function DownloadStep() {
  const { order, orderItem, artworks, onClose, goBack, personalizeApi } = usePersonalizeOrderItem();

  const downloadUrl = order?.order?.id && orderItem?.id
    ? personalizeApi.downloadOrderItemArtworks(order.order.id, orderItem.id)
    : null;
  const printifyUrl = order?.order?.printifyShopId && order?.order?.orderId
    ? `https://printify.com/app/store/${order.order.printifyShopId}/order/${order.order.orderId}`
    : null;
  const imageUrls = artworks.filter(Boolean).flatMap((a) => {
    if (a.variants && a.variants.length > 0) {
      return a.variants.map((v) => cacheBustUrl(v.url));
    }
    return [cacheBustUrl(a.url)];
  });

  return (
    <div className="flex flex-col h-full space-y-4">
      {printifyUrl && (
        <div className="flex justify-end">
          <a href={printifyUrl} target="_blank" rel="noreferrer" className="text-sm text-blue-600 dark:text-blue-400 underline">View Order on Printify</a>
        </div>
      )}
      <div className="w-full flex items-center justify-center">
        {imageUrls.length > 0 ? (
          <div className="w-full">
            <Carousel images={imageUrls} alt="Artwork" />
          </div>
        ) : (
          <span className="text-sm text-gray-500 dark:text-gray-400">No artworks available.</span>
        )}
      </div>
      <p className="text-sm text-gray-600 dark:text-gray-300 max-w-[500px]">
        Download the personalized artwork and apply it to your order item on Printify by clicking the Review button for the order item and uploading the artwork.
      </p>
      <div className="buttons flex justify-between gap-2 mt-auto">
        <div className="flex gap-2">
          {downloadUrl && (
            <ButtonOutline onClick={() => window.location.href = downloadUrl}>Download Images</ButtonOutline>
          )}
        </div>
        <div className="flex gap-2">
          <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
          <ButtonOutline color="gray" className="cancel" onClick={onClose}>Close</ButtonOutline>
        </div>
      </div>
    </div>
  );
}

function PersonalizeOrderItemInner() {
  const { step, STEPS, currentArtworkIndex, setCurrentArtworkIndex, usedArtworks, loadingPlacements, loadingQuestions, onClose } = usePersonalizeOrderItem();
  const [showChecklist, setShowChecklist] = useState(true);

  const currentIndex = step === STEPS.QUESTIONS
    ? 0
    : step === STEPS.DOWNLOAD
      ? usedArtworks.length + 1
      : currentArtworkIndex + 1;
  const stepLabels = usedArtworks.map((a, i) => a.artworkItemTitle || a.artworkPrompt || a.artworkImageModel || `Artwork ${i + 1}`);
  const steps = ['Project Questions', ...stepLabels, 'Download'];

  const handleStepClick = (index) => {
    if (index === 0) {
      setStep(STEPS.QUESTIONS);
      setCurrentArtworkIndex(0);
    } else if (index <= usedArtworks.length) {
      setStep(STEPS.GENERATE);
      setCurrentArtworkIndex(index - 1);
    } else {
      setStep(STEPS.DOWNLOAD);
    }
  };

  const showStepper = !loadingPlacements && !loadingQuestions;

  return (
    <Modal title="Personalize Order Item" onClose={onClose} className="min-w-[40em] max-w-full" top>
      {loadingPlacements || loadingQuestions ? (
        <div className="flex items-center justify-center py-12">
          <Spinner className="text-4xl" />
        </div>
      ) : (
        <>
          {showStepper && (
            <Steps
              steps={steps}
              currentIndex={currentIndex}
              onStepClick={handleStepClick}
            />
          )}
          <Chevron showChecklist={showChecklist} setShowChecklist={setShowChecklist} />
          <div className={showChecklist ? 'flex gap-4 items-stretch overflow-x-hidden' : ''}>
            {showChecklist && (
              <div className="min-w-[280px] w-fit max-w-[45%] shrink-0 overflow-y-auto overflow-x-hidden max-h-[60vh]">
                <PersonalizeSetupList />
              </div>
            )}
            <div className={showChecklist ? 'flex-1 min-w-[500px] flex flex-col' : ''}>
              {step === STEPS.QUESTIONS && <QuestionsStep />}
              {step === STEPS.GENERATE && <GenerateStep />}
              {step === STEPS.DOWNLOAD && <DownloadStep />}
            </div>
          </div>
        </>
      )}
    </Modal>
  );
}

export default function PersonalizeOrderItem({ order, orderItem, onClose }) {
  return (
    <PersonalizeOrderItemProvider
      order={order}
      orderItem={orderItem}
      onClose={onClose}
    >
      <PersonalizeOrderItemInner />
    </PersonalizeOrderItemProvider>
  );
}
