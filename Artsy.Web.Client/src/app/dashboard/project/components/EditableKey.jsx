import React, { useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Input from '@/components/forms/input';
import ButtonIcon from '@/components/ui/button-icon';
import Message from '@/components/ui/message';

export default function EditableKey({ projectId, keyValue, onUpdated }) {
  const session = useSession();
  const { updateKey } = Projects(session);
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(keyValue || '');
  const [message, setMessage] = useState(null);

  const handleKeyChange = (val) => {
    const clean = val.replace(/[^a-zA-Z0-9-]/g, '').slice(0, 16);
    setValue(clean);
  };

  const handleStartEdit = () => {
    setValue(keyValue || '');
    setEditing(true);
  };

  const handleCancel = () => {
    setEditing(false);
  };

  const handleSave = async () => {
    const trimmed = value.trim();
    if (!trimmed) {
      setMessage({ type: 'error', text: 'Key is required.' });
      return;
    }
    try {
      const response = await updateKey({ id: projectId, key: trimmed.toLowerCase() });
      if (response.data.success) {
        onUpdated(response.data.data.key);
        setEditing(false);
        setMessage(null);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to update key' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update key' });
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      handleSave();
    } else if (e.key === 'Escape') {
      handleCancel();
    }
  };

  return (
    <>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      {editing ? (
        <div className="flex items-center gap-2">
          <Input
            name="key"
            value={value}
            onChange={(e) => handleKeyChange(e.target.value)}
            onKeyDown={handleKeyDown}
            className="mb-0 w-64"
            autoFocus
          />
          <ButtonIcon name="check" onClick={handleSave} title="Save" />
          <ButtonIcon name="close" onClick={handleCancel} title="Cancel" />
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <p className="text-gray-600 dark:text-gray-400">Key: {keyValue}</p>
          <ButtonIcon name="edit" onClick={handleStartEdit} title="Edit key" />
        </div>
      )}
    </>
  );
}
