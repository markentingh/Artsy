import React, { useRef, useState, useMemo } from 'react';
import Tooltip from '@/components/ui/tooltip';
import Select from '@/components/forms/select';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Icon from '@/components/ui/icon';
import { useProductBlueprint } from '@/context/productBlueprint';

export default function SeamlessPlacements() {
  const {
    allPlaceholders,
    placementSettings,
    placementGroups,
    handleCreatePlacementGroup,
    handleDeletePlacementGroup,
    handleSavePlacementGroupImage,
    handleDeletePlacementGroupImage,
    handleReorderPlacementGroupImages,
    handleToggleFlip,
    getPlacementCarouselImages,
    formatPosition,
    projectId,
    blueprint,
  } = useProductBlueprint();

  const dragItem = useRef(null);
  const dragOverItem = useRef(null);
  const [saving, setSaving] = useState(false);

  // Placements that have an artwork (source === 'item') and not a custom image
  const artworkPlacements = useMemo(() => {
    return placementSettings
      .filter(p => p.source === 'item' && p.itemId)
      .map(p => {
        const ph = allPlaceholders.find(ph => formatPosition(ph.position) === p.position || ph.position === p.position);
        return { ...p, placeholder: ph };
      });
  }, [placementSettings, allPlaceholders]);

  // Group artwork placements by itemId to find which artworks are used by 2+ placements
  const artworkPlacementGroups = useMemo(() => {
    const map = new Map();
    for (const ap of artworkPlacements) {
      if (!map.has(ap.itemId)) map.set(ap.itemId, []);
      map.get(ap.itemId).push(ap);
    }
    return map;
  }, [artworkPlacements]);

  // Only show this section if at least 2 placements use the same artwork
  const hasEligibleArtwork = useMemo(() => {
    for (const [, placements] of artworkPlacementGroups) {
      if (placements.length >= 2) return true;
    }
    return false;
  }, [artworkPlacementGroups]);

  // Build dropdown options for the first dropdown: placements with artwork, not custom
  const firstDropdownOptions = useMemo(() => {
    return [
      { value: '', label: 'Select placement...' },
      ...artworkPlacements.map(ap => ({
        value: ap.position,
        label: formatPosition(ap.placeholder?.position || ap.position),
      })),
    ];
  }, [artworkPlacements]);

  if (!hasEligibleArtwork) return null;

  // Get the dimensions for a placement position
  const getPlacementDimensions = (position) => {
    const settings = placementSettings.find(p => p.position === position);
    if (!settings) return null;
    const ph = allPlaceholders.find(ph => formatPosition(ph.position) === formatPosition(position) || ph.position === position);
    if (!ph) return null;
    const dm = ph.decorationMethods?.find(d => d.method === settings.decorationMethod);
    if (!dm || !dm.dimensions || dm.dimensions.length === 0) return null;
    return dm.dimensions[0]; // "WxH"
  };

  // Get the artwork image URL for a placement position
  const getArtworkImageUrl = (position) => {
    const images = getPlacementCarouselImages(position);
    return images.length > 0 ? images[0] : null;
  };

  // Get the itemId for a placement position
  const getPlacementItemId = (position) => {
    const settings = placementSettings.find(p => p.position === position);
    return settings?.itemId || null;
  };

  const handleAddGroup = async () => {
    try {
      await handleCreatePlacementGroup();
    } catch (e) {
      console.error(e);
    }
  };

  const handleGroupDelete = async (groupId) => {
    try {
      await handleDeletePlacementGroup(groupId);
    } catch (e) {
      console.error(e);
    }
  };

  // When the first dropdown changes in a group
  const handleFirstDropdownChange = async (group, position) => {
    try {
      setSaving(true);
      // Remove all existing images for this group (they'll be re-added as needed)
      for (const img of (group.images || [])) {
        if (img.id) await handleDeletePlacementGroupImage(img.id, group.id);
      }
      // If a position was selected, create the first image
      if (position) {
        await handleSavePlacementGroupImage({
          projectId,
          blueprintId: blueprint.id,
          groupId: group.id,
          index: 0,
          artworkId: getPlacementItemId(position),
          customId: null,
          position,
        });
      }
    } catch (e) {
      console.error(e);
    } finally {
      setSaving(false);
    }
  };

  // When a non-first dropdown changes
  const handleImageDropdownChange = async (group, imageId, position, index) => {
    try {
      setSaving(true);
      if (position) {
        await handleSavePlacementGroupImage({
          id: imageId,
          projectId,
          blueprintId: blueprint.id,
          groupId: group.id,
          index,
          artworkId: getPlacementItemId(position),
          customId: null,
          position,
        });
      } else if (imageId) {
        // Clearing the selection — delete the image
        await handleDeletePlacementGroupImage(imageId, group.id);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setSaving(false);
    }
  };

  // Remove a placement from a group (delete the image and reindex remaining)
  const handleRemovePlacement = async (group, imageId) => {
    try {
      setSaving(true);
      await handleDeletePlacementGroupImage(imageId, group.id);
      // Reindex remaining images
      const remaining = (group.images || []).filter(i => i.id !== imageId);
      for (let i = 0; i < remaining.length; i++) {
        await handleSavePlacementGroupImage({
          id: remaining[i].id,
          projectId,
          blueprintId: blueprint.id,
          groupId: group.id,
          index: i,
          artworkId: remaining[i].artworkId,
          customId: remaining[i].customId,
          position: remaining[i].position || null,
          flipped: remaining[i].flipped || false,
        });
      }
    } catch (e) {
      console.error(e);
    } finally {
      setSaving(false);
    }
  };

  // Add a new placement dropdown to a group
  const handleAddPlacement = async (group) => {
    try {
      setSaving(true);
      const images = group.images || [];
      const nextIndex = images.length;
      await handleSavePlacementGroupImage({
        projectId,
        blueprintId: blueprint.id,
        groupId: group.id,
        index: nextIndex,
        artworkId: null,
        customId: null,
        position: null,
      });
    } catch (e) {
      console.error(e);
    } finally {
      setSaving(false);
    }
  };

  // Drag & drop reorder
  const handleDragStart = (e, groupIndex, imageIndex) => {
    dragItem.current = { groupIndex, imageIndex };
  };

  const handleDragEnter = (e, groupIndex, imageIndex) => {
    if (!dragItem.current) return;
    if (dragItem.current.groupIndex !== groupIndex) return;
    dragOverItem.current = { groupIndex, imageIndex };
  };

  const handleDragEnd = async (group) => {
    if (!dragItem.current || !dragOverItem.current) return;
    if (dragItem.current.groupIndex !== dragOverItem.current.groupIndex) return;
    if (dragItem.current.imageIndex === dragOverItem.current.imageIndex) return;

    const images = [...(group.images || [])];
    const draggedItem = images[dragItem.current.imageIndex];
    images.splice(dragItem.current.imageIndex, 1);
    images.splice(dragOverItem.current.imageIndex, 0, draggedItem);

    dragItem.current = null;
    dragOverItem.current = null;

    // Reindex and save
    const reordered = images.map((img, i) => ({ ...img, index: i }));
    try {
      setSaving(true);
      await handleReorderPlacementGroupImages(group.id, reordered);
    } catch (e) {
      console.error(e);
    } finally {
      setSaving(false);
    }
  };

  // Render a single group panel
  const renderGroup = (group, groupIndex) => {
    const images = group.images || [];
    const firstImage = images[0];
    const firstPosition = firstImage?.position || null;
    const firstItemId = firstImage?.artworkId || null;

    // For additional dropdowns: only show placements with the same artwork, not already selected
    const getAvailablePlacements = (currentIndex) => {
      if (!firstItemId) return [];
      const sameArtworkPlacements = artworkPlacementGroups.get(firstItemId) || [];
      const selectedPositions = new Set(
        images
          .filter((_, i) => i !== currentIndex)
          .map(img => img.position)
          .filter(Boolean)
      );
      return sameArtworkPlacements.filter(ap => !selectedPositions.has(ap.position));
    };

    // Image container
    const artworkUrl = firstPosition ? getArtworkImageUrl(firstPosition) : null;
    const containerWidth = 225;

    // Compute dashed overlays for each image in the group
    const overlays = images.map((img, i) => {
      if (!img.position) return null;
      const dims = getPlacementDimensions(img.position);
      if (!dims) return null;
      const [w, h] = dims.split('x').map(Number);
      const ratio = w / h;
      // Width = container width, height = containerWidth / ratio
      const overlayHeight = containerWidth / ratio;
      return { index: i, height: overlayHeight, position: img.position, flipped: img.flipped, imageId: img.id };
    }).filter(Boolean);

    // Total height of the image container = sum of overlay heights (or 200 if no images)
    const containerHeight = overlays.length > 0
      ? Math.max(200, overlays.reduce((sum, o) => sum + o.height, 0))
      : 200;

    return (
      <div key={group.id} className="p-3 rounded-lg bg-gray-50 dark:bg-gray-700 flex gap-3">
        {/* Image container */}
        <div
          className="relative rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 bg-gray-100 dark:bg-gray-800 flex-shrink-0"
          style={{ width: containerWidth, height: containerHeight }}
        >
          {artworkUrl ? (
            <div
              className="absolute inset-0"
              style={{
                backgroundImage: `url(${artworkUrl})`,
                backgroundRepeat: 'repeat-y',
                backgroundSize: `${containerWidth}px auto`,
                backgroundPosition: 'top center',
              }}
            />
          ) : (
            <div className="w-full h-full flex items-center justify-center text-xs text-gray-400">
              No Image
            </div>
          )}
          {/* Dashed overlays for each placement */}
          {overlays.map((overlay, i) => {
            let topOffset = 0;
            for (let j = 0; j < overlay.index; j++) {
              topOffset += overlays[j].height;
            }
            return (
              <div
                key={i}
                className="absolute border-2 border-dashed border-yellow-400 rounded-sm pointer-events-none"
                style={{
                  left: 0,
                  top: topOffset,
                  width: containerWidth,
                  height: overlay.height,
                  boxShadow: '0 0 0 1px rgba(0,0,0,0.3)',
                }}
              >
                <span className="absolute top-1 left-1 text-[10px] bg-yellow-400/80 text-black px-1 rounded">
                  {formatPosition(overlay.position)}
                </span>
                {overlay.flipped && (
                  <span className="absolute bottom-1 right-1 text-yellow-400 bg-black/50 rounded-full w-5 h-5 flex items-center justify-center">
                    <Icon name="autorenew" className="text-sm" />
                  </span>
                )}
              </div>
            );
          })}
        </div>

        {/* Dropdowns + controls */}
        <div className="flex flex-col flex-1 min-w-0">
          {/* First dropdown + flip */}
          <div
            className="flex items-center gap-1 mb-2"
            draggable
            onDragStart={(e) => handleDragStart(e, groupIndex, 0)}
            onDragEnter={(e) => handleDragEnter(e, groupIndex, 0)}
            onDragOver={(e) => e.preventDefault()}
            onDragEnd={() => handleDragEnd(group)}
          >
            <Icon
              name="drag_indicator"
              className="cursor-grab text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 flex-shrink-0"
            />
            <Select
              name={`group-${group.id}-first`}
              options={firstDropdownOptions}
              value={firstPosition || ''}
              onChange={(e) => handleFirstDropdownChange(group, e.target.value)}
              className="flex-1"
              disabled={saving}
            />
            {firstImage && firstImage.id && firstPosition && (
              <button
                type="button"
                onClick={() => handleToggleFlip(group.id, firstImage.id)}
                title="Flip 180°"
                disabled={saving}
                className={`flex-shrink-0 w-6 h-6 -mt-2 flex items-center justify-center rounded transition text-sm ${firstImage.flipped ? 'text-yellow-500 bg-yellow-50 dark:bg-yellow-900/20' : 'text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-600'}`}
              >
                <Icon name="autorenew" />
              </button>
            )}
            {firstImage && firstImage.id && (
              <button
                type="button"
                onClick={() => handleRemovePlacement(group, firstImage.id)}
                title="Remove"
                disabled={saving}
                className="flex-shrink-0 w-6 h-6 -mt-2 flex items-center justify-center rounded transition text-sm text-red-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20"
              >
                <Icon name="close" />
              </button>
            )}
          </div>

          {/* Additional dropdowns */}
          {images.slice(1).map((img, imgIdx) => {
            const actualIdx = imgIdx + 1;
            const available = getAvailablePlacements(actualIdx);
            const currentPosition = img.position || '';
            const options = [
              { value: '', label: 'Select placement...' },
              ...available.map(ap => ({
                value: ap.position,
                label: formatPosition(ap.placeholder?.position || ap.position),
              })),
            ];
            return (
              <div
                key={img.id || imgIdx}
                draggable
                onDragStart={(e) => handleDragStart(e, groupIndex, actualIdx)}
                onDragEnter={(e) => handleDragEnter(e, groupIndex, actualIdx)}
                onDragOver={(e) => e.preventDefault()}
                onDragEnd={() => handleDragEnd(group)}
                className="flex items-center gap-1 mb-2"
              >
                <Icon
                  name="drag_indicator"
                  className="cursor-grab text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 flex-shrink-0"
                />
                <Select
                  name={`group-${group.id}-img-${imgIdx}`}
                  options={options}
                  value={currentPosition || ''}
                  onChange={(e) => handleImageDropdownChange(group, img.id, e.target.value, actualIdx)}
                  className="flex-1"
                  disabled={saving}
                />
                {currentPosition && (
                  <button
                    type="button"
                    onClick={() => handleToggleFlip(group.id, img.id)}
                    title="Flip 180°"
                    disabled={saving}
                    className={`flex-shrink-0 w-6 h-6 -mt-2 flex items-center justify-center rounded transition text-sm ${img.flipped ? 'text-yellow-500 bg-yellow-50 dark:bg-yellow-900/20' : 'text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-600'}`}
                  >
                    <Icon name="autorenew" />
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => handleRemovePlacement(group, img.id)}
                  title="Remove"
                  disabled={saving}
                  className="flex-shrink-0 w-6 h-6 -mt-2 flex items-center justify-center rounded transition text-sm text-red-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20"
                >
                  <Icon name="close" />
                </button>
              </div>
            );
          })}

          {/* Add Placement dashed button */}
          {firstPosition && (() => {
            const available = getAvailablePlacements(-1);
            if (available.length === 0) return null;
            return (
              <ButtonOutline
                onClick={() => handleAddPlacement(group)}
                disabled={saving}
                className="w-full mb-2 border-dashed"
              >
                <Icon name="add" className="mr-1" />
                <span>Add Placement</span>
              </ButtonOutline>
            );
          })()}

          {/* Delete group button */}
          <div className="flex justify-end mt-auto">
            <ButtonIcon
              name="delete"
              color="red"
              onClick={() => handleGroupDelete(group.id)}
              title="Delete group"
            />
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="mt-6">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-1">
          <label className="block text-sm font-medium">Seamless Placements</label>
          <Tooltip marginTop={2} text="Group multiple placements that share the same artwork so a single seamless image can be generated to cover all of them. The artwork repeats vertically and each dashed outline shows how it will be cropped for each placement." />
        </div>
        <ButtonOutline onClick={handleAddGroup} disabled={saving}>
          <Icon name="add" className="mr-1" />
          <span>Add Group</span>
        </ButtonOutline>
      </div>
      {placementGroups.length > 0 ? (
        <div className="grid grid-cols-[repeat(auto-fill,500px)] gap-4">
          {placementGroups.map((group, idx) => renderGroup(group, idx))}
        </div>
      ) : (
        <p className="py-12 text-center text-sm text-gray-500 dark:text-gray-400">No Seamless Placement Groups created yet</p>
      )}
    </div>
  );
}
