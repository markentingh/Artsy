import React, { useCallback, useMemo, useState, useRef, useEffect, lazy, Suspense } from 'react';
import { useCollection } from '@/context/collection';
import { useDashboard } from '@/context/dashboard';
import { artworkImageUrl, artworkThumbUrl } from '@/utils/artworkUrls';
import TextArea from '@/components/forms/textarea';
import Select from '@/components/forms/select';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Spinner from '@/components/ui/spinner';
const TokenCostBreakdownModal = lazy(() => import('../TokenCostBreakdownModal'));

export default function ProductImagePrompt() {
  const {
    productImagePrompt, setProductImagePrompt,
    setProductImageGenerateTrigger,
    selectedProductCombos,
    currentProductComboIndex,
    setStep, setMessage, STEPS, onClose, goBack,
    collectionId, api, projectId,
    setSelectedProductCombos, setCurrentProductComboIndex,
    collectionArtwork, blueprints,
    allProductImages,
    setArtworkPreview,
    mockups, printifyProducts, loadMockups,
    imageModels, selectedProductImageModel, setSelectedProductImageModel,
  } = useCollection();
  const { refreshTokens } = useDashboard();

  const combo = selectedProductCombos[currentProductComboIndex];

  useEffect(() => {
    if (collectionId && mockups.length === 0) {
      loadMockups(collectionId);
    }
  }, [collectionId, mockups.length, loadMockups]);

  const comboMockups = useMemo(() => {
    if (!combo) return [];
    const pp = printifyProducts.find(p => p.projectBlueprintId === combo.projectBlueprintId);
    const productMockups = mockups.filter(m => m.printifyProductId === pp?.id);
    const comboVariantIds = new Set((combo.variantIds || []).map(String));
    if (comboVariantIds.size === 0) return productMockups;
    return productMockups.filter(m => {
      const mockupVariantIds = new Set((m.variantIds || '').split(',').map(s => s.trim()).filter(Boolean));
      for (const id of comboVariantIds) {
        if (mockupVariantIds.has(id)) return true;
      }
      return false;
    });
  }, [combo, printifyProducts, mockups]);

  useEffect(() => {
    if (!combo || comboMockups.length === 0 || combo.selectedMockupImageIds !== undefined) return;
    setSelectedProductCombos(prev => prev.map((c, i) =>
      i === currentProductComboIndex ? { ...c, selectedMockupImageIds: [] } : c
    ));
  }, [combo, comboMockups, currentProductComboIndex, setSelectedProductCombos]);

  const toggleMockup = useCallback((mockupId) => {
    if (!combo) return;
    setSelectedProductCombos(prev => prev.map((c, i) => {
      if (i !== currentProductComboIndex) return c;
      const ids = new Set(c.selectedMockupImageIds || []);
      if (ids.has(mockupId)) {
        ids.delete(mockupId);
      } else {
        ids.add(mockupId);
      }
      return { ...c, selectedMockupImageIds: Array.from(ids) };
    }));
  }, [combo, currentProductComboIndex, setSelectedProductCombos]);

  const modelOptions = useMemo(() => imageModels.map(m => ({ value: m.id, label: m.name })), [imageModels]);

  const [thumbRetried, setThumbRetried] = useState({});
  const [thumbFailed, setThumbFailed] = useState({});
  const retryRef = useRef({});
  const [tokenEstimate, setTokenEstimate] = useState(null);
  const [estimateGenerations, setEstimateGenerations] = useState(null);
  const [estimatingTokens, setEstimatingTokens] = useState(false);
  const [showCostBreakdown, setShowCostBreakdown] = useState(false);
  const estimateTimerRef = useRef(null);

  const blueprintPlacements = useMemo(() => {
    if (!combo) return [];
    const bp = blueprints.find(b => b.id === combo.projectBlueprintId);
    if (!bp || !bp.placementJson) return [];
    try {
      const placementArr = JSON.parse(bp.placementJson);
      if (!placementArr || !Array.isArray(placementArr)) return [];
      return placementArr.filter(p => p.source === 'item' && p.itemId);
    } catch { /* skip */ }
    return [];
  }, [combo, blueprints]);

  const placementItemId = useMemo(() => {
    if (blueprintPlacements.length === 0) return null;
    return String(blueprintPlacements[0].itemId);
  }, [blueprintPlacements]);

  // Fetch placement group IDs for this blueprint so we can filter group placements
  const [blueprintGroupIds, setBlueprintGroupIds] = useState(new Set());
  useEffect(() => {
    if (!projectId || !combo) { setBlueprintGroupIds(new Set()); return; }
    let cancelled = false;
    (async () => {
      try {
        const res = await api.getPlacementGroups(projectId, combo.blueprintId);
        if (!cancelled && res.data.success) {
          setBlueprintGroupIds(new Set((res.data.data || []).map(g => g.id)));
        }
      } catch { /* skip */ }
    })();
    return () => { cancelled = true; };
  }, [projectId, combo, api]);

  const artworkImages = useMemo(() => {
    if (!collectionId || !placementItemId) return [];
    const result = [];
    for (const a of collectionArtwork.filter(a => a.active && String(a.itemId) === placementItemId)) {
      const artworkPlacements = a.placements || [];
      if (artworkPlacements.length > 0) {
        // Only show placement images that match this product's blueprint placements
        for (const bpPlacement of blueprintPlacements) {
          const [pw, ph] = (bpPlacement.dimensions || '').split('x').map(n => parseInt(n) || 0);
          const position = bpPlacement.position || '';

          // Match by position (for group placements, only if groupId belongs to this blueprint), then by aspect ratio
          let matched = artworkPlacements.find(p =>
            p.groupId && p.position && position &&
            blueprintGroupIds.has(p.groupId) &&
            String(p.position).toLowerCase() === String(position).toLowerCase()
          );

          if (!matched && pw > 0 && ph > 0) {
            const placementRatio = pw / ph;
            matched = artworkPlacements.find(p => {
              if (!p.groupId && p.width > 0 && p.height > 0) {
                return Math.abs((p.width / p.height) - placementRatio) < 0.01;
              }
              return false;
            });
          }

          if (matched) {
            result.push({
              itemId: a.itemId,
              artworkId: a.id,
              placementIndex: matched.index,
              url: artworkImageUrl(collectionId, a.itemId, a.id, { placementIndex: matched.index }),
              thumbUrl: artworkThumbUrl(collectionId, a.itemId, a.id, { placementIndex: matched.index }),
            });
          }
        }
      } else {
        // No placement variants — show the base artwork
        result.push({
          itemId: a.itemId,
          artworkId: a.id,
          placementIndex: null,
          url: artworkImageUrl(collectionId, a.itemId, a.id),
          thumbUrl: artworkThumbUrl(collectionId, a.itemId, a.id),
        });
      }
    }
    return result;
  }, [collectionId, collectionArtwork, placementItemId, blueprintPlacements, blueprintGroupIds]);

  const existingProductImage = useMemo(() => {
    if (!combo || !collectionId) return null;
    const img = allProductImages.find(i =>
      i.projectBlueprintId === combo.projectBlueprintId &&
      i.productImageId === combo.productImageId
    );
    if (!img || !img.accepted) return null;
    return `/api/projects/collection/${collectionId}/product-image/${img.id}`;
  }, [combo, allProductImages, collectionId]);

  const allImages = useMemo(() => {
    const productImg = existingProductImage ? [`${existingProductImage}?thumb=true`] : [];
    return [...productImg, ...artworkImages.map(a => a.thumbUrl)];
  }, [existingProductImage, artworkImages]);

  const allFullSizeImages = useMemo(() => {
    const productImg = existingProductImage ? [existingProductImage] : [];
    return [...productImg, ...artworkImages.map(a => a.url)];
  }, [existingProductImage, artworkImages]);

  const handleImageError = useCallback(async (index) => {
    if (retryRef.current[index]) return;
    retryRef.current[index] = true;

    const productImgCount = existingProductImage ? 1 : 0;
    const artworkIndex = index - productImgCount;
    const artwork = artworkImages[artworkIndex];
    if (!artwork || !collectionId) {
      setThumbFailed(prev => ({ ...prev, [index]: true }));
      return;
    }

    try {
      const res = await api.generateArtworkThumbnail({ collectionId, itemId: artwork.itemId });
      if (res.data.success) {
        setThumbRetried(prev => ({ ...prev, [index]: Date.now() }));
        refreshTokens();
      } else {
        setThumbFailed(prev => ({ ...prev, [index]: true }));
      }
    } catch {
      setThumbFailed(prev => ({ ...prev, [index]: true }));
    }
  }, [artworkImages, existingProductImage, collectionId, api, refreshTokens]);

  const { displayImages, fullSizeImages } = useMemo(() => {
    const thumbs = [];
    const fulls = [];
    allImages.forEach((url, i) => {
      if (thumbFailed[i]) return;
      if (thumbRetried[i]) {
        thumbs.push(`${url}&r=${thumbRetried[i]}`);
        fulls.push(`${allFullSizeImages[i]}${allFullSizeImages[i].includes('?') ? '&' : '?'}r=${thumbRetried[i]}`);
      } else {
        thumbs.push(url);
        fulls.push(allFullSizeImages[i]);
      }
    });
    return { displayImages: thumbs, fullSizeImages: fulls };
  }, [allImages, allFullSizeImages, thumbRetried, thumbFailed]);

  useEffect(() => {
    if (!combo || !collectionId || !projectId) return;
    if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current);
    estimateTimerRef.current = setTimeout(async () => {
      setEstimatingTokens(true);
      try {
        const res = await api.estimateProductImageTokens({
          projectId,
          collectionId,
          projectBlueprintId: combo.projectBlueprintId,
          productImageId: combo.productImageId,
          prompt: productImagePrompt,
          variantColor: combo.variantColor || '',
          modelId: selectedProductImageModel?.id,
          mockupImageIds: combo.selectedMockupImageIds || [],
        });
        if (res.data.success) {
          const data = res.data.data;
          const total = typeof data === 'number' ? data : data.totalTokens;
          setTokenEstimate(total);
          setEstimateGenerations(data?.generations || null);
        } else {
          setTokenEstimate(null);
          setEstimateGenerations(null);
        }
      } catch {
        setTokenEstimate(null);
      } finally {
        setEstimatingTokens(false);
      }
    }, 2000);
    return () => { if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current); };
  }, [combo, collectionId, projectId, api, productImagePrompt, selectedProductImageModel, comboMockups]);

  const handleNext = useCallback(() => {
    if (!productImagePrompt.trim()) {
      setMessage({ type: 'error', text: 'Enter a product image prompt.' });
      return;
    }
    setProductImageGenerateTrigger(prev => prev + 1);
    setStep(STEPS.PRODUCT_IMAGE_PREVIEW);
  }, [productImagePrompt, setStep, setMessage, STEPS, setProductImageGenerateTrigger]);

  const moveToNextCombo = useCallback(() => {
    const nextIndex = currentProductComboIndex >= selectedProductCombos.length - 1
      ? selectedProductCombos.length - 1
      : currentProductComboIndex;
    if (nextIndex >= selectedProductCombos.length - 1) {
      setStep(STEPS.PUBLISH_PRODUCTS);
    } else {
      setCurrentProductComboIndex(nextIndex + 1);
    }
  }, [currentProductComboIndex, selectedProductCombos.length, setStep, STEPS, setCurrentProductComboIndex]);

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-sm text-gray-600 dark:text-gray-400 mb-4">
        {selectedProductCombos.length} product image{selectedProductCombos.length !== 1 ? 's' : ''} to generate.
      </p>

      {combo && (
        <div className="flex flex-col items-center mb-4">
          <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            {combo.blueprintName} — {combo.title} - {combo.variantColor}
          </h4>
          {displayImages.length > 0 && (
            <div className="w-full max-w-[300px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-2">
              <Carousel
                images={displayImages}
                alt="Artwork & Product"
                singleImage
                infiniteScroll
                imageClassName="!max-h-[350px] w-full h-full object-contain"
                onImageError={handleImageError}
                onImageClick={(_src, index) => setArtworkPreview({ images: fullSizeImages, _idx: index, alt: 'Image Preview' })}
                placeholder="No Thumbnail"
              />
            </div>
          )}
          {displayImages.length === 0 && (
            <div className="w-full max-w-[300px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-2 p-8 text-center">
              <span className="text-sm text-gray-500 dark:text-gray-400">No Thumbnail</span>
            </div>
          )}
        </div>
      )}

      {combo && comboMockups.length > 0 && (
        <div className="mb-4 w-full">
          <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Select Mockup Images as References for AI</h4>
          <div className="grid grid-cols-[repeat(auto-fill,150px)] gap-3">
            {comboMockups.map((m) => {
              const checked = (combo?.selectedMockupImageIds || []).includes(m.id);
              return (
                <div
                  key={m.id}
                  className="relative w-[150px] h-[150px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600"
                >
                  <img
                    src={m.imageUrl}
                    alt="Mockup"
                    className="w-full h-full object-cover"
                  />
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={() => toggleMockup(m.id)}
                    className="absolute top-2 left-2 z-10 w-5 h-5 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                  />
                </div>
              );
            })}
          </div>
        </div>
      )}

      <div className="mb-4">
        <div className="flex items-end justify-between gap-3 mb-2">
          <Select
            label="AI Image Model"
            name="productImageModel"
            value={selectedProductImageModel?.id || ''}
            onChange={(value) => {
              const model = imageModels.find(m => m.id === value);
              setSelectedProductImageModel(model || null);
            }}
            options={modelOptions}
            fitContent
          />
              {estimatingTokens ? (
                <div className="flex items-center gap-1 text-sm text-gray-500 dark:text-gray-400" style={{ marginBottom: '2em' }}>
                  <Spinner className="text-sm" />
                  <span>Estimating...</span>
                </div>
              ) : tokenEstimate != null ? (
                <div className="flex items-center gap-3" style={{ marginBottom: '2em' }}>
                  {estimateGenerations && estimateGenerations.length > 0 && (
                    <ButtonOutline color="gray" size="small" onClick={() => setShowCostBreakdown(true)}>
                      Cost Breakdown
                    </ButtonOutline>
                  )}
                  <div className="text-sm text-gray-500 dark:text-gray-400">
                    <span className="font-medium">Token Cost: <span className="text-white font-bold">{tokenEstimate.toLocaleString()}</span></span>
                  </div>
                </div>
              ) : null}
        </div>
        <TextArea
          name="productImagePrompt"
          label="Product Image Prompt"
          value={productImagePrompt}
          onChange={(e) => setProductImagePrompt(e.target.value)}
          placeholder="Describe how the product should be presented..."
          rows={4}
        />
      </div>

      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext} disabled={!productImagePrompt.trim()}>
          Generate Image
        </ButtonOutline>
      </div>
      {showCostBreakdown && (
        <Suspense fallback={null}>
          <TokenCostBreakdownModal
            show={showCostBreakdown}
            onClose={() => setShowCostBreakdown(false)}
            generations={estimateGenerations}
          />
        </Suspense>
      )}
    </div>
  );
}
