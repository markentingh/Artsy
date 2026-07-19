import React, { useRef, useState, useCallback, useEffect } from 'react';
import ButtonIcon from '@/components/ui/button-icon';

export default function Carousel({ images = [], alt = '', onImageClick, singleImage = false, defaultIndex = 0, infiniteScroll = false }) {
  const scrollRef = useRef(null);
  const [atStart, setAtStart] = useState(true);
  const [atEnd, setAtEnd] = useState(false);
  const [overflowing, setOverflowing] = useState(false);
  const [singleIndex, setSingleIndex] = useState(defaultIndex);

  const updateScrollState = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    setOverflowing(el.scrollWidth > el.clientWidth);
    setAtStart(el.scrollLeft <= 1);
    setAtEnd(el.scrollLeft + el.clientWidth >= el.scrollWidth - 1);
  }, []);

  useEffect(() => {
    updateScrollState();
    window.addEventListener('resize', updateScrollState);
    return () => window.removeEventListener('resize', updateScrollState);
  }, [images, updateScrollState]);

  if (images.length === 0) return null;

  if (singleImage) {
    const showNav = images.length > 1;
    const handleSinglePrev = () => setSingleIndex((prev) => {
      if (prev === 0) return infiniteScroll ? images.length - 1 : 0;
      return prev - 1;
    });
    const handleSingleNext = () => setSingleIndex((prev) => {
      if (prev === images.length - 1) return infiniteScroll ? 0 : images.length - 1;
      return prev + 1;
    });
    const singleAtStart = !infiniteScroll && singleIndex === 0;
    const singleAtEnd = !infiniteScroll && singleIndex === images.length - 1;

    return (
      <div className="relative w-full rounded-lg">
        <div className="flex items-center justify-center">
          <img
            src={images[singleIndex]}
            alt={`${alt} ${singleIndex + 1}`}
            className="max-w-full max-h-[70vh] object-contain rounded-lg"
          />
        </div>
        {showNav && (
          <>
            <div className="absolute inset-y-0 left-0 flex items-center pl-1">
              <ButtonIcon
                name="chevron_left"
                onClick={handleSinglePrev}
                title="Previous"
                className={`bg-white/80 dark:bg-gray-800/80 shadow-sm ${singleAtStart ? 'opacity-30 pointer-events-none' : ''}`}
              />
            </div>
            <div className="absolute inset-y-0 right-0 flex items-center pr-1">
              <ButtonIcon
                name="chevron_right"
                onClick={handleSingleNext}
                title="Next"
                className={`bg-white/80 dark:bg-gray-800/80 shadow-sm ${singleAtEnd ? 'opacity-30 pointer-events-none' : ''}`}
              />
            </div>
          </>
        )}
      </div>
    );
  }

  const handlePrev = () => {
    const el = scrollRef.current;
    if (!el) return;
    if (infiniteScroll && el.scrollLeft <= 1) {
      el.scrollTo({ left: el.scrollWidth, behavior: 'smooth' });
    } else {
      el.scrollBy({ left: -el.clientWidth * 0.75, behavior: 'smooth' });
    }
  };

  const handleNext = () => {
    const el = scrollRef.current;
    if (!el) return;
    if (infiniteScroll && el.scrollLeft + el.clientWidth >= el.scrollWidth - 1) {
      el.scrollTo({ left: 0, behavior: 'smooth' });
    } else {
      el.scrollBy({ left: el.clientWidth * 0.75, behavior: 'smooth' });
    }
  };

  return (
    <div className="relative w-full rounded-lg">
      <div
        ref={scrollRef}
        onScroll={updateScrollState}
        className="overflow-x-auto scroll-smooth rounded-lg"
        style={{ scrollbarWidth: 'none' }}
      >
        <div
          className={overflowing ? 'flex justify-start' : 'flex justify-center'}
          style={{ gap: '1em' }}
        >
          {images.map((src, i) => (
            <img
              key={i}
              src={src}
              alt={`${alt} ${i + 1}`}
              className="max-h-48 object-contain cursor-pointer"
              style={{ width: '8rem', flexShrink: 0 }}
              onClick={() => onImageClick?.(src, i)}
            />
          ))}
        </div>
      </div>
      {overflowing && images.length > 1 && (
        <>
          <div className="absolute inset-y-0 left-0 flex items-center pl-1">
            <ButtonIcon
              name="chevron_left"
              onClick={handlePrev}
              title="Previous"
              className={`bg-white/80 dark:bg-gray-800/80 shadow-sm ${!infiniteScroll && atStart ? 'opacity-30 pointer-events-none' : ''}`}
            />
          </div>
          <div className="absolute inset-y-0 right-0 flex items-center pr-1">
            <ButtonIcon
              name="chevron_right"
              onClick={handleNext}
              title="Next"
              className={`bg-white/80 dark:bg-gray-800/80 shadow-sm ${!infiniteScroll && atEnd ? 'opacity-30 pointer-events-none' : ''}`}
            />
          </div>
        </>
      )}
    </div>
  );
}
