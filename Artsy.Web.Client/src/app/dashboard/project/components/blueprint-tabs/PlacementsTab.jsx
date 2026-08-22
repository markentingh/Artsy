import React, { useState, useMemo, lazy, Suspense } from 'react';
import Carousel from '@/components/ui/carousel';
import Tooltip from '@/components/ui/tooltip';
import Select from '@/components/forms/select';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import { useProductBlueprint } from '@/context/productBlueprint';
import { aspectRatioOptions } from '@/components/ui/aspect-ratio-icons';
import SeamlessPlacements from './SeamlessPlacements';

const NewArtworkModal = lazy(() => import('../NewArtworkModal'));
const EditArtworkModal = lazy(() => import('../EditArtworkModal'));

// Compute the closest aspect ratio option from a WxH dimension string
function closestAspectRatio(dimensions) {
  if (!dimensions) return '1:1';
  const parts = dimensions.split('x');
  if (parts.length !== 2) return '1:1';
  const w = parseInt(parts[0], 10);
  const h = parseInt(parts[1], 10);
  if (!w || !h) return '1:1';
  const targetRatio = w / h;
  let best = '1:1';
  let bestDiff = Infinity;
  for (const opt of aspectRatioOptions) {
    const [ow, oh] = opt.value.split(':').map(Number);
    const ratio = ow / oh;
    const diff = Math.abs(Math.log(ratio / targetRatio));
    if (diff < bestDiff) {
      bestDiff = diff;
      best = opt.value;
    }
  }
  return best;
}

