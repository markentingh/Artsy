import React, { useState, useMemo, useCallback, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import ButtonOutline from '@/components/ui/button-outline';
import Button from '@/components/ui/button';
import List, { Item } from '@/components/ui/list';
import Checked from '@/components/ui/checked';
import Icon from '@/components/ui/icon';
import Carousel from '@/components/ui/carousel';

export default function PublishProductsStep() {
  const session = useSession();
  const {
    project, blueprints, allProductImages, collectionId,
    productImageVariants, STEPS, setStep,
    handleSaveDraft, setMessage, printifyProducts,
    collectionArtwork, api,
  } = useCollection();

  const printifyApi = Projects(session);

  const [publishing, setPublishing] = useState(false);
  const [publishedBlueprints, setPublishedBlueprints] = useState({});
  const [allPublished, setAllPublished] = useState(false);

  useEffect(() => {
    const map = {};
    for (const pp of printifyProducts) {
      if (pp.published && pp.projectBlueprintId) {
        map[pp.projectBlueprintId] = true;
      }
    }
    setPublishedBlueprints(map);
    if (printifyProducts.length > 0 && printifyProducts.every(pp => pp.published)) {
      setAllPublished(true);
    }
  }, [printifyProducts]);

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

  const printifyProductIds = useMemo(() => {
    const map = {};
    for (const pp of printifyProducts) {
      if (pp.printifyProductId && pp.projectBlueprintId) {
        map[pp.projectBlueprintId] = pp.printifyProductId;
      }
    }
    return map;
  }, [printifyProducts]);

  const handlePublishProducts = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setPublishing(true);
    setMessage(null);

    let successCount = 0;
    const total = printifyProducts.length;

    for (const record of printifyProducts) {
      try {
        const response = await printifyApi.publishPrintifyProduct({
          collectionId,
          productId: record.productId,
        });
        if (response.data.success) {
          setPublishedBlueprints(prev => ({ ...prev, [record.projectBlueprintId]: true }));
          successCount++;
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to publish a product' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to publish a product' });
      }
    }

    setPublishing(false);
    if (successCount === total && total > 0) {
      setAllPublished(true);
    }
  }, [collectionId, project, printifyProducts, printifyApi, setMessage]);

  const handleNext = useCallback(() => {
    setStep(STEPS.SOCIAL_MEDIA);
  }, [setStep, STEPS]);

  const handleUnpublish = useCallback(async (bpId) => {
    const pp = printifyProducts.find(p => p.projectBlueprintId === bpId);
    if (!pp || !collectionId) return;

    try {
      const response = await printifyApi.unpublishPrintifyProduct({
        collectionId,
        productId: pp.productId,
      });
      if (response.data.success) {
        setPublishedBlueprints(prev => {
          const next = { ...prev };
          delete next[bpId];
          return next;
        });
        setAllPublished(false);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to unpublish product' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to unpublish product' });
    }
  }, [collectionId, printifyProducts, printifyApi, setMessage]);

  const blueprintIdsWithPrintify = useMemo(() => {
    return new Set(printifyProducts.map(pp => pp.projectBlueprintId));
  }, [printifyProducts]);

  const allImages = useMemo(() => {
    const productImgs = (allProductImages || [])
      .filter(img => img.accepted && img.active)
      .map(img => img.imageUrl);
    const artworkImgs = (collectionArtwork || [])
      .filter(a => a.accepted && a.active)
      .map(a => api.getCollectionArtworkImageUrl(collectionId, a.itemId, a.id, true));
    return [...productImgs, ...artworkImgs];
  }, [allProductImages, collectionArtwork, collectionId, api]);

  const apiBase = import.meta.env.VITE_API_URL || '';
  const downloadUrl = collectionId ? `${apiBase}/printify/image/download/${collectionId}` : null;

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-lg mb-4">
        The following products will be published on Printify.
      </p>
      <div className="mb-6">
        <List inModal={true}>
          {blueprintsWithImages.map((bp) => {
            const variantCount = variantCountByBlueprint[bp.id] || 0;
            const isPublished = publishedBlueprints[bp.id] || false;
            const hasPrintifyProduct = blueprintIdsWithPrintify.has(bp.id);
            if (!hasPrintifyProduct) return null;
            return (
              <Item key={bp.id}>
                <div className="flex items-center w-full">
                  <Checked checked={isPublished} />
                  <span className="ml-3 text-sm font-medium text-gray-700 dark:text-gray-300">
                    {bp.name}
                  </span>
                  <div className="ml-auto flex items-center gap-3">
                    <span className="text-xs text-gray-500 dark:text-gray-400">
                      {variantCount} {variantCount === 1 ? 'variant' : 'variants'}
                    </span>
                    {printifyProductIds[bp.id] && (
                      <ButtonOutline
                        size="small"
                        color="green"
                        onClick={() => window.open(`https://printify.com/app/product-details/${printifyProductIds[bp.id]}`, '_blank', 'noopener noreferrer')}
                      >
                        View on Printify
                      </ButtonOutline>
                    )}
                    {isPublished && (
                      <ButtonOutline
                        size="small"
                        color="gray"
                        onClick={() => handleUnpublish(bp.id)}
                      >
                        Unpublish
                      </ButtonOutline>
                    )}
                  </div>
                </div>
              </Item>
            );
          })}
        </List>
      </div>
      {allImages.length > 0 && (
        <>
          <div className="mb-4">
            <Carousel
              images={allImages}
              alt="Collection images"
              imageWidth="8rem"
              imageHeight="8rem"
            />
          </div>
          <p className="text-sm text-gray-600 dark:text-gray-400 mb-8 text-center mx-auto" style={{ maxWidth: '550px' }}>
            <a href={downloadUrl} target="_blank" rel="noreferrer" className="text-blue-600 dark:text-blue-400 underline">Download</a> all mockup images above and manually upload them to your products on Printify since their API does not currently support mockup uploads.
          </p>
        </>
      )}
      {allPublished && (
        <div className="flex flex-col items-center justify-center mb-4">
          <p className="text-sm font-medium text-green-600 dark:text-green-400 text-center mb-4">
            All products have been published successfully!
          </p>
        </div>
      )}
      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline className="cancel" onClick={handleSaveDraft}>Save Draft</ButtonOutline>
        {allPublished ? (
          <Button onClick={handleNext}>Next</Button>
        ) : (
          <Button onClick={handlePublishProducts} disabled={publishing || !project?.printifyStoreId || printifyProducts.length === 0}>
            {publishing ? (
              <>
                <Icon name="progress_activity" spin className="w-4 h-4 inline mr-1" />
                Publishing...
              </>
            ) : (
              'Publish Products'
            )}
          </Button>
        )}
      </div>
    </div>
  );
}
