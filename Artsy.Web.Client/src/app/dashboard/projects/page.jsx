import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Icon from '@/components/ui/icon';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import TextArea from '@/components/forms/textarea';
import Message from '@/components/ui/message';
import ColorPicker from '@/components/ui/ColorPicker';

export default function DashboardProjects() {
  const session = useSession();
  const { getAll, create } = Projects(session);

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
        setProjects((prev) => [response.data.data, ...prev]);
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

  const renderRow = (project) => (
    <div
      key={project.id}
      className="flex items-center gap-4 p-4 border-b border-gray-200 dark:border-gray-700 last:border-b-0 hover:bg-gray-50 dark:hover:bg-gray-700/50"
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
  );

  return (
    <div>
      <div className="tool-bar mb-6">
        <h1 className="text-3xl font-bold">Projects</h1>
        <div className="right-side">
          <button
            type="button"
            onClick={handleOpenModal}
          >
            <Icon name="add" />
            <span>New Project</span>
          </button>
        </div>
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
              <button type="button" onClick={handleCloseModal} className="cancel">
                Cancel
              </button>
              <button type="submit" disabled={saving}>
                {saving ? 'Creating...' : 'Create Project'}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
