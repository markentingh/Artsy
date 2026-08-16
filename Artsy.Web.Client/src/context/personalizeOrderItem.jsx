import React, { createContext, useContext, useMemo, useState, useCallback } from 'react';

const STEPS = {
  GENERATE: 0,
  DOWNLOAD: 1,
};

const PersonalizeOrderItemContext = createContext(null);

export function PersonalizeOrderItemProvider({ children, order, orderItem, collectionProduct, onClose }) {
  const [step, setStep] = useState(STEPS.GENERATE);
  const [requestText, setRequestText] = useState('');
  const [imageModel, setImageModel] = useState('');
  const [artworks, setArtworks] = useState([]);
  const [generating, setGenerating] = useState(false);
  const [currentArtworkIndex, setCurrentArtworkIndex] = useState(0);

  const addArtwork = useCallback((artwork) => {
    setArtworks((prev) => [...prev, artwork]);
    setCurrentArtworkIndex((prev) => (prev === prev.length ? prev : prev + 1));
  }, []);

  const value = useMemo(() => ({
    STEPS,
    order,
    orderItem,
    collectionProduct,
    step,
    setStep,
    requestText,
    setRequestText,
    imageModel,
    setImageModel,
    artworks,
    setArtworks,
    addArtwork,
    generating,
    setGenerating,
    currentArtworkIndex,
    setCurrentArtworkIndex,
    onClose,
  }), [
    order,
    orderItem,
    collectionProduct,
    step,
    requestText,
    imageModel,
    artworks,
    generating,
    currentArtworkIndex,
    onClose,
  ]);

  return (
    <PersonalizeOrderItemContext.Provider value={value}>
      {children}
    </PersonalizeOrderItemContext.Provider>
  );
}

export function usePersonalizeOrderItem() {
  const ctx = useContext(PersonalizeOrderItemContext);
  if (!ctx) throw new Error('usePersonalizeOrderItem must be used within PersonalizeOrderItemProvider');
  return ctx;
}
