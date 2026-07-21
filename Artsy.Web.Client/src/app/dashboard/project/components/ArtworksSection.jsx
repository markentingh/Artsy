import React, { useEffect, useRef, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import EditArtworkModal from './EditArtworkModal';
import NewArtworkModal from './NewArtworkModal';
import Message from '@/components/ui/message';

export default function ArtworksSection({ projectId, onArtworkChanged }) {
  const session = useSession();
  const { getItems, createItem, deleteItem, reorderItems } = Projects(session);
  const [items, setItems] = useState([]);
  const [mount, setMount] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null);
  const [showNewModal, setShowNewModal] = useState(false);
  const [message, setMessage] = useState(null);
  const dragItem = useRef(null);
  const dragOverItem = useRef(null);
  const draggedRef = useRef(false);

  const fetchItems = async () => {
    try {
      const response = await getItems(projectId);
      if (response.data.success) {
        setItems(response.data.data || []);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to load artworks' });
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
    const itemIds = items.map((item) => item.id);
    dragItem.current = null;
    dragOverItem.current = null;
    try {
      await reorderItems({ projectId, itemIds });
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

  const handleDeleteArtwork = async (e, id) => {
    e.stopPropagation();
    if (!window.confirm('Delete this artwork?')) return;
    try {
      const response = await deleteItem({ id });
      if (response.data.success) {
        setItems((prev) => prev.filter((item) => item.id !== id));
        if (onArtworkChanged) onArtworkChanged();
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete artwork' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete artwork' });
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
        <h2 className="text-xl font-semibold">Artworks</h2>
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
              className="rounded-lg bg-white dark:bg-gray-800 shadow hover:shadow-md cursor-pointer overflow-hidden transition"
            >
              <div className="aspect-square w-full">
                <Carousel
                  images={item.thumbnails || []}
                  alt={item.title || 'Artwork'}
                  singleImage
                  infiniteScroll
                  placeholder="No Preview"
                  imageClassName="!max-h-none w-full h-full object-cover"
                />
              </div>
              <div className="p-3">
                <div className="flex items-baseline gap-2">
                  <span className="font-bold text-lg">{String(item.index).padStart(2, '0')}</span>
                  <span className="flex-1 text-sm text-gray-700 dark:text-gray-200 truncate">
                    {item.title ? item.title : 'Untitled Artwork'}
                  </span>
                </div>
                <div className="flex items-center justify-between mt-1">
                  <div className="flex items-center gap-3 text-xs text-gray-500 dark:text-gray-400">
                    {item.productCount > 0 && <span>{item.productCount} {item.productCount === 1 ? 'Product' : 'Products'}</span>}
                    {item.questionCount > 0 && <span>{item.questionCount} {item.questionCount === 1 ? 'Question' : 'Questions'}</span>}
                    {item.socialMedia && (
                      <Icon name="share" className="text-green-500" title="Social Media enabled" />
                    )}
                  </div>
                  <button
                    type="button"
                    onClick={(e) => handleDeleteArtwork(e, item.id)}
                    className="text-red-500 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300"
                    title="Delete artwork"
                  >
                    <Icon name="delete" />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      <NewArtworkModal
        show={showNewModal}
        onClose={() => setShowNewModal(false)}
        onSave={handleCreateArtwork}
      />
      <EditArtworkModal
        show={showModal}
        item={editingItem}
        onClose={handleCloseModal}
        onChanged={() => fetchItems()}
      />
    </div>
  );
}
