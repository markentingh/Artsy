import React, { useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Input from '@/components/forms/input';
import ButtonIcon from '@/components/ui/button-icon';
import Message from '@/components/ui/message';

export default function EditableTitle({ projectId, title, onUpdated }) {
  const session = useSession();
  const { updateTitle } = Projects(session);
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(title || '');
  const [message, setMessage] = useState(null);

  const handleStartEdit = () => {
    setValue(title || '');
    setEditing(true);
  };

  const handleCancel = () => {
    setEditing(false);
  };

  const handleSave = async () => {
    const trimmed = value.trim();
    if (!trimmed) {
      setMessage({ type: 'error', text: 'Title is required.' });
      return;
    }
    try {
      const response = await updateTitle({ id: projectId, title: trimmed });
      if (response.data.success) {
        onUpdated(response.data.data.title);
        setEditing(false);
        setMessage(null);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to update title' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update title' });
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
        <div className="flex items-center gap-2 flex-1">
          <Input
            name="title"
            value={value}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={handleKeyDown}
            className="mb-0 flex-1"
            autoFocus
          />
          <ButtonIcon name="check" onClick={handleSave} title="Save" />
          <ButtonIcon name="close" onClick={handleCancel} title="Cancel" />
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <h1 className="text-3xl">{title}</h1>
          <ButtonIcon name="edit" onClick={handleStartEdit} title="Edit title" />
        </div>
      )}
    </>
  );
}
