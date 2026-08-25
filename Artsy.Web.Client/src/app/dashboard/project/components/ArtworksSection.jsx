import React, { useEffect, useRef, useState, lazy, Suspense } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Carousel from '@/components/ui/carousel';
import Tooltip from '@/components/ui/tooltip';
import Message from '@/components/ui/message';
import Spinner from '@/components/ui/spinner';
const NewArtworkModal = lazy(() => import('./NewArtworkModal'));
const EditArtworkModal = lazy(() => import('./EditArtworkModal'));
const ConfirmModal = lazy(() => import('@/components/ui/confirm-modal'));

export default function ArtworksSection({ projectId, onArtworkChanged }) {
  const session = useSession();
  const { getItems, createItem, deleteItem, reorderItems, getAllItemReferences } = Projects(session);
  const [items, setItems] = useState([]);
  const [mount, setMount] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null);
  const [showNewModal, setShowNewModal] = useState(false);
  const [message, setMessage] = useState(null);
  const [deleteTargetId, setDeleteTargetId] = useState(null);
  const [references, setReferences] = useState([]);
  const dragItem = useRef(null);
  const dragOverItem = useRef(null);
  const draggedRef = useRef(false);

  const fetchItems = async () => {
    try {
      const [itemsRes, refsRes] = await Promise.all([
        getItems(projectId),
        getAllItemReferences(projectId),
      ]);
      if (itemsRes.data.success) {
        setItems(itemsRes.data.data || []);
      } else {
        setMessage({ type: 'error', text: itemsRes.data.message || 'Failed to load artworks' });
      }
      if (refsRes.data.success) {
        setReferences(refsRes.data.data || []);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load artworks' });
    } finally {
      setMount(true);
    }
  };

  useEffect(() => {
    fetchItems();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  const handleNewArtwork = () => {
    setShowNewModal(true);
  };

  const handleCreateArtwork = async (title) => {
    try {
      const response = await createItem({ projectId, title });
      if (response.data.success) {
        const newItem = response.data.data;
        setItems((prev) => [...prev, newItem]);
        setShowNewModal(false);
        setEditingItem(newItem);
        setShowModal(true);
        if (onArtworkChanged) onArtworkChanged();
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to create artwork' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to create artwork' });
    }
  };

  const handleOpenEditArtwork = (item) => {
    if (draggedRef.current) {
      draggedRef.current = false;
      return;
    }
    setEditingItem(item);
    setShowModal(true);
  };

  const handleDragStart = (e, index) => {
    dragItem.current = index;
    draggedRef.current = true;
    e.dataTransfer.effectAllowed = 'move';
  };

  const handleDragEnter = (e, index) => {
    if (dragItem.current === null) return;
    dragOverItem.current = index;
    setItems((prev) => {
      const copied = [...prev];
      const dragged = copied[dragItem.current];
      copied.splice(dragItem.current, 1);
      copied.splice(index, 0, dragged);
      dragItem.current = index;
      return copied;
    });
  };

  const handleDragEnd = async (e) => {
    if (dragOverItem.current === null) {
      dragItem.current = null;
      dragOverItem.current = null;
      return;
    }

    const draggedId = items[dragOverItem.current]?.id;
    const frontId = items[dragOverItem.current + 1]?.id;
    const behindId = items[dragOverItem.current - 1]?.id;

    if (draggedId && frontId) {
      const draggedRefs = references.filter(r => r.itemId === draggedId && r.artworkId);
      if (draggedRefs.some(r => r.artworkId === frontId)) {
        setMessage({ type: 'error', text: 'You cannot move this artwork in front of another artwork that it references' });
        dragItem.current = null;
        dragOverItem.current = null;
        fetchItems();
        return;
      }
    }

    if (draggedId && behindId) {
      const behindRefs = references.filter(r => r.itemId === behindId && r.artworkId);
      if (behindRefs.some(r => r.artworkId === draggedId)) {
        setMessage({ type: 'error', text: 'You cannot move an artwork behind an artwork that references this artwork.' });
        dragItem.current = null;
        dragOverItem.current = null;
        fetchItems();
        return;
      }
    }

    const itemIds = items.map((item) => item.id);
    dragItem.current = null;
    dragOverItem.current = null;
    try {
      await reorderItems({ projectId, itemIds });
      // Update local index properties to match the new order (1-based, matching backend)
      setItems(prev => prev.map((item, i) => ({ ...item, index: i + 1 })));
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to reorder artworks' });
      fetchItems();
    }
  };

  const handleCloseModal = () => {
    setShowModal(false);
    setEditingItem(null);
    fetchItems();
    if (onArtworkChanged) onArtworkChanged();
  };

  const handleDeleteArtwork = (e, id) => {
    e.stopPropagation();
    setDeleteTargetId(id);
  };

  const handleConfirmDelete = async () => {
    if (!deleteTargetId) return;
    try {
      const response = await deleteItem({ id: deleteTargetId });
      if (response.data.success) {
        setItems((prev) => prev.filter((item) => item.id !== deleteTargetId));
        if (onArtworkChanged) onArtworkChanged();
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete artwork' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete artwork' });
    } finally {
      setDeleteTargetId(null);
    }
  };

  if (!mount) {
    return (
      <div className="p-8 text-center">
        <Icon name="progress_activity" spin className="w-6 h-6 mx-auto mb-2" />
        Loading artworks...
      </div>
    );
  }

  return (
    <div>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-1">
          <h2 className="text-xl font-semibold">Artwork Blueprints</h2>
          <Tooltip text="Artworks are the individual designs or uploaded images you assign to your project. They can be applied to printable products for sale, and collections will automatically post them to all of your connected social media accounts to help promote your products. Drag and drop your artworks below to reorder them." />
        </div>
        <ButtonOutline onClick={handleNewArtwork}>
          <Icon name="add" />
          <span className="ml-2">New Artwork</span>
        </ButtonOutline>
      </div>
      {items.length === 0 ? (
        <div className="p-12 text-center text-gray-600 dark:text-gray-400">
          No Artworks exist for this project
        </div>
      ) : (
        <div className="grid grid-cols-[repeat(auto-fill,250px)] gap-4 mb-8">
          {items.map((item, index) => (
            <div
              key={item.id}
              draggable
              onDragStart={(e) => handleDragStart(e, index)}
              onDragEnter={(e) => handleDragEnter(e, index)}
              onDragOver={(e) => e.preventDefault()}
              onDragEnd={handleDragEnd}
              onClick={() => handleOpenEditArtwork(item)}
              className="w-[300px] bg-white dark:bg-gray-800 rounded-lg shadow p-4 hover:shadow-md transition cursor-pointer"
            >
              <div className="w-full mb-3 rounded-lg overflow-hidden">
                <Carousel
                  images={item.thumbnails || []}
                  alt={item.title || 'Artwork'}
                  singleImage
                  infiniteScroll
                  placeholder="No Previews"
                  imageClassName="!max-w-[260px] object-contain"
                  maxHeight="260px"
                />
              </div>
              <div>
                <div className="flex items-baseline gap-2">
                  <span className="font-bold text-lg">{String(item.index).padStart(2, '0')}</span>
                  <span
                    className="flex-1 min-w-0 text-sm text-gray-700 dark:text-gray-200 truncate"
                    title={item.title ? item.title : 'Untitled Artwork'}
                  >
                    {item.title ? item.title : 'Untitled Artwork'}
                  </span>
                </div>
                <div className="flex items-center justify-between mt-1">
                  <p className="text-sm text-gray-500 dark:text-gray-400">
                    {item.artworkType === 'custom' ? 'Custom Image' : 'AI Artwork'}
                  </p>
                  <ButtonIcon name="delete" color="red" onClick={(e) => handleDeleteArtwork(e, item.id)} title="Delete artwork" />
                </div>
                <div className="flex items-center gap-3 text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {item.productCount > 0 && <span>{item.productCount} {item.productCount === 1 ? 'Product' : 'Products'}</span>}
                  {item.questionCount > 0 && <span>{item.questionCount} {item.questionCount === 1 ? 'Question' : 'Questions'}</span>}
                  {item.socialMedia && (
                    <Icon name="share" className="text-green-500" title="Social Media enabled" />
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {showNewModal && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <NewArtworkModal
            show={showNewModal}
            onClose={() => setShowNewModal(false)}
            onSave={handleCreateArtwork}
          />
        </Suspense>
      )}
      {showModal && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <EditArtworkModal
            show={showModal}
            item={editingItem}
            onClose={handleCloseModal}
            onChanged={() => fetchItems()}
          />
        </Suspense>
      )}

      {deleteTargetId && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ConfirmModal
            show={!!deleteTargetId}
            title="Delete Artwork"
            message="Do you really want to delete this artwork? This cannot be undone."
            onConfirm={handleConfirmDelete}
            onClose={() => setDeleteTargetId(null)}
          />
        </Suspense>
      )}
    </div>
  );
}
