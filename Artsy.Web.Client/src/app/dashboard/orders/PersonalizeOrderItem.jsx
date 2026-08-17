import React, { useState, useMemo, useEffect } from 'react';
import Modal from '@/components/ui/modal';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import ButtonOutline from '@/components/ui/button-outline';
import Steps from '@/components/ui/steps';
import Select from '@/components/forms/select';
import Carousel from '@/components/ui/carousel';
import { PersonalizeOrderItemProvider, usePersonalizeOrderItem } from '@/context/personalizeOrderItem';
import PersonalizeSetupList from './PersonalizeSetupList';

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

function GenerateStep() {
  const {
    order,
    orderItem,
    usedArtworks,
    ordersApi,
    requestText,
    setRequestText,
    imageModels,
    selectedImageModel,
    setSelectedImageModel,
    generating,
    artworks,
    currentArtworkIndex,
    setCurrentArtworkIndex,
    setStep,
    addArtwork,
    STEPS,
  } = usePersonalizeOrderItem();

  const currentArtwork = artworks[currentArtworkIndex];
  const currentUsedArtwork = usedArtworks[currentArtworkIndex] || null;
  const currentPlacements = currentUsedArtwork?.placements || [];
  const shopId = order?.order?.printifyShopId;
  const printifyOrderId = order?.order?.orderId;

  const modelOptions = useMemo(() => imageModels.map((m) => ({ value: m.id, label: m.name })), [imageModels]);
  const [tokenCost, setTokenCost] = useState(null);

  useEffect(() => {
    if (!selectedImageModel?.id || !currentUsedArtwork?.artworkItemId || !order?.order?.id || !orderItem?.id) {
      setTokenCost(null);
      return;
    }
    let cancelled = false;
    ordersApi.estimateOrderItemToken(order.order.id, orderItem.id, currentUsedArtwork.artworkItemId, selectedImageModel.id)
      .then((res) => {
        if (cancelled) return;
        const cost = res.data?.data;
        setTokenCost(typeof cost === 'number' ? Math.round(cost) : null);
      })
      .catch(() => { if (!cancelled) setTokenCost(null); });
    return () => { cancelled = true; };
  }, [selectedImageModel, currentUsedArtwork, order, orderItem, ordersApi]);

  const handleGenerate = () => {
    setGenerating(true);
    setTimeout(() => {
      setGenerating(false);
      addArtwork({ id: `artwork-${Date.now()}`, url: currentUsedArtwork?.sourceImageUrl, status: 'done' });
    }, 500);
  };

  const handleNext = () => {
    if (currentArtworkIndex < (usedArtworks.length || 1) - 1) {
      setCurrentArtworkIndex(currentArtworkIndex + 1);
      setStep(STEPS.GENERATE);
    } else {
      setStep(STEPS.DOWNLOAD);
    }
  };

  const currentImages = useMemo(() => {
    const generated = currentArtwork?.url;
    const source = currentUsedArtwork?.sourceImageUrl;
    return [generated, source].filter(Boolean);
  }, [currentArtwork, currentUsedArtwork]);

  return (
    <div className="space-y-4">
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

      <div>
        <label className="text-sm font-medium block mb-1">Copy/Paste Customer Personalization Request</label>
        <textarea
          value={requestText}
          onChange={(e) => setRequestText(e.target.value)}
          rows={4}
          className="w-full border rounded p-2 text-sm dark:bg-gray-800 dark:border-gray-600"
          placeholder="Enter the customer's request..."
        />
      </div>

      {generating && (
        <div className="w-full h-48 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center">
          <Spinner className="text-4xl" />
        </div>
      )}

      {currentArtwork && !generating && (
        <div className="w-full h-48 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center">
          <img src={currentArtwork.url || ''} alt="Generated artwork" className="h-full w-full object-contain" />
        </div>
      )}

      <div className="flex justify-end gap-2">
        {!currentArtwork ? (
          <ButtonOutline onClick={handleGenerate} disabled={!selectedImageModel || !requestText || !currentPlacement}>
            Generate Artwork
          </ButtonOutline>
        ) : (
          <ButtonOutline onClick={handleNext}>
            {currentArtworkIndex < (placements.length || 1) - 1 ? 'Next' : 'Finish'}
          </ButtonOutline>
        )}
      </div>
    </div>
  );
}

function DownloadStep() {
  const { order, artworks, onClose } = usePersonalizeOrderItem();

  return (
    <div className="space-y-4">
      <div className="w-full grid grid-cols-2 sm:grid-cols-3 gap-2">
        {artworks.map((a, i) => (
          <div key={i} className="h-40 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center">
            {a.url ? (
              <img src={a.url} alt="Artwork" className="h-full w-full object-contain" />
            ) : (
              <span className="text-xs text-gray-500">Artwork {i + 1}</span>
            )}
          </div>
        ))}
      </div>
      <p className="text-sm text-gray-600 dark:text-gray-300">
        Download the personalized artwork and apply it to your order item on Printify by clicking the Review button for the order item and uploading the artwork.
      </p>
      <div className="flex justify-end gap-2">
        <ButtonOutline onClick={onClose}>Close</ButtonOutline>
      </div>
    </div>
  );
}

function PersonalizeOrderItemInner() {
  const { step, STEPS, currentArtworkIndex, setCurrentArtworkIndex, setStep, usedArtworks, loadingPlacements } = usePersonalizeOrderItem();
  const [showChecklist, setShowChecklist] = useState(true);

  const currentIndex = step === STEPS.GENERATE ? currentArtworkIndex : usedArtworks.length;
  const totalArtworks = usedArtworks.length || 1;
  const stepLabels = usedArtworks.map((a, i) => a.artworkItemTitle || a.artworkPrompt || a.artworkImageModel || `Artwork ${i + 1}`);
  const steps = [...stepLabels, 'Download'];

  const handleStepClick = (index) => {
    if (index < usedArtworks.length) {
      setCurrentArtworkIndex(index);
      setStep(STEPS.GENERATE);
    } else {
      setStep(STEPS.DOWNLOAD);
    }
  };

  return (
    <Modal title="Personalize Order Item" onClose={() => {}} className="min-w-[40em] max-w-full" top>
      {loadingPlacements ? (
        <div className="flex items-center justify-center py-12">
          <Spinner className="text-4xl" />
        </div>
      ) : (
        <>
          <Steps
            steps={steps}
            currentIndex={currentIndex}
            onStepClick={handleStepClick}
          />
          <Chevron showChecklist={showChecklist} setShowChecklist={setShowChecklist} />
          <div className={showChecklist ? 'flex gap-4 items-stretch overflow-x-hidden' : ''}>
            {showChecklist && (
              <div className="min-w-[280px] w-fit max-w-[45%] shrink-0 overflow-y-auto overflow-x-hidden max-h-[60vh]">
                <PersonalizeSetupList />
              </div>
            )}
            <div className={showChecklist ? 'flex-1 min-w-[500px] flex flex-col' : ''}>
              {step === STEPS.GENERATE ? <GenerateStep /> : <DownloadStep />}
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
