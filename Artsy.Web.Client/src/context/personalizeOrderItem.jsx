import React, { createContext, useContext, useMemo, useState, useCallback, useEffect } from 'react';
import { useSession } from '@/context/session';
import { ImageGeneration } from '@/api/user/imageGeneration';
import { Orders } from '@/api/orders';
import { Projects } from '@/api/user/projects';

const STEPS = {
  GENERATE: 0,
  DOWNLOAD: 1,
};

const PersonalizeOrderItemContext = createContext(null);

export function PersonalizeOrderItemProvider({ children, order, orderItem, collectionProduct, onClose }) {
  const session = useSession();
  const imageGenerationApi = useMemo(() => ImageGeneration(session), [session]);
  const ordersApi = useMemo(() => Orders(session), [session]);
  const projectsApi = useMemo(() => Projects(session), [session]);
  const { getActiveModels } = imageGenerationApi;
  const [step, setStepState] = useState(STEPS.GENERATE);
  const [maxStepIndex, setMaxStepIndex] = useState(0);
  const [requestText, setRequestText] = useState('');
  const [imageModels, setImageModels] = useState([]);
  const [selectedImageModel, setSelectedImageModel] = useState(null);
  const [placements, setPlacements] = useState([]);
  const [collectionProductData, setCollectionProductData] = useState(collectionProduct || null);
  const [loadingPlacements, setLoadingPlacements] = useState(true);
  const [artworks, setArtworks] = useState([]);
  const [generating, setGenerating] = useState(false);
  const [currentArtworkIndex, setCurrentArtworkIndex] = useState(0);

  const setStep = useCallback((next) => {
    setStepState(next);
    setMaxStepIndex((prev) => Math.max(prev, next));
  }, []);

  const addArtwork = useCallback((artwork) => {
    setArtworks((prev) => [...prev, artwork]);
    setCurrentArtworkIndex((prev) => (prev === prev.length ? prev : prev + 1));
  }, []);

  const wizardSteps = useMemo(() => [
    'Generate Personalized Artworks',
    'Download Artworks',
  ], []);

  const usedArtworks = useMemo(() => {
    const map = new Map();
    const emptyGuid = '00000000-0000-0000-0000-000000000000';
    for (const p of placements) {
      if (!p.artworkId || p.artworkId === '' || p.artworkId === emptyGuid) continue;
      if (p.source === 'custom') continue;
      if (map.has(p.artworkId)) {
        map.get(p.artworkId).placements.push(p);
      } else {
        map.set(p.artworkId, {
          id: p.artworkId,
          artworkItemId: p.artworkItemId,
          artworkItemTitle: p.artworkItemTitle,
          artworkImageModel: p.artworkImageModel,
          artworkPrompt: p.artworkPrompt,
          sourceImageUrl: p.sourceImageUrl,
          placements: [p],
        });
      }
    }
    return Array.from(map.values());
  }, [placements]);

  useEffect(() => {
    let cancelled = false;
    getActiveModels().then((res) => {
      if (cancelled) return;
      if (res.data?.success) {
        const models = res.data.data || [];
        setImageModels(models);
        if (models.length > 0 && !selectedImageModel) {
          setSelectedImageModel(models[0]);
        }
      }
    }).catch(() => {});
    return () => { cancelled = true; };
  }, [getActiveModels]);

  useEffect(() => {
    if (!order?.order?.id || !orderItem?.id) return;
    let cancelled = false;
    setLoadingPlacements(true);
    ordersApi.getOrderItemPlacements(order.order.id, orderItem.id).then((res) => {
      if (cancelled) return;
      if (res.data?.success) {
        const data = res.data.data || {};
        const cp = data.collectionProduct;
        const placementList = data.placements || [];
        const collectionId = cp?.collectionId;
        setCollectionProductData(cp);
        setPlacements(placementList.map((p) => ({
          ...p,
          sourceImageUrl: collectionId && p.artworkItemId && p.artworkId
            ? projectsApi.getCollectionArtworkThumbUrl(collectionId, p.artworkItemId, p.artworkId)
            : '',
        })));
      }
    }).catch(() => {}).finally(() => {
      if (!cancelled) setLoadingPlacements(false);
    });
    return () => { cancelled = true; };
  }, [order?.order?.id, orderItem?.id, ordersApi, projectsApi]);

  const value = useMemo(() => ({
    STEPS,
    order,
    orderItem,
    collectionProduct: collectionProductData,
    placements,
    usedArtworks,
    ordersApi,
    loadingPlacements,
    step,
    setStep,
    maxStepIndex,
    wizardSteps,
    requestText,
    setRequestText,
    imageModels,
    selectedImageModel,
    setSelectedImageModel,
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
    collectionProductData,
    placements,
    usedArtworks,
    ordersApi,
    loadingPlacements,
    step,
    maxStepIndex,
    wizardSteps,
    requestText,
    imageModels,
    selectedImageModel,
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
