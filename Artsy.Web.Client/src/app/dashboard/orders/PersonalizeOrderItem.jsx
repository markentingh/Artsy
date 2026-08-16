import React, { useMemo } from 'react';
import Modal from '@/components/ui/modal';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import { PersonalizeOrderItemProvider, usePersonalizeOrderItem } from '@/context/personalizeOrderItem';

const formatCents = (cents) => (cents / 100).toFixed(2);

function StepHeader() {
  const { step, STEPS, setStep, artworks } = usePersonalizeOrderItem();
  const steps = [
    { key: STEPS.GENERATE, label: 'Generate Personalized Artworks' },
    { key: STEPS.DOWNLOAD, label: 'Download Artworks' },
  ];

  return (
    <div className="flex items-center gap-2 mb-4">
      {steps.map((s, idx) => (
        <React.Fragment key={s.key}>
          {idx > 0 && <span className="text-gray-400">→</span>}
          <button
            type="button"
            onClick={() => s.key === STEPS.DOWNLOAD && artworks.length > 0 ? setStep(s.key) : setStep(STEPS.GENERATE)}
            className={`text-sm px-3 py-1 rounded border ${
              step === s.key
                ? 'bg-blue-600 text-white border-blue-600'
                : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 border-gray-300 dark:border-gray-600'
            }`}
          >
            {s.label}
          </button>
        </React.Fragment>
      ))}
    </div>
  );
}

function Checklist({ productImages = [] }) {
  const { currentArtworkIndex, artworks } = usePersonalizeOrderItem();

  const items = useMemo(() => {
    const list = productImages.map((_, i) => ({ label: `Artwork ${i + 1}`, index: i }));
    return list;
  }, [productImages]);

  return (
    <div className="w-56 shrink-0 pr-4 border-r border-gray-200 dark:border-gray-700">
      <h4 className="font-semibold mb-2 text-sm">Setup Checklist</h4>
      <ul className="space-y-2 text-sm">
        <li className="font-medium">Generate Personalized Artworks</li>
        {items.map((it) => (
          <li
            key={it.index}
            className={`pl-4 ${currentArtworkIndex === it.index ? 'text-blue-600 dark:text-blue-400' : 'text-gray-500 dark:text-gray-400'}`}
          >
            {it.label}
          </li>
        ))}
        <li className="text-gray-500 dark:text-gray-400">Download Artworks</li>
      </ul>
    </div>
  );
}

function GenerateStep({ productImages = [] }) {
  const {
    order,
    orderItem,
    requestText,
    setRequestText,
    imageModel,
    setImageModel,
    generating,
    artworks,
    currentArtworkIndex,
    setCurrentArtworkIndex,
    setStep,
    addArtwork,
  } = usePersonalizeOrderItem();

  const currentArtwork = artworks[currentArtworkIndex];
  const productId = orderItem?.productId;
  const shopId = order?.order?.printifyShopId;
  const printifyOrderId = order?.order?.orderId;

  const imageModels = [
    { id: 'dall-e-3', name: 'DALL·E 3', cost: 4000 },
    { id: 'sd-xl', name: 'Stable Diffusion XL', cost: 2000 },
  ];

  const handleGenerate = () => {
    // TODO: call generate API
    setGenerating(true);
    setTimeout(() => {
      setGenerating(false);
      addArtwork({ id: `artwork-${Date.now()}`, url: '', status: 'done' });
    }, 500);
  };

  const handleNext = () => {
    if (currentArtworkIndex < productImages.length - 1) {
      setCurrentArtworkIndex(currentArtworkIndex + 1);
    } else {
      setStep(1);
    }
  };

  return (
    <div className="space-y-4 flex-1">
      {shopId && printifyOrderId && (
        <a
          href={`https://printify.com/app/store/${shopId}/order/${printifyOrderId}`}
          target="_blank"
          rel="noopener noreferrer"
          className="text-blue-600 dark:text-blue-400 hover:underline text-sm"
        >
          View Order on Printify
        </a>
      )}

      <div className="w-full h-64 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center overflow-hidden">
        {productImages.length > 0 ? (
          <img src={productImages[0]} alt="Product" className="h-full w-full object-contain" />
        ) : (
          <span className="text-gray-500 dark:text-gray-400 text-sm">No product images</span>
        )}
      </div>

      <div className="flex items-center gap-4">
        <label className="text-sm font-medium whitespace-nowrap">Image Model</label>
        <select
          value={imageModel}
          onChange={(e) => setImageModel(e.target.value)}
          className="border rounded px-2 py-1 text-sm dark:bg-gray-800 dark:border-gray-600"
        >
          <option value="">Select model</option>
          {imageModels.map((m) => (
            <option key={m.id} value={m.id}>
              {m.name} ({m.cost} tokens)
            </option>
          ))}
        </select>
      </div>

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

      {generating ? (
        <div className="w-full h-48 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center">
          <Icon name="progress_activity" spin className="w-8 h-8 text-gray-500" />
        </div>
      ) : currentArtwork ? (
        <div className="w-full h-64 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center">
          <img src={currentArtwork.url || ''} alt="Generated artwork" className="h-full w-full object-contain" />
        </div>
      ) : null}

      <div className="flex justify-end gap-2">
        {!currentArtwork ? (
          <ButtonOutline onClick={handleGenerate} disabled={!imageModel || !requestText}>
            Generate Artwork
          </ButtonOutline>
        ) : (
          <ButtonOutline onClick={handleNext}>
            {currentArtworkIndex < productImages.length - 1 ? 'Next' : 'Finish'}
          </ButtonOutline>
        )}
      </div>
    </div>
  );
}

function DownloadStep() {
  const { order, artworks, onClose } = usePersonalizeOrderItem();
  const shopId = order?.order?.printifyShopId;
  const orderId = order?.order?.orderId;

  return (
    <div className="space-y-4 flex-1">
      <div className="w-full grid grid-cols-2 sm:grid-cols-3 gap-2">
        {artworks.map((a) => (
          <div key={a.id} className="h-40 bg-gray-100 dark:bg-gray-700 rounded flex items-center justify-center">
            {a.url ? (
              <img src={a.url} alt="Artwork" className="h-full w-full object-contain" />
            ) : (
              <span className="text-xs text-gray-500">placeholder</span>
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

function PersonalizeOrderItemInner({ productImages }) {
  const { step, STEPS, onClose } = usePersonalizeOrderItem();

  return (
    <Modal title="Personalize Order Item" onClose={onClose} className="max-w-5xl">
      <div className="flex min-h-[400px]">
        <Checklist productImages={productImages} />
        <div className="flex-1 pl-4">
          <StepHeader />
          {step === STEPS.GENERATE ? <GenerateStep productImages={productImages} /> : <DownloadStep />}
        </div>
      </div>
    </Modal>
  );
}

export default function PersonalizeOrderItem({ order, orderItem, productImages = [], onClose }) {
  return (
    <PersonalizeOrderItemProvider
      order={order}
      orderItem={orderItem}
      onClose={onClose}
    >
      <PersonalizeOrderItemInner productImages={productImages} />
    </PersonalizeOrderItemProvider>
  );
}
