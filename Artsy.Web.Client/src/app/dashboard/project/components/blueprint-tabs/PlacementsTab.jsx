import React from 'react';
import Carousel from '@/components/ui/carousel';
import Tooltip from '@/components/ui/tooltip';
import Select from '@/components/forms/select';
import ButtonOutline from '@/components/ui/button-outline';
import Icon from '@/components/ui/icon';
import { useProductBlueprint } from '@/context/productBlueprint';

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
  } = useProductBlueprint();

  const handlePlacementSourceChange = (position, value) => {
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

  const cropXOptions = [
    { value: 'left', label: 'Left' },
    { value: 'center', label: 'Center' },
    { value: 'right', label: 'Right' },
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
          return (
            <div key={ph.position} className="p-3 rounded-lg bg-gray-50 dark:bg-gray-700">
              <div className="w-full aspect-square mb-2 rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
                {carouselImages.length > 0 ? (
                  <Carousel
                    images={carouselImages}
                    alt={formatPosition(ph.position)}
                    singleImage
                    infiniteScroll
                    imageClassName="!max-h-none w-full h-full object-cover"
                    overlayRender={() => computeCropOverlay(selectedDim, settings.cropX, settings.cropY)}
                  />
                ) : (
                  <div className="w-full h-full flex items-center justify-center text-xs text-gray-400">
                    No Image
                  </div>
                )}
              </div>
              <p className="text-sm font-medium mb-2">{formatPosition(ph.position)}</p>
              {(() => {
                return (
                  <>
                    <Select
                      name={`placement-dm-${ph.position}`}
                      options={dmOptions}
                      value={selectedDm}
                      onChange={(e) => handlePlacementDecorationMethodChange(ph.position, e.target.value)}
                      className="mb-2 w-full"
                    />
                    <Select
                      name={`placement-dims-${ph.position}`}
                      options={dimOptions}
                      value={selectedDim}
                      onChange={(e) => handlePlacementDimensionsChange(ph.position, e.target.value)}
                      className="mb-2 w-full"
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
              />
              <Select
                name={`placement-cropx-${ph.position}`}
                options={cropXOptions}
                value={settings.cropX || 'center'}
                onChange={(e) => handleCropXChange(ph.position, e.target.value)}
                className="mb-2 w-full"
              />
              <Select
                name={`placement-cropy-${ph.position}`}
                options={cropYOptions}
                value={settings.cropY || 'center'}
                onChange={(e) => handleCropYChange(ph.position, e.target.value)}
                className="mb-0 w-full"
              />
              {settings.source === 'custom' && (
                <ButtonOutline
                  onClick={() => setCustomImageSelectorTarget({ position: ph.position, itemId: projectItems[0]?.id })}
                  className="mb-0 mt-2 w-full"
                >
                  <Icon name="image" className="mr-2" />
                  <span>Select</span>
                </ButtonOutline>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
