import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Message from '@/components/ui/message';
import ConfirmModal from '@/components/ui/confirm-modal';
import { List, Item } from '@/components/ui/list';
import IdeaModal from './IdeaModal';
import CollectionModal from './CollectionModal';

export default function IdeaWorkshopSection({ projectId, project }) {
  const session = useSession();
  const { getIdeas, deleteIdea } = Projects(session);

  const [ideas, setIdeas] = useState([]);
  const [mount, setMount] = useState(false);
  const [message, setMessage] = useState(null);
  const [showNew, setShowNew] = useState(false);
  const [selectedIdeaId, setSelectedIdeaId] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [showCollectionModal, setShowCollectionModal] = useState(false);
  const [resumeCollectionId, setResumeCollectionId] = useState(null);
  const [resumeCollectionTitle, setResumeCollectionTitle] = useState('');

  const fetchIdeas = async () => {
    try {
      const res = await getIdeas(projectId);
      if (res.data.success) {
        setIdeas(res.data.data || []);
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to load ideas' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load ideas' });
    } finally {
      setMount(true);
    }
  };

  useEffect(() => {
    fetchIdeas();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  const handleOpenNew = () => {
    setSelectedIdeaId(null);
    setShowNew(true);
  };

  const handleOpenIdea = (idea) => {
    setShowNew(false);
    setSelectedIdeaId(idea.id);
  };

  const handleCloseModal = () => {
    setShowNew(false);
    setSelectedIdeaId(null);
  };

  const handleIdeaCreated = (idea) => {
    setShowNew(false);
    setSelectedIdeaId(idea.id);
    fetchIdeas();
  };

  const handleCollectionCreated = (collection) => {
    setShowNew(false);
    setSelectedIdeaId(null);
    setResumeCollectionId(collection.id);
    setResumeCollectionTitle(collection.title || 'New Collection');
    setShowCollectionModal(true);
    fetchIdeas();
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      const res = await deleteIdea(projectId, deleteTarget.id);
      if (res.data.success) {
        setIdeas((prev) => prev.filter((i) => i.id !== deleteTarget.id));
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to delete idea' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete idea' });
    } finally {
      setDeleteTarget(null);
    }
  };

  if (!mount) {
    return (
      <div className="p-8 text-center">
        <Icon name="progress_activity" spin className="w-6 h-6 mx-auto mb-2" />
        Loading ideas...
      </div>
    );
  }

  return (
    <div className="mb-8">
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold">Idea Workshop</h2>
        <ButtonOutline onClick={handleOpenNew}>
          <Icon name="add" />
          <span className="ml-2">New Idea</span>
        </ButtonOutline>
      </div>
      {ideas.length === 0 ? (
        <div className="p-12 text-center text-gray-600 dark:text-gray-400">
          No ideas yet. Click New Idea to get started.
        </div>
      ) : (
        <List>
          {ideas.map((idea) => (
            <Item
              key={idea.id}
              className="cursor-pointer"
              onClick={() => handleOpenIdea(idea)}
            >
              <div className="flex-1 min-w-0">
                <p className="font-medium truncate">{idea.title}</p>
                <p className="text-xs text-gray-500 dark:text-gray-400">
                  {new Date(idea.created).toLocaleDateString()} · {idea.variations?.length || 0} variations
                </p>
              </div>
              <ButtonIcon
                name="delete"
                color="red"
                title="Delete idea"
                onClick={(e) => {
                  e.stopPropagation();
                  setDeleteTarget(idea);
                }}
              />
            </Item>
          ))}
        </List>
      )}

      {(showNew || selectedIdeaId) && (
        <IdeaModal
          projectId={projectId}
          ideaId={selectedIdeaId}
          onClose={handleCloseModal}
          onIdeaCreated={handleIdeaCreated}
          onCollectionCreated={handleCollectionCreated}
        />
      )}

      <CollectionModal
        show={showCollectionModal}
        projectId={projectId}
        project={project}
        collectionId={resumeCollectionId}
        collectionTitle={resumeCollectionTitle}
        onClose={() => setShowCollectionModal(false)}
        onSaved={fetchIdeas}
      />

      <ConfirmModal
        show={!!deleteTarget}
        title="Delete Idea"
        message={`Delete idea "${deleteTarget?.title}"? This cannot be undone.`}
        onConfirm={handleDelete}
        onClose={() => setDeleteTarget(null)}
      />
    </div>
  );
}
