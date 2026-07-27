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
    handleSaveDraft, setMessage,
  } = useCollection();

  const printifyApi = Projects(session);

  const [uploading, setUploading] = useState(false);
  const [creating, setCreating] = useState(false);
  const [createdBlueprints, setCreatedBlueprints] = useState({});
  const [allCreated, setAllCreated] = useState(false);
  const [products, setProducts] = useState([]);
  const [imageUploadState, setImageUploadState] = useState({});

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

  const productByBlueprint = useMemo(() => {
    const map = {};
    for (const p of products) {
      map[p.projectBlueprintId] = p;
    }
    return map;
  }, [products]);

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
        imageUrl: api.getProductImageUrl(collectionId, img.id),
      }))
    );
  }, [blueprintsWithImages, imagesByBlueprint, collectionId, api]);

  useEffect(() => {
    if (!collectionId) return;
    const loadProducts = async () => {
      try {
        const response = await printifyApi.getProductsByCollection(collectionId);
        if (response.data.success) {
          setProducts(response.data.data || []);
        }
      } catch (error) {
        // non-critical
      }
    };
    loadProducts();
  }, [collectionId]);

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

  const handleUploadImages = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setUploading(true);
    setMessage(null);

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
  }, [collectionId, project, allImages, imageUploadState, printifyApi, setMessage]);

  const allImagesUploaded = useMemo(() => {
    if (allImages.length === 0) return false;
    return allImages.every(img => imageUploadState[img.id]?.status === 'done');
  }, [allImages, imageUploadState]);

  const handleCreateProducts = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setCreating(true);
    setMessage(null);

    let successCount = 0;
    let processedCount = 0;
    const total = blueprintsWithImages.length;

    for (const bp of blueprintsWithImages) {
      const variantCount = variantCountByBlueprint[bp.id] || 0;
      if (variantCount === 0) continue;

      const product = productByBlueprint[bp.id];
      if (!product) continue;

      processedCount++;

      try {
        const response = await printifyApi.createPrintifyProduct({
          collectionId,
          productId: product.id,
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
  }, [collectionId, project, blueprintsWithImages, variantCountByBlueprint, productByBlueprint, printifyApi, setMessage]);

  const handleNext = useCallback(() => {
    setStep(STEPS.PUBLISH_PRODUCTS);
  }, [setStep, STEPS]);

  const handleStart = useCallback(async () => {
    await handleUploadImages();
  }, [handleUploadImages]);

  useEffect(() => {
    if (allImagesUploaded && !creating && !allCreated) {
      handleCreateProducts();
    }
  }, [allImagesUploaded, creating, allCreated, handleCreateProducts]);

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-lg mb-4">
        The following product images will be uploaded to Printify, then products will be created.
      </p>

      {allImages.length > 0 && (
        <div className="mb-6">
          <div className="flex flex-wrap gap-3 justify-center">
            {allImages.map((img) => {
              const state = imageUploadState[img.id];
              const isUploading = state?.status === 'uploading';
              const isDone = state?.status === 'done';
              const isError = state?.status === 'error';
              return (
                <div key={img.id} className="relative w-[120px] h-[120px] rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
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

      {allCreated && (
        <div className="flex items-center justify-between mb-4">
          <p className="text-sm font-medium text-green-600 dark:text-green-400">
            All products have been created successfully!
          </p>
          <Button onClick={handleNext}>Next</Button>
        </div>
      )}

      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline className="cancel" onClick={handleSaveDraft}>Save Draft</ButtonOutline>
        <Button
          onClick={handleStart}
          disabled={uploading || creating || !project?.printifyStoreId || allCreated || allImagesUploaded}
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
          ) : allImagesUploaded ? (
            'Images Uploaded'
          ) : (
            'Upload & Create Products'
          )}
        </Button>
      </div>
    </div>
  );
}
