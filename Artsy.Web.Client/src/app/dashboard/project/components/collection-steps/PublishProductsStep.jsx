import React, { useMemo, useCallback, useState } from 'react';
import { useCollection } from '@/context/collection';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import CarouselElements from '@/components/ui/carousel-elements';
import EditCollectionProductDetails from './EditCollectionProductDetails';

export default function PublishProductsStep() {
  const {
    blueprints, allProductImages, collectionId, api,
    goBack, onClose,
    collectionProducts, setCollectionProducts, setArtworkPreview, mockups,
    printifyProducts,
  } = useCollection();

  const [editProductBp, setEditProductBp] = useState(null);

  // Only show active products
  const activeBlueprints = useMemo(() => {
    const activeIds = new Set(
      collectionProducts.filter(cp => cp.active).map(cp => cp.projectBlueprintId)
    );
    return blueprints.filter(bp => activeIds.has(bp.id));
  }, [blueprints, collectionProducts]);

  // Map printifyProductId by projectBlueprintId
  const printifyProductIdMap = useMemo(() => {
    const map = {};
    for (const pp of printifyProducts) {
      if (pp.printifyProductId && pp.projectBlueprintId) {
        map[pp.projectBlueprintId] = pp.printifyProductId;
      }
    }
    return map;
  }, [printifyProducts]);

  // Map mockups by projectBlueprintId (via printifyProduct entity ID)
  const mockupsByBlueprint = useMemo(() => {
    // printifyProducts: pp.Id (entity) → pp.projectBlueprintId
    // mockups: mockup.printifyProductId → pp.Id (entity)
    const entityToBlueprint = {};
    for (const pp of printifyProducts) {
      entityToBlueprint[pp.id] = pp.projectBlueprintId;
    }
    const map = {};
    for (const m of (mockups || [])) {
      const bpId = entityToBlueprint[m.printifyProductId];
      if (bpId) {
        if (!map[bpId]) map[bpId] = [];
        map[bpId].push(m);
      }
    }
    return map;
  }, [mockups, printifyProducts]);

  // Product images by blueprint
  const productImagesByBlueprint = useMemo(() => {
    const map = {};
    for (const img of (allProductImages || [])) {
      if (img.accepted && img.active) {
        if (!map[img.projectBlueprintId]) map[img.projectBlueprintId] = [];
        map[img.projectBlueprintId].push(img);
      }
    }
    return map;
  }, [allProductImages]);

  const getMinPrice = useCallback((bp) => {
    if (bp.minPrice != null) return `$${Number(bp.minPrice).toFixed(2)}`;
    // Try to compute from pricingJson
    if (bp.pricingJson) {
      try {
        const pricing = typeof bp.pricingJson === 'string' ? JSON.parse(bp.pricingJson) : bp.pricingJson;
        if (Array.isArray(pricing) && pricing.length > 0) {
          let min = null;
          for (const p of pricing) {
            if (p.price && p.price > 0 && (min === null || p.price < min)) min = p.price;
          }
          if (min != null) return `$${Number(min).toFixed(2)}`;
        }
      } catch { /* ignore */ }
    }
    return 'No price set';
  }, []);

  const apiBase = import.meta.env.VITE_API_URL || '';
  const downloadUrl = collectionId ? `${apiBase}/printify/image/download/${collectionId}` : null;

  // Build images for a blueprint: product images + mockup images
  const getBlueprintImages = useCallback((bp) => {
    const productImgs = (productImagesByBlueprint[bp.id] || []).map(img => img.imageUrl);
    const mockupImgs = (mockupsByBlueprint[bp.id] || []).map(m => m.imageUrl);
    return [...productImgs, ...mockupImgs];
  }, [productImagesByBlueprint, mockupsByBlueprint]);

  const getBlueprintFullImages = useCallback((bp) => {
    const productImgs = (productImagesByBlueprint[bp.id] || []).map(img => (img.imageUrl || '').replace('?thumb=true', ''));
    const mockupImgs = (mockupsByBlueprint[bp.id] || []).map(m => (m.imageUrl || '').replace('&thumb=true', ''));
    return [...productImgs, ...mockupImgs];
  }, [productImagesByBlueprint, mockupsByBlueprint]);

  const handleImageClick = useCallback((bp) => {
    const images = getBlueprintFullImages(bp);
    if (images.length > 0) {
      setArtworkPreview({ images, _idx: 0, alt: bp.name });
    }
  }, [getBlueprintFullImages, setArtworkPreview]);

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-lg mb-4">
        Your products have been created on Printify. Review them below and publish them manually.
      </p>

      {activeBlueprints.length > 0 ? (
        <CarouselElements
          className="mb-6"
          elements={activeBlueprints.map((bp) => {
            const images = getBlueprintImages(bp);
            const printifyId = printifyProductIdMap[bp.id];
            const cp = collectionProducts.find(c => c.projectBlueprintId === bp.id);
            const displayName = (cp && cp.name) ? cp.name : bp.name;
            return (
              <div
                key={bp.id}
                className="w-[300px] bg-white dark:bg-gray-800 rounded-lg shadow p-4 transition"
              >
                <div className="w-full mb-3 rounded-lg overflow-hidden relative">
                  <Carousel
                    images={images}
                    alt={displayName}
                    singleImage
                    infiniteScroll
                    placeholder="No Image"
                    imageClassName="!max-w-[260px] object-contain"
                    maxHeight="260px"
                    onImageClick={() => handleImageClick(bp)}
                  />
                </div>
                <div>
                  <p className="text-sm font-medium truncate" title={displayName}>{displayName}</p>
                  <div className="flex items-center justify-between mt-1">
                    <span className="text-gray-500 dark:text-gray-400">
                      {getMinPrice(bp)}
                    </span>
                    <div className="flex items-center gap-2">
                      <ButtonOutline
                        size="small"
                        onClick={() => setEditProductBp(bp)}
                      >
                        Edit Details
                      </ButtonOutline>
                      {printifyId && (
                        <ButtonOutline
                          size="small"
                          color="green"
                          onClick={() => window.open(`https://printify.com/app/product-details/${printifyId}`, '_blank', 'noopener noreferrer')}
                        >
                          View on Printify
                        </ButtonOutline>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            );
          })}
        />
      ) : (
        <div className="p-12 text-center text-gray-600 dark:text-gray-400 mb-6">
          No active products to publish.
        </div>
      )}

      {/* 2-column grid of help sections */}
      <div className="grid grid-cols-2 gap-6 mt-auto">
        <div>
          <h4 className="font-medium mb-2">Download Mockup Images</h4>
          <p className="text-sm text-gray-600 dark:text-gray-400 mb-3">
            Download the AI generated product images for all of your products within this collection and upload them to your each of your Printify products. To do so, Click on the "View on Printify" button for each product listed above and then upload your mockup images under the "Selected mockups" section within your Printify product details page.
          </p>
          <ButtonOutline
            size="small"
            onClick={() => window.open(downloadUrl, '_blank', 'noopener noreferrer')}
            disabled={!downloadUrl}
          >
            Download Mockups
          </ButtonOutline>
        </div>
        <div>
          <h4 className="font-medium mb-2">Manual Publishing</h4>
          <p className="text-sm text-gray-600 dark:text-gray-400 mb-3">
            You must go to Printify.com and manually publish each product yourself. Make sure to check the details of each product, upload your product images as mockups, add tags, select listing attributes, check shipping and pricing settings, and if you want to publish your product as a draft within your e-commerce store, check the box "Hide in store" under Publishing settings.
          </p>
          <ButtonOutline
            size="small"
            onClick={() => window.open('https://printify.com/app/store/products/', '_blank', 'noopener noreferrer')}
          >
            View Products
          </ButtonOutline>
        </div>
        <div>
          <h4 className="font-medium mb-2">Multi-Product Publishing</h4>
          <p className="text-sm text-gray-600 dark:text-gray-400 mb-3">
            You can add your products to a new multi-product listing in the My Products section of your Printify dashboard. This will convert all your products within this collection into a single listing within your e-commerce store and can save you potential listing fees in the long term.
          </p>
          <ButtonOutline
            size="small"
            onClick={() => window.open('https://printify.com/app/store/products/?tab=multiProductListings', '_blank', 'noopener noreferrer')}
          >
            Multi-Product Listings
          </ButtonOutline>
        </div>
        <div>
          <h4 className="font-medium mb-2">Manual Personalization</h4>
          <p className="text-sm text-gray-600 dark:text-gray-400 mb-3">
            You can set up manual personalization for each of your products to allow your customers to describe how they want to change the image, which will generate a new AI image for their order.
          </p>
          <ButtonOutline
            size="small"
            onClick={() => window.open('https://help.printify.com/hc/en-us/articles/29856933892241-How-do-I-set-up-product-personalization', '_blank', 'noopener noreferrer')}
          >
            Manual Personalization
          </ButtonOutline>
        </div>
      </div>

      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline color="gray" onClick={goBack}>Back</ButtonOutline>
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Close</ButtonOutline>
      </div>

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
