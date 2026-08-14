import React, { useState, useMemo, lazy, Suspense } from 'react';
import Modal from '@/components/ui/modal';
import Select from '@/components/forms/select';
import TextArea from '@/components/forms/textarea';
import Input from '@/components/forms/input';
import Button from '@/components/ui/button';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import { List, Item } from '@/components/ui/list';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import { useProductBlueprint } from '@/context/productBlueprint';
const ConfirmModal = lazy(() => import('@/components/ui/confirm-modal'));

export default function ProductImagesTab() {
  const session = useSession();
  const { createProductBlueprintImage, updateProductBlueprintImage, deleteProductBlueprintImage } = Projects(session);

  const {
    projectId,
    projectBlueprintId,
    blueprint,
    productBlueprintImages,
    setProductBlueprintImages,
    blueprintImages,
    getBlueprintImageUrl,
    variantColorOptions,
    selectedVariants,
    variants,
    saving,
    setSaving,
    setMessage,
  } = useProductBlueprint();

  const [showAddProductImage, setShowAddProductImage] = useState(false);
  const [newProductImageTitle, setNewProductImageTitle] = useState('');
  const [newProductImageColor, setNewProductImageColor] = useState('');
  const [newProductImagePrompt, setNewProductImagePrompt] = useState('');
  const [newProductImageImageId, setNewProductImageImageId] = useState(null);
  const [deleteProductImageTarget, setDeleteProductImageTarget] = useState(null);
  const [editingTitleId, setEditingTitleId] = useState(null);
  const [editingTitleValue, setEditingTitleValue] = useState('');
  const [showImageSelector, setShowImageSelector] = useState(false);
  const [imageSelectorColor, setImageSelectorColor] = useState('');
  const [imageSelectorCallback, setImageSelectorCallback] = useState(null);

  const missingColors = useMemo(() => {
    if (selectedVariants.length === 0) return [];
    const selectedColors = new Set();
    for (const v of variants) {
      if (selectedVariants.includes(v.id)) {
        selectedColors.add(v.color || 'Default');
      }
    }
    const existingColors = new Set(
      productBlueprintImages.map(img => (img.variantColor || '').toLowerCase())
    );
    return [...selectedColors].filter(c => !existingColors.has(c.toLowerCase()));
  }, [selectedVariants, variants, productBlueprintImages]);

  const filteredBlueprintImages = useMemo(() => {
    if (!imageSelectorColor) return [];
    return blueprintImages.filter(img => (img.variantColors || []).includes(imageSelectorColor));
  }, [blueprintImages, imageSelectorColor]);

  const openImageSelector = (color, callback) => {
    setImageSelectorColor(color);
    setImageSelectorCallback(() => callback);
    setShowImageSelector(true);
  };

  const handleSelectImage = (image) => {
    setShowImageSelector(false);
    if (imageSelectorCallback) {
      imageSelectorCallback(image);
    }
    setImageSelectorCallback(null);
  };

  const handleCreateMissingImage = (color) => {
    setNewProductImageColor(color);
    setNewProductImageTitle(`${color} Product Image`);
    setNewProductImagePrompt('');
    setNewProductImageImageId(null);
    setShowAddProductImage(true);
  };

  const handleAddProductImage = async () => {
    if (!projectBlueprintId) {
      setMessage({ type: 'error', text: 'Save the blueprint first before adding product images.' });
      return;
    }
    if (!newProductImageTitle.trim()) {
      setMessage({ type: 'error', text: 'Title is required.' });
      return;
    }
    if (!newProductImageColor) {
      setMessage({ type: 'error', text: 'Variant color is required.' });
      return;
    }
    setSaving(true);
    try {
      const resp = await createProductBlueprintImage({
        projectId,
        projectBlueprintId,
        title: newProductImageTitle.trim(),
        variantColor: newProductImageColor,
        prompt: newProductImagePrompt.trim(),
        imageId: newProductImageImageId,
      });
      if (resp.data.success) {
        setProductBlueprintImages(prev => [...prev, resp.data.data]);
        setShowAddProductImage(false);
        setNewProductImageTitle('');
        setNewProductImageColor('');
        setNewProductImagePrompt('');
        setNewProductImageImageId(null);
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to create product image.' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to create product image.' });
    } finally {
      setSaving(false);
    }
  };

  const handleConfirmDeleteProductImage = async () => {
    if (!deleteProductImageTarget) return;
    setSaving(true);
    try {
      const resp = await deleteProductBlueprintImage({ id: deleteProductImageTarget.id });
      if (resp.data.success) {
        setProductBlueprintImages(prev => prev.filter(img => img.id !== deleteProductImageTarget.id));
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to delete product image.' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete product image.' });
    } finally {
      setSaving(false);
      setDeleteProductImageTarget(null);
    }
  };

  const handleProductImagePromptChange = (id, newPrompt) => {
    setProductBlueprintImages(prev => prev.map(img => img.id === id ? { ...img, prompt: newPrompt } : img));
  };

  const handleProductImagePromptBlur = async (id) => {
    const img = productBlueprintImages.find(i => i.id === id);
    if (!img) return;
    try {
      await updateProductBlueprintImage({
        id,
        title: img.title,
        variantColor: img.variantColor,
        prompt: img.prompt,
        imageId: img.imageId || null,
      });
    } catch { /* ignore */ }
  };

  const handleStartEditTitle = (img) => {
    setEditingTitleId(img.id);
    setEditingTitleValue(img.title || '');
  };

  const handleConfirmEditTitle = async (id) => {
    const img = productBlueprintImages.find(i => i.id === id);
    if (!img) return;
    const newTitle = editingTitleValue.trim();
    if (!newTitle) {
      setMessage({ type: 'error', text: 'Title is required.' });
      return;
    }
    setProductBlueprintImages(prev => prev.map(i => i.id === id ? { ...i, title: newTitle } : i));
    setEditingTitleId(null);
    try {
      await updateProductBlueprintImage({
        id,
        title: newTitle,
        variantColor: img.variantColor,
        prompt: img.prompt,
        imageId: img.imageId || null,
      });
    } catch { /* ignore */ }
  };

  const handleCancelEditTitle = () => {
    setEditingTitleId(null);
    setEditingTitleValue('');
  };

  const newProductImageReference = blueprintImages.find(bi => bi.id === newProductImageImageId);

  return (
    <div>
      <div className="flex justify-end mb-2">
        <ButtonOutline onClick={() => setShowAddProductImage(true)} disabled={!projectBlueprintId}>
          <Icon name="add" className="mr-2" />
          <span>Add Product Image</span>
        </ButtonOutline>
      </div>

      {!projectBlueprintId && (
        <p className="text-sm text-gray-500 dark:text-gray-400 mt-2">Save the blueprint first to add product images.</p>
      )}

      <div className="mt-2">
        {productBlueprintImages.map((img) => (
          <div key={img.id} className="py-3 border-b border-gray-200 dark:border-gray-700 last:border-b-0">
            <div className="flex items-center justify-between mb-2">
              {editingTitleId === img.id ? (
                <div className="flex items-center gap-2">
                  <Input
                    name={`edit-title-${img.id}`}
                    value={editingTitleValue}
                    onChange={(e) => setEditingTitleValue(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') handleConfirmEditTitle(img.id);
                      if (e.key === 'Escape') handleCancelEditTitle();
                    }}
                    className="text-sm w-[30em]"
                    formPadding={false}
                    autoFocus
                  />
                  <ButtonIcon
                    name="check"
                    onClick={() => handleConfirmEditTitle(img.id)}
                    title="Save"
                  />
                  <ButtonIcon
                    name="close"
                    onClick={handleCancelEditTitle}
                    color="red"
                    title="Cancel"
                  />
                </div>
              ) : (
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium leading-none">{img.title}</span>
                  <ButtonIcon
                    name="edit"
                    onClick={() => handleStartEditTitle(img)}
                    title="Edit title"
                  />
                </div>
              )}
              <div className="flex items-center gap-2">
                <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 leading-none">
                  {img.variantColor}
                </span>
                <ButtonIcon
                  name="delete"
                  onClick={() => setDeleteProductImageTarget(img)}
                  color="red"
                  title="Delete"
                />
              </div>
            </div>
            <div className="flex gap-4 items-start">
              <div className="flex-1">
                <TextArea
                  name={`product-image-prompt-${img.id}`}
                  value={img.prompt || ''}
                  onChange={(e) => handleProductImagePromptChange(img.id, e.target.value)}
                  onBlur={() => handleProductImagePromptBlur(img.id)}
                  placeholder="Describe how the product should be displayed..."
                  rows={4}
                />
              </div>
              <div className="flex flex-col items-center shrink-0">
                <div
                  className="relative group"
                  style={{ width: 150, height: 150 }}
                >
                  {(() => {
                    const referenceImage = blueprintImages.find(bi => bi.id === img.imageId);
                    return referenceImage && blueprint ? (
                      <>
                        <img
                          src={getBlueprintImageUrl(blueprint.id, referenceImage.imageIndex, true)}
                          alt="Reference"
                          className="w-full h-full object-cover rounded-lg border border-gray-300 dark:border-gray-600"
                        />
                        <ButtonIcon
                          name="close"
                          color="red"
                          title="Remove reference image"
                          className="absolute top-1 right-1 z-10 opacity-0 group-hover:opacity-100 transition"
                          onClick={() => {
                            setProductBlueprintImages(prev =>
                              prev.map(i => i.id === img.id ? { ...i, imageId: null } : i)
                            );
                            updateProductBlueprintImage({
                              id: img.id,
                              title: img.title,
                              variantColor: img.variantColor,
                              prompt: img.prompt,
                              imageId: null,
                            }).catch(() => { });
                          }}
                        />
                      </>
                    ) : (
                      <div
                        className="w-full h-full rounded-lg border-2 border-dashed border-gray-300 dark:border-gray-600 flex items-center justify-center text-center p-2"
                      >
                        <span className="text-sm text-gray-500 dark:text-gray-400">No Reference Image</span>
                      </div>
                    );
                  })()}
                  <div className="absolute inset-0 flex items-center justify-center rounded-lg opacity-0 group-hover:opacity-100 transition bg-black/40">
                    <Button
                      size="small"
                      onClick={() => openImageSelector(img.variantColor, (image) => {
                        setProductBlueprintImages(prev =>
                          prev.map(i => i.id === img.id ? { ...i, imageId: image.id } : i)
                        );
                        updateProductBlueprintImage({
                          id: img.id,
                          title: img.title,
                          variantColor: img.variantColor,
                          prompt: img.prompt,
                          imageId: image.id,
                        }).catch(() => { });
                      })}
                    >
                      Select Image
                    </Button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>

      {projectBlueprintId && missingColors.length > 0 && (
        <div className="mt-4 mb-8">
          <label className="block text-sm font-medium mb-2">Missing Product Images</label>
          <List inModal={true}>
            {missingColors.map((color) => (
              <Item key={color} className="flex items-center justify-between">
                <span className="text-sm text-gray-700 dark:text-gray-300">
                  Missing Product Image for {color}
                </span>
                <ButtonOutline size="small" onClick={() => handleCreateMissingImage(color)}>
                  Create
                </ButtonOutline>
              </Item>
            ))}
          </List>
        </div>
      )}

      {showAddProductImage && (
        <Modal
          title="Add Product Image"
          onClose={() => setShowAddProductImage(false)}
          top
          className="min-w-[30em] max-w-full"
        >
          <Input
            label="Title"
            name="newProductImageTitle"
            value={newProductImageTitle}
            onChange={(e) => setNewProductImageTitle(e.target.value)}
            placeholder="Enter a title..."
            required
          />
          <Select
            label="Variant Color"
            name="newProductImageColor"
            options={variantColorOptions}
            value={newProductImageColor}
            onChange={(e) => {
              setNewProductImageColor(e.target.value);
              setNewProductImageImageId(null);
            }}
            placeholder="Select a variant color"
          />
          {newProductImageColor && (
            <div className="mb-4">
              <div className="flex items-center justify-between mb-2">
                <label className="block text-sm font-medium">Reference Image</label>
                <ButtonOutline
                  size="small"
                  onClick={() => openImageSelector(newProductImageColor, (image) => {
                    setNewProductImageImageId(image.id);
                  })}
                >
                  Select Image
                </ButtonOutline>
              </div>
              {newProductImageReference ? (
                <img
                  src={getBlueprintImageUrl(blueprint.id, newProductImageReference.imageIndex, true)}
                  alt="Reference"
                  className="w-[250px] h-[250px] object-cover rounded-lg border border-gray-300 dark:border-gray-600"
                />
              ) : (
                <div className="py-8 text-center text-sm text-gray-500 dark:text-gray-400">
                  Select a mockup image to use as a reference when generating your product image with AI
                </div>
              )}
            </div>
          )}
          <div className="mb-4">
            <label className="block text-sm font-medium mb-1">Prompt</label>
            <TextArea
              name="newProductImagePrompt"
              value={newProductImagePrompt}
              onChange={(e) => setNewProductImagePrompt(e.target.value)}
              placeholder="Describe how the product should be displayed..."
              rows={4}
            />
          </div>
          <div className="buttons flex justify-end gap-2">
            <ButtonOutline color="gray" className="cancel" onClick={() => setShowAddProductImage(false)}>
              Cancel
            </ButtonOutline>
            <ButtonOutline onClick={handleAddProductImage} disabled={saving}>
              {saving ? 'Saving...' : 'Add'}
            </ButtonOutline>
          </div>
        </Modal>
      )}

      {showImageSelector && (
        <Modal
          title="Select Reference Image"
          onClose={() => setShowImageSelector(false)}
          top
          className="min-w-[50em] max-w-full"
        >
          {filteredBlueprintImages.length > 0 ? (
            <div className="grid grid-cols-[repeat(auto-fill,200px)] gap-4 max-h-[60vh] overflow-y-auto">
              {filteredBlueprintImages.map((img) => (
                <div
                  key={img.id}
                  className="border border-gray-300 dark:border-gray-600 rounded-lg overflow-hidden cursor-pointer hover:border-primary-500 hover:ring-2 hover:ring-primary-500 transition"
                  onClick={() => handleSelectImage(img)}
                >
                  <img
                    src={getBlueprintImageUrl(blueprint.id, img.imageIndex, true)}
                    alt=""
                    className="w-[200px] h-[200px] object-cover"
                  />
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-gray-500 dark:text-gray-400">No reference images available for this variant color.</p>
          )}
          <div className="buttons flex justify-end gap-2 mt-4">
            <ButtonOutline color="gray" onClick={() => setShowImageSelector(false)}>
              Cancel
            </ButtonOutline>
          </div>
        </Modal>
      )}

      {deleteProductImageTarget && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ConfirmModal
            show={!!deleteProductImageTarget}
            title="Delete Product Image"
            message={`Do you really want to delete this product image${deleteProductImageTarget ? ` (${deleteProductImageTarget.title} - ${deleteProductImageTarget.variantColor})` : ''}? This cannot be undone.`}
            onConfirm={handleConfirmDeleteProductImage}
            onClose={() => setDeleteProductImageTarget(null)}
          />
        </Suspense>
      )}
    </div>
  );
}
