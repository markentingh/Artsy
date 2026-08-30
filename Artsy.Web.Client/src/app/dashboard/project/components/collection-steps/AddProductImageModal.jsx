import React, { useState } from 'react';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';

export default function AddProductImageModal({ show, onClose, onConfigure, api, collectionId, projectId }) {
  const [title, setTitle] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const handleConfigure = async () => {
    if (!title.trim()) {
      setError('Enter a product image title.');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const res = await api.addCollectionProductImage({
        projectId,
        collectionId,
        title: title.trim(),
      });
      if (res.data.success) {
        const newId = res.data.data.id;
        onConfigure(newId, title.trim());
        setTitle('');
      } else {
        setError(res.data.message || 'Failed to create product image');
      }
    } catch (err) {
      setError(err?.response?.data?.message || 'Failed to create product image');
    } finally {
      setSaving(false);
    }
  };

  const handleClose = () => {
    setTitle('');
    setError(null);
    onClose();
  };

  return (
    <Modal show={show} onClose={handleClose} title="Add Product Image" size="sm">
      <div className="flex flex-col gap-4 p-4">
        {error && <div className="text-sm text-red-500">{error}</div>}
        <Input
          name="productImageTitle"
          label="Product Image Title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="e.g. Front View, Lifestyle, etc."
        />
        <div className="flex justify-end gap-2">
          <ButtonOutline color="gray" onClick={handleClose}>Cancel</ButtonOutline>
          <ButtonOutline onClick={handleConfigure} disabled={saving || !title.trim()}>
            {saving ? <Spinner className="text-sm" /> : 'Configure'}
          </ButtonOutline>
        </div>
      </div>
    </Modal>
  );
}
