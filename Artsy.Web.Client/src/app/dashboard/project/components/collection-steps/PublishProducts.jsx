import React, { useCallback, useMemo } from 'react';
import { useCollection } from '@/context/collection';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';

export default function PublishProducts() {
  const {
    project, blueprints, allProductImages, collectionId, api,
    handleSaveDraft, setMessage, setArtworkPreview,
  } = useCollection();

  const handlePublishProducts = useCallback(() => {
    setMessage({ type: 'info', text: 'Publishing will be implemented at a later time.' });
  }, [setMessage]);

  const platforms = [];
  if (project?.publishToPrintify) platforms.push('Printify');

  const imagesByBlueprint = useMemo(() => {
    const map = {};
    for (const img of allProductImages) {
      const key = img.projectBlueprintId;
      if (!map[key]) map[key] = [];
      map[key].push(api.getProductImageUrl(collectionId, img.id));
    }
    return map;
  }, [allProductImages, collectionId, api]);

  const allImages = useMemo(() => {
    return blueprints.flatMap(bp => imagesByBlueprint[bp.id] || []);
  }, [blueprints, imagesByBlueprint]);

  return (
    <div>
      <p className="text-center text-lg mb-4">
        The following products will be published via {platforms.join(', ')}.
      </p>
      <div className="grid gap-4 mb-6 max-h-[40vh] overflow-y-auto justify-items-center" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(0, 12em))', justifyContent: 'center' }}>
        {blueprints.map((bp) => {
          const images = imagesByBlueprint[bp.id] || [];
          if (images.length === 0) return null;
          return (
            <div key={bp.id} className="flex flex-col items-center">
              <div className="w-full rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
                <Carousel
                  images={images}
                  alt={bp.name}
                  singleImage
                  infiniteScroll
                  onImageClick={(src) => setArtworkPreview({ images: allImages, src, alt: 'Product Images' })}
                  imageClassName="!max-h-none w-full h-full object-contain"
                />
              </div>
              <p className="text-sm font-medium text-gray-700 dark:text-gray-300 mt-2 text-center">{bp.name}</p>
            </div>
          );
        })}
      </div>
      <div className="buttons flex justify-end gap-2">
        <ButtonOutline className="cancel" onClick={handleSaveDraft}>Save Draft</ButtonOutline>
        <ButtonOutline onClick={handlePublishProducts}>Publish Products</ButtonOutline>
      </div>
    </div>
  );
}
