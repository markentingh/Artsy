import React, { useState, useMemo, useCallback, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { PrintifyProducts } from '@/api/user/printifyProducts';
import { artworkThumbUrl, artworkImageUrl } from '@/utils/artworkUrls';
import Button from '@/components/ui/button';
import ButtonOutline from '@/components/ui/button-outline';
import List, { Item } from '@/components/ui/list';
import Checked from '@/components/ui/checked';
import Icon from '@/components/ui/icon';
import Tooltip from '@/components/ui/tooltip';
import ConfirmModal from '@/components/ui/confirm-modal';
import EditCollectionProductDetails from './EditCollectionProductDetails';

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
    collectionProducts, setCollectionProducts, mockups,
    cancelRef, cancelAll,
  } = useCollection();

  const printifyProductsApi = PrintifyProducts(session);

  const [uploading, setUploading] = useState(false);
  const [creating, setCreating] = useState(false);
  const [downloadingMockups, setDownloadingMockups] = useState(false);
  const [artworkUploadState, setArtworkUploadState] = useState({});
  const [deletingProduct, setDeletingProduct] = useState({});
  const [productToDelete, setProductToDelete] = useState(null);
  const [archivingUpload, setArchivingUpload] = useState({});
  const [editProductBp, setEditProductBp] = useState(null);
  const [createCheckMap, setCreateCheckMap] = useState({});

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
    const activeBlueprintIds = new Set(
      collectionProducts.filter(cp => cp.active).map(cp => cp.projectBlueprintId)
    );
    return blueprints.filter(bp =>
      (bp.configured === true || bp.configured === undefined) &&
      activeBlueprintIds.has(bp.id)
    );
  }, [blueprints, collectionProducts]);

  // Initialize create checkboxes: checked by default for all blueprintsWithImages
  useEffect(() => {
    setCreateCheckMap(prev => {
      const next = {};
      for (const bp of blueprintsWithImages) {
        next[bp.id] = prev[bp.id] !== false; // default true, preserve user toggles
      }
      return next;
    });
  }, [blueprintsWithImages]);

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
    blueprintsWithImages.filter(bp => createCheckMap[bp.id] !== false),
    [blueprintsWithImages, createCheckMap]
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
      // Refresh artwork data to get current printifyImageId values
      api.getCollectionArtwork(collectionId).then(res => {
        if (res.data.success) {
          setCollectionArtwork(res.data.data || []);
        }
      }).catch(() => {});
    }
  }, [collectionId, loadMockups, api, setCollectionArtwork]);

  useEffect(() => {
    const existing = {};
    for (const art of collectionArtwork) {
      // Group placements
      if (art.groupPlacements) {
        for (const grp of art.groupPlacements) {
          for (const gp of grp.placements) {
            if (gp.printifyImageId) {
              existing[`${art.id}_group_${grp.groupId}_${gp.index}`] = { status: 'done' };
            }
          }
        }
      }
      // Non-group placements
      if (art.placements) {
        for (const p of art.placements) {
          if (!p.groupId && p.printifyImageId) {
            existing[`${art.id}_placement_${p.index}`] = { status: 'done' };
          }
        }
      }
      // Base artwork (no placements)
      if ((!art.groupPlacements || art.groupPlacements.length === 0) &&
          (!art.placements || art.placements.length === 0) &&
          art.printifyImageId) {
        existing[art.id] = { status: 'done' };
      }
    }
    // Replace state entirely: keep only entries that still have printifyImageId,
    // preserve uploading/error status for in-progress items
    setArtworkUploadState(prev => {
      const next = {};
      // Carry over uploading/error states (in-progress operations)
      for (const [key, val] of Object.entries(prev)) {
        if (val.status === 'uploading' || val.status === 'error') {
          next[key] = val;
        }
      }
      // Apply current done states from data
      Object.assign(next, existing);
      return next;
    });
  }, [collectionArtwork]);

  const acceptedArtwork = useMemo(() =>
    (collectionArtwork || []).filter(a => a.accepted && a.active && blueprintItemIds.has(String(a.itemId))),
    [collectionArtwork, blueprintItemIds]
  );

  const allCreated = useMemo(() => {
    return activeBlueprints.length === 0 || activeBlueprints.every(bp => createdBlueprints[bp.id]);
  }, [activeBlueprints, createdBlueprints]);

  const artworkImages = useMemo(() =>
    acceptedArtwork.flatMap(a => {
      const thumbs = [];

      // Pattern mode: show only the base artwork image (1 image used across all placements)
      if (a.design === 'pattern') {
        thumbs.push({
          ...a,
          imageUrl: artworkThumbUrl(collectionId, a.itemId, a.id),
          type: 'artwork',
        });
        return thumbs;
      }

      // Show group placements as separate thumbnails
      if (a.hasGroups && a.groupPlacements) {
        for (const grp of a.groupPlacements) {
          for (const gp of grp.placements) {
            thumbs.push({
              ...a,
              id: `${a.id}_group_${grp.groupId}_${gp.index}`,
              artworkId: a.id,
              groupId: grp.groupId,
              groupPosition: gp.position,
              groupIndex: gp.index,
              imageUrl: `/api/projects/collection/${collectionId}/item/${a.itemId}/artwork/${a.id}/group/${grp.groupId}/${gp.position}`,
              type: 'artwork',
            });
          }
        }
      }

      // Show non-group (variant) placements as separate thumbnails
      const nonGroupPlacements = (a.placements || []).filter(p => !p.groupId);
      if (nonGroupPlacements.length > 0) {
        for (const p of nonGroupPlacements) {
          thumbs.push({
            ...a,
            id: `${a.id}_placement_${p.index}`,
            artworkId: a.id,
            placementIndex: p.index,
            imageUrl: artworkThumbUrl(collectionId, a.itemId, a.id, { placementIndex: p.index }),
            type: 'artwork',
          });
        }
      }

      // If no placements at all, show the base artwork
      if (thumbs.length === 0) {
        thumbs.push({
          ...a,
          imageUrl: artworkThumbUrl(collectionId, a.itemId, a.id),
          type: 'artwork',
        });
      }

      return thumbs;
    }),
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
    cancelRef.current = false;

    for (const art of activeArtworkImages) {
      if (cancelRef.current) break;
      const artKey = art.id;
      const artwork = acceptedArtwork.find(a => a.id === art.artworkId);

      // Skip only if the actual PrintifyImageId is set in the data
      if (art.groupId && art.groupPosition) {
        const gp = artwork?.groupPlacements?.flatMap(g => g.placements).find(p => p.position === art.groupPosition);
        if (gp?.printifyImageId) continue;
      } else if (art.placementIndex != null) {
        const p = artwork?.placements?.find(pl => pl.index === art.placementIndex);
        if (p?.printifyImageId) continue;
      } else if (art.printifyImageId) {
        continue;
      }

      setArtworkUploadState(prev => ({ ...prev, [artKey]: { status: 'uploading' } }));

      try {
        if (art.groupId && art.groupPosition) {
          // Seamless group placement upload
          const response = await printifyProductsApi.uploadArtworkImage({
            collectionId,
            artworkId: art.artworkId,
            placementIndex: art.groupIndex,
            groupId: art.groupId,
            position: art.groupPosition,
          });
          if (response.data.success) {
            const newId = response.data.data?.printifyImageId;
            setArtworkUploadState(prev => ({ ...prev, [artKey]: { status: 'done' } }));
            if (newId) {
              setCollectionArtwork(prev => prev.map(a => {
                if (a.id !== art.artworkId) return a;
                return {
                  ...a,
                  groupPlacements: (a.groupPlacements || []).map(grp => {
                    if (grp.groupId !== art.groupId) return grp;
                    return {
                      ...grp,
                      placements: (grp.placements || []).map(gp =>
                        gp.position === art.groupPosition
                          ? { ...gp, printifyImageId: newId }
                          : gp
                      ),
                    };
                  }),
                };
              }));
            }
          } else {
            setArtworkUploadState(prev => ({ ...prev, [artKey]: { status: 'error' } }));
            setMessage({ type: 'error', text: response.data.message || 'Failed to upload group placement' });
          }
        } else if (art.placementIndex != null) {
          // Non-group placement variant upload
          const response = await printifyProductsApi.uploadArtworkImage({
            collectionId,
            artworkId: art.artworkId,
            placementIndex: art.placementIndex,
          });
          if (response.data.success) {
            const newId = response.data.data?.printifyImageId;
            setArtworkUploadState(prev => ({ ...prev, [artKey]: { status: 'done' } }));
            if (newId) {
              setCollectionArtwork(prev => prev.map(a => {
                if (a.id !== art.artworkId) return a;
                return {
                  ...a,
                  placements: (a.placements || []).map(p =>
                    p.index === art.placementIndex && !p.groupId
                      ? { ...p, printifyImageId: newId }
                      : p
                  ),
                };
              }));
            }
          } else {
            setArtworkUploadState(prev => ({ ...prev, [artKey]: { status: 'error' } }));
            setMessage({ type: 'error', text: response.data.message || 'Failed to upload placement' });
          }
        } else {
          // Standard single artwork upload
          const response = await printifyProductsApi.uploadArtworkImage({
            collectionId,
            artworkId: art.artworkId || art.id,
          });
          if (response.data.success) {
            const newId = response.data.data?.printifyImageId;
            setArtworkUploadState(prev => ({ ...prev, [artKey]: { status: 'done' } }));
            if (newId) {
              setCollectionArtwork(prev => prev.map(a =>
                a.id === (art.artworkId || art.id) ? { ...a, printifyImageId: newId } : a
              ));
            }
          } else {
            setArtworkUploadState(prev => ({ ...prev, [artKey]: { status: 'error' } }));
            setMessage({ type: 'error', text: response.data.message || 'Failed to upload artwork' });
          }
        }
      } catch (error) {
        setArtworkUploadState(prev => ({ ...prev, [artKey]: { status: 'error' } }));
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to upload artwork' });
      }
    }

    setUploading(false);
  }, [collectionId, project, activeArtworkImages, artworkUploadState, printifyProductsApi, setMessage, cancelRef, setCollectionArtwork]);

  const allImagesUploaded = useMemo(() => {
    if (activeArtworkImages.length === 0) return true;
    // Check actual printifyImageId from artwork data, not just component state
    return activeArtworkImages.every(art => {
      const artwork = acceptedArtwork.find(a => a.id === art.artworkId);
      // For group placements, check the specific group placement's printifyImageId
      if (art.groupId && art.groupPosition) {
        const gp = artwork?.groupPlacements?.flatMap(g => g.placements).find(p => p.position === art.groupPosition);
        return !!gp?.printifyImageId;
      }
      // For non-group placement variants, check the specific placement's printifyImageId
      if (art.placementIndex != null) {
        const p = artwork?.placements?.find(pl => pl.index === art.placementIndex);
        return !!p?.printifyImageId;
      }
      // Standard single artwork
      return !!art.printifyImageId;
    });
  }, [activeArtworkImages, acceptedArtwork]);

  const handleCreateProducts = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setCreating(true);
    setMessage(null);
    cancelRef.current = false;

    let successCount = 0;
    let processedCount = 0;

    for (const bp of activeBlueprints) {
      if (cancelRef.current) break;
      if (createCheckMap[bp.id] === false) continue;
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
          } else if (response.data.data?.recreate) {
            // Product no longer exists on Printify — remove stale record and create a new one
            setPrintifyProducts(prev => prev.filter(p => p.projectBlueprintId !== bp.id));
            const createResponse = await printifyProductsApi.create({
              collectionId,
              projectBlueprintId: bp.id,
            });

            if (createResponse.data.success) {
              setPrintifyProducts(prev => [...prev.filter(p => p.projectBlueprintId !== bp.id), createResponse.data.data]);
              if (createResponse.data.data.mockupsDownloaded) {
                successCount++;
              }
            } else {
              setMessage({ type: 'error', text: createResponse.data.message || `Failed to create product for ${bp.name}` });
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
  }, [collectionId, project, activeBlueprints, variantCountByBlueprint, printifyProducts, printifyProductsApi, setMessage, setPrintifyProducts, loadMockups, createCheckMap, cancelRef]);

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
        setStep(STEPS.GENERATE_PRODUCT_IMAGES);
        return;
      }
    } catch (e) { }

    setStep(STEPS.GENERATE_PRODUCT_IMAGES);
  }, [collectionId, ensureCollection, printifyProducts, activeBlueprints, api, setProductBlueprintImages, setAllProductImages, setSelectedProductCombos, setCurrentProductComboIndex, setProductImagePrompt, setStep, STEPS, setMessage]);

  const handleStart = useCallback(async () => {
    if (!collectionId) return;

    if (allImagesUploaded) {
      await handleCreateProducts();
    } else {
      await handleUploadImages();
      if (cancelRef.current) return;
      await handleCreateProducts();
    }
  }, [collectionId, allImagesUploaded, handleUploadImages, handleCreateProducts, cancelRef]);

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
    if (!collectionId || !art?.artworkId || archivingUpload[art.id]) return;
    setArchivingUpload(prev => ({ ...prev, [art.id]: true }));
    try {
      const response = await printifyProductsApi.archiveUpload({
        collectionId,
        artworkId: art.artworkId,
        placementIndex: art.placementIndex,
        groupId: art.groupId,
        groupPosition: art.groupPosition,
      });
      if (response.data.success) {
        // Update local state for the specific image that was archived
        const stateKey = art.id;
        setArtworkUploadState(prev => {
          const next = { ...prev };
          delete next[stateKey];
          return next;
        });

        // Update collectionArtwork to reflect the cleared printifyImageId
        if (art.placementIndex != null) {
          // Non-group placement
          setCollectionArtwork(prev => prev.map(a => {
            if (a.id !== art.artworkId) return a;
            return {
              ...a,
              placements: (a.placements || []).map(p =>
                p.index === art.placementIndex && !p.groupId
                  ? { ...p, printifyImageId: '' }
                  : p
              ),
            };
          }));
        } else if (art.groupId) {
          // Group placement
          setCollectionArtwork(prev => prev.map(a => {
            if (a.id !== art.artworkId) return a;
            return {
              ...a,
              groupPlacements: (a.groupPlacements || []).map(grp => {
                if (grp.groupId !== art.groupId) return grp;
                return {
                  ...grp,
                  placements: (grp.placements || []).map(gp =>
                    gp.index === art.groupIndex
                      ? { ...gp, printifyImageId: '' }
                      : gp
                  ),
                };
              }),
            };
          }));
        } else {
          // Base artwork
          setCollectionArtwork(prev => prev.map(a =>
            a.id === art.artworkId ? { ...a, printifyImageId: '' } : a
          ));
        }
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to archive image' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to archive image' });
    } finally {
      setArchivingUpload(prev => ({ ...prev, [art.id]: false }));
    }
  }, [collectionId, printifyProductsApi, setMessage, setCollectionArtwork, archivingUpload]);

  const allPreviewImages = useMemo(() => {
    return [
      ...artworkImages.map(a => a.imageUrl),
      ...allImages.map(img => img.imageUrl),
    ];
  }, [artworkImages, allImages]);

  const fullSizePreviewImages = useMemo(() => {
    const artworkFull = artworkImages.map(a => {
      // For group artworks, the imageUrl is already the full URL
      if (a.groupId && a.groupPosition) return a.imageUrl;
      // For non-group placement variants, use the actual placement index
      if (a.placementIndex != null) {
        return artworkImageUrl(collectionId, a.itemId, a.artworkId, { placementIndex: a.placementIndex });
      }
      // Standard single artwork
      return artworkImageUrl(collectionId, a.itemId, a.artworkId || a.id);
    });
    const productFull = allImages.map(img => (img.imageUrl || '').replace('?thumb=true', ''));
    return [...artworkFull, ...productFull];
  }, [artworkImages, allImages, collectionId]);

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
                    className="w-full h-full object-contain"
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
            const pp = printifyProducts.find(p => p.projectBlueprintId === bp.id);
            const cp = collectionProducts.find(c => c.projectBlueprintId === bp.id);
            const displayName = (cp && cp.name) ? cp.name : bp.name;
            return (
              <Item key={bp.id}>
                <div className="flex items-center w-full">
                  <input
                    type="checkbox"
                    checked={createCheckMap[bp.id] !== false}
                    onChange={() => setCreateCheckMap(prev => ({ ...prev, [bp.id]: prev[bp.id] === false }))}
                    disabled={isCreated}
                    className="w-4 h-4 accent-blue-600 cursor-pointer flex-shrink-0 mr-3"
                  />
                  <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    {displayName}
                  </span>
                  <div className="ml-auto flex items-center gap-3">
                    <span className="text-xs text-gray-500 dark:text-gray-400">
                      {variantCount} {variantCount === 1 ? 'variant' : 'variants'}
                    </span>
                    <ButtonOutline
                      size="small"
                      onClick={() => setEditProductBp(bp)}
                    >
                      Edit Details
                    </ButtonOutline>
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
        <ButtonOutline color="gray" className="cancel" onClick={() => { cancelAll(); onClose(); }}>Cancel</ButtonOutline>
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
      <EditCollectionProductDetails
        show={!!editProductBp}
        collectionId={collectionId}
        projectBlueprintId={editProductBp?.id}
        blueprintName={editProductBp?.name}
        collectionProducts={collectionProducts}
        allProductImages={allProductImages}
        mockups={mockups}
        printifyProducts={printifyProducts}
        api={api}
        onClose={() => setEditProductBp(null)}
        onSaved={() => {
          // Refresh collection products to reflect name changes
          if (collectionId) {
            api.getCollectionProducts(collectionId).then(res => {
              if (res.data.success) {
                const products = res.data.data || [];
                setCollectionProducts(prev => products.length > 0 ? products : prev);
              }
            }).catch(() => {});
          }
        }}
      />
    </div>
  );
}
