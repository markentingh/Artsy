import React, { useState, useMemo, useCallback, useEffect, useRef } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { Printify } from '@/api/user/printify';
import { PrintifyProducts } from '@/api/user/printifyProducts';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Icon from '@/components/ui/icon';

export default function SelectProductsStep() {
  const session = useSession();
  const {
    project, blueprints, collectionId, collectionProducts,
    STEPS, setStep, onClose, goBack,
    setMessage, ensureCollection, api,
    projectQuestions, collectionArtwork, aiItems, blueprintItemIds,
    setCurrentItemIndex, loadItemData,
    setCollectionProducts, setPrintifyProducts, setAllProductImages, setProductBlueprintImages,
  } = useCollection();

  const { getBlueprintImageUrl } = Printify(session);
  const printifyApi = useMemo(() => Printify(session), [session]);
  const getBlueprintImages = printifyApi.getBlueprintImages;
  const printifyProductsApi = useMemo(() => PrintifyProducts(session), [session]);

  const [blueprintImageMap, setBlueprintImageMap] = useState({});
  const [checkMap, setCheckMap] = useState({});
  const [saving, setSaving] = useState(false);

  // Track which blueprint IDs we've already loaded images for
  const loadedBlueprintIds = useRef(new Set());

  // Load blueprint images for all blueprints
  useEffect(() => {
    let cancelled = false;
    const toLoad = blueprints.filter(bp => !loadedBlueprintIds.current.has(bp.blueprintId));
    if (toLoad.length === 0) return;

    (async () => {
      const imgMap = {};
      for (const bp of toLoad) {
        try {
          const imgResp = await getBlueprintImages(bp.blueprintId);
          if (imgResp.data.success) {
            imgMap[bp.blueprintId] = imgResp.data.data || [];
            loadedBlueprintIds.current.add(bp.blueprintId);
          }
        } catch { /* ignore */ }
      }
      if (!cancelled) setBlueprintImageMap(prev => ({ ...prev, ...imgMap }));
    })();
    return () => { cancelled = true; };
  }, [blueprints, getBlueprintImages]);

  // Initialize check state: checked by default if no record exists in collectionProducts
  useEffect(() => {
    if (blueprints.length === 0) return;
    const existingMap = {};
    for (const cp of collectionProducts) {
      existingMap[cp.projectBlueprintId] = cp.active;
    }
    const next = {};
    for (const bp of blueprints) {
      if (bp.id in existingMap) {
        next[bp.id] = existingMap[bp.id];
      } else {
        next[bp.id] = true; // checked by default if no record
      }
    }
    setCheckMap(next);
  }, [blueprints, collectionProducts]);

  const handleToggle = useCallback((bpId) => {
    setCheckMap(prev => ({ ...prev, [bpId]: !prev[bpId] }));
  }, []);

  const handleNext = useCallback(async () => {
    setSaving(true);
    setMessage(null);
    try {
      const colId = collectionId || await ensureCollection();
      if (!colId) {
        setSaving(false);
        return;
      }

      const products = blueprints.map(bp => ({
        projectBlueprintId: bp.id,
        active: checkMap[bp.id] !== false,
      }));

      await api.saveCollectionProducts({ collectionId: colId, products });

      // Reload all dependent state so downstream steps reflect the updated selection
      const [cpRes, ppRes, prodImgRes, pbImgRes] = await Promise.all([
        api.getCollectionProducts(colId),
        printifyProductsApi.getByCollection(colId),
        api.getProductImages(colId),
        api.getAllProductBlueprintImages(project.id),
      ]);
      if (cpRes.data.success) setCollectionProducts(cpRes.data.data || []);
      if (ppRes.data.success) setPrintifyProducts(ppRes.data.data || []);
      if (prodImgRes.data.success) setAllProductImages((prodImgRes.data.data || []).filter(img => img.active));
      if (pbImgRes.data.success) setProductBlueprintImages(pbImgRes.data.data || []);

      // Skip project questions step if there are no project questions
      if (projectQuestions.length === 0) {
        const acceptedItemIds = new Set(
          collectionArtwork.filter(a => a.accepted).map(a => String(a.itemId))
        );
        const firstBlueprintItemIndex = aiItems.findIndex(item =>
          blueprintItemIds.has(String(item.id)) &&
          !acceptedItemIds.has(String(item.id))
        );
        if (firstBlueprintItemIndex === -1) {
          setStep(STEPS.READY_TO_GENERATE);
        } else {
          setCurrentItemIndex(firstBlueprintItemIndex);
          loadItemData(firstBlueprintItemIndex);
        }
      } else {
        setStep(STEPS.PROJECT_QUESTIONS);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save product selection' });
    } finally {
      setSaving(false);
    }
  }, [collectionId, ensureCollection, blueprints, checkMap, api, setStep, STEPS, setMessage, projectQuestions, collectionArtwork, aiItems, blueprintItemIds, setCurrentItemIndex, loadItemData, project, printifyProductsApi, setCollectionProducts, setPrintifyProducts, setAllProductImages, setProductBlueprintImages]);

  if (blueprints.length === 0) {
    return (
      <div className="flex flex-col h-full">
        <p className="text-center text-lg mb-4">
          No products configured for this project. Please add product blueprints first.
        </p>
        <div className="buttons flex justify-end gap-2 mt-auto">
          <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-lg mb-4">
        Select which products to include in this collection.
      </p>
      <div className="grid w-full gap-4 mb-6 overflow-y-auto" style={{ gridTemplateColumns: 'repeat(auto-fill, 240px)', justifyContent: 'center' }}>
        {blueprints.map((bp) => {
          const cfg = (() => {
            try { return typeof bp.blueprintJson === 'string' ? JSON.parse(bp.blueprintJson || '{}') : (bp.blueprintJson || {}); } catch { return {}; }
          })();
          const selectedColors = new Set((cfg.variantColors || []).map(String));
          const imgData = blueprintImageMap[bp.blueprintId] || [];
          const matchingIndices = imgData
            .filter(img => (img.variantColors || []).some(c => selectedColors.has(String(c))))
            .map(img => img.imageIndex);
          const images = matchingIndices.map(i => getBlueprintImageUrl(bp.blueprintId, i, true));
          const isChecked = checkMap[bp.id] !== false;
          return (
            <div
              key={bp.id}
              onClick={() => handleToggle(bp.id)}
              className={`group bg-gray-50 dark:bg-gray-700 rounded-lg shadow p-4 hover:shadow-md transition cursor-pointer ${!isChecked ? 'opacity-60' : ''}`}
            >
              <div className="w-[200px] h-[200px] mx-auto mb-3 rounded-lg overflow-hidden bg-gray-100 dark:bg-gray-600 flex items-center justify-center">
                <Carousel
                  images={images}
                  alt={bp.name}
                  singleImage
                  infiniteScroll
                  placeholder="No Image"
                  imageClassName="!max-w-[200px] object-contain"
                  maxHeight="200px"
                />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={isChecked}
                    onChange={() => handleToggle(bp.id)}
                    onClick={(e) => e.stopPropagation()}
                    className="w-4 h-4 accent-blue-600 cursor-pointer flex-shrink-0"
                  />
                  <p className="text-sm font-medium truncate" title={bp.name}>{bp.name}</p>
                </div>
                <div className="flex items-center justify-between mt-1">
                  <span className="text-sm text-gray-500 dark:text-gray-400">
                    {bp.minPrice != null ? `$${Number(bp.minPrice).toFixed(2)}` : 'No price set'}
                  </span>
                </div>
              </div>
            </div>
          );
        })}
      </div>
      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext} disabled={saving}>
          {saving ? (
            <>
              <Icon name="progress_activity" spin className="w-4 h-4 inline mr-1" />
              Saving...
            </>
          ) : (
            'Next'
          )}
        </ButtonOutline>
      </div>
    </div>
  );
}
