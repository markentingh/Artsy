import React, { useState, useMemo, useCallback, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { PrintifyProducts } from '@/api/user/printifyProducts';
import Button from '@/components/ui/button';
import ButtonOutline from '@/components/ui/button-outline';
import List, { Item } from '@/components/ui/list';
import Checked from '@/components/ui/checked';
import Icon from '@/components/ui/icon';
import Tooltip from '@/components/ui/tooltip';
import ConfirmModal from '@/components/ui/confirm-modal';

export default function CreateProducts() {
  const session = useSession();
  const {
    project, blueprints, blueprintItemIds, allProductImages, collectionId, api,
    STEPS, setStep, onClose, goBack,
    setMessage, setArtworkPreview,
    collectionArtwork, setCollectionArtwork, printifyProducts, setPrintifyProducts,
    setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex,
    setProductBlueprintImages, setProductImagePrompt, loadImageModels,
    ensureCollection, loadMockups,
  } = useCollection();

  const printifyProductsApi = PrintifyProducts(session);

  const [uploading, setUploading] = useState(false);
  const [creating, setCreating] = useState(false);
  const [downloadingMockups, setDownloadingMockups] = useState(false);
  const [artworkUploadState, setArtworkUploadState] = useState({});
  const [activeMap, setActiveMap] = useState({});
  const [deletingProduct, setDeletingProduct] = useState({});
  const [productToDelete, setProductToDelete] = useState(null);
  const [archivingUpload, setArchivingUpload] = useState({});

  const createdBlueprints = useMemo(() => {
    const map = {};
    for (const pp of printifyProducts) {
      if (pp.printifyProductId && pp.projectBlueprintId && pp.mockupsDownloaded) {
        map[pp.projectBlueprintId] = true;
      }
    }
    return map;
  }, [printifyProducts]);

  const blueprintsWithImages = useMemo(() => {
    return blueprints.filter(bp => bp.configured === true || bp.configured === undefined);
  }, [blueprints]);

  const variantCountByBlueprint = useMemo(() => {
    const map = {};
    for (const bp of blueprints) {
      let count = 0;
      if (bp.blueprintJson) {
        try {
          const parsed = typeof bp.blueprintJson === 'string' ? JSON.parse(bp.blueprintJson) : bp.blueprintJson;
          count = (parsed.variantIds || []).length;
        } catch { /* ignore */ }
      }
      map[bp.id] = count;
    }
    return map;
  }, [blueprints]);

  const imagesByBlueprint = useMemo(() => {
    const map = {};
    for (const bp of blueprintsWithImages) {
      map[bp.id] = allProductImages.filter(img =>
        img.projectBlueprintId === bp.id && img.accepted
      );
    }
    return map;
  }, [blueprintsWithImages, allProductImages]);

  const allImages = useMemo(() => {
    return blueprintsWithImages.flatMap(bp =>
      (imagesByBlueprint[bp.id] || []).map(img => ({
        ...img,
        blueprintName: bp.name,
      }))
    );
  }, [blueprintsWithImages, imagesByBlueprint]);

  const activeBlueprints = useMemo(() =>
    blueprintsWithImages.filter(bp => activeMap[bp.id] !== false),
    [blueprintsWithImages, activeMap]
  );

  const activeItemIds = useMemo(() => {
    const ids = new Set();
    for (const bp of activeBlueprints) {
      if (bp.placementJson) {
        try {
          const placements = typeof bp.placementJson === 'string' ? JSON.parse(bp.placementJson) : bp.placementJson;
          if (placements && Array.isArray(placements)) {
            for (const p of placements) {
              if (p.source === 'item' && p.itemId) ids.add(String(p.itemId));
            }
          }
        } catch { /* ignore */ }
      }
    }
    return ids;
  }, [activeBlueprints]);

  const activeAllImages = useMemo(() =>
    activeBlueprints.flatMap(bp =>
      (imagesByBlueprint[bp.id] || []).map(img => ({
        ...img,
        blueprintName: bp.name,
      }))
    ),
    [activeBlueprints, imagesByBlueprint]
  );

  useEffect(() => {
    if (collectionId) {
      loadMockups(collectionId);
    }
  }, [collectionId, loadMockups]);

  useEffect(() => {
    const existing = {};
    for (const art of collectionArtwork) {
      if (art.printifyImageId) {
        existing[art.id] = { status: 'done' };
      }
    }
    if (Object.keys(existing).length > 0) {
      setArtworkUploadState(prev => ({ ...existing, ...prev }));
    }
  }, [collectionArtwork]);

  useEffect(() => {
    setActiveMap(prev => {
      const next = { ...prev };
      for (const bp of blueprintsWithImages) {
        if (next[bp.id] === undefined || createdBlueprints[bp.id]) next[bp.id] = true;
      }
      return next;
    });
  }, [blueprintsWithImages, createdBlueprints]);

  const acceptedArtwork = useMemo(() =>
    (collectionArtwork || []).filter(a => a.accepted && a.active && blueprintItemIds.has(String(a.itemId))),
    [collectionArtwork, blueprintItemIds]
  );

  const allCreated = useMemo(() => {
    return activeBlueprints.length === 0 || activeBlueprints.every(bp => createdBlueprints[bp.id]);
  }, [activeBlueprints, createdBlueprints]);

  const artworkImages = useMemo(() =>
    acceptedArtwork.map(a => ({
      ...a,
      imageUrl: api.getCollectionArtworkThumbUrl(collectionId, a.itemId, a.id),
      type: 'artwork',
    })),
    [acceptedArtwork, collectionId, api]
  );

  const activeArtworkImages = useMemo(() =>
    artworkImages.filter(art => activeItemIds.has(String(art.itemId))),
    [artworkImages, activeItemIds]
  );

  const handleUploadImages = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setUploading(true);
    setMessage(null);

    for (const art of activeArtworkImages) {
      if (artworkUploadState[art.id]?.status === 'done') continue;

      setArtworkUploadState(prev => ({ ...prev, [art.id]: { status: 'uploading' } }));

      try {
        const response = await printifyProductsApi.uploadArtworkImage({
          collectionId,
          artworkId: art.id,
        });

        if (response.data.success) {
          setArtworkUploadState(prev => ({ ...prev, [art.id]: { status: 'done' } }));
        } else {
          setArtworkUploadState(prev => ({ ...prev, [art.id]: { status: 'error' } }));
          setMessage({ type: 'error', text: response.data.message || 'Failed to upload artwork' });
        }
      } catch (error) {
        setArtworkUploadState(prev => ({ ...prev, [art.id]: { status: 'error' } }));
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to upload artwork' });
      }
    }

    setUploading(false);
  }, [collectionId, project, activeArtworkImages, artworkUploadState, printifyProductsApi, setMessage]);

  const allImagesUploaded = useMemo(() => {
    const artworkDone = activeArtworkImages.length > 0 && activeArtworkImages.every(art => artworkUploadState[art.id]?.status === 'done');
    const artworkReady = activeArtworkImages.length === 0 || artworkDone;
    return artworkReady;
  }, [activeArtworkImages, artworkUploadState]);

  const handleCreateProducts = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setCreating(true);
    setMessage(null);

    let successCount = 0;
    let processedCount = 0;

    for (const bp of activeBlueprints) {
      const variantCount = variantCountByBlueprint[bp.id] || 0;
      if (variantCount === 0) continue;

      const existingPp = printifyProducts.find(p => p.projectBlueprintId === bp.id);
      if (existingPp && existingPp.mockupsDownloaded) continue;

      processedCount++;

      try {
        if (existingPp && existingPp.printifyProductId) {
          const response = await printifyProductsApi.downloadMockups({
            collectionId,
            projectBlueprintId: bp.id,
          });

          if (response.data.success) {
            setPrintifyProducts(prev => [...prev.filter(p => p.projectBlueprintId !== bp.id), response.data.data]);
            if (response.data.data.mockupsDownloaded) {
              successCount++;
            }
          } else {
            setMessage({ type: 'error', text: response.data.message || `Failed to download mockups for ${bp.name}` });
          }
        } else {
          const response = await printifyProductsApi.create({
            collectionId,
            projectBlueprintId: bp.id,
          });

          if (response.data.success) {
            setPrintifyProducts(prev => [...prev.filter(p => p.projectBlueprintId !== bp.id), response.data.data]);
            if (response.data.data.mockupsDownloaded) {
              successCount++;
            }
          } else {
            setMessage({ type: 'error', text: response.data.message || `Failed to create product for ${bp.name}` });
          }
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || `Failed to process ${bp.name}` });
      }
    }

    await loadMockups(collectionId);

    try {
      const ppRes = await printifyProductsApi.getByCollection(collectionId);
      if (ppRes.data.success) {
        setPrintifyProducts(ppRes.data.data || []);
      }
    } catch { /* ignore */ }

    setCreating(false);
  }, [collectionId, project, activeBlueprints, variantCountByBlueprint, printifyProducts, printifyProductsApi, setMessage, setPrintifyProducts, loadMockups]);

  const handleNext = useCallback(async () => {
    const colId = collectionId || await ensureCollection();
    if (!colId) return;

    const hasMockups = printifyProducts.some(pp => pp.mockupsDownloaded);
    if (!hasMockups) {
      setMessage({ type: 'error', text: 'At least one mockup must be downloaded before generating product images.' });
      return;
    }

    try {
      const allPbImages = [];
      for (const bp of activeBlueprints) {
        try {
          const pbiResp = await api.getProductBlueprintImages(bp.id);
          if (pbiResp.data.success) {
            const imgs = (pbiResp.data.data || []).map(img => ({
              ...img,
              projectBlueprintId: bp.id,
              blueprintName: bp.name,
            }));
            allPbImages.push(...imgs);
          }
        } catch { /* ignore */ }
      }
      setProductBlueprintImages(allPbImages);

      const imgRes = await api.getProductImages(colId);
      if (imgRes.data.success) {
        const allImages = (imgRes.data.data || []).filter(img => img.active);
        setAllProductImages(allImages);

        const acceptedProductImageIds = new Set(
          allImages.filter(img => img.accepted).map(img => img.productImageId)
        );

        const missing = allPbImages.filter(pbi => !acceptedProductImageIds.has(pbi.id));

        if (allPbImages.length === 0 || missing.length === 0) {
          setStep(STEPS.PUBLISH_PRODUCTS);
          return;
        }

        const combos = missing.map(pbi => ({
          productImageId: pbi.id,
          projectBlueprintId: pbi.projectBlueprintId,
          blueprintName: pbi.blueprintName,
          title: pbi.title,
          variantColor: pbi.variantColor,
          variantIds: pbi.variantIds || [],
          prompt: pbi.prompt || '',
        }));
        setSelectedProductCombos(combos);
        setCurrentProductComboIndex(0);
        setProductImagePrompt(combos[0]?.prompt || '');
        setStep(STEPS.PRODUCT_IMAGE_PROMPT);
        return;
      }
    } catch (e) { }

    setStep(STEPS.PRODUCT_IMAGE_PROMPT);
  }, [collectionId, ensureCollection, printifyProducts, activeBlueprints, api, setProductBlueprintImages, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex, setProductImagePrompt, setStep, STEPS, setMessage]);

  const handleStart = useCallback(async () => {
    if (!collectionId) return;
    const products = blueprintsWithImages.map(bp => ({
      projectBlueprintId: bp.id,
      active: activeMap[bp.id] !== false,
    }));
    await api.updateCollectionProductsActive({ collectionId, products });

    if (allImagesUploaded) {
      await handleCreateProducts();
    } else {
      await handleUploadImages();
      await handleCreateProducts();
    }
  }, [collectionId, blueprintsWithImages, activeMap, api, allImagesUploaded, handleUploadImages, handleCreateProducts]);

  const handleDeleteProduct = useCallback(async (pp) => {
    if (!pp?.productId || !collectionId) return;
    setProductToDelete(null);
    setDeletingProduct(prev => ({ ...prev, [pp.id]: true }));
    try {
      const response = await printifyProductsApi.delete({ collectionId, productId: pp.productId });
      if (response.data.success) {
        setPrintifyProducts(prev => prev.filter(p => p.id !== pp.id));
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete product' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete product' });
    } finally {
      setDeletingProduct(prev => ({ ...prev, [pp.id]: false }));
    }
  }, [collectionId, printifyProductsApi, setMessage, setPrintifyProducts]);

  const handleArchiveUpload = useCallback(async (art) => {
    if (!collectionId || !art?.id || archivingUpload[art.id]) return;
    setArchivingUpload(prev => ({ ...prev, [art.id]: true }));
    try {
      const response = await printifyProductsApi.archiveUpload({
        collectionId,
        artworkId: art.id,
      });
      if (response.data.success) {
        setCollectionArtwork(prev => prev.map(a =>
          a.id === art.id ? { ...a, printifyImageId: '' } : a
        ));
        setArtworkUploadState(prev => {
          const next = { ...prev };
          delete next[art.id];
          return next;
        });

        const itemId = String(art.itemId);
        const relatedBlueprints = blueprintsWithImages.filter(bp => {
          if (!bp.placementJson) return false;
          try {
            const placements = typeof bp.placementJson === 'string' ? JSON.parse(bp.placementJson) : bp.placementJson;
            return (placements || []).some(p => p.source === 'item' && String(p.itemId) === itemId);
          } catch { return false; }
        });
        for (const bp of relatedBlueprints) {
          const pp = printifyProducts.find(p => p.projectBlueprintId === bp.id);
          if (pp) await handleDeleteProduct(pp);
        }
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to archive image' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to archive image' });
    } finally {
      setArchivingUpload(prev => ({ ...prev, [art.id]: false }));
    }
  }, [collectionId, printifyProductsApi, setMessage, setCollectionArtwork, archivingUpload, blueprintsWithImages, printifyProducts, handleDeleteProduct]);

  const allPreviewImages = useMemo(() => {
    return [
      ...artworkImages.map(a => a.imageUrl),
      ...allImages.map(img => img.imageUrl),
    ];
  }, [artworkImages, allImages]);

  const fullSizePreviewImages = useMemo(() => {
    const artworkFull = acceptedArtwork.map(a => api.getCollectionArtworkImageUrl(collectionId, a.itemId, a.id, true));
    const productFull = allImages.map(img => (img.imageUrl || '').replace('?thumb=true', ''));
    return [...artworkFull, ...productFull];
  }, [acceptedArtwork, allImages, collectionId, api]);

  const handleImageClick = useCallback((clickedImg) => {
    const idx = allPreviewImages.indexOf(clickedImg.imageUrl);
    setArtworkPreview({ images: fullSizePreviewImages, src: fullSizePreviewImages[idx] || clickedImg.imageUrl, _idx: idx >= 0 ? idx : 0 });
  }, [allPreviewImages, fullSizePreviewImages, setArtworkPreview]);

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-lg mb-4">
        Collection artwork will be uploaded to Printify, then products will be created.
      </p>

      {artworkImages.length > 0 && (
        <div className="mb-4">
          <h4 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-400">Artworks</h4>
          <div className="flex flex-wrap gap-3 justify-center">
            {artworkImages.map((art) => {
              const state = artworkUploadState[art.id];
              const isUploading = state?.status === 'uploading';
              const isDone = state?.status === 'done';
              const isError = state?.status === 'error';
              const isArtworkActive = activeItemIds.has(String(art.itemId));
              return (
                <div
                  key={art.id}
                  className={`group relative w-[120px] h-[120px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 ${isArtworkActive ? 'cursor-pointer' : 'opacity-40 cursor-not-allowed'}`}
                  onClick={isArtworkActive ? () => handleImageClick(art) : undefined}
                >
                  <img
                    src={art.imageUrl}
                    alt="Artwork"
                    className="w-full h-full object-cover"
                  />
                  {isUploading && (
                    <div className="absolute inset-0 flex items-center justify-center bg-black/40">
                      <Icon name="progress_activity" spin className="w-6 h-6 text-white" />
                    </div>
                  )}
                  {isDone && (
                    <div className="absolute inset-0 flex items-center justify-center bg-black/30">
                      <Icon name="check_circle" className="w-8 h-8 text-green-500" />
                    </div>
                  )}
                  {isError && (
                    <div className="absolute inset-0 flex items-center justify-center bg-black/40">
                      <Icon name="error" className="w-8 h-8 text-red-500" />
                    </div>
                  )}
                  {isDone && (
                    <div className="hidden group-hover:flex absolute bottom-1 left-1 right-1 justify-center z-10">
                      <Button
                        size="small"
                        color="red"
                        disabled={archivingUpload[art.id]}
                        onClick={(e) => {
                          e.stopPropagation();
                          handleArchiveUpload(art);
                        }}
                      >
                        {archivingUpload[art.id] ? (
                          <Icon name="progress_activity" spin className="w-4 h-4 inline" />
                        ) : 'Delete'}
                      </Button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}

      <div className="mb-6">
        <h3 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-300">Products</h3>
        <List inModal={true}>
          {blueprintsWithImages.map((bp) => {
            const variantCount = variantCountByBlueprint[bp.id] || 0;
            const isCreated = createdBlueprints[bp.id] || false;
            const isActive = activeMap[bp.id] !== false;
            const pp = printifyProducts.find(p => p.projectBlueprintId === bp.id);
            return (
              <Item key={bp.id}>
                <div className="flex items-center w-full">
                  <input
                    type="checkbox"
                    checked={isActive}
                    disabled={isCreated}
                    onChange={() => !isCreated && setActiveMap(prev => ({ ...prev, [bp.id]: !isActive }))}
                    className={`mr-3 w-4 h-4 accent-blue-600 ${isCreated ? 'cursor-not-allowed opacity-50' : 'cursor-pointer'}`}
                  />
                  <span className={`text-sm font-medium ${isActive ? 'text-gray-700 dark:text-gray-300' : 'text-gray-400 dark:text-gray-500 line-through'}`}>
                    {bp.name}
                  </span>
                  <div className="ml-auto flex items-center gap-3">
                    <span className="text-xs text-gray-500 dark:text-gray-400">
                      {variantCount} {variantCount === 1 ? 'variant' : 'variants'}
                    </span>
                    {isCreated && pp && (
                      <>
                        <ButtonOutline
                          size="small"
                          onClick={() => window.open(`https://printify.com/app/product-details/${pp.printifyProductId}`, '_blank', 'noopener noreferrer')}
                        >
                          View on Printify
                        </ButtonOutline>
                        <ButtonOutline
                          size="small"
                          color="red"
                          onClick={() => setProductToDelete(pp)}
                          disabled={deletingProduct[pp.id]}
                        >
                          Delete
                        </ButtonOutline>
                      </>
                    )}
                    <Checked checked={isCreated} />
                  </div>
                </div>
              </Item>
            );
          })}
        </List>
      </div>

      <div className="buttons flex justify-end gap-2 mt-auto items-center">
        {!allCreated && (
          <Tooltip text="This will upload the selected images to Printify, then create new products for your store on Printify. This will not publish products to your connected store on Printify." className="pr-8" />
        )}
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        <ButtonOutline
          onClick={allCreated ? handleNext : handleStart}
          disabled={uploading || creating || !project?.printifyStoreId || activeBlueprints.length === 0}
        >
          {uploading ? (
            <>
              <Icon name="progress_activity" spin className="w-4 h-4 inline mr-1" />
              Uploading Images...
            </>
          ) : creating ? (
            <>
              <Icon name="progress_activity" spin className="w-4 h-4 inline mr-1" />
              Creating Products...
            </>
          ) : allCreated ? (
            'Next'
          ) : allImagesUploaded ? (
            'Create Products'
          ) : (
            'Upload & Create Products'
          )}
        </ButtonOutline>
      </div>

      <ConfirmModal
        show={!!productToDelete}
        title="Delete Product"
        message={`Do you really want to delete the product ${productToDelete?.blueprintName || ''}? This will delete it from your Printify shop.`}
        confirmLabel="Delete"
        onConfirm={() => productToDelete && handleDeleteProduct(productToDelete)}
        onClose={() => setProductToDelete(null)}
      />
    </div>
  );
}
