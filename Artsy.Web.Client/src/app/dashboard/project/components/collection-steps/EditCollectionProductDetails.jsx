import React, { useState, useEffect, useCallback, useMemo } from 'react';
import Modal from '@/components/ui/modal';
import Carousel from '@/components/ui/carousel';
import Input from '@/components/forms/input';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import { List, Item } from '@/components/ui/list';
import Tooltip from '@/components/ui/tooltip';
import { artworkImageUrl, artworkThumbUrl } from '@/utils/artworkUrls';

export default function EditCollectionProductDetails({
  show, collectionId, projectBlueprintId, blueprintName,
  collectionProducts, allProductImages, mockups, printifyProducts,
  api, onClose, onSaved,
}) {
  const [activeTab, setActiveTab] = useState('info');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [message, setMessage] = useState(null);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [safetyInfo, setSafetyInfo] = useState('');
  const [variants, setVariants] = useState([]);
  const [variantPrices, setVariantPrices] = useState({});

  // Track original values to detect changes
  const [original, setOriginal] = useState({});

  const load = useCallback(async () => {
    if (!collectionId || !projectBlueprintId) return;
    setLoading(true);
    setMessage(null);
    try {
      const res = await api.getCollectionProductDetails(collectionId, projectBlueprintId);
      if (res.data.success) {
        const d = res.data.data;
        setName(d.name || '');
        setDescription(d.description || '');
        setSafetyInfo(d.safetyInfo || '');
        setVariants(d.variants || []);
        const prices = {};
        for (const v of (d.variants || [])) {
          prices[v.id] = v.price ? v.price.toFixed(2) : '';
        }
        setVariantPrices(prices);
        setOriginal({
          name: d.name || '',
          description: d.description || '',
          safetyInfo: d.safetyInfo || '',
          pricingJson: d.pricingJson || '[]',
        });
      }
    } catch (e) {
      setMessage({ type: 'error', text: 'Failed to load product details.' });
    } finally {
      setLoading(false);
    }
  }, [collectionId, projectBlueprintId, api]);

  useEffect(() => {
    if (show) {
      load();
      setActiveTab('info');
    }
  }, [show, load]);

  // Build carousel images from product images + mockups for this blueprint
  const carouselImages = useMemo(() => {
    const imgs = [];
    // Product images for this blueprint
    const productImgs = (allProductImages || []).filter(
      img => img.projectBlueprintId === projectBlueprintId && img.accepted && img.active
    );
    for (const img of productImgs) {
      imgs.push(img.imageUrl || '');
    }
    // Mockups for this blueprint (mockup.printifyProductId is the entity ID, pp.id)
    const pp = (printifyProducts || []).find(p => p.projectBlueprintId === projectBlueprintId);
    if (pp) {
      const mockupImgs = (mockups || [])
        .filter(m => m.printifyProductId === pp.id)
        .map(m => m.imageUrl || '');
      imgs.push(...mockupImgs);
    }
    return imgs.filter(Boolean);
  }, [allProductImages, projectBlueprintId, mockups, printifyProducts]);

  const isCreated = useMemo(() => {
    const pp = (printifyProducts || []).find(p => p.projectBlueprintId === projectBlueprintId);
    return pp && pp.printifyProductId;
  }, [printifyProducts, projectBlueprintId]);

  const handleGenerateInfo = async () => {
    setGenerating(true);
    setMessage(null);
    try {
      const res = await api.generateCollectionProductInfo({ collectionId, projectBlueprintId });
      if (res.data.success) {
        setName(res.data.data.title || '');
        setDescription(res.data.data.description || '');
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to generate info.' });
      }
    } catch (e) {
      setMessage({ type: 'error', text: e?.response?.data?.message || 'Failed to generate info.' });
    } finally {
      setGenerating(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      // Build pricing JSON from variant prices
      const pricingJson = JSON.stringify(
        variants.map(v => ({
          variantId: v.id,
          price: parseFloat(variantPrices[v.id] || 0),
        }))
      );

      // Detect changed fields
      const changedFields = [];
      if (name !== original.name) changedFields.push('name');
      if (description !== original.description) changedFields.push('description');
      if (safetyInfo !== original.safetyInfo) changedFields.push('safetyInfo');
      if (pricingJson !== original.pricingJson) changedFields.push('pricing');

      const res = await api.updateCollectionProductDetails({
        collectionId,
        projectBlueprintId,
        name,
        description,
        safetyInfo,
        pricingJson,
        updatePrintify: !!isCreated,
        changedFields,
      });

      if (res.data.success) {
        setOriginal({ name, description, safetyInfo, pricingJson });
        if (onSaved) onSaved();
        onClose();
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to save.' });
      }
    } catch (e) {
      setMessage({ type: 'error', text: e?.response?.data?.message || 'Failed to save.' });
    } finally {
      setSaving(false);
    }
  };

  if (!show) return null;

  const tabs = [
    { key: 'info', label: 'Info' },
    { key: 'pricing', label: 'Pricing' },
  ];

  return (
    <Modal title={`Edit Details — ${blueprintName || ''}`} onClose={onClose} className="max-w-[800px]">
      {message && (
        <p className={`text-sm mb-4 ${message.type === 'error' ? 'text-red-600 dark:text-red-400' : 'text-green-600'}`}>
          {message.text}
        </p>
      )}

      {/* Carousel at top */}
      {carouselImages.length > 0 && (
        <div className="mb-4">
          <Carousel
            images={carouselImages}
            imageClassName="object-contain"
            imageWidth="240px"
            imageHeight="240px"
          />
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-2 mb-4 border-b border-gray-200 dark:border-gray-700">
        {tabs.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`pb-2 px-3 text-sm font-medium transition ${
              activeTab === tab.key
                ? 'text-primary-600 dark:text-primary-500 border-b-2 border-primary-600 dark:border-primary-500'
                : 'text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="flex justify-center py-8">
          <Spinner className="text-2xl" />
        </div>
      ) : (
        <>
          {activeTab === 'info' && (
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Product Name & Description</span>
                <ButtonOutline onClick={handleGenerateInfo} disabled={generating} className="!py-1 !px-3 !text-sm">
                  {generating ? <Spinner className="text-base" /> : 'Generate Info'}
                </ButtonOutline>
              </div>
              <Input
                name="productName"
                label="Name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Enter product name"
              />
              <TextArea
                name="productDescription"
                label="Description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Enter product description"
                rows={5}
              />
              <TextArea
                name="safetyInfo"
                label="Safety Information"
                value={safetyInfo}
                onChange={(e) => setSafetyInfo(e.target.value)}
                placeholder="Enter safety information"
                rows={3}
              />
            </div>
          )}

          {activeTab === 'pricing' && (
            <div className="pb-4">
              {variants.length === 0 ? (
                <p className="text-sm text-gray-500 dark:text-gray-400">No variants available.</p>
              ) : (
                <>
                  <div className="flex items-center gap-1 mb-1">
                    <label className="block text-sm font-medium">Variant Pricing</label>
                    <Tooltip marginTop={2} text="Each variant can have its own price. Prices do not include shipping costs." />
                  </div>
                  <List inModal>
                    <Item bg={false} hover={false}>
                      <div className="flex items-center justify-between w-full">
                        <span className="text-sm font-medium"></span>
                        <div className="flex items-center gap-1">
                          <span className="text-sm text-gray-500 mr-5">Change All Variants</span>
                          <span className="text-sm text-gray-500">$</span>
                          <input
                            type="number"
                            min="0"
                            step="0.01"
                            placeholder="0.00"
                            className="w-24 px-2 py-1 text-right border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-primary-500"
                            onChange={(e) => {
                              const val = e.target.value;
                              const newPrices = {};
                              variants.forEach(v => { newPrices[v.id] = val; });
                              setVariantPrices(newPrices);
                            }}
                          />
                        </div>
                      </div>
                    </Item>
                    {[...variants]
                      .sort((a, b) => {
                        const aColor = a.color || 'Default';
                        const bColor = b.color || 'Default';
                        if (aColor !== bColor) return aColor.localeCompare(bColor);
                        const sizeOrder = ['XS', 'S', 'M', 'L', 'XL', '2XL', '3XL', '4XL', '5XL'];
                        const aIdx = sizeOrder.indexOf(a.size || '');
                        const bIdx = sizeOrder.indexOf(b.size || '');
                        if (aIdx !== -1 && bIdx !== -1) return aIdx - bIdx;
                        if (aIdx !== -1) return -1;
                        if (bIdx !== -1) return 1;
                        return (a.size || '').localeCompare(b.size || '');
                      })
                      .map((v) => {
                        const color = v.color || 'Default';
                        const size = v.size || v.color;
                        return (
                          <Item key={v.id}>
                            <div className="flex items-center justify-between w-full">
                              <span className="text-sm">{color} - {size}</span>
                              <div className="flex items-center gap-1">
                                <span className="text-sm text-gray-500">$</span>
                                <input
                                  type="number"
                                  min="0"
                                  step="0.01"
                                  placeholder="0.00"
                                  value={variantPrices[v.id] || ''}
                                  onChange={(e) => {
                                    setVariantPrices(prev => ({ ...prev, [v.id]: e.target.value }));
                                  }}
                                  className="w-24 px-2 py-1 text-right border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-primary-500"
                                />
                              </div>
                            </div>
                          </Item>
                        );
                      })}
                  </List>
                </>
              )}
            </div>
          )}

          <div className="flex justify-end mt-4">
            <ButtonOutline onClick={handleSave} disabled={saving}>
              {saving ? 'Saving...' : 'Save Changes'}
            </ButtonOutline>
          </div>
        </>
      )}
    </Modal>
  );
}
