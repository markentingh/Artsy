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

  const handlePlacementSourceChange = (key, value) => {
    setPlacementSettings((prev) => ({
      ...prev,
      [key]: {
        ...prev[key],
        source: value === 'custom' ? 'custom' : (value ? 'item' : ''),
        itemId: value && value !== 'custom' ? value : null,
        customImageId: value === 'custom' ? prev[key]?.customImageId || null : null,
        customItemId: value === 'custom' ? prev[key]?.customItemId || null : null,
      },
    }));
  };

  const handlePlacementDecorationMethodChange = (key, value) => {
    setPlacementSettings((prev) => {
      const ph = allPlaceholders.find((p) => p.key === key);
      const methodData = ph?.decorationMethods.find((d) => d.method === value);
      const availableDims = methodData?.dimensions || [];
      return {
        ...prev,
        [key]: {
          ...prev[key],
          decorationMethod: value,
          dimensions: availableDims.length > 0 ? availableDims[0] : '',
        },
      };
    });
  };

  const handlePlacementDimensionsChange = (key, value) => {
    setPlacementSettings((prev) => ({
      ...prev,
      [key]: {
        ...prev[key],
        dimensions: value,
      },
    }));
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
          const settings = placementSettings[ph.key] || { source: '', customImageId: null };
          const carouselImages = getPlacementCarouselImages(ph.key);
          return (
            <div key={ph.key} className="p-3 rounded-lg bg-gray-50 dark:bg-gray-700">
              <div className="w-full aspect-square mb-2 rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
                {carouselImages.length > 0 ? (
                  <Carousel
                    images={carouselImages}
                    alt={formatPosition(ph.position)}
                    singleImage
                    infiniteScroll
                    imageClassName="!max-h-none w-full h-full object-cover"
                  />
                ) : (
                  <div className="w-full h-full flex items-center justify-center text-xs text-gray-400">
                    No Image
                  </div>
                )}
              </div>
              <p className="text-sm font-medium mb-2">{formatPosition(ph.position)}</p>
              {(() => {
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
                  <>
                    <Select
                      name={`placement-dm-${ph.key}`}
                      options={dmOptions}
                      value={selectedDm}
                      onChange={(e) => handlePlacementDecorationMethodChange(ph.key, e.target.value)}
                      className="mb-2 w-full"
                    />
                    <Select
                      name={`placement-dims-${ph.key}`}
                      options={dimOptions}
                      value={selectedDim}
                      onChange={(e) => handlePlacementDimensionsChange(ph.key, e.target.value)}
                      className="mb-2 w-full"
                    />
                  </>
                );
              })()}
              <Select
                name={`placement-${ph.key}`}
                options={artworkOptions}
                value={settings.source === 'item' ? (settings.itemId || '') : (settings.source || '')}
                onChange={(e) => handlePlacementSourceChange(ph.key, e.target.value)}
                className="mb-0 w-full"
              />
              {settings.source === 'custom' && (
                <ButtonOutline
                  onClick={() => setCustomImageSelectorTarget({ key: ph.key, itemId: projectItems[0]?.id })}
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
