import React, { useState, useCallback, useMemo, useEffect, useRef, lazy, Suspense } from 'react';
import { useCollection } from '@/context/collection';
import { useDashboard } from '@/context/dashboard';
import ButtonOutline from '@/components/ui/button-outline';
import Button from '@/components/ui/button';
import ButtonIcon from '@/components/ui/button-icon';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';

const ConfigureProductImageModal = lazy(() => import('./ConfigureProductImageModal'));
const AddProductImageModal = lazy(() => import('./AddProductImageModal'));

export default function GenerateProductImages() {
  const {
    collectionId, api, projectId,
    setStep, setMessage, STEPS, onClose, goBack,
    allProductImages, setAllProductImages,
    blueprints, printifyProducts, loadMockups,
    mockups, selectedProductImageModel,
    collectionProducts,
  } = useCollection();
  const { refreshTokens } = useDashboard();

  const [productImageList, setProductImageList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showAddModal, setShowAddModal] = useState(false);
  const [configureTarget, setConfigureTarget] = useState(null);
  const [generatingIds, setGeneratingIds] = useState(new Set());
  const [isGenerating, setIsGenerating] = useState(false);
  const productImageListRef = useRef([]);
  const generatingIdsRef = useRef(new Set());

  useEffect(() => {
    productImageListRef.current = productImageList;
  }, [productImageList]);

  useEffect(() => {
    generatingIdsRef.current = generatingIds;
  }, [generatingIds]);

  // Load product images for this collection
  const loadProductImages = useCallback(async () => {
    if (!collectionId) return;
    try {
      const res = await api.getProductImages(collectionId);
      if (res.data.success) {
        const cacheBust = Math.floor(Math.random() * 1000000);
        const images = (res.data.data || [])
          .filter(img => img.active)
          .map(img => img.imageUrl ? { ...img, imageUrl: `${img.imageUrl}&r=${cacheBust}` } : img);
        setProductImageList(images);
        setAllProductImages(images);
      }
    } catch { /* ignore */ }
    setLoading(false);
  }, [collectionId, api, setAllProductImages]);

  useEffect(() => {
    loadProductImages();
  }, [loadProductImages]);

  // Load mockups if not already loaded
  useEffect(() => {
    if (collectionId && mockups.length === 0) {
      loadMockups(collectionId);
    }
  }, [collectionId, mockups.length, loadMockups]);

  // Group product images by blueprint
  const blueprintsWithImages = useMemo(() => {
    const map = new Map();
    for (const img of productImageList) {
      if (!map.has(img.projectBlueprintId)) {
        const bp = blueprints.find(b => b.id === img.projectBlueprintId);
        const pp = printifyProducts.find(p => p.projectBlueprintId === img.projectBlueprintId);
        map.set(img.projectBlueprintId, {
          projectBlueprintId: img.projectBlueprintId,
          blueprintName: bp?.name || '',
          printifyProductId: pp?.id,
          images: [],
        });
      }
      map.get(img.projectBlueprintId).images.push(img);
    }
    return Array.from(map.values());
  }, [productImageList, blueprints, printifyProducts]);

  const allGenerated = useMemo(() => {
    return productImageList.length > 0 && productImageList.every(img => img.generated);
  }, [productImageList]);

  const handleAddProductImage = useCallback(async (newId, title) => {
    setShowAddModal(false);
    await loadProductImages();
    // Open configure modal for the newly created image
    const newImg = productImageList.find(img => img.id === newId);
    if (newImg) {
      setConfigureTarget(newImg);
    } else {
      // Image not yet in list, create a temp object
      const bp = blueprintsWithImages[0];
      setConfigureTarget({
        id: newId,
        variantColor: title,
        productImageId: null,
        projectBlueprintId: bp?.projectBlueprintId,
        prompt: '',
        imageModel: '',
        selectedMockups: '',
        generated: false,
      });
    }
  }, [loadProductImages, productImageList, blueprintsWithImages]);

  const handleDeleteProductImage = useCallback(async (img) => {
    if (!img || img.productImageId) return; // Can only delete custom images (null ProductImageId)
    try {
      await api.deleteCollectionProductImage(img.id);
      setProductImageList(prev => prev.filter(p => p.id !== img.id));
      setAllProductImages(prev => prev.filter(p => p.id !== img.id));
    } catch (err) {
      setMessage({ type: 'error', text: err?.response?.data?.message || 'Failed to delete product image' });
    }
  }, [api, setAllProductImages, setMessage]);

  const handleConfigureClose = useCallback(async () => {
    setConfigureTarget(null);
    await loadProductImages();
  }, [loadProductImages]);

  const handleGenerateSingle = useCallback(async (img) => {
    // Close the modal first
    setConfigureTarget(null);
    // Reload to get the saved config
    await loadProductImages();

    // Find the updated image from the refreshed list
    const updatedImg = productImageListRef.current.find(p => p.id === img.id) || img;

    // Enforce max 4 concurrent generations
    if (generatingIdsRef.current.size >= 4) {
      setMessage({ type: 'error', text: 'You can generate up to 4 product images at once. Please wait for one to finish.' });
      return;
    }

    setGeneratingIds(prev => new Set([...prev, updatedImg.id]));
    setMessage(null);

    // Fire-and-forget so the user can trigger more generations
    (async () => {
      try {
        const cp = collectionProducts.find(p => p.projectBlueprintId === updatedImg.projectBlueprintId);
        const mockupImageIds = (updatedImg.selectedMockups || '')
          .split(',')
          .map(s => s.trim())
          .filter(Boolean);

        const res = await api.generateProductImage({
          projectId,
          collectionId,
          projectBlueprintId: updatedImg.projectBlueprintId,
          productImageId: updatedImg.productImageId || '00000000-0000-0000-0000-000000000000',
          id: updatedImg.id,
          modelId: updatedImg.imageModel ? parseInt(updatedImg.imageModel) : (selectedProductImageModel?.id || 0),
          prompt: updatedImg.prompt || '',
          variantColor: updatedImg.variantColor || '',
          productName: cp?.name || undefined,
          mockupImageIds,
        });

        if (res.data.success) {
          const cacheBust = Math.floor(Math.random() * 1000000);
          setProductImageList(prev => prev.map(p =>
            p.id === updatedImg.id
              ? { ...p, generated: true, imageUrl: `${res.data.data.imageUrl}&r=${cacheBust}`, accepted: res.data.data.accepted }
              : p
          ));
          refreshTokens();
        } else {
          setMessage({ type: 'error', text: res.data.message || `Failed to generate: ${updatedImg.variantColor}` });
        }
      } catch (err) {
        setMessage({ type: 'error', text: err?.response?.data?.message || `Failed to generate: ${updatedImg.variantColor}` });
      } finally {
        setGeneratingIds(prev => {
          const next = new Set(prev);
          next.delete(updatedImg.id);
          return next;
        });
        await loadProductImages();
      }
    })();
  }, [collectionId, projectId, api, collectionProducts, selectedProductImageModel, setMessage, refreshTokens, loadProductImages]);

  const handleGenerateImages = useCallback(async () => {
    if (!collectionId || !projectId) return;
    const toGenerate = productImageList.filter(img => !img.generated);
    if (toGenerate.length === 0) return;

    setIsGenerating(true);
    setMessage(null);

    for (const img of toGenerate) {
      setGeneratingIds(prev => new Set([...prev, img.id]));
      try {
        const cp = collectionProducts.find(p => p.projectBlueprintId === img.projectBlueprintId);
        const mockupImageIds = (img.selectedMockups || '')
          .split(',')
          .map(s => s.trim())
          .filter(Boolean);

        const res = await api.generateProductImage({
          projectId,
          collectionId,
          projectBlueprintId: img.projectBlueprintId,
          productImageId: img.productImageId || '00000000-0000-0000-0000-000000000000',
          id: img.id,
          modelId: img.imageModel ? parseInt(img.imageModel) : (selectedProductImageModel?.id || 0),
          prompt: img.prompt || '',
          variantColor: img.variantColor || '',
          productName: cp?.name || undefined,
          mockupImageIds,
        });

        if (res.data.success) {
          // Update the image in the list
          const cacheBust = Math.floor(Math.random() * 1000000);
          setProductImageList(prev => prev.map(p =>
            p.id === img.id
              ? { ...p, generated: true, imageUrl: `${res.data.data.imageUrl}&r=${cacheBust}`, accepted: res.data.data.accepted }
              : p
          ));
          refreshTokens();
        } else {
          setMessage({ type: 'error', text: res.data.message || `Failed to generate: ${img.variantColor}` });
        }
      } catch (err) {
        setMessage({ type: 'error', text: err?.response?.data?.message || `Failed to generate: ${img.variantColor}` });
      } finally {
        setGeneratingIds(prev => {
          const next = new Set(prev);
          next.delete(img.id);
          return next;
        });
      }
    }

    setIsGenerating(false);
    await loadProductImages();
  }, [collectionId, projectId, productImageList, api, collectionProducts, selectedProductImageModel, setMessage, refreshTokens, loadProductImages]);

  const handleNext = useCallback(() => {
    setStep(STEPS.PUBLISH_PRODUCTS);
  }, [setStep, STEPS]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Spinner className="text-4xl" />
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300">Product Images</h3>
        <ButtonOutline size="small" onClick={() => setShowAddModal(true)}>
          <Icon name="add" className="w-4 h-4 inline mr-1" />
          Add Product Image
        </ButtonOutline>
      </div>

      {productImageList.length === 0 && (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">No product images yet.</p>
          <ButtonOutline onClick={() => setShowAddModal(true)}>
            <Icon name="add" className="w-4 h-4 inline mr-1" />
            Add Product Image
          </ButtonOutline>
        </div>
      )}

      <div className="flex-1 overflow-y-auto">
        <div className="grid w-full justify-content-center gap-4" style={{ gridTemplateColumns: 'repeat(auto-fill, 240px)', justifyContent: 'center' }}>
          {productImageList.map((img) => {
            const isGeneratingThis = generatingIds.has(img.id);
            const isCustom = !img.productImageId || img.productImageId === '00000000-0000-0000-0000-000000000000';

            return (
              <div
                key={img.id}
                className="group bg-gray-50 dark:bg-gray-700 rounded-lg shadow p-4 hover:shadow-md transition cursor-pointer"
                onClick={() => setConfigureTarget(img)}
              >
                <div className="w-[200px] h-[200px] mx-auto mb-3 rounded-lg overflow-hidden bg-gray-100 dark:bg-gray-700 flex items-center justify-center">
                  {isGeneratingThis ? (
                    <Spinner className="text-3xl" />
                  ) : img.generated && img.imageUrl ? (
                    <img src={img.imageUrl} alt={img.subtitle || img.title} className="w-full h-full object-contain" />
                  ) : (
                    <span className="text-sm text-gray-400 dark:text-gray-500">No Image</span>
                  )}
                </div>
                <div>
                  <div className="flex items-baseline gap-2">
                    <span
                      className="flex-1 min-w-0 text-sm text-gray-700 dark:text-gray-200 truncate"
                      title={isCustom ? (img.variantColor || '') : (img.title || '')}
                    >
                      {isCustom ? (img.variantColor || '') : (img.title || '')}
                    </span>
                  </div>
                  {!isCustom && (
                    <div className="flex items-center justify-between mt-1">
                      <p className="text-sm text-gray-500 dark:text-gray-400 truncate">
                        {img.subtitle || ''}
                      </p>
                      <div className="flex items-center gap-1 flex-shrink-0">
                        {img.generated && (
                          <Icon name="check_circle" className="w-4 h-4 text-green-500" />
                        )}
                      </div>
                    </div>
                  )}
                  {isCustom && (
                    <div className="flex items-center justify-end mt-1">
                      <div className="flex items-center gap-1 flex-shrink-0">
                        {img.generated && (
                          <Icon name="check_circle" className="w-4 h-4 text-green-500" />
                        )}
                        {isCustom && !isGenerating && (
                          <ButtonIcon
                            name="delete"
                            color="red"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleDeleteProductImage(img);
                            }}
                            title="Delete product image"
                          />
                        )}
                      </div>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      <div className="buttons flex justify-end gap-2 mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        {allGenerated ? (
          <ButtonOutline onClick={handleNext}>Next</ButtonOutline>
        ) : (
          <Button onClick={handleGenerateImages} disabled={isGenerating || productImageList.length === 0}>
            {isGenerating ? <Spinner className="text-sm" /> : 'Generate Images'}
          </Button>
        )}
      </div>

      {showAddModal && (
        <Suspense fallback={null}>
          <AddProductImageModal
            show={showAddModal}
            onClose={() => setShowAddModal(false)}
            onConfigure={handleAddProductImage}
            api={api}
            collectionId={collectionId}
            projectId={projectId}
          />
        </Suspense>
      )}

      {configureTarget && (
        <Suspense fallback={null}>
          <ConfigureProductImageModal
            show={!!configureTarget}
            onClose={handleConfigureClose}
            onGenerate={handleGenerateSingle}
            productImage={configureTarget}
            projectBlueprintId={configureTarget.projectBlueprintId}
            blueprintName={blueprints.find(b => b.id === configureTarget.projectBlueprintId)?.name || ''}
          />
        </Suspense>
      )}
    </div>
  );
}
