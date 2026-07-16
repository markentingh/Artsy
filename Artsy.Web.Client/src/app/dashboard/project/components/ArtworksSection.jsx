import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import EditArtworkModal from './EditArtworkModal';
import Message from '@/components/ui/message';

export default function ArtworksSection({ projectId }) {
  const session = useSession();
  const { getItems, createItem, deleteItem } = Projects(session);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null);
  const [message, setMessage] = useState(null);

  const fetchItems = async () => {
    setLoading(true);
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
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchItems();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  const handleNewArtwork = async () => {
    try {
      const response = await createItem({ projectId });
      if (response.data.success) {
        const newItem = response.data.data;
        setItems((prev) => [...prev, newItem]);
        setEditingItem(newItem);
        setShowModal(true);
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
  };

  const handleDeleteArtwork = async (e, id) => {
    e.stopPropagation();
    if (!window.confirm('Delete this artwork?')) return;
    try {
      const response = await deleteItem({ id });
      if (response.data.success) {
        setItems((prev) => prev.filter((item) => item.id !== id));
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete artwork' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete artwork' });
    }
  };

  if (loading) {
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
                {item.blueprintNames && item.blueprintNames.length > 0
                  ? item.blueprintNames.join(', ')
                  : 'No product blueprints assigned to this artwork'}
              </span>
              <ButtonIcon name="delete" onClick={(e) => handleDeleteArtwork(e, item.id)} title="Delete artwork" />
            </div>
          ))}
        </div>
      )}

      <EditArtworkModal
        show={showModal}
        item={editingItem}
        onClose={handleCloseModal}
        onBlueprintsChanged={() => fetchItems()}
      />
    </div>
  );
}
