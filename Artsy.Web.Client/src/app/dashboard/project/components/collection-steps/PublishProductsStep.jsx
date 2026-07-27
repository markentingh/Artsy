import React, { useState, useMemo, useCallback, useEffect } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import ButtonOutline from '@/components/ui/button-outline';
import Button from '@/components/ui/button';
import List, { Item } from '@/components/ui/list';
import Checked from '@/components/ui/checked';
import Icon from '@/components/ui/icon';

export default function PublishProductsStep() {
  const session = useSession();
  const {
    project, blueprints, allProductImages, collectionId,
    productImageVariants, STEPS, setStep,
    handleSaveDraft, setMessage,
  } = useCollection();

  const printifyApi = Projects(session);

  const [publishing, setPublishing] = useState(false);
  const [publishedBlueprints, setPublishedBlueprints] = useState({});
  const [allPublished, setAllPublished] = useState(false);
  const [printifyProducts, setPrintifyProducts] = useState([]);

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

  useEffect(() => {
    if (!collectionId) return;
    const loadProducts = async () => {
      try {
        const response = await printifyApi.getPrintifyProductsByCollection(collectionId);
        if (response.data.success) {
          setPrintifyProducts(response.data.data || []);
        }
      } catch (error) {
        // non-critical
      }
    };
    loadProducts();
  }, [collectionId]);

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

  const blueprintIdsWithPrintify = useMemo(() => {
    return new Set(printifyProducts.map(pp => pp.projectBlueprintId));
  }, [printifyProducts]);

  return (
    <div>
      <p className="text-center text-lg mb-4">
        The following products will be published on Printify.
      </p>
      <div className="mb-6">
        <List>
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
                  <span className="ml-auto text-xs text-gray-500 dark:text-gray-400">
                    {variantCount} {variantCount === 1 ? 'variant' : 'variants'}
                  </span>
                </div>
              </Item>
            );
          })}
        </List>
      </div>
      {allPublished && (
        <div className="flex items-center justify-between mb-4">
          <p className="text-sm font-medium text-green-600 dark:text-green-400">
            All products have been published successfully!
          </p>
          <Button onClick={handleNext}>Next</Button>
        </div>
      )}
      <div className="buttons flex justify-end gap-2">
        <ButtonOutline className="cancel" onClick={handleSaveDraft}>Save Draft</ButtonOutline>
        <Button onClick={handlePublishProducts} disabled={publishing || !project?.printifyStoreId || allPublished || printifyProducts.length === 0}>
          {publishing ? (
            <>
              <Icon name="progress_activity" spin className="w-4 h-4 inline mr-1" />
              Publishing...
            </>
          ) : (
            'Publish Products'
          )}
        </Button>
      </div>
    </div>
  );
}
