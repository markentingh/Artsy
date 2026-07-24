import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import Message from '@/components/ui/message';
import ConfirmModal from '@/components/ui/confirm-modal';
import EditableTitle from './components/EditableTitle';
import EditableKey from './components/EditableKey';
import CollectionsSection from './components/CollectionsSection';
import ProjectChecklist from './components/ProjectChecklist';
import ArtworksSection from './components/ArtworksSection';
import QuestionsSection from './components/QuestionsSection';
import PublishingSection from './components/PublishingSection';
import ProductsSection from './components/ProductsSection';

export default function DashboardProject() {
  const { projectId } = useParams();
  const navigate = useNavigate();
  const session = useSession();
  const { getById, getChecklist, archiveProject, unarchiveProject } = Projects(session);

  const [project, setProject] = useState(null);
  const [mount, setMount] = useState(false);
  const [message, setMessage] = useState(null);
  const [checklist, setChecklist] = useState(null);
  const [showArchiveModal, setShowArchiveModal] = useState(false);

  const fetchProject = async () => {
    try {
      const response = await getById(projectId);
      if (response.data.success) {
        setProject(response.data.data);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to load project' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load project' });
    }
  };

  const fetchChecklist = async () => {
    try {
      const response = await getChecklist(projectId);
      if (response.data.success) {
        setChecklist(response.data.data);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load checklist' });
    }
  };

  useEffect(() => {
    const load = async () => {
      try {
        await fetchProject();
      } catch (error) {
        // error already handled
      }
      try {
        await fetchChecklist();
      } catch (error) {
        // error already handled
      }
      setMount(true);
    };
    load();
  }, [projectId]);

  const handleBack = () => {
    navigate('/dashboard/projects');
  };

  const isComplete = checklist &&
    checklist.imageGenerationSetup &&
    checklist.productBlueprintsAdded;

  const isArchived = project?.status === 0;

  const handleArchive = async () => {
    try {
      const resp = await archiveProject({ projectId });
      if (resp.data.success) {
        setProject((prev) => prev ? { ...prev, status: 0 } : prev);
        setShowArchiveModal(false);
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to archive project' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to archive project' });
    }
  };

  const handleUnarchive = async () => {
    try {
      const resp = await unarchiveProject({ projectId });
      if (resp.data.success) {
        setProject((prev) => prev ? { ...prev, status: 1 } : prev);
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to unarchive project' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to unarchive project' });
    }
  };

  if (!mount) {
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

      <ProjectChecklist checklist={checklist} />
      <CollectionsSection projectId={projectId} project={project} showNewButton={!!isComplete} />

      <hr className="border-gray-200 dark:border-gray-700 mb-8" />

      <ArtworksSection
        projectId={projectId}
        onArtworkChanged={fetchChecklist}
      />

      <hr className="border-gray-200 dark:border-gray-700 mb-8" />

      <ProductsSection
        projectId={projectId}
        onProductsChanged={fetchChecklist}
      />

      <hr className="border-gray-200 dark:border-gray-700 mb-8" />

      <QuestionsSection
        projectId={projectId}
        onChecklistChanged={fetchChecklist}
      />

      <hr className="border-gray-200 dark:border-gray-700 mb-8" />

      <PublishingSection
        projectId={projectId}
        project={project}
        onProjectUpdated={(updated) => setProject(updated)}
      />

      <hr className="border-gray-200 dark:border-gray-700 mb-8" />

      <div className="flex justify-center mb-8">
        {isArchived ? (
          <ButtonOutline onClick={handleUnarchive}>
            <Icon name="unarchive" />
            <span className="ml-2">Unarchive</span>
          </ButtonOutline>
        ) : (
          <button
            type="button"
            onClick={() => setShowArchiveModal(true)}
            className="px-4 py-2 border-2 border-red-500 text-red-500 rounded hover:bg-red-50 dark:hover:bg-red-900/20 transition"
          >
            Archive Project
          </button>
        )}
      </div>

      <ConfirmModal
        show={showArchiveModal}
        title="Archive Project"
        message="Do you really want to Archive this project? You can unarchive it later."
        confirmLabel="Archive"
        onConfirm={handleArchive}
        onClose={() => setShowArchiveModal(false)}
      />
    </div>
  );
}
