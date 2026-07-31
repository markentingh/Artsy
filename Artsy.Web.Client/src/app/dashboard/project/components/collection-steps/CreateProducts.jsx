import React, { useState, useMemo, useCallback, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import ButtonOutline from '@/components/ui/button-outline';
import Button from '@/components/ui/button';
import List, { Item } from '@/components/ui/list';
import Checked from '@/components/ui/checked';
import Icon from '@/components/ui/icon';

export default function CreateProducts() {
  const session = useSession();
  const {
    project, blueprints, allProductImages, collectionId, api,
    productImageVariants, STEPS, setStep,
    handleSaveDraft, setMessage, setArtworkPreview,
    loadProductImageVariants, collectionArtwork,
  } = useCollection();

  const printifyApi = Projects(session);

  const [uploading, setUploading] = useState(false);
  const [creating, setCreating] = useState(false);
  const [createdBlueprints, setCreatedBlueprints] = useState({});
  const [allCreated, setAllCreated] = useState(false);
  const [imageUploadState, setImageUploadState] = useState({});
  const [artworkUploadState, setArtworkUploadState] = useState({});

  const blueprintsWithImages = useMemo(() => {
    const imageBlueprintIds = new Set(allProductImages.map(img => img.projectBlueprintId));
    return blueprints.filter(bp => imageBlueprintIds.has(bp.id));
  }, [blueprints, allProductImages]);

  const variantCountByBlueprint = useMemo(() => {
    const map = {};
    for (const bp of productImageVariants) {
      map[bp.projectBlueprintId] = (bp.variants || []).length;
    }
    return map;
  }, [productImageVariants]);

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

  useEffect(() => {
    if (collectionId && productImageVariants.length === 0) {
      loadProductImageVariants(collectionId);
    }
  }, [collectionId, productImageVariants.length, loadProductImageVariants]);

  useEffect(() => {
    const existing = {};
    for (const img of allProductImages) {
      if (img.printifyImageId) {
        existing[img.id] = { status: 'done' };
      }
    }
    if (Object.keys(existing).length > 0) {
      setImageUploadState(prev => ({ ...existing, ...prev }));
    }
  }, [allProductImages]);

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

  const acceptedArtwork = useMemo(() =>
    (collectionArtwork || []).filter(a => a.accepted && a.active),
    [collectionArtwork]
  );

  const artworkImages = useMemo(() =>
    acceptedArtwork.map(a => ({
      ...a,
      imageUrl: api.getCollectionArtworkImageUrl(collectionId, a.itemId, a.id),
      type: 'artwork',
    })),
    [acceptedArtwork, collectionId, api]
  );

  const handleUploadImages = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setUploading(true);
    setMessage(null);

    for (const art of artworkImages) {
      if (artworkUploadState[art.id]?.status === 'done') continue;

      setArtworkUploadState(prev => ({ ...prev, [art.id]: { status: 'uploading' } }));

      try {
        const response = await printifyApi.uploadPrintifyArtworkImage({
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

    for (const img of allImages) {
      if (imageUploadState[img.id]?.status === 'done') continue;

      setImageUploadState(prev => ({ ...prev, [img.id]: { status: 'uploading' } }));

      try {
        const response = await printifyApi.uploadPrintifyProductImage({
          collectionId,
          productImageId: img.id,
        });

        if (response.data.success) {
          setImageUploadState(prev => ({ ...prev, [img.id]: { status: 'done' } }));
        } else {
          setImageUploadState(prev => ({ ...prev, [img.id]: { status: 'error' } }));
          setMessage({ type: 'error', text: response.data.message || `Failed to upload image for ${img.blueprintName}` });
        }
      } catch (error) {
        setImageUploadState(prev => ({ ...prev, [img.id]: { status: 'error' } }));
        setMessage({ type: 'error', text: error?.response?.data?.message || `Failed to upload image for ${img.blueprintName}` });
      }
    }

    setUploading(false);
  }, [collectionId, project, allImages, imageUploadState, artworkImages, artworkUploadState, printifyApi, setMessage]);

  const allImagesUploaded = useMemo(() => {
    const artworkDone = artworkImages.length > 0 && artworkImages.every(art => artworkUploadState[art.id]?.status === 'done');
    const productDone = allImages.length > 0 && allImages.every(img => imageUploadState[img.id]?.status === 'done');
    const artworkReady = artworkImages.length === 0 || artworkDone;
    const productReady = allImages.length === 0 || productDone;
    return artworkReady && productReady;
  }, [allImages, imageUploadState, artworkImages, artworkUploadState]);

  const handleCreateProducts = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setCreating(true);
    setMessage(null);

    let successCount = 0;
    let processedCount = 0;

    for (const bp of blueprintsWithImages) {
      const variantCount = variantCountByBlueprint[bp.id] || 0;
      if (variantCount === 0) continue;

      processedCount++;

      try {
        const response = await printifyApi.createPrintifyProduct({
          collectionId,
          projectBlueprintId: bp.id,
        });

        if (response.data.success) {
          setCreatedBlueprints(prev => ({ ...prev, [bp.id]: true }));
          successCount++;
        } else {
          setMessage({ type: 'error', text: response.data.message || `Failed to create product for ${bp.name}` });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || `Failed to create product for ${bp.name}` });
      }
    }

    setCreating(false);
    if (processedCount > 0 && successCount === processedCount) {
      setAllCreated(true);
    }
  }, [collectionId, project, blueprintsWithImages, variantCountByBlueprint, printifyApi, setMessage]);

  const handleNext = useCallback(() => {
    setStep(STEPS.PUBLISH_PRODUCTS);
  }, [setStep, STEPS]);

  const handleStart = useCallback(async () => {
    if (allImagesUploaded) {
      await handleCreateProducts();
    } else {
      await handleUploadImages();
      await handleCreateProducts();
    }
  }, [allImagesUploaded, handleUploadImages, handleCreateProducts]);

  const handleImageClick = useCallback((clickedImg) => {
    const images = allImages.map(img => img.imageUrl);
    setArtworkPreview({ images, src: clickedImg.imageUrl });
  }, [allImages, setArtworkPreview]);

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-lg mb-4">
        Collection artwork and product images will be uploaded to Printify, then products will be created.
      </p>

      {artworkImages.length > 0 && (
        <div className="mb-4">
          <h4 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-400">Collection Artwork (placements)</h4>
          <div className="flex flex-wrap gap-3 justify-center">
            {artworkImages.map((art) => {
              const state = artworkUploadState[art.id];
              const isUploading = state?.status === 'uploading';
              const isDone = state?.status === 'done';
              const isError = state?.status === 'error';
              return (
                <div key={art.id} className="relative w-[120px] h-[120px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 cursor-pointer" onClick={() => handleImageClick(art)}>
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
                </div>
              );
            })}
          </div>
        </div>
      )}

      {allImages.length > 0 && (
        <div className="mb-6">
          <h4 className="text-sm font-medium mb-2 text-gray-600 dark:text-gray-400">Product Images</h4>
          <div className="flex flex-wrap gap-3 justify-center">
            {allImages.map((img) => {
              const state = imageUploadState[img.id];
              const isUploading = state?.status === 'uploading';
              const isDone = state?.status === 'done';
              const isError = state?.status === 'error';
              return (
                <div key={img.id} className="relative w-[120px] h-[120px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 cursor-pointer" onClick={() => handleImageClick(img)}>
                  <img
                    src={img.imageUrl}
                    alt={img.blueprintName}
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
                </div>
              );
            })}
          </div>
        </div>
      )}

      <div className="mb-6">
        <List inModal={true}>
          {blueprintsWithImages.map((bp) => {
            const variantCount = variantCountByBlueprint[bp.id] || 0;
            const isCreated = createdBlueprints[bp.id] || false;
            return (
              <Item key={bp.id}>
                <div className="flex items-center w-full">
                  <Checked checked={isCreated} />
                  <span className="ml-3 text-sm font-medium text-gray-700 dark:text-gray-300">
                    {bp.name}
                  </span>
                  <span className="ml-auto text-xs text-gray-500 dark:text-gray-400">
                    {variantCount} {variantCount === 1 ? 'variant' : 'variants'}
                  </span>
                </div>
              </Item>
            );
          })}
        </List>
      </div>

      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline className="cancel" onClick={handleSaveDraft}>Save Draft</ButtonOutline>
        <Button
          onClick={allCreated ? handleNext : handleStart}
          disabled={uploading || creating || !project?.printifyStoreId}
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
        </Button>
      </div>
    </div>
  );
}
