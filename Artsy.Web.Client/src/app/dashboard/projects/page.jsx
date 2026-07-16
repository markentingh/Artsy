import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import TextArea from '@/components/forms/textarea';
import Message from '@/components/ui/message';
import ColorPicker from '@/components/ui/ColorPicker';

export default function DashboardProjects() {
  const session = useSession();
  const navigate = useNavigate();
  const { getAll, getCollectionArtworkUrl, create } = Projects(session);

  const [projects, setProjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [showColorPicker, setShowColorPicker] = useState(false);
  const [message, setMessage] = useState(null);
  const [saving, setSaving] = useState(false);

  const [form, setForm] = useState({
    title: '',
    description: '',
    key: '',
    color: '#3B82F6'
  });

  const fetchProjects = async () => {
    setLoading(true);
    try {
      const response = await getAll();
      if (response.data.success) {
        setProjects(response.data.data || []);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to load projects' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load projects' });
    } finally {
      setLoading(false);
    }
  };

  const buildImageUrls = (artworkRows) => {
    const urls = [];
    for (const row of artworkRows || []) {
      for (let index = 1; index <= row.images && urls.length < 5; index++) {
        urls.push(getCollectionArtworkUrl(row.collectionId, row.id, index));
      }
      if (urls.length >= 5) break;
    }
    return urls;
  };

  useEffect(() => {
    fetchProjects();
  }, []);

  const handleOpenModal = () => {
    setForm({ title: '', description: '', key: '', color: '#3B82F6' });
    setShowModal(true);
  };

  const handleCloseModal = () => {
    setShowModal(false);
    setShowColorPicker(false);
  };

  const handleChange = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleKeyChange = (value) => {
    const clean = value.replace(/[^a-zA-Z0-9-]/g, '').slice(0, 16);
    setForm((prev) => ({ ...prev, key: clean }));
  };

  const handleColorChange = (color) => {
    setForm((prev) => ({ ...prev, color: color.hex }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setMessage(null);
    try {
      const response = await create({
        title: form.title,
        description: form.description,
        key: form.key,
        color: form.color
      });
      if (response.data.success) {
        setProjects((prev) => [{ ...response.data.data, artwork: [] }, ...prev]);
        handleCloseModal();
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to create project' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to create project' });
    } finally {
      setSaving(false);
    }
  };

  const handleRowClick = (projectId) => {
    navigate(`/dashboard/project/${projectId}`);
  };

  const renderCarousel = (project) => {
    const urls = buildImageUrls(project.artwork);
    if (urls.length === 0) {
      return (
        <div className="h-32 bg-gray-100 dark:bg-gray-700/50 rounded mb-4 flex items-center justify-center opacity-50">
          <span className="text-sm text-gray-600 dark:text-gray-400">
            No Collections to display just yet
          </span>
        </div>
      );
    }

    return (
      <div className="flex gap-2 overflow-x-auto mb-4 pb-2">
        {urls.map((url, index) => (
          <img
            key={`${project.id}-${index}`}
            src={url}
            alt={`Project artwork ${index + 1}`}
            className="h-32 w-32 object-cover rounded shrink-0 bg-gray-100 dark:bg-gray-700"
          />
        ))}
      </div>
    );
  };

  const renderRow = (project) => (
    <div
      key={project.id}
      className="border-b border-gray-200 dark:border-gray-700 last:border-b-0"
    >
      {renderCarousel(project)}
      <div
        className="flex items-center gap-4 p-4 hover:bg-gray-50 dark:hover:bg-gray-700/50 cursor-pointer"
        onClick={() => handleRowClick(project.id)}
      >
        <div
          className="w-8 h-12 rounded shrink-0"
          style={{ backgroundColor: project.color, width: '2em' }}
        />
        <div className="flex-1 min-w-0">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100 truncate">
            {project.title}
          </h3>
          {project.description && (
            <p className="text-sm text-gray-600 dark:text-gray-400 truncate">
              {project.description}
            </p>
          )}
          <p className="text-xs text-gray-500 dark:text-gray-500 mt-1">
            Key: {project.key}
          </p>
        </div>
      </div>
    </div>
  );

  return (
    <div>
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl">Projects</h1>
        <ButtonOutline onClick={handleOpenModal}>
          <Icon name="add" />
          <span className="ml-2">New Project</span>
        </ButtonOutline>
      </div>

      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <div className="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
        {loading ? (
          <div className="p-8 text-center">
            <Icon name="progress_activity" spin className="w-8 h-8 mx-auto mb-2" />
            Loading projects...
          </div>
        ) : projects.length === 0 ? (
          <div className="p-8 text-center text-gray-600 dark:text-gray-400">
            No projects yet. Click "+ New Project" to create one.
          </div>
        ) : (
          <div>{projects.map(renderRow)}</div>
        )}
      </div>

      {showModal && (
        <Modal title="New Project" onClose={handleCloseModal}>
          <form onSubmit={handleSubmit}>
            <Input
              label="Title"
              name="title"
              value={form.title}
              onChange={(e) => handleChange('title', e.target.value)}
              required
              maxLength={64}
            />

            <TextArea
              label="Description"
              name="description"
              value={form.description}
              onChange={(e) => handleChange('description', e.target.value)}
              maxLength={255}
              rows={3}
            />

            <Input
              label="Key"
              name="key"
              value={form.key}
              onChange={(e) => handleKeyChange(e.target.value)}
              required
              maxLength={16}
              note="Letters, numbers, and dashes only. Max 16 characters."
            />

            <div className="mb-4">
              <label className="block text-sm font-medium mb-1">Color</label>
              <div className="flex items-center gap-3">
                <div
                  className="w-10 h-10 rounded border border-gray-300 dark:border-gray-600 cursor-pointer"
                  style={{ backgroundColor: form.color }}
                  onClick={() => setShowColorPicker(true)}
                />
                <span className="text-sm font-mono text-gray-700 dark:text-gray-300">
                  {form.color}
                </span>
              </div>
            </div>

            {showColorPicker && (
              <ColorPicker
                color={form.color}
                onChange={handleColorChange}
                onClose={() => setShowColorPicker(false)}
              />
            )}

            <div className="buttons">
              <button
                type="submit"
                disabled={saving}
                className="px-4 py-2 bg-primary-600 text-white rounded hover:bg-primary-700 transition disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {saving ? 'Creating...' : 'Create Project'}
              </button>
              <button
                type="button"
                onClick={handleCloseModal}
                className="cancel px-4 py-2 bg-gray-500 text-white rounded hover:bg-gray-600 transition"
              >
                Cancel
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