export default function PlacementsTab() {
  const {
    allPlaceholders,
    placementSettings,
    setPlacementSettings,
    artworkOptions,
    projectItems,
    setCustomImageSelectorTarget,
    formatDecorationMethod,
    formatPosition,
    getPlacementCarouselImages,
    handleCreateArtwork,
    projectId,
    placementGroups,
    refreshItemPreviews,
  } = useProductBlueprint();

  const [showNewArtworkModal, setShowNewArtworkModal] = useState(false);
  const [newArtworkPosition, setNewArtworkPosition] = useState(null);
  const [newArtworkAspectRatio, setNewArtworkAspectRatio] = useState('1:1');
  const [editingItem, setEditingItem] = useState(null);
  const [showEditArtworkModal, setShowEditArtworkModal] = useState(false);
  const [newArtworkMessage, setNewArtworkMessage] = useState(null);

  // Compute which placement positions are used in any placement group
  const groupedPlacementPositions = useMemo(() => {
    const positions = new Set();
    for (const group of placementGroups) {
      for (const img of (group.images || [])) {
        if (img.position) {
          positions.add(img.position);
        }
      }
    }
    return positions;
  }, [placementGroups, placementSettings]);

  const handlePlacementSourceChange = (position, value) => {
    if (value === '__new__') {
      // Compute the closest aspect ratio from the placement's current dimensions
      const settings = placementSettings.find(p => p.position === position);
      setNewArtworkAspectRatio(closestAspectRatio(settings?.dimensions));
      setNewArtworkPosition(position);
      setShowNewArtworkModal(true);
      return;
    }
    setPlacementSettings((prev) => prev.map(p => {
      if (p.position !== position) return p;
      return {
        ...p,
        source: value === 'custom' ? 'custom' : (value ? 'item' : ''),
        itemId: value && value !== 'custom' ? value : null,
        customImageId: value === 'custom' ? p.customImageId || null : null,
        customItemId: value === 'custom' ? p.customItemId || null : null,
      };
    }));
  };

  const handleEditPlacementArtwork = (itemId) => {
    const item = projectItems.find(i => i.id === itemId);
    if (item) {
      setEditingItem(item);
      setShowEditArtworkModal(true);
    }
  };

  const handleNewArtworkSave = async (title) => {
    try {
      const newItem = await handleCreateArtwork(title, newArtworkAspectRatio);
      setShowNewArtworkModal(false);
      // Select the new artwork in the placement dropdown
      if (newArtworkPosition) {
        setPlacementSettings((prev) => prev.map(p => p.position === newArtworkPosition ? {
          ...p,
          source: 'item',
          itemId: newItem.id,
          customImageId: null,
          customItemId: null,
        } : p));
      }
      setNewArtworkPosition(null);
      // Show the edit artwork modal
      setEditingItem(newItem);
      setShowEditArtworkModal(true);
    } catch (error) {
      setNewArtworkMessage({ type: 'error', text: error?.message || 'Failed to create artwork' });
    }
  };

  const handleNewArtworkClose = () => {
    setShowNewArtworkModal(false);
    setNewArtworkPosition(null);
    setNewArtworkMessage(null);
  };

  const handleEditArtworkClose = () => {
    const editedItemId = editingItem?.id;
    setShowEditArtworkModal(false);
    setEditingItem(null);
    // Refresh the preview images for the edited artwork
    if (editedItemId) {
      refreshItemPreviews(editedItemId);
    }
  };

  const cropXOptions = [
    { value: 'left', label: 'Left' },
    { value: 'center', label: 'Center' },
    { value: 'right', label: 'Right' },
    { value: 'fit', label: 'Fit' },
  ];

  const cropYOptions = [
    { value: 'top', label: 'Top' },
    { value: 'center', label: 'Center' },
    { value: 'bottom', label: 'Bottom' },
  ];

  const handleCropXChange = (position, value) => {
    setPlacementSettings((prev) => prev.map(p => p.position === position ? { ...p, cropX: value } : p));
  };

  const handleCropYChange = (position, value) => {
    setPlacementSettings((prev) => prev.map(p => p.position === position ? { ...p, cropY: value } : p));
  };

  const handlePlacementDecorationMethodChange = (position, value) => {
    setPlacementSettings((prev) => prev.map(p => {
      if (p.position !== position) return p;
      const ph = allPlaceholders.find((ph) => ph.position === position);
      const methodData = ph?.decorationMethods.find((d) => d.method === value);
      const availableDims = methodData?.dimensions || [];
      return { ...p, decorationMethod: value, dimensions: availableDims.length > 0 ? availableDims[0] : '' };
    }));
  };

  const handlePlacementDimensionsChange = (position, value) => {
    setPlacementSettings((prev) => prev.map(p => p.position === position ? { ...p, dimensions: value } : p));
  };

  const computeCropOverlay = (dimensions, cropX, cropY) => {
    const dims = dimensions || '';
    const parts = dims.split('x');
    if (parts.length !== 2) return null;
    const w = parseInt(parts[0], 10);
    const h = parseInt(parts[1], 10);
    if (!w || !h) return null;

    const targetRatio = w / h;
    if (cropX === 'fit') {
      cropX = 'center';
      cropY = 'center';
    }
    cropX = cropX || 'center';
    cropY = cropY || 'center';

    let overlayWidth, overlayHeight, left, top;

    if (targetRatio > 1) {
      overlayWidth = '100%';
      overlayHeight = `${(1 / targetRatio) * 100}%`;
      left = '0';
      top = cropY === 'top' ? '0' : cropY === 'bottom' ? `${100 - (1 / targetRatio) * 100}%` : `${(100 - (1 / targetRatio) * 100) / 2}%`;
    } else if (targetRatio < 1) {
      overlayWidth = `${targetRatio * 100}%`;
      overlayHeight = '100%';
      top = '0';
      left = cropX === 'left' ? '0' : cropX === 'right' ? `${100 - targetRatio * 100}%` : `${(100 - targetRatio * 100) / 2}%`;
    } else {
      return null;
    }

    return (
      <div
        className="absolute pointer-events-none border-2 border-dashed border-yellow-400 rounded-sm"
        style={{ left, top, width: overlayWidth, height: overlayHeight, boxShadow: '0 0 0 1px rgba(0,0,0,0.3)' }}
      />
    );
  };

  const computeFitStyle = (dimensions, cropX, cropY) => {
    if (cropX !== 'fit') return undefined;
    const dims = dimensions || '';
    const parts = dims.split('x');
    if (parts.length !== 2) return undefined;
    const w = parseInt(parts[0], 10);
    const h = parseInt(parts[1], 10);
    if (!w || !h) return undefined;
    const targetRatio = w / h;
    const yPos = cropY === 'top' ? 'top' : cropY === 'bottom' ? 'bottom' : 'center';
    const objectPosition = `center ${yPos}`;
    if (targetRatio > 1) return { width: '100%', height: `${(1 / targetRatio) * 100}%`, objectFit: 'contain', objectPosition };
    if (targetRatio < 1) return { width: `${targetRatio * 100}%`, height: '100%', objectFit: 'contain', objectPosition };
    return { width: '100%', height: '100%', objectFit: 'contain', objectPosition };
  };

  if (allPlaceholders.length === 0) {
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400">No placements available. Select variants to load placement options.</p>
    );
  }

  return (
    <div>
      <div className="flex items-center gap-1 mb-2">
        <label className="block text-sm font-medium">Placements</label>
        <Tooltip marginTop={2} text="Each placement represents a print area on the product. Choose which artwork to display in each area, and select the decoration method and dimensions for printing. At least one placement must be configured — the rest are optional." />
      </div>
      <div className="grid grid-cols-[repeat(auto-fill,200px)] gap-4">
        {allPlaceholders.map((ph) => {
          const settings = placementSettings.find(p => p.position === ph.position) || { source: '', customImageId: null };
          const carouselImages = getPlacementCarouselImages(ph.position);
          const dmOptions = ph.decorationMethods.map((d) => ({
            value: d.method,
            label: formatDecorationMethod(d.method),
          }));
          const selectedDm = settings.decorationMethod || dmOptions[0]?.value || '';
          const dimOptions = (ph.decorationMethods.find((d) => d.method === selectedDm)?.dimensions || []).map((dim) => ({
            value: dim,
            label: dim.replace('x', ' × '),
          }));
          const selectedDim = settings.dimensions || dimOptions[0]?.value || '';
          const isGrouped = groupedPlacementPositions.has(ph.position);
          return (
            <div key={ph.position} className="relative p-3 rounded-lg bg-gray-50 dark:bg-gray-700">
              {isGrouped && (
                <div className="absolute inset-0 z-10 rounded-lg bg-black/50 flex items-center justify-center pointer-events-none">
                  <span className="text-xs text-white font-medium px-2 py-1 bg-black/40 rounded">In Group</span>
                </div>
              )}
              <div className="relative w-full aspect-square mb-2 rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
                {carouselImages.length > 0 ? (
                  <Carousel
                    images={carouselImages}
                    alt={formatPosition(ph.position)}
                    singleImage
                    infiniteScroll
                    imageClassName={settings.cropX === 'fit' ? '!max-h-none object-contain' : '!max-h-none w-full h-full object-cover'}
                    imageStyle={computeFitStyle(selectedDim, settings.cropX, settings.cropY)}
                    overlayRender={() => computeCropOverlay(selectedDim, settings.cropX, settings.cropY)}
                  />
                ) : (
                  <div className="w-full h-full flex items-center justify-center text-xs text-gray-400">
                    No Image
                  </div>
                )}
              </div>
              <div className="flex items-center justify-between mb-2">
                <p className="text-sm font-medium">{formatPosition(ph.position)}</p>
                {settings.source === 'item' && settings.itemId && (
                  <ButtonIcon
                    name="edit"
                    color="gray"
                    onClick={() => handleEditPlacementArtwork(settings.itemId)}
                    title="Edit artwork"
                    disabled={isGrouped}
                  />
                )}
              </div>
              {(() => {
                return (
                  <>
                    <Select
                      name={`placement-dm-${ph.position}`}
                      options={dmOptions}
                      value={selectedDm}
                      onChange={(e) => handlePlacementDecorationMethodChange(ph.position, e.target.value)}
                      className="mb-2 w-full"
                      disabled={isGrouped}
                    />
                    <Select
                      name={`placement-dims-${ph.position}`}
                      options={dimOptions}
                      value={selectedDim}
                      onChange={(e) => handlePlacementDimensionsChange(ph.position, e.target.value)}
                      className="mb-2 w-full"
                      disabled={isGrouped}
                    />
                  </>
                );
              })()}
              <Select
                name={`placement-${ph.position}`}
                options={artworkOptions}
                value={settings.source === 'item' ? (settings.itemId || '') : (settings.source || '')}
                onChange={(e) => handlePlacementSourceChange(ph.position, e.target.value)}
                className="mb-2 w-full"
                disabled={isGrouped}
              />
              <Select
                name={`placement-cropx-${ph.position}`}
                options={cropXOptions}
                value={settings.cropX || 'center'}
                onChange={(e) => handleCropXChange(ph.position, e.target.value)}
                className="mb-2 w-full"
                disabled={isGrouped}
              />
              <Select
                name={`placement-cropy-${ph.position}`}
                options={cropYOptions}
                value={settings.cropY || 'center'}
                onChange={(e) => handleCropYChange(ph.position, e.target.value)}
                className="mb-0 w-full"
                disabled={isGrouped}
              />
              {settings.source === 'custom' && (
                <ButtonOutline
                  onClick={() => setCustomImageSelectorTarget({ position: ph.position })}
                  className="mb-0 mt-2 w-full"
                  disabled={isGrouped}
                >
                  <Icon name="image" className="mr-2" />
                  <span>Select</span>
                </ButtonOutline>
              )}
            </div>
          );
        })}
      </div>

      <SeamlessPlacements />

      {showNewArtworkModal && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <NewArtworkModal
            show={showNewArtworkModal}
            onClose={handleNewArtworkClose}
            onSave={handleNewArtworkSave}
            aspectRatio={newArtworkAspectRatio}
            onAspectRatioChange={setNewArtworkAspectRatio}
          />
        </Suspense>
      )}

      {showEditArtworkModal && editingItem && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <EditArtworkModal
            show={showEditArtworkModal}
            item={editingItem}
            onClose={handleEditArtworkClose}
            onChanged={() => {}}
          />
        </Suspense>
      )}

      {newArtworkMessage && (
        <Message type={newArtworkMessage.type} onClose={() => setNewArtworkMessage(null)}>
          {newArtworkMessage.text}
        </Message>
      )}
    </div>
  );
}
