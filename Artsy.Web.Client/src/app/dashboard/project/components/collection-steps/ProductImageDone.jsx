import React, { useCallback } from 'react';
import { useCollection } from '@/context/collection';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';

export default function ProductImageDone() {
  const {
    allProductImages, collectionId, setStep, STEPS, onClose, api,
  } = useCollection();

  const handleNext = useCallback(() => {
    setStep(STEPS.NEXT_STEP);
  }, [setStep, STEPS]);

  const images = allProductImages.map(img => api.getProductImageUrl(collectionId, img.id));

  return (
    <div>
      <p className="text-center text-lg mb-4">
        {allProductImages.length} product image{allProductImages.length !== 1 ? 's' : ''} generated successfully.
      </p>
      {images.length > 0 && (
        <div className="flex justify-center mb-4">
          <div className="w-full">
            <Carousel
              images={images}
              alt="Generated product images"
              infiniteScroll
              imageClassName="!max-h-none w-[150px] h-[150px] object-contain rounded-lg"
            />
          </div>
        </div>
      )}
      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline className="cancel" onClick={onClose}>Close</ButtonOutline>
        <ButtonOutline onClick={handleNext}>Next</ButtonOutline>
      </div>
    </div>
  );
}
