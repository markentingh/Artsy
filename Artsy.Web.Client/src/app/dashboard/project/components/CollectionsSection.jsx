import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Message from '@/components/ui/message';
import Carousel from '@/components/ui/carousel';
import CarouselElements from '@/components/ui/carousel-elements';
import Tooltip from '@/components/ui/tooltip';
import ConfirmModal from '@/components/ui/confirm-modal';
import CollectionModal from './CollectionModal';

export default function CollectionsSection({ projectId, project, showNewButton = true }) {
  const session = useSession();
  const { getCollections, getCollectionArtworkThumbUrl, deleteCollection, updateCollectionTitle } = Projects(session);
  const [collections, setCollections] = useState([]);
  const [mount, setMount] = useState(false);
  const [message, setMessage] = useState(null);
  const [showCollectionModal, setShowCollectionModal] = useState(false);
  const [resumeCollectionId, setResumeCollectionId] = useState(null);
  const [resumeCollectionTitle, setResumeCollectionTitle] = useState('');
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [editingCollectionId, setEditingCollectionId] = useState(null);
  const [editingTitle, setEditingTitle] = useState('');

  const fetchCollections = async () => {
    try {
      const response = await getCollections(projectId);
      if (response.data.success) {
        setCollections(response.data.data || []);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to load collections' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load collections' });
    } finally {
      setMount(true);
    }
  };

  useEffect(() => {
    fetchCollections();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  const handleNewCollection = () => {
    setResumeCollectionId(null);
    setResumeCollectionTitle('');
    setShowCollectionModal(true);
  };

  const handleResumeCollection = (collection) => {
    setResumeCollectionId(collection.id);
    setResumeCollectionTitle(collection.title || `Collection #${collection.sequence}`);
    setShowCollectionModal(true);
  };

  const handleCollectionSaved = () => {
    fetchCollections();
  };

  const handleDeleteCollection = (collection, e) => {
    e.stopPropagation();
    setDeleteTarget(collection);
  };

  const handleEditTitle = (collection, e) => {
    e.stopPropagation();
    setEditingCollectionId(collection.id);
    setEditingTitle(collection.title || `Collection #${collection.sequence}`);
  };

  const handleCancelEditTitle = (e) => {
    e.stopPropagation();
    setEditingCollectionId(null);
    setEditingTitle('');
  };

  const handleSaveTitle = async (collection, e) => {
    e.stopPropagation();
    const trimmed = editingTitle.trim();
    if (!trimmed) {
      setMessage({ type: 'error', text: 'Title is required.' });
      return;
    }
    try {
      const resp = await updateCollectionTitle({ id: collection.id, title: trimmed });
      if (resp.data.success) {
        setCollections((prev) => prev.map((c) => c.id === collection.id ? { ...c, title: resp.data.data.title } : c));
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to update title' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update title' });
    } finally {
      setEditingCollectionId(null);
      setEditingTitle('');
    }
  };

  const handleConfirmDelete = async () => {
    if (!deleteTarget) return;
    try {
      const resp = await deleteCollection({ id: deleteTarget.id });
      if (resp.data.success) {
        setCollections((prev) => prev.filter((c) => c.id !== deleteTarget.id));
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to delete collection' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete collection' });
    } finally {
      setDeleteTarget(null);
    }
  };

  if (!showNewButton && mount && collections.length === 0) return null;

  return (
    <div className="mb-8">
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-1">
          <h2 className="text-xl font-semibold">Collections</h2>
          <Tooltip text="Create a new collection to publish products to your online shop and post the artworks to your social media accounts. Each collection combines your artworks with your selected products to generate the final designs that will be listed and sold." />
        </div>
        {showNewButton && (
          <ButtonOutline onClick={handleNewCollection}>
            <Icon name="add" />
            <span className="ml-2">New Collection</span>
          </ButtonOutline>
        )}
      </div>
      {!mount ? (
        <div className="p-8 text-center">
          <Icon name="progress_activity" spin className="w-6 h-6 mx-auto mb-2" />
          Loading collections...
        </div>
      ) : collections.length === 0 ? (
        <div className="p-12 text-center text-gray-600 dark:text-gray-400">
          No Collections exist for this project
        </div>
      ) : (
        <CarouselElements
          elements={collections.map((collection) => {
            const artworkImages = (collection.artwork || [])
              .filter(a => a.active)
              .map(a => getCollectionArtworkThumbUrl(collection.id, a.itemId, a.id));
            const productImages = (collection.productImages || [])
              .filter(p => p.active)
              .map(p => p.imageUrl);
            const images = [...productImages, ...artworkImages];
            return (
              <div
                key={collection.id}
                onClick={() => handleResumeCollection(collection)}
                className="w-[300px] bg-white dark:bg-gray-800 rounded-lg shadow p-4 hover:shadow-md transition cursor-pointer"
              >
                <div className="w-full mb-3 rounded-lg overflow-hidden">
                  <Carousel
                    images={images}
                    alt={`Collection #${collection.sequence}`}
                    singleImage
                    infiniteScroll
                    placeholder="No Artwork"
                    imageClassName="!max-h-none w-full h-full object-contain"
                  />
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex-1 min-w-0">
                    {editingCollectionId === collection.id ? (
                      <div className="flex items-center gap-1">
                        <input
                          type="text"
                          value={editingTitle}
                          onChange={(e) => setEditingTitle(e.target.value)}
                          onClick={(e) => e.stopPropagation()}
                          onKeyDown={(e) => { if (e.key === 'Enter') handleSaveTitle(collection, e); if (e.key === 'Escape') handleCancelEditTitle(e); }}
                          autoFocus
                          className="flex-1 px-2 py-1 text-sm border rounded bg-white dark:bg-gray-700 border-gray-300 dark:border-gray-600 focus:outline-none focus:ring-2 focus:ring-primary-500"
                        />
                        <ButtonIcon name="check" color="green" onClick={(e) => handleSaveTitle(collection, e)} title="Save title" />
                        <ButtonIcon name="close" color="red" onClick={handleCancelEditTitle} title="Cancel" />
                      </div>
                    ) : (
                      <>
                        <h3 className="font-medium mb-1">
                          {collection.title || `Collection #${collection.sequence}`}
                        </h3>
                        <p className="text-sm text-gray-500 dark:text-gray-400">
                          {new Date(collection.created).toLocaleDateString()}
                        </p>
                      </>
                    )}
                  </div>
                  {editingCollectionId !== collection.id && (
                    <div className="flex items-center gap-1">
                      <ButtonIcon name="edit" color="blue" onClick={(e) => handleEditTitle(collection, e)} title="Edit title" />
                      <ButtonIcon name="delete" color="red" onClick={(e) => handleDeleteCollection(collection, e)} title="Delete collection" />
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        />
      )}

      <CollectionModal
        show={showCollectionModal}
        projectId={projectId}
        project={project}
        collectionId={resumeCollectionId}
        collectionTitle={resumeCollectionTitle}
        onClose={() => setShowCollectionModal(false)}
        onSaved={handleCollectionSaved}
      />

      <ConfirmModal
        show={!!deleteTarget}
        title="Delete Collection"
        message={`Do you really want to delete this collection? This cannot be undone.`}
        onConfirm={handleConfirmDelete}
        onClose={() => setDeleteTarget(null)}
      />
    </div>
  );
}
