import React, { useState, useEffect } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Modal from '@/components/ui/modal';
import Button from '@/components/ui/button';
import TextArea from '@/components/forms/textarea';
import Tooltip from '@/components/ui/tooltip';
import Message from '@/components/ui/message';
import Icon from '@/components/ui/icon';

export default function ConfigureSocialMediaPosts({ show, projectId, project, onClose, onSaved }) {
  const session = useSession();
  const { updateSocialMediaConfig } = Projects(session);
  const [prompt, setPrompt] = useState('');
  const [description, setDescription] = useState('');
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    if (project) {
      setPrompt(project.socialMediaPrompt || '');
      setDescription(project.socialMediaDescription || '');
    }
  }, [project]);

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const response = await updateSocialMediaConfig({
        id: projectId,
        socialMediaPrompt: prompt || null,
        socialMediaDescription: description || null,
      });
      if (response.data.success) {
        if (onSaved) onSaved(response.data.data);
        onClose();
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to save' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save' });
    } finally {
      setSaving(false);
    }
  };

  if (!show) return null;

  return (
    <Modal title="Configure Social Media Posts" onClose={onClose}>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div className="flex items-center gap-1 mb-1">
        <label className="block text-sm font-medium">Prompt</label>
        <Tooltip text="Used to generate an SEO-friendly rich description for the social media post using AI. Optional — if left blank, only your description below will be used." />
      </div>
      <TextArea
        name="socialMediaPrompt"
        value={prompt}
        rows={4}
        placeholder="Optional — describe what you want the AI to generate for your social media post description..."
        onChange={(e) => setPrompt(e.target.value)}
        className="mb-4"
      />
      <div className="flex items-center gap-1 mb-1">
        <label className="block text-sm font-medium">Description</label>
        <Tooltip text="Appended below the AI-generated description when posting to Instagram. Use this for personal notes, links, or custom text you want included in every post." />
      </div>
      <TextArea
        name="socialMediaDescription"
        value={description}
        rows={4}
        placeholder="Your custom description text, appended below the AI-generated description..."
        onChange={(e) => setDescription(e.target.value)}
        className="mb-4"
      />
      <div className="buttons">
        <Button onClick={handleSave} disabled={saving}>
          {saving ? (
            <>
              <Icon name="progress_activity" spin className="w-4 h-4 mr-1" />
              Saving...
            </>
          ) : 'Save'}
        </Button>
        <Button className="cancel" onClick={onClose}>Cancel</Button>
      </div>
    </Modal>
  );
}
