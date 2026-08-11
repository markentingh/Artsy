import React, { useCallback, useMemo, useState, useRef, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import { useDashboard } from '@/context/dashboard';
import TextArea from '@/components/forms/textarea';
import Select from '@/components/forms/select';
import Input from '@/components/forms/input';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Spinner from '@/components/ui/spinner';

export default function ProductImagePrompt() {
  const {
    productImagePrompt, setProductImagePrompt,
    selectedProductCombos,
    currentProductComboIndex,
    setStep, setMessage, STEPS, onClose,
    collectionId, api, projectId,
    setSelectedProductCombos, setCurrentProductComboIndex,
    collectionArtwork, blueprints,
    allProductImages,
    setArtworkPreview,
    mockups, printifyProducts, loadMockups,
    imageModels, selectedProductImageModel, setSelectedProductImageModel,
    collectionProducts, setCollectionProducts,
  } = useCollection();
  const { refreshTokens } = useDashboard();

  const combo = selectedProductCombos[currentProductComboIndex];

  // Product name: load from collectionProducts, default to blueprint name
  const collectionProduct = useMemo(() => {
    if (!combo) return null;
    return collectionProducts.find(p => p.projectBlueprintId === combo.projectBlueprintId);
  }, [combo, collectionProducts]);

  const [productName, setProductName] = useState('');
  const productNameTimerRef = useRef(null);

  useEffect(() => {
    if (!combo) return;
    const bp = blueprints.find(b => b.id === combo.projectBlueprintId);
    const existing = collectionProduct?.name;
    setProductName(existing || bp?.name || '');
  }, [combo, collectionProduct, blueprints]);

  const handleProductNameChange = useCallback((e) => {
    const value = e.target.value;
    setProductName(value);
    if (!collectionId || !combo) return;
    if (productNameTimerRef.current) clearTimeout(productNameTimerRef.current);
    productNameTimerRef.current = setTimeout(async () => {
      try {
        await api.updateCollectionProductName({
          collectionId,
          projectBlueprintId: combo.projectBlueprintId,
          name: value,
        });
        setCollectionProducts(prev => prev.map(p =>
          p.projectBlueprintId === combo.projectBlueprintId
            ? { ...p, name: value }
            : p
        ));
      } catch { /* ignore */ }
    }, 1000);
  }, [collectionId, combo, api, setCollectionProducts]);

  useEffect(() => () => {
    if (productNameTimerRef.current) clearTimeout(productNameTimerRef.current);
  }, []);

  useEffect(() => {
    if (collectionId && mockups.length === 0) {
      loadMockups(collectionId);
    }
  }, [collectionId, mockups.length, loadMockups]);

  const mockupImages = useMemo(() => {
    if (!combo) return [];
    // If mockups exist for this product, use them
    if (printifyProducts.length && mockups.length) {
      const pp = printifyProducts.find(p => p.projectBlueprintId === combo.projectBlueprintId);
      if (pp) {
        const imgs = mockups.filter(m => m.printifyProductId === pp.id).map(m => m.imageUrl);
        if (imgs.length > 0) return imgs;
      }
    }
    // Fallback: use the Printify blueprint image for the variant color
    if (combo.printifyImageUrl) return [combo.printifyImageUrl];
    return [];
  }, [combo, printifyProducts, mockups]);

  const modelOptions = useMemo(() => imageModels.map(m => ({ value: m.id, label: m.name })), [imageModels]);

  const [thumbRetried, setThumbRetried] = useState({});
  const [thumbFailed, setThumbFailed] = useState({});
  const retryRef = useRef({});
  const [tokenEstimate, setTokenEstimate] = useState(null);
  const [estimatingTokens, setEstimatingTokens] = useState(false);
  const estimateTimerRef = useRef(null);

  const placementItemId = useMemo(() => {
    if (!combo) return null;
    const bp = blueprints.find(b => b.id === combo.projectBlueprintId);
    if (!bp || !bp.placementJson) return null;
    try {
      const placementArr = JSON.parse(bp.placementJson);
      if (!placementArr || !Array.isArray(placementArr) || placementArr.length === 0) return null;
      const placement = placementArr[0];
      if (placement && placement.source === 'item' && placement.itemId) return String(placement.itemId);
    } catch { /* skip */ }
    return null;
  }, [combo, blueprints]);

  const artworkImages = useMemo(() => {
    if (!collectionId || !placementItemId) return [];
    return collectionArtwork
      .filter(a => a.active && String(a.itemId) === placementItemId)
      .map(a => ({
        itemId: a.itemId,
        artworkId: a.id,
        url: api.getCollectionArtworkImageUrl(collectionId, a.itemId, a.id, false),
        thumbUrl: api.getCollectionArtworkThumbUrl(collectionId, a.itemId, a.id),
      }));
  }, [collectionId, collectionArtwork, api, placementItemId]);

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
    return [...productImg, ...mockupImages, ...artworkImages.map(a => a.thumbUrl)];
  }, [existingProductImage, mockupImages, artworkImages]);

  const allFullSizeImages = useMemo(() => {
    const productImg = existingProductImage ? [existingProductImage] : [];
    const mockupFull = mockupImages.map(url => url.replace(/([?&])thumb=true&?/, '$1').replace(/[?&]$/, ''));
    return [...productImg, ...mockupFull, ...artworkImages.map(a => a.url)];
  }, [existingProductImage, mockupImages, artworkImages]);

  const handleImageError = useCallback(async (index) => {
    if (retryRef.current[index]) return;
    retryRef.current[index] = true;

    const productImgCount = existingProductImage ? 1 : 0;
    const mockupImgCount = mockupImages.length;
    const artworkIndex = index - productImgCount - mockupImgCount;
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
  }, [artworkImages, existingProductImage, mockupImages, collectionId, api, refreshTokens]);

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
        });
        if (res.data.success) {
          setTokenEstimate(res.data.data);
        } else {
          setTokenEstimate(null);
        }
      } catch {
        setTokenEstimate(null);
      } finally {
        setEstimatingTokens(false);
      }
    }, 2000);
    return () => { if (estimateTimerRef.current) clearTimeout(estimateTimerRef.current); };
  }, [combo, collectionId, projectId, api, productImagePrompt, selectedProductImageModel]);

  const handleNext = useCallback(() => {
    if (!productName.trim()) {
      setMessage({ type: 'error', text: 'Enter a product title.' });
      return;
    }
    if (!productImagePrompt.trim()) {
      setMessage({ type: 'error', text: 'Enter a product image prompt.' });
      return;
    }
    setStep(STEPS.PRODUCT_IMAGE_PREVIEW);
  }, [productName, productImagePrompt, setStep, setMessage, STEPS]);

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
                imageClassName="!max-h-none w-full h-full object-contain"
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
          {!productName.trim() && (
            <p className="text-sm text-red-600 dark:text-red-400 mt-2">Product title is required.</p>
          )}
        </div>
      )}

      <div className="mb-4">
        <div className="grid grid-cols-2 gap-4 mb-2">
          <div>
            <Input
              label="Product Title"
              name="productName"
              value={productName}
              onChange={handleProductNameChange}
              placeholder="Product name..."
            />
          </div>
          <div>
            <div className="flex items-end gap-3">
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
                <div className="text-sm text-gray-500 dark:text-gray-400" style={{ marginBottom: '2em' }}>
                  <span className="font-medium">Token Cost: <span className="text-white font-bold">{tokenEstimate.toLocaleString()}</span></span>
                </div>
              ) : null}
            </div>
          </div>
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
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline onClick={handleNext} disabled={!productImagePrompt.trim() || !productName.trim()}>
          Generate Image
        </ButtonOutline>
      </div>
    </div>
  );
}
