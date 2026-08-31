import React, { useCallback, useMemo, useState, useRef, useEffect, lazy, Suspense } from 'react';
import { useCollection } from '@/context/collection';
import { artworkImageUrl, artworkThumbUrl } from '@/utils/artworkUrls';
import Modal from '@/components/ui/modal';
import TextArea from '@/components/forms/textarea';
const ReplaceMockupModal = lazy(() => import('./ReplaceMockupModal'));
import Input from '@/components/forms/input';
import Select from '@/components/forms/select';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Spinner from '@/components/ui/spinner';
import Icon from '@/components/ui/icon';
const TokenCostBreakdownModal = lazy(() => import('../TokenCostBreakdownModal'));

export default function ConfigureProductImageModal({ show, onClose, onGenerate, productImage, projectBlueprintId, blueprintName }) {
  const {
    collectionId, api, projectId,
    collectionArtwork, blueprints, collectionProducts,
    setArtworkPreview,
    mockups, printifyProducts, loadMockups,
    imageModels, selectedProductImageModel,
  } = useCollection();

  const [title, setTitle] = useState('');
  const [prompt, setPrompt] = useState('');
  const [selectedModelId, setSelectedModelId] = useState('');
  const [selectedMockupIds, setSelectedMockupIds] = useState([]);
  const [includeArtworkRef, setIncludeArtworkRef] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [tokenEstimate, setTokenEstimate] = useState(null);
  const [estimateGenerations, setEstimateGenerations] = useState(null);
  const [estimatingTokens, setEstimatingTokens] = useState(false);
  const [showCostBreakdown, setShowCostBreakdown] = useState(false);
  const estimateTimerRef = useRef(null);
  const [replaceMockup, setReplaceMockup] = useState(null);
  const [replacingMockupIds, setReplacingMockupIds] = useState(new Set());
  const [mockupImageOverrides, setMockupImageOverrides] = useState({});

  // Initialize state when productImage changes
  useEffect(() => {
    if (productImage) {
      setTitle(productImage.variantColor || '');
      setPrompt(productImage.prompt || '');
      // imageModel stores the model name; find the matching ID from imageModels
      // Wait until imageModels are loaded before setting the model ID
      if (imageModels.length > 0) {
        const modelByName = imageModels.find(m => m.name === productImage.imageModel);
        if (modelByName) {
          setSelectedModelId(modelByName.id);
        } else if (selectedProductImageModel) {
          setSelectedModelId(selectedProductImageModel.id);
        } else {
          setSelectedModelId(imageModels[0].id);
        }
      }
      setSelectedMockupIds(
        (productImage.selectedMockups || '')
          .split(',')
          .map(s => s.trim())
          .filter(Boolean)
      );
      setIncludeArtworkRef(productImage.includeArtworkRef !== false);
    }
  }, [productImage, imageModels, selectedProductImageModel]);

  // Load mockups
  useEffect(() => {
    if (collectionId && mockups.length === 0) {
      loadMockups(collectionId);
    }
  }, [collectionId, mockups.length, loadMockups]);

  const isCustom = productImage && (!productImage.productImageId || productImage.productImageId === '00000000-0000-0000-0000-000000000000');

  // Get mockups — for custom images (null ProductImageId), show all mockups for all products in the collection
  const comboMockups = useMemo(() => {
    if (isCustom) {
      // Show all mockups for all printify products in the collection
      const collectionProductIds = new Set(printifyProducts.map(p => p.id));
      return mockups.filter(m => collectionProductIds.has(m.printifyProductId));
    }
    if (!projectBlueprintId) return [];
    const pp = printifyProducts.find(p => p.projectBlueprintId === projectBlueprintId);
    return mockups.filter(m => m.printifyProductId === pp?.id);
  }, [isCustom, projectBlueprintId, printifyProducts, mockups]);

  // Blueprint placements for artwork matching
  const blueprintPlacements = useMemo(() => {
    if (!projectBlueprintId) return [];
    const bp = blueprints.find(b => b.id === projectBlueprintId);
    if (!bp || !bp.placementJson) return [];
    try {
      const placementArr = JSON.parse(bp.placementJson);
      if (!placementArr || !Array.isArray(placementArr)) return [];
      return placementArr.filter(p => p.source === 'item' && p.itemId);
    } catch { return []; }
  }, [projectBlueprintId, blueprints]);

  const placementItemId = useMemo(() => {
    if (blueprintPlacements.length === 0) return null;
    return String(blueprintPlacements[0].itemId);
  }, [blueprintPlacements]);

  const [blueprintGroupIds, setBlueprintGroupIds] = useState(new Set());
  useEffect(() => {
    if (!projectId || !projectBlueprintId) { setBlueprintGroupIds(new Set()); return; }
    let cancelled = false;
    (async () => {
      try {
        const bp = blueprints.find(b => b.id === projectBlueprintId);
        if (!bp) return;
        const res = await api.getPlacementGroups(projectId, bp.blueprintId);
        if (!cancelled && res.data.success) {
          setBlueprintGroupIds(new Set((res.data.data || []).map(g => g.id)));
        }
      } catch { /* skip */ }
    })();
    return () => { cancelled = true; };
  }, [projectId, projectBlueprintId, api, blueprints]);

  // Artwork images for carousel
  const artworkImages = useMemo(() => {
    if (!collectionId || !placementItemId) return [];
    const result = [];
    const matchedPlacementKeys = new Set();
    for (const a of collectionArtwork.filter(a => a.active && String(a.itemId) === placementItemId)) {
      const artworkPlacements = a.placements || [];
      if (artworkPlacements.length > 0) {
        for (const bpPlacement of blueprintPlacements) {
          const [pw, ph] = (bpPlacement.dimensions || '').split('x').map(n => parseInt(n) || 0);
          const position = bpPlacement.position || '';
          let matched = null;
          if (position) {
            matched = artworkPlacements.find(p => {
              if (!p.position) return false;
              if (String(p.position).toLowerCase() !== String(position).toLowerCase()) return false;
              if (p.groupId) return blueprintGroupIds.has(p.groupId);
              return true;
            });
          }
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
            const key = `${a.id}_${matched.index}`;
            if (matchedPlacementKeys.has(key)) continue;
            matchedPlacementKeys.add(key);
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

  const generatedCacheBust = useMemo(() => Math.floor(Math.random() * 1000000), [productImage?.id]);
  const generatedImageUrl = productImage?.generated && productImage?.id
    ? `/api/projects/collection/${collectionId}/product-image/${productImage.id}?thumb=true&r=${generatedCacheBust}`
    : null;
  const generatedFullUrl = productImage?.generated && productImage?.id
    ? `/api/projects/collection/${collectionId}/product-image/${productImage.id}?r=${generatedCacheBust}`
    : null;

  const displayImages = useMemo(() => {
    const imgs = artworkImages.map(a => a.thumbUrl);
    if (generatedImageUrl) imgs.unshift(generatedImageUrl);
    return imgs;
  }, [artworkImages, generatedImageUrl]);
  const fullSizeImages = useMemo(() => {
    const imgs = artworkImages.map(a => a.url);
    if (generatedFullUrl) imgs.unshift(generatedFullUrl);
    return imgs;
  }, [artworkImages, generatedFullUrl]);

  const modelOptions = useMemo(() => imageModels.map(m => ({ value: m.id, label: m.name })), [imageModels]);

  const handleMockupReplaced = useCallback((mockupId, newImageUrl) => {
    setReplacingMockupIds(prev => {
      const next = new Set(prev);
      next.add(mockupId);
      return next;
    });
    // Simulate upload delay then show new image
    setTimeout(() => {
      setMockupImageOverrides(prev => ({ ...prev, [mockupId]: newImageUrl }));
      setReplacingMockupIds(prev => {
        const next = new Set(prev);
        next.delete(mockupId);
        return next;
      });
    }, 500);
  }, []);

  const toggleMockup = useCallback((mockupId) => {
    setSelectedMockupIds(prev => {
      const idStr = String(mockupId);
      if (prev.includes(idStr)) return prev.filter(id => id !== idStr);
      return [...prev, idStr];
    });
  }, []);

  // Token estimation
  useEffect(() => {
    if (!productImage || !collectionId || !projectId) return;
    if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current);
    estimateTimerRef.current = setTimeout(async () => {
      setEstimatingTokens(true);
      try {
        const res = await api.estimateProductImageTokens({
          projectId,
          collectionId,
          projectBlueprintId: productImage.projectBlueprintId || undefined,
          productImageId: productImage.productImageId || '00000000-0000-0000-0000-000000000000',
          prompt,
          variantColor: title || '',
          modelId: selectedModelId ? parseInt(selectedModelId) : (selectedProductImageModel?.id || undefined),
          mockupImageIds: selectedMockupIds,
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
        setEstimateGenerations(null);
      } finally {
        setEstimatingTokens(false);
      }
    }, 2000);
    return () => { if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current); };
  }, [productImage, collectionId, projectId, api, prompt, title, selectedModelId, selectedMockupIds, selectedProductImageModel]);

  const handleSave = useCallback(async () => {
    if (!productImage) return;
    setSaving(true);
    setError(null);
    try {
      const res = await api.updateCollectionProductImageConfig({
        id: productImage.id,
        collectionId,
        variantColor: title.trim(),
        imageModel: String(selectedModelId),
        prompt: prompt.trim(),
        selectedMockups: selectedMockupIds.join(','),
        includeArtworkRef,
      });
      if (res.data.success) {
        onClose();
      } else {
        setError(res.data.message || 'Failed to save');
      }
    } catch (err) {
      setError(err?.response?.data?.message || 'Failed to save');
    } finally {
      setSaving(false);
    }
  }, [productImage, collectionId, title, selectedModelId, prompt, selectedMockupIds, includeArtworkRef, api, onClose]);

  const handleGenerate = useCallback(async () => {
    if (!productImage) return;
    setSaving(true);
    setError(null);
    try {
      const res = await api.updateCollectionProductImageConfig({
        id: productImage.id,
        collectionId,
        variantColor: title.trim(),
        imageModel: String(selectedModelId),
        prompt: prompt.trim(),
        selectedMockups: selectedMockupIds.join(','),
        includeArtworkRef,
      });
      if (res.data.success) {
        if (onGenerate) onGenerate({ ...productImage, modelId: parseInt(selectedModelId), includeArtworkRef });
      } else {
        setError(res.data.message || 'Failed to save');
      }
    } catch (err) {
      setError(err?.response?.data?.message || 'Failed to save');
    } finally {
      setSaving(false);
    }
  }, [productImage, collectionId, title, selectedModelId, prompt, selectedMockupIds, includeArtworkRef, api, onGenerate]);

  const handleClose = () => {
    setError(null);
    onClose();
  };

  const productName = collectionProducts?.find(p => p.projectBlueprintId === productImage?.projectBlueprintId)?.name || productImage?.title || blueprintName || '';
  const subtitle = productImage?.subtitle || '';

  if (!productImage) return null;

  return (
    <Modal show={show} onClose={handleClose} title="Configure Product Image" className="w-[800px] max-w-[95vw]">
      <div className="flex flex-col gap-4 p-4 max-h-[70vh] overflow-y-auto">
        {error && <div className="text-sm text-red-500">{error}</div>}

        <div className="text-left">
          <div className="text-lg font-medium text-gray-700 dark:text-gray-300">{productName}</div>
          {subtitle && <div className="text-sm text-gray-500 dark:text-gray-400">{subtitle}</div>}
        </div>

        {displayImages.length > 0 && (
          <div className="flex flex-col items-center">
            <div className="w-full max-w-[300px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
              <Carousel
                images={displayImages}
                alt="Artwork"
                singleImage
                infiniteScroll
                imageClassName="!max-h-[250px] w-full h-full object-contain"
                onImageClick={(_src, index) => setArtworkPreview({ images: fullSizeImages, _idx: index, alt: 'Image Preview' })}
                placeholder="No Thumbnail"
              />
            </div>
          </div>
        )}

        {isCustom && (
          <div className="w-[200px]">
            <Input
              name="title"
              label="Title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Product image title"
            />
          </div>
        )}

        {comboMockups.length > 0 && (
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Select Mockup Images as References for AI</label>
            <div className="grid grid-cols-[repeat(auto-fill,120px)] gap-3 overflow-y-auto" style={{ maxHeight: '420px' }}>
              {comboMockups.map((m) => {
                const checked = selectedMockupIds.includes(String(m.id));
                const isReplacing = replacingMockupIds.has(m.id);
                const overrideUrl = mockupImageOverrides[m.id];
                const imgUrl = overrideUrl || m.imageUrl;
                return (
                  <div
                    key={m.id}
                    className="group relative w-[120px] h-[120px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600"
                  >
                    {isReplacing ? (
                      <div className="w-full h-full flex items-center justify-center bg-gray-100 dark:bg-gray-700">
                        <Spinner className="text-sm" />
                      </div>
                    ) : (
                      <img src={imgUrl} alt="Mockup" className="w-full h-full object-cover" />
                    )}
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={() => toggleMockup(m.id)}
                      className="absolute top-2 left-2 z-10 w-5 h-5 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                    />
                    {!isReplacing && (
                      <button
                        type="button"
                        onClick={() => setReplaceMockup(m)}
                        className="absolute bottom-2 right-2 z-10 px-2 py-1 text-xs font-medium text-white bg-blue-600 rounded opacity-0 group-hover:opacity-100 transition"
                      >
                        Replace
                      </button>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}

        <div className="flex items-end gap-4">
          <div>
            <Select
              label="AI Image Model"
              name="productImageModel"
              value={selectedModelId}
              onChange={(value) => setSelectedModelId(value)}
              options={modelOptions}
              fitContent
            />
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300 pb-6 whitespace-nowrap cursor-pointer">
            <input
              type="checkbox"
              checked={includeArtworkRef}
              onChange={(e) => setIncludeArtworkRef(e.target.checked)}
              className="w-4 h-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
            />
            Include Artwork Reference
          </label>
          {estimatingTokens ? (
            <div className="flex gap-2 text-sm text-gray-500 dark:text-gray-400 pb-3 ml-auto">
              <Spinner className="text-sm" />
              <span>Estimating...</span>
            </div>
          ) : tokenEstimate != null ? (
            <div className="flex flex-col items-end gap-1 pb-2 ml-auto">
              <span className="text-sm text-gray-500 dark:text-gray-400">
                Token Cost: <span className="text-white font-bold">{tokenEstimate.toLocaleString()}</span>
              </span>
              {estimateGenerations && estimateGenerations.length > 0 && (
                <ButtonOutline color="gray" size="small" onClick={() => setShowCostBreakdown(true)}>
                  Cost Breakdown
                </ButtonOutline>
              )}
            </div>
          ) : null}
        </div>

        <TextArea
          name="productImagePrompt"
          label="Product Image Prompt"
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          placeholder="Describe how the product should be presented..."
          rows={4}
        />

        <div className="flex justify-end gap-2 mt-2">
          <ButtonOutline color="gray" onClick={handleClose}>Cancel</ButtonOutline>
          <ButtonOutline color="green" onClick={handleGenerate} disabled={saving}>
            {saving ? <Spinner className="text-sm" /> : 'Generate Image'}
          </ButtonOutline>
          <ButtonOutline onClick={handleSave} disabled={saving}>
            {saving ? <Spinner className="text-sm" /> : 'Save Changes'}
          </ButtonOutline>
        </div>
      </div>

      {showCostBreakdown && estimateGenerations && (
        <Suspense fallback={null}>
          <TokenCostBreakdownModal
            generations={estimateGenerations}
            onClose={() => setShowCostBreakdown(false)}
          />
        </Suspense>
      )}

      {replaceMockup && (
        <Suspense fallback={null}>
          <ReplaceMockupModal
            show={!!replaceMockup}
            mockup={replaceMockup}
            projectId={projectId}
            collectionId={collectionId}
            onClose={() => setReplaceMockup(null)}
            onReplaced={handleMockupReplaced}
          />
        </Suspense>
      )}
    </Modal>
  );
}
