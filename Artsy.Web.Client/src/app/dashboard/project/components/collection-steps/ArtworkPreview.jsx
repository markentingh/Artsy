import React, { useCallback } from 'react';
import { useCollection } from '@/context/collection';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';

export default function ArtworkPreview() {
  const {
    aiItems, currentItemIndex, currentItem,
    isGenerating, previewImageData,
    showChanges, setShowChanges,
    requestedChanges, setRequestedChanges,
    collectionId, ensureCollection,
    doGeneratePreview, advanceToNextItem,
    setCollectionArtwork,
    api, onClose,
  } = useCollection();

  const handleMakeChanges = useCallback(() => {
    setShowChanges(true);
  }, [setShowChanges]);

  const handleSubmitChanges = useCallback(() => {
    if (!requestedChanges.trim()) return;
    setShowChanges(false);
    if (collectionId) {
      doGeneratePreview(collectionId);
    } else {
      ensureCollection().then((colId) => {
        if (colId) doGeneratePreview(colId);
      });
    }
  }, [requestedChanges, collectionId, doGeneratePreview, ensureCollection, setShowChanges]);

  const handleAccept = useCallback(async () => {
    const colId = await ensureCollection();
    if (colId) {
      const item = aiItems[currentItemIndex];
      if (item) {
        try {
          await api.acceptCollectionArtwork({ collectionId: colId, itemId: item.id });
          const artRes = await api.getCollectionArtwork(colId);
          if (artRes.data.success) {
            setCollectionArtwork(artRes.data.data || []);
          }
        } catch (error) {
          console.error('acceptCollectionArtwork error:', error?.response?.data || error);
        }
      }
    }
    advanceToNextItem();
  }, [ensureCollection, aiItems, currentItemIndex, api, advanceToNextItem, setCollectionArtwork]);

  return (
    <div>
      <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">
        Artwork {currentItemIndex + 1} of {aiItems.length}: {currentItem?.title || 'Untitled'}
      </h3>
      <div className="flex flex-col items-center gap-4">
        <div className="w-[512px] h-[512px] max-w-full flex items-center justify-center rounded-lg bg-gray-100 dark:bg-gray-700 overflow-hidden">
          {isGenerating ? (
            <Spinner className="text-3xl" />
          ) : previewImageData ? (
            <img
              src={previewImageData}
              alt="Preview"
              className="w-full h-full object-contain"
            />
          ) : (
            <span className="text-sm text-gray-500 dark:text-gray-400">No preview generated yet.</span>
          )}
        </div>

        {!showChanges && !isGenerating && previewImageData && (
          <div className="buttons flex gap-2">
            <ButtonOutline onClick={handleMakeChanges}>Make Changes</ButtonOutline>
            <ButtonOutline onClick={handleAccept}>Accept</ButtonOutline>
          </div>
        )}

        {showChanges && !isGenerating && (
          <div className="w-full max-w-[512px]">
            <TextArea
              name="requestedChanges"
              label="Requested Changes"
              value={requestedChanges}
              onChange={(e) => setRequestedChanges(e.target.value)}
              placeholder="Describe the changes you want..."
              rows={4}
            />
            <div className="buttons flex justify-end gap-2">
              <ButtonOutline onClick={handleSubmitChanges} disabled={!requestedChanges.trim()}>
                Regenerate
              </ButtonOutline>
            </div>
          </div>
        )}
      </div>
      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>Cancel</ButtonOutline>
      </div>
    </div>
  );
}
