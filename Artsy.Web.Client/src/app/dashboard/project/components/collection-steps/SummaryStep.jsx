import React, { useState, useMemo, useCallback } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import ButtonOutline from '@/components/ui/button-outline';
import List, { Item } from '@/components/ui/list';
import Checked from '@/components/ui/checked';
import Icon from '@/components/ui/icon';
import Carousel from '@/components/ui/carousel';

export default function SummaryStep() {
  const session = useSession();
  const {
    project, collectionId, collectionTitle, blueprints,
    allProductImages, collectionArtwork, printifyProducts,
    api, onClose, setMessage, instagramPost,
    setArtworkPreview, collectionProducts, goBack,
  } = useCollection();

  const printifyApi = Projects(session);
  const [publishedBlueprints, setPublishedBlueprints] = useState(() => {
    const map = {};
    for (const pp of printifyProducts) {
      if (pp.published && pp.projectBlueprintId) {
        map[pp.projectBlueprintId] = true;
      }
    }
    return map;
  });
  const [expanded, setExpanded] = useState(false);

  const allImages = useMemo(() => {
    const productImgs = (allProductImages || [])
      .filter(img => img.accepted && img.active)
      .map(img => img.imageUrl);
    const artworkImgs = (collectionArtwork || [])
      .filter(a => a.accepted && a.active)
      .map(a => api.getCollectionArtworkThumbUrl(collectionId, a.itemId, a.id));
    return [...productImgs, ...artworkImgs];
  }, [allProductImages, collectionArtwork, collectionId, api]);

  const fullSizeImages = useMemo(() => {
    const productImgs = (allProductImages || [])
      .filter(img => img.accepted && img.active)
      .map(img => (img.imageUrl || '').replace('?thumb=true', ''));
    const artworkImgs = (collectionArtwork || [])
      .filter(a => a.accepted && a.active)
      .map(a => api.getCollectionArtworkImageUrl(collectionId, a.itemId, a.id, true));
    return [...productImgs, ...artworkImgs];
  }, [allProductImages, collectionArtwork, collectionId, api]);

  const blueprintsWithImages = useMemo(() => {
    const imageBlueprintIds = new Set(allProductImages.map(img => img.projectBlueprintId));
    return blueprints.filter(bp => imageBlueprintIds.has(bp.id));
  }, [blueprints, allProductImages]);

  const printifyProductIds = useMemo(() => {
    const map = {};
    for (const pp of printifyProducts) {
      if (pp.printifyProductId && pp.projectBlueprintId) {
        map[pp.projectBlueprintId] = pp.printifyProductId;
      }
    }
    return map;
  }, [printifyProducts]);

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
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to unpublish product' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to unpublish product' });
    }
  }, [collectionId, printifyProducts, printifyApi, setMessage]);

  const handleImageClick = useCallback((src, index) => {
    setArtworkPreview({ images: fullSizeImages, src: fullSizeImages[index], _idx: index });
  }, [fullSizeImages, setArtworkPreview]);

  return (
    <div className="flex flex-col h-full" style={{ maxWidth: '900px' }}>
      <div className="flex flex-col items-center justify-center mb-6">
        <div className="flex items-center gap-2 mb-2">
          <Icon name="check_circle" className="w-6 h-6 text-green-600 dark:text-green-400" />
          <p className="text-lg font-medium text-green-600 dark:text-green-400 text-center">
            This Collection "{collectionTitle || 'Untitled'}" has been published successfully!
          </p>
        </div>
      </div>

      {allImages.length > 0 && (
        <div className="mb-6">
          <Carousel
            images={allImages}
            alt="Collection images"
            imageWidth="8rem"
            imageHeight="8rem"
            onImageClick={handleImageClick}
          />
        </div>
      )}

      <div className="mb-6">
        <h3 className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">Products Published to Printify</h3>
        <List inModal={true}>
          {blueprintsWithImages.map((bp) => {
            const isPublished = publishedBlueprints[bp.id] || false;
            const hasPrintifyProduct = printifyProducts.some(pp => pp.projectBlueprintId === bp.id);
            if (!hasPrintifyProduct) return null;
            const cp = collectionProducts.find(p => p.projectBlueprintId === bp.id);
            const displayName = cp?.name || bp.name;
            return (
              <Item key={bp.id}>
                <div className="flex items-center w-full">
                  <Checked checked={isPublished} />
                  <span className="ml-3 text-sm font-medium text-gray-700 dark:text-gray-300">
                    {displayName}
                  </span>
                  <div className="ml-auto flex items-center gap-3">
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

      <div className="mb-6">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold text-gray-700 dark:text-gray-300">Social Media Post</h3>
          {instagramPost?.permalink && (
            <ButtonOutline
              size="small"
              color="green"
              onClick={() => window.open(instagramPost.permalink, '_blank', 'noopener noreferrer')}
            >
              View on Instagram
            </ButtonOutline>
          )}
        </div>
        {instagramPost?.description && (
          <div className="bg-gray-50 dark:bg-gray-800 rounded-lg p-4">
            <p
              className={`text-sm text-gray-600 dark:text-gray-400 whitespace-pre-wrap ${!expanded ? 'line-clamp-5' : ''}`}
            >
              {instagramPost.description}
            </p>
            {instagramPost.description.length > 200 && (
              <button
                onClick={() => setExpanded(prev => !prev)}
                className="text-blue-600 dark:text-blue-400 text-xs mt-2 hover:underline"
              >
                {expanded ? 'Show Less...' : 'Read More...'}
              </button>
            )}
          </div>
        )}
      </div>

      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Close</ButtonOutline>
      </div>
    </div>
  );
}
