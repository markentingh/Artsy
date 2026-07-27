import React, { useState, useMemo, useCallback } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import ButtonOutline from '@/components/ui/button-outline';
import Button from '@/components/ui/button';
import List, { Item } from '@/components/ui/list';
import Checked from '@/components/ui/checked';
import Icon from '@/components/ui/icon';

export default function PublishProducts() {
  const session = useSession();
  const {
    project, blueprints, allProductImages, collectionId, api,
    productImageVariants,
    handleSaveDraft, setMessage,
  } = useCollection();

  const printifyApi = Projects(session);

  const [creating, setCreating] = useState(false);
  const [createdBlueprints, setCreatedBlueprints] = useState({});

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

  const handleCreateProducts = useCallback(async () => {
    if (!collectionId || !project?.printifyStoreId) {
      setMessage({ type: 'error', text: 'No Printify store selected for this project.' });
      return;
    }

    setCreating(true);
    setMessage(null);

    for (const bp of blueprintsWithImages) {
      const variantCount = variantCountByBlueprint[bp.id] || 0;
      if (variantCount === 0) continue;

      let variantIds = [];
      let pricing = [];
      try {
        const cfg = JSON.parse(bp.blueprintJson || '{}');
        variantIds = cfg.variantIds || [];
      } catch { /* skip */ }
      try {
        pricing = JSON.parse(bp.pricingJson || '[]');
      } catch { /* skip */ }

      const priceMap = {};
      pricing.forEach(p => { priceMap[p.variantId] = p.price; });

      const variants = variantIds.map(vid => ({
        id: vid,
        price: Math.round((priceMap[vid] || 0) * 100),
        is_enabled: true,
      }));

      let placements = {};
      try {
        placements = JSON.parse(bp.placementJson || '{}');
      } catch { /* skip */ }

      const printAreas = [];
      for (const [position, placement] of Object.entries(placements)) {
        const images = allProductImages.filter(img =>
          img.projectBlueprintId === bp.id && img.accepted
        );
        if (images.length === 0) continue;

        printAreas.push({
          variant_ids: variantIds,
          placeholders: [{
            position: position.toLowerCase(),
            images: images.map(img => ({
              id: img.responseId || '',
              x: 0.5,
              y: 0.5,
              scale: 1,
              angle: 0,
            })),
          }],
        });
      }

      const productRequest = {
        title: bp.name,
        description: bp.description || '',
        safety_information: bp.safetyInfo || '',
        blueprint_id: bp.blueprintId,
        print_provider_id: 1,
        variants,
        print_areas: printAreas,
      };

      try {
        const response = await printifyApi.createPrintifyProduct({
          collectionId,
          projectBlueprintId: bp.id,
          productId: bp.id,
          product: productRequest,
        });

        if (response.data.success) {
          setCreatedBlueprints(prev => ({ ...prev, [bp.id]: true }));
        } else {
          setMessage({ type: 'error', text: response.data.message || `Failed to create product for ${bp.name}` });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || `Failed to create product for ${bp.name}` });
      }
    }

    setCreating(false);
  }, [collectionId, project, blueprintsWithImages, variantCountByBlueprint, allProductImages, printifyApi, setMessage]);

  return (
    <div>
      <p className="text-center text-lg mb-4">
        The following products will be created on Printify.
      </p>
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
      <div className="buttons flex justify-end gap-2">
        <ButtonOutline className="cancel" onClick={handleSaveDraft}>Save Draft</ButtonOutline>
        <Button onClick={handleCreateProducts} disabled={creating || !project?.printifyStoreId}>
          {creating ? (
            <>
              <Icon name="progress_activity" spin className="w-4 h-4 inline mr-1" />
              Creating...
            </>
          ) : (
            'Create Products'
          )}
        </Button>
      </div>
    </div>
  );
}
