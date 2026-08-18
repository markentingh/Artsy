import React, { createContext, useContext, useMemo, useRef, useState, useCallback, useEffect } from 'react';
import { useSession } from '@/context/session';
import { ImageGeneration } from '@/api/user/imageGeneration';
import { PersonalizeOrder } from '@/api/user/personalizeOrder';
import { Projects } from '@/api/user/projects';

const STEPS = {
  QUESTIONS: 0,
  GENERATE: 1,
  DOWNLOAD: 2,
};

const PersonalizeOrderItemContext = createContext(null);

export function PersonalizeOrderItemProvider({ children, order, orderItem, collectionProduct, onClose }) {
  const session = useSession();
  const imageGenerationApi = useMemo(() => ImageGeneration(session), [session]);
  const personalizeApi = useMemo(() => PersonalizeOrder(session), [session]);
  const projectsApi = useMemo(() => Projects(session), [session]);
  const { getActiveModels } = imageGenerationApi;
  const [step, setStepState] = useState(STEPS.QUESTIONS);
  const [maxStepIndex, setMaxStepIndex] = useState(0);
  const [projectQuestions, setProjectQuestions] = useState([]);
  const [answers, setAnswers] = useState({});
  const [requestText, setRequestText] = useState('');
  const [imageModels, setImageModels] = useState([]);
  const [selectedImageModel, setSelectedImageModel] = useState(null);
  const [placements, setPlacements] = useState([]);
  const [collectionProductData, setCollectionProductData] = useState(collectionProduct || null);
  const [loadingPlacements, setLoadingPlacements] = useState(true);
  const [loadingQuestions, setLoadingQuestions] = useState(false);
  const [savingAnswers, setSavingAnswers] = useState(false);
  const [artworks, setArtworks] = useState([]);
  const [generating, setGenerating] = useState(false);
  const [currentArtworkIndex, setCurrentArtworkIndex] = useState(0);
  const [orderItemArtworks, setOrderItemArtworks] = useState([]);
  const hasInitializedStep = useRef(false);

  const setStep = useCallback((next) => {
    setStepState(next);
    setMaxStepIndex((prev) => Math.max(prev, next));
  }, []);

  const addArtwork = useCallback((artwork) => {
    setArtworks((prev) => [...prev, artwork]);
    setCurrentArtworkIndex((prev) => (prev === prev.length ? prev : prev + 1));
  }, []);

  const wizardSteps = useMemo(() => [
    'Project Questions',
    'Generate Personalized Artworks',
    'Download Artworks',
  ], []);

  const usedArtworks = useMemo(() => {
    const map = new Map();
    const emptyGuid = '00000000-0000-0000-0000-000000000000';
    const byArtworkItemId = new Map(orderItemArtworks.map((a) => [a.itemId, a]));
    for (const p of placements) {
      if (!p.artworkId || p.artworkId === '' || p.artworkId === emptyGuid) continue;
      if (p.source === 'custom') continue;
      const existing = byArtworkItemId.get(p.artworkItemId);
      if (map.has(p.artworkId)) {
        map.get(p.artworkId).placements.push(p);
      } else {
        map.set(p.artworkId, {
          id: p.artworkId,
          artworkItemId: p.artworkItemId,
          artworkItemTitle: p.artworkItemTitle,
          artworkItemIndex: p.artworkItemIndex ?? 0,
          artworkImageModel: p.artworkImageModel,
          artworkPrompt: p.artworkPrompt,
          sourceImageUrl: p.sourceImageUrl,
          placements: [p],
          orderItemArtworkId: existing?.id,
          accepted: existing?.accepted === true,
        });
      }
    }
    return Array.from(map.values()).sort((a, b) => a.artworkItemIndex - b.artworkItemIndex);
  }, [placements, orderItemArtworks]);

  useEffect(() => {
    if (usedArtworks.length === 0 || !orderItem?.id) return;
    const byArtworkItemId = new Map(orderItemArtworks.map((a) => [a.itemId, a]));
    const next = usedArtworks.map((u) => {
      const a = byArtworkItemId.get(u.artworkItemId);
      if (!a) return undefined;
      return {
        id: a.id,
        url: `/api/orders/order-items/${orderItem.id}/artworks/${a.id}`,
        prompt: a.prompt,
        width: a.width,
        height: a.height,
        status: a.accepted ? 'accepted' : 'done',
      };
    });
    setArtworks(next);
    const firstPending = usedArtworks.findIndex((u) => !u.accepted);
    setCurrentArtworkIndex(firstPending >= 0 ? firstPending : 0);
  }, [usedArtworks, orderItemArtworks, orderItem?.id]);

  useEffect(() => {
    const u = usedArtworks[currentArtworkIndex];
    if (!u || !u.artworkItemId) return;
    const a = orderItemArtworks.find((x) => x.itemId === u.artworkItemId);
    setRequestText(a?.requestText || '');
  }, [currentArtworkIndex, usedArtworks, orderItemArtworks]);

  const goBack = useCallback(() => {
    if (step === STEPS.GENERATE) {
      if (currentArtworkIndex > 0) {
        setCurrentArtworkIndex(currentArtworkIndex - 1);
      } else {
        setStep(STEPS.QUESTIONS);
      }
    } else if (step === STEPS.DOWNLOAD) {
      setStep(STEPS.GENERATE);
      setCurrentArtworkIndex(usedArtworks.length - 1);
    }
  }, [step, currentArtworkIndex, usedArtworks.length, setStep, setCurrentArtworkIndex]);

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
    personalizeApi.getOrderItemPlacements(order.order.id, orderItem.id).then((res) => {
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
  }, [order?.order?.id, orderItem?.id, personalizeApi, projectsApi]);

  useEffect(() => {
    if (!order?.order?.id || !orderItem?.id) return;
    let cancelled = false;
    personalizeApi.getOrderItemArtworks(order.order.id, orderItem.id).then((res) => {
      if (cancelled) return;
      if (res.data?.success) {
        setOrderItemArtworks(res.data.data || []);
      }
    }).catch(() => {});
    return () => { cancelled = true; };
  }, [order?.order?.id, orderItem?.id, personalizeApi]);

  useEffect(() => {
    if (!order?.order?.id || !orderItem?.id) return;
    let cancelled = false;
    setLoadingQuestions(true);
    personalizeApi.getProjectQuestions(order.order.id, orderItem.id).then((res) => {
      if (cancelled) return;
      if (res.data?.success) {
        const data = res.data.data || {};
        const questions = data.questions || [];
        const answerList = data.answers || [];
        const answersById = {};
        for (const a of answerList) {
          answersById[a.questionId] = a.answer;
        }
        setProjectQuestions(questions);
        setAnswers(answersById);
      }
    }).catch(() => {}).finally(() => {
      if (!cancelled) setLoadingQuestions(false);
    });
    return () => { cancelled = true; };
  }, [order?.order?.id, orderItem?.id, personalizeApi]);

  useEffect(() => {
    if (hasInitializedStep.current) return;
    if (loadingQuestions || loadingPlacements) return;
    if (usedArtworks.length === 0) return;
    hasInitializedStep.current = true;

    const allAnswered = projectQuestions.length === 0 || projectQuestions.every((q) => !!answers[q.id]?.trim());
    const allAccepted = usedArtworks.every((u, i) => u.accepted || artworks[i]?.status === 'accepted');

    if (allAccepted) {
      setStep(STEPS.DOWNLOAD);
    } else if (allAnswered) {
      setStep(STEPS.GENERATE);
    }
  }, [loadingQuestions, loadingPlacements, projectQuestions, answers, usedArtworks, artworks, setStep, STEPS.DOWNLOAD, STEPS.GENERATE]);

  const saveAnswers = useCallback(async () => {
    if (!order?.order?.id || !orderItem?.id) return false;
    setSavingAnswers(true);
    try {
      const answersPayload = [];
      for (const question of projectQuestions) {
        const answer = answers[question.id] || '';
        answersPayload.push({ questionId: question.id, itemId: null, answer });
      }
      const res = await personalizeApi.saveProjectQuestions(order.order.id, orderItem.id, answersPayload);
      return res.data?.success === true;
    } catch {
      return false;
    } finally {
      setSavingAnswers(false);
    }
  }, [order?.order?.id, orderItem?.id, projectQuestions, answers, personalizeApi]);

  const value = useMemo(() => ({
    STEPS,
    order,
    orderItem,
    collectionProduct: collectionProductData,
    placements,
    usedArtworks,
    personalizeApi,
    projectsApi,
    loadingPlacements,
    step,
    setStep,
    goBack,
    maxStepIndex,
    wizardSteps,
    projectQuestions,
    answers,
    setAnswers,
    saveAnswers,
    loadingQuestions,
    savingAnswers,
    requestText,
    setRequestText,
    imageModels,
    selectedImageModel,
    setSelectedImageModel,
    artworks,
    setArtworks,
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
    personalizeApi,
    projectsApi,
    loadingPlacements,
    step,
    goBack,
    maxStepIndex,
    wizardSteps,
    projectQuestions,
    answers,
    saveAnswers,
    loadingQuestions,
    savingAnswers,
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
