import React, { useState, useMemo, useCallback, useEffect, useRef, lazy, Suspense } from 'react';
import { useCollection } from '@/context/collection';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import ButtonOutline from '@/components/ui/button-outline';
import Button from '@/components/ui/button';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';
import Spinner from '@/components/ui/spinner';
const ReconnectInstagramModal = lazy(() => import('@/components/modals/ReconnectInstagramModal'));

export default function PostSocialMedia() {
  const session = useSession();
  const {
    project, collectionId, collectionArtwork, allProductImages,
    items, setMessage, api, onClose,
    socialMediaImageOrder, setSocialMediaImageOrder,
    socialMediaSelectedImages, setSocialMediaSelectedImages,
    instagramPosted, setInstagramPosted,
    instagramPost, setInstagramPost,
    STEPS, setStep,
  } = useCollection();

  const printifyApi = Projects(session);

  const [posting, setPosting] = useState(false);
  const [posted, setPosted] = useState(false);
  const [description, setDescription] = useState('');
  const [generatingDescription, setGeneratingDescription] = useState(false);
  const [showReconnectModal, setShowReconnectModal] = useState(false);

  const gridRef = useRef(null);
  const draggedIdRef = useRef(null);
  const floatingPreviewRef = useRef(null);
  const ghostCellRef = useRef(null);

  const socialMediaItemIds = useMemo(() => {
    return new Set(items.filter(i => i.socialMedia).map(i => String(i.id)));
  }, [items]);

  const gridImages = useMemo(() => {
    const artworkImgs = (collectionArtwork || [])
      .filter(a => a.accepted && a.active && socialMediaItemIds.has(String(a.itemId)))
      .map(a => ({
        id: `artwork-${a.id}`,
        type: 'artwork',
        artworkId: a.id,
        itemId: a.itemId,
        url: a.opacity
          ? api.getCollectionArtworkJpgWithBgThumbUrl(collectionId, a.itemId, a.id)
          : api.getCollectionArtworkThumbUrl(collectionId, a.itemId, a.id),
        label: 'Artwork',
      }));

    const productImgs = (allProductImages || [])
      .filter(img => img.accepted && img.active)
      .map(img => ({
        id: `product-${img.id}`,
        type: 'product',
        productImageId: img.id,
        url: img.imageUrl,
        label: 'Product Image',
      }));

    return [...artworkImgs, ...productImgs];
  }, [collectionArtwork, allProductImages, socialMediaItemIds, collectionId, api]);

  useEffect(() => {
    if (!collectionId) return;
    let cancelled = false;
    setGeneratingDescription(true);
    printifyApi.generateSocialMediaDescription({ collectionId })
      .then(response => {
        if (cancelled) return;
        if (response.data.success && response.data.data?.description) {
          setDescription(response.data.data.description);
        }
      })
      .catch(error => {
        if (!cancelled) {
          console.error('Failed to generate social media description:', error);
        }
      })
      .finally(() => {
        if (!cancelled) setGeneratingDescription(false);
      });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [collectionId]);

  useEffect(() => {
    const currentIds = new Set(gridImages.map(img => img.id));
    setSocialMediaImageOrder(prev => {
      const existingOrder = prev.filter(id => currentIds.has(id));
      const existingSet = new Set(existingOrder);
      const newIds = gridImages.filter(img => !existingSet.has(img.id)).map(img => img.id);
      return [...existingOrder, ...newIds];
    });
    setSocialMediaSelectedImages(prev => {
      const updated = {};
      gridImages.forEach((img, i) => {
        const wasSelected = prev[img.id];
        updated[img.id] = wasSelected !== undefined ? wasSelected : i < 10;
      });
      return updated;
    });
  }, [gridImages, setSocialMediaImageOrder, setSocialMediaSelectedImages]);

  const selectedCount = useMemo(() => {
    return Object.values(socialMediaSelectedImages).filter(Boolean).length;
  }, [socialMediaSelectedImages]);

  const orderedImages = useMemo(() => {
    return socialMediaImageOrder
      .map(id => gridImages.find(img => img.id === id))
      .filter(Boolean);
  }, [socialMediaImageOrder, gridImages]);

  const handleToggleSelect = useCallback((id) => {
    setSocialMediaSelectedImages(prev => ({ ...prev, [id]: !prev[id] }));
  }, [setSocialMediaSelectedImages]);

  const cleanupDrag = useCallback(() => {
    if (floatingPreviewRef.current) {
      floatingPreviewRef.current.remove();
      floatingPreviewRef.current = null;
    }
    if (ghostCellRef.current) {
      ghostCellRef.current.remove();
      ghostCellRef.current = null;
    }
    if (blankRef.current) {
      blankRef.current.remove();
      blankRef.current = null;
    }
    const grid = gridRef.current;
    if (grid) {
      grid.querySelectorAll('[data-dragged="true"]').forEach(el => {
        el.style.display = '';
        el.removeAttribute('data-dragged');
      });
    }
    draggedIdRef.current = null;
  }, []);

  const blankRef = useRef(null);

  const handleDragStart = useCallback((e, id) => {
    draggedIdRef.current = id;
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', id);

    const draggedImg = gridImages.find(i => i.id === id);
    if (!draggedImg) return;

    const blank = document.createElement('div');
    blank.style.width = '1px';
    blank.style.height = '1px';
    blank.style.position = 'absolute';
    blank.style.top = '-9999px';
    document.body.appendChild(blank);
    e.dataTransfer.setDragImage(blank, 0, 0);
    blankRef.current = blank;

    const cell = e.currentTarget;
    cell.setAttribute('data-dragged', 'true');
    requestAnimationFrame(() => {
      if (cell.getAttribute('data-dragged') === 'true') {
        cell.style.display = 'none';
      }
    });

    const preview = document.createElement('div');
    preview.style.cssText = 'position:fixed;pointer-events:none;z-index:9999;width:150px;height:150px;opacity:0.5;transform:rotate(-5deg);left:' + (e.clientX - 75) + 'px;top:' + (e.clientY - 75) + 'px;';
    const previewImg = document.createElement('img');
    previewImg.src = draggedImg.url;
    previewImg.style.cssText = 'width:100%;height:100%;object-fit:cover;border-radius:0.5rem;border:1px solid #d1d5db;';
    preview.appendChild(previewImg);
    document.body.appendChild(preview);
    floatingPreviewRef.current = preview;
  }, [gridImages]);

  const handleDragEnd = useCallback(() => {
    cleanupDrag();
  }, [cleanupDrag]);

  const handleGridDragOver = useCallback((e) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';

    const preview = floatingPreviewRef.current;
    if (preview) {
      preview.style.left = (e.clientX - 75) + 'px';
      preview.style.top = (e.clientY - 75) + 'px';
    }

    const grid = gridRef.current;
    if (!grid) return;

    const cell = e.target.closest('[data-idx]');
    if (!cell) return;

    const idx = parseInt(cell.getAttribute('data-idx'), 10);
    const rect = cell.getBoundingClientRect();
    const isRight = (e.clientX - rect.left) > rect.width / 2;

    let ghost = ghostCellRef.current;
    if (!ghost) {
      ghost = document.createElement('div');
      ghost.className = 'rounded-lg border-2 border-dashed border-blue-500 bg-blue-100/80 dark:bg-blue-900/40 flex items-center justify-center';
      ghost.style.aspectRatio = '1 / 1';
      const label = document.createElement('span');
      label.className = 'text-sm font-medium text-blue-700 dark:text-blue-200';
      label.textContent = 'Drop here';
      ghost.appendChild(label);
      ghostCellRef.current = ghost;
    }

    const existingGhost = grid.querySelector('[data-ghost="true"]');
    if (existingGhost && existingGhost !== ghost) {
      existingGhost.remove();
    }

    ghost.setAttribute('data-ghost', 'true');

    if (isRight) {
      const nextSibling = cell.nextSibling;
      if (nextSibling !== ghost) {
        grid.insertBefore(ghost, nextSibling);
      }
    } else {
      if (cell !== ghost.nextSibling) {
        grid.insertBefore(ghost, cell);
      }
    }
  }, []);

  const handleGridDrop = useCallback((e) => {
    e.preventDefault();
    const draggedId = e.dataTransfer.getData('text/plain') || draggedIdRef.current;
    if (!draggedId) return;

    const grid = gridRef.current;
    if (!grid) return;

    const ghost = grid.querySelector('[data-ghost="true"]');
    if (!ghost) return;

    const cells = Array.from(grid.querySelectorAll('[data-idx]'));
    let dropIndex = cells.length;
    for (let i = 0; i < cells.length; i++) {
      if (cells[i] === ghost.nextSibling) {
        dropIndex = i;
        break;
      }
    }
    if (dropIndex === cells.length && ghost.previousSibling && cells.includes(ghost.previousSibling)) {
      dropIndex = cells.indexOf(ghost.previousSibling) + 1;
    }

    setSocialMediaImageOrder(prev => {
      const fromIdx = prev.indexOf(draggedId);
      if (fromIdx < 0) return prev;
      const next = [...prev];
      next.splice(fromIdx, 1);
      const adjustedDrop = fromIdx < dropIndex ? dropIndex - 1 : dropIndex;
      next.splice(adjustedDrop, 0, draggedId);
      return next;
    });

    cleanupDrag();
  }, [cleanupDrag]);

  const handlePost = useCallback(async () => {
    if (!collectionId || !project?.id) {
      setMessage({ type: 'error', text: 'Missing project or collection information.' });
      return;
    }

    if (!project?.postToInstagram) {
      setMessage({ type: 'error', text: 'Instagram posting is not enabled for this project.' });
      return;
    }

    const selected = orderedImages.filter(img => socialMediaSelectedImages[img.id]);
    if (selected.length === 0) {
      setMessage({ type: 'error', text: 'Please select at least one image to post.' });
      return;
    }

    setPosting(true);
    setMessage(null);

    try {
      const response = await printifyApi.postToSocialMedia({
        projectId: project.id,
        collectionId,
        description,
        images: selected.map((img, i) => ({
          productImageId: img.type === 'product' ? img.productImageId : null,
          artworkId: img.type === 'artwork' ? img.artworkId : null,
          itemId: img.type === 'artwork' ? img.itemId : null,
          sortOrder: i,
        })),
      });

      if (response.data.success) {
        setPosted(true);
        setInstagramPosted(true);
        if (response.data.data?.permalink) {
          setInstagramPost(prev => ({ ...prev, permalink: response.data.data.permalink, description }));
        } else {
          setInstagramPost(prev => ({ ...prev, description }));
        }
      } else {
        if (response.data.data?.tokenExpired) {
          setShowReconnectModal(true);
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to post to Instagram' });
        }
      }
    } catch (error) {
      if (error?.response?.data?.data?.tokenExpired) {
        setShowReconnectModal(true);
      } else {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to post to Instagram' });
      }
    }

    setPosting(false);
  }, [collectionId, project, orderedImages, socialMediaSelectedImages, description, printifyApi, setMessage, setInstagramPosted, setInstagramPost]);

  if (!project?.postToInstagram) {
    return (
      <div className="flex flex-col h-full">
        <p className="text-center text-lg mb-4">
          Instagram posting is not enabled for this project.
        </p>
        <div className="buttons flex justify-end gap-2 mt-auto">
          <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      <p className="text-center text-lg mb-4">
        Select and arrange images to post to Instagram.
      </p>

      {orderedImages.length > 0 ? (
        <div className="mb-4 flex justify-center">
          <div
            ref={gridRef}
            className="grid gap-3"
            style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))', maxWidth: '900px', width: '100%' }}
            onDragOver={handleGridDragOver}
            onDrop={handleGridDrop}
            onDragEnd={handleDragEnd}
          >
            {orderedImages.map((img, idx) => (
              <div
                key={img.id}
                data-idx={idx}
                draggable
                onDragStart={(e) => handleDragStart(e, img.id)}
                className="relative group cursor-grab active:cursor-grabbing"
              >
                <div className="relative rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600" style={{ aspectRatio: '1 / 1' }}>
                  <img
                    src={img.url}
                    alt={img.label}
                    className="w-full h-full object-cover"
                  />
                  <button
                    type="button"
                    onClick={() => handleToggleSelect(img.id)}
                    className="absolute top-2 left-2 z-10"
                    title={socialMediaSelectedImages[img.id] ? 'Deselect' : 'Select'}
                  >
                    <div
                      className="flex items-center justify-center rounded"
                      style={{
                        width: '24px',
                        height: '24px',
                        backgroundColor: 'rgba(59, 130, 246, 0.9)'
                      }}
                    >
                      <Icon
                        name={socialMediaSelectedImages[img.id] ? 'check' : ''}
                        className="text-white"
                        style={{ fontSize: '1rem' }}
                      />
                    </div>
                  </button>
                  {!socialMediaSelectedImages[img.id] && (
                    <div className="absolute inset-0 bg-black/40" />
                  )}
                  <span className="absolute bottom-2 right-2 text-xs text-white bg-black/50 px-2 py-0.5 rounded">
                    {img.label}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : (
        <p className="text-center text-gray-500 dark:text-gray-400 mb-4">
          No images available for posting.
        </p>
      )}

      {selectedCount > 10 && (
        <div className="mb-4">
          <Message type="warning">
            You can only post up to 10 images on Instagram
          </Message>
        </div>
      )}

      <div className="mb-4">
        <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
          Description
          {generatingDescription && (
            <span className="ml-2 text-xs text-gray-500 dark:text-gray-400 inline-flex items-center gap-1">
              <Icon name="progress_activity" spin className="w-3 h-3" />
              Generating...
            </span>
          )}
        </label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder={generatingDescription ? 'Generating description from AI...' : 'Enter the description for your Instagram post...'}
          rows={10}
          disabled={generatingDescription}
          className="w-full rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-700 dark:text-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-60"
        />
      </div>

      {(posted || instagramPosted) && (
        <div className="flex flex-col items-center justify-center mb-4">
          <p className="text-sm font-medium text-green-600 dark:text-green-400 text-center mb-4">
            Images have been posted to Instagram successfully!
          </p>
        </div>
      )}

      <div className="buttons flex justify-end gap-2 mt-auto">
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>Cancel</ButtonOutline>
        {!instagramPosted && (
          <Button onClick={handlePost} disabled={posting || orderedImages.filter(img => socialMediaSelectedImages[img.id]).length === 0}>
            {posting ? (
              <>
                <Icon name="progress_activity" spin className="w-4 h-4 inline mr-1" />
                Posting...
              </>
            ) : (
              'Post Images'
            )}
          </Button>
        )}
        {instagramPosted && (
          <Button onClick={() => setStep(STEPS.SUMMARY)}>Next</Button>
        )}
      </div>

      {showReconnectModal && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ReconnectInstagramModal
            show={showReconnectModal}
            onClose={() => setShowReconnectModal(false)}
            onReconnected={() => {
              setShowReconnectModal(false);
              handlePost();
            }}
          />
        </Suspense>
      )}

    </div>
  );
}
