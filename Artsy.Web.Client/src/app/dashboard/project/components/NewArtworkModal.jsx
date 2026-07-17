import React, { useState, useEffect } from 'react';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import ButtonOutline from '@/components/ui/button-outline';
import Message from '@/components/ui/message';

export default function NewArtworkModal({ show, onClose, onSave }) {
  const [title, setTitle] = useState('');
  const [message, setMessage] = useState(null);

  useEffect(() => {
    if (show) {
      setTitle('');
      setMessage(null);
    }
  }, [show]);

  const handleSave = async () => {
    const trimmed = title.trim();
    if (!trimmed) {
      setMessage({ type: 'error', text: 'Title is required.' });
      return;
    }
    await onSave(trimmed);
  };

  if (!show) return null;

  return (
    <Modal title="New Artwork" onClose={onClose}>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <Input
        name="title"
        label="Artwork Title"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        placeholder="Enter artwork title"
        required
      />
      <div className="buttons flex justify-end gap-2">
        <ButtonOutline onClick={onClose} className="cancel">
          Cancel
        </ButtonOutline>
        <ButtonOutline onClick={handleSave}>
          Create
        </ButtonOutline>
      </div>
    </Modal>
  );
}
