import React from 'react';
import Modal from '@/components/ui/modal';
import Carousel from '@/components/ui/carousel';

export default function ProductImagePreview({ show, images = [], alt, defaultIndex = 0, onClose }) {
  if (!show || images.length === 0) return null;

  return (
    <Modal title={alt || 'Product Image'} onClose={onClose} className="max-w-3xl">
      <Carousel images={images} alt={alt} singleImage defaultIndex={defaultIndex} infiniteScroll={true} />
    </Modal>
  );
}
