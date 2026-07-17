import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import EditArtworkModal from './EditArtworkModal';
import NewArtworkModal from './NewArtworkModal';
import Message from '@/components/ui/message';

export default function ArtworksSection({ projectId, onArtworkChanged }) {
  const session = useSession();
  const { getItems, createItem, deleteItem } = Projects(session);
  const [items, setItems] = useState([]);
  const [mount, setMount] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null);
  const [showNewModal, setShowNewModal] = useState(false);
  const [message, setMessage] = useState(null);

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
    setEditingItem(item);
    setShowModal(true);
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
        <div className="space-y-2">
          {items.map((item) => (
            <div
              key={item.id}
              onClick={() => handleOpenEditArtwork(item)}
              className="flex items-center gap-4 rounded-lg bg-white dark:bg-gray-800 p-4 shadow hover:bg-gray-50 dark:hover:bg-gray-700 cursor-pointer"
            >
              <span className="font-bold text-lg w-8 text-center">{String(item.index).padStart(2, '0')}</span>
              <span className="flex-1 text-gray-700 dark:text-gray-200">
                {item.title ? item.title : 'Untitled Artwork'}
              </span>
              <span className="text-sm text-gray-500 dark:text-gray-400 mr-2">
                {item.productCount || 0} {item.productCount === 1 ? 'Product' : 'Products'}
              </span>
              <ButtonIcon name="delete" onClick={(e) => handleDeleteArtwork(e, item.id)} title="Delete artwork" />
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
