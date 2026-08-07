import React from 'react';
import { PrintifyBlueprintProvider, usePrintifyBlueprint } from '@/context/printifyBlueprint';
import Modal from '@/components/ui/modal';
import Select from '@/components/forms/select';
import Tabs from '@/components/ui/tabs';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import ImagesTab from './configure-tabs/ImagesTab';
import VariantsTab from './configure-tabs/VariantsTab';
import PlacementsTab from './configure-tabs/PlacementsTab';
import ImagePromptTab from './configure-tabs/ImagePromptTab';

export default function ConfigurePrintifyBlueprint({ show, blueprint, onClose, onSave }) {
  return (
    <PrintifyBlueprintProvider show={show} blueprint={blueprint} onClose={onClose} onSave={onSave}>
      <ConfigureModal show={show} onClose={onClose} />
    </PrintifyBlueprintProvider>
  );
}

function ConfigureModal({ show, onClose }) {
  const {
    message, setMessage, published, saving, loading,
    allImagesHaveVariants, hasSettingsChanged,
    handleSave, handlePublish, handleUnpublish,
    saveMessage,
  } = usePrintifyBlueprint();

  if (!show) return null;

  return (
    <Modal
      title="Configure Blueprint"
      onClose={onClose}
      top
      className="min-w-[50em] max-w-full"
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <ConfigureContent />

      <div className="buttons flex justify-between gap-2 mt-4">
        <div className="flex gap-2">
          {!published && (
            <ButtonOutline
              onClick={handlePublish}
              disabled={saving || loading}
              color="green"
            >
              Publish
            </ButtonOutline>
          )}
          {published && (
            <ButtonOutline
              onClick={handleUnpublish}
              disabled={saving || loading}
              color="red"
            >
              Unpublish
            </ButtonOutline>
          )}
        </div>
        <div className="flex gap-2 items-center">
          {saveMessage && (
            <span className="text-sm text-green-600 dark:text-green-400 mr-2 transition-opacity duration-500">
              {saveMessage}
            </span>
          )}
          <ButtonOutline className="cancel" onClick={onClose}>
            Cancel
          </ButtonOutline>
          <ButtonOutline onClick={handleSave} disabled={saving || loading || !hasSettingsChanged}>
            {saving ? 'Saving...' : 'Save Changes'}
          </ButtonOutline>
        </div>
      </div>
    </Modal>
  );
}

function ConfigureContent() {
  const {
    detail, blueprint, published, loading,
    descriptionExpanded, setDescriptionExpanded,
    printProviders, selectedProvider, handleProviderChange,
    scrollRef, scrollMaxHeight,
  } = usePrintifyBlueprint();

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Spinner className="text-4xl" />
      </div>
    );
  }

  if (!detail) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">No blueprint data available.</p>;
  }

  const providerOptions = printProviders.map((p) => ({
    value: String(p.id),
    label: p.title,
  }));

  const tabs = [
    { id: 'images', label: 'Images', content: <ImagesTab /> },
    { id: 'variants', label: 'Variants', content: <VariantsTab /> },
    { id: 'placements', label: 'Placements', content: <PlacementsTab /> },
    { id: 'imagePrompt', label: 'Image Prompt', content: <ImagePromptTab /> },
  ];

  return (
    <div ref={scrollRef} className="overflow-y-auto space-y-4 p-2" style={{ maxHeight: scrollMaxHeight }}>
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h3 className="text-lg font-medium">{detail.title}</h3>
          {published && (
            <span className="px-2 py-0.5 rounded text-xs font-bold bg-green-500 text-white whitespace-nowrap">
              Published
            </span>
          )}
        </div>
        <a
          href={`https://printify.com/app/products/${blueprint.id}/${(detail.brand || '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')}/${(detail.title || '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')}`}
          target="_blank"
          rel="noopener noreferrer"
          className="text-sm text-primary-600 dark:text-primary-400 hover:underline"
        >
          View on Printify
        </a>
      </div>

      <div className="space-y-1">
        <p className="text-sm text-gray-500 dark:text-gray-400">
          {detail.brand} {detail.model ? `· ${detail.model}` : ''}
        </p>
        {detail.description && (
          <div className="text-sm text-gray-500 dark:text-gray-400">
            <div className="flex gap-3">
              <div className="flex-1">
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
              <div className="w-56 shrink-0">
                <Select
                  name="printProvider"
                  label="Print Provider"
                  placeholder="Select a print provider"
                  options={providerOptions}
                  value={selectedProvider}
                  onChange={(e) => handleProviderChange(e.target.value)}
                />
              </div>
            </div>
          </div>
        )}
        {!detail.description && (
          <div>
            <Select
              name="printProvider"
              label="Print Provider"
              placeholder="Select a print provider"
              options={providerOptions}
              value={selectedProvider}
              onChange={(e) => handleProviderChange(e.target.value)}
              className="max-w-xs"
            />
          </div>
        )}
      </div>

      <hr className="border-gray-200 dark:border-gray-700" />

      <Tabs tabs={tabs} defaultTab="images" />
    </div>
  );
}
