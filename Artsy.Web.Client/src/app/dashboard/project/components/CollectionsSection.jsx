import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import Message from '@/components/ui/message';

export default function CollectionsSection({ projectId, showNewButton = true }) {
  const session = useSession();
  const { getCollections } = Projects(session);
  const [collections, setCollections] = useState([]);
  const [mount, setMount] = useState(false);
  const [message, setMessage] = useState(null);

  useEffect(() => {
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
    fetchCollections();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  const handleNewCollection = () => {
    // TODO: open new collection modal
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
        <h2 className="text-xl font-semibold">Collections</h2>
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
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {collections.map((collection) => (
            <div
              key={collection.id}
              className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 hover:shadow-md transition cursor-pointer"
            >
              <h3 className="font-medium mb-1">{collection.title}</h3>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                {new Date(collection.created).toLocaleDateString()}
              </p>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
