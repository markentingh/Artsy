import React from 'react';
import Modal from '@/components/ui/modal';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import Tabs from '@/components/ui/tabs';
import { ProductBlueprintProvider, useProductBlueprint } from '@/context/productBlueprint';
import ProductImagePreview from './ProductImagePreview';
import CustomImageSelector from './CustomImageSelector';
import InfoTab from './blueprint-tabs/InfoTab';
import VariantsTab from './blueprint-tabs/VariantsTab';
import PricingTab from './blueprint-tabs/PricingTab';
import PlacementsTab from './blueprint-tabs/PlacementsTab';
import ProductImagesTab from './blueprint-tabs/ProductImagesTab';
import SetupChecklist from './blueprint-tabs/SetupChecklist';

function ConfigureProductBlueprintInner() {
  const {
    blueprint,
    detail,
    loading,
    saving,
    message,
    setMessage,
    isEditing,
    onClose,
    handleSave,
    descriptionExpanded,
    setDescriptionExpanded,
    previewImage,
    setPreviewImage,
    previewIndex,
    customImageSelectorTarget,
    setCustomImageSelectorTarget,
    placementSettings,
    setPlacementSettings,
    projectItems,
    projectId,
    getBlueprintImageUrl,
  } = useProductBlueprint();

  const handleSelectCustomImage = (img) => {
    if (!customImageSelectorTarget) return;
    const { position, itemId } = customImageSelectorTarget;
    setPlacementSettings((prev) => prev.map(p => p.position !== position ? p : {
      ...p,
      source: 'custom',
      itemId,
      customImageId: img.id,
      customItemId: itemId,
    }));
    setCustomImageSelectorTarget(null);
  };

  return (
    <Modal
      title={isEditing ? 'Edit Product Blueprint' : 'Configure Product Blueprint'}
      onClose={onClose}
      top
      className="min-w-[40em] max-w-full"
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Spinner className="text-4xl" />
        </div>
      ) : detail ? (
        <div className="space-y-4 px-[1em]">
          <div className="flex gap-4 items-start">
            <div className="flex-1 space-y-1">
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-medium">{detail.title}</h3>
                <a
                  href={`https://printify.com/app/products/${blueprint.id}/${(detail.brand || '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')}/${(detail.title || '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-sm text-primary-600 dark:text-primary-400 hover:underline"
                >
                  View on Printify
                </a>
              </div>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                {detail.brand} {detail.model ? `· ${detail.model}` : ''}
              </p>
              {detail.description && (
                <div className="text-sm text-gray-500 dark:text-gray-400">
                  <div
                    className={descriptionExpanded ? '' : 'line-clamp-2'}
                    dangerouslySetInnerHTML={{ __html: detail.description }}
                  />
                  <button
                    type="button"
                    onClick={() => setDescriptionExpanded((prev) => !prev)}
                    className="text-primary-600 dark:text-primary-400 hover:underline mt-1"
                  >
                    {descriptionExpanded ? 'Read less...' : 'Read more...'}
                  </button>
                </div>
              )}
            </div>
            <SetupChecklist />
          </div>

          <Tabs tabs={[
            { id: 'info', label: 'Info', content: <InfoTab /> },
            { id: 'variants', label: 'Variants', content: <VariantsTab /> },
            { id: 'pricing', label: 'Pricing', content: <PricingTab /> },
            { id: 'placements', label: 'Placements', content: <PlacementsTab /> },
            { id: 'product-images', label: 'Product Images', content: <ProductImagesTab /> },
          ]} />
        </div>
      ) : (
        <p className="text-sm text-gray-500 dark:text-gray-400">No blueprint data available.</p>
      )}

      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>
          Cancel
        </ButtonOutline>
        <ButtonOutline onClick={handleSave} disabled={saving || loading}>
          {isEditing ? 'Save Changes' : 'Save Blueprint'}
        </ButtonOutline>
      </div>

      <ProductImagePreview
        show={!!previewImage}
        images={detail?.imageCount > 0
          ? Array.from({ length: detail.imageCount }, (_, i) => getBlueprintImageUrl(blueprint.id, i))
          : []}
        alt={detail?.title || ''}
        defaultIndex={previewIndex}
        onClose={() => setPreviewImage(null)}
      />

      {customImageSelectorTarget && (
        <CustomImageSelector
          show={!!customImageSelectorTarget}
          itemId={projectItems[0]?.id}
          projectId={projectId}
          selectedImageId={placementSettings.find(p => p.position === customImageSelectorTarget.position)?.customImageId}
          onSelect={handleSelectCustomImage}
          onClose={() => setCustomImageSelectorTarget(null)}
        />
      )}
    </Modal>
  );
}

export default function ConfigureProductBlueprint(props) {
  if (!props.show) return null;
  return (
    <ProductBlueprintProvider {...props}>
      <ConfigureProductBlueprintInner />
    </ProductBlueprintProvider>
  );
}
