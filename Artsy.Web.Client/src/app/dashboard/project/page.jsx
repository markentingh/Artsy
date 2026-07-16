import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import Message from '@/components/ui/message';
import EditableTitle from './components/EditableTitle';
import EditableKey from './components/EditableKey';
import CollectionsSection from './components/CollectionsSection';
import ArtworksSection from './components/ArtworksSection';
import QuestionsSection from './components/QuestionsSection';

export default function DashboardProject() {
  const { projectId } = useParams();
  const navigate = useNavigate();
  const session = useSession();
  const { getById } = Projects(session);

  const [project, setProject] = useState(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState(null);

  const fetchProject = async () => {
    setLoading(true);
    try {
      const response = await getById(projectId);
      if (response.data.success) {
        setProject(response.data.data);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to load project' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load project' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchProject();
  }, [projectId]);

  const handleBack = () => {
    navigate('/dashboard/projects');
  };

  if (loading) {
    return (
      <div className="p-8 text-center">
        <Icon name="progress_activity" spin className="w-8 h-8 mx-auto mb-2" />
        Loading project...
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center gap-4 mb-6">
        <ButtonOutline onClick={handleBack}>
          <Icon name="arrow_back" />
          <span className="ml-2">Back</span>
        </ButtonOutline>
        <EditableTitle
          projectId={projectId}
          title={project?.title}
          onUpdated={(title) => setProject((prev) => prev ? { ...prev, title } : prev)}
        />
        <div className="ml-auto">
          <EditableKey
            projectId={projectId}
            keyValue={project?.key}
            onUpdated={(key) => setProject((prev) => prev ? { ...prev, key } : prev)}
          />
        </div>
      </div>

      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <CollectionsSection projectId={projectId} />

      <hr className="border-gray-200 dark:border-gray-700 mb-8" />

      <ArtworksSection projectId={projectId} />

      <hr className="border-gray-200 dark:border-gray-700 mb-8" />

      <QuestionsSection projectId={projectId} />
    </div>
  );
}
