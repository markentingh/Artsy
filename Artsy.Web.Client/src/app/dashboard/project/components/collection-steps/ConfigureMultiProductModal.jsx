import React, { useState, useEffect, useCallback } from 'react';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';

export default function ConfigureMultiProductModal({ show, collectionId, api, onClose }) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [tags, setTags] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!show || !collectionId) return;
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await api.getMultiProductJson(collectionId);
        if (cancelled) return;
        if (res.data.success && res.data.data.multiProductJson) {
          try {
            const parsed = JSON.parse(res.data.data.multiProductJson);
            setTitle(parsed.title || '');
            setDescription(parsed.description || '');
            setTags(parsed.tags || '');
          } catch {
            setTitle('');
            setDescription('');
            setTags('');
          }
        } else {
          setTitle('');
          setDescription('');
          setTags('');
        }
      } catch (err) {
        setError(err?.response?.data?.message || 'Failed to load multi-product config.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [show, collectionId, api]);

  const handleGenerateInfo = useCallback(async () => {
    if (!collectionId) return;
    setGenerating(true);
    setError(null);
    try {
      const res = await api.generateMultiProductInfo({ collectionId });
      if (res.data.success) {
        setTitle(res.data.data.title || '');
        setDescription(res.data.data.description || '');
        setTags(res.data.data.tags || '');
      } else {
        setError(res.data.message || 'Failed to generate info.');
      }
    } catch (err) {
      setError(err?.response?.data?.message || 'Failed to generate info.');
    } finally {
      setGenerating(false);
    }
  }, [collectionId, api]);

  const handleSave = useCallback(async () => {
    if (!collectionId) return;
    setSaving(true);
    setError(null);
    try {
      const json = JSON.stringify({ title: title.trim(), description: description.trim(), tags: tags.trim() });
      const res = await api.saveMultiProductJson({ collectionId, multiProductJson: json });
      if (res.data.success) {
        onClose();
      } else {
        setError(res.data.message || 'Failed to save.');
      }
    } catch (err) {
      setError(err?.response?.data?.message || 'Failed to save.');
    } finally {
      setSaving(false);
    }
  }, [collectionId, title, description, tags, api, onClose]);

  return (
    <Modal show={show} onClose={onClose} title="Configure Multi-Product Listing" className="w-[700px] max-w-[95vw]">
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Spinner className="text-4xl" />
        </div>
      ) : (
        <div className="flex flex-col gap-4 p-4">
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Generate the information for your Multi-Product listing, then copy &amp; paste them into your listing on Printify.
          </p>

          <div className="flex items-center justify-between">
            <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300">Listing Details</h4>
            <ButtonOutline size="small" color="green" onClick={handleGenerateInfo} disabled={generating}>
              {generating ? <Spinner className="text-sm" /> : 'Generate Info'}
            </ButtonOutline>
          </div>

          <Input
            label="Title"
            name="multiProductTitle"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Listing title"
          />

          <TextArea
            name="multiProductDescription"
            label="Description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Listing description"
            rows={6}
          />

          <Input
            label="Tags"
            name="multiProductTags"
            value={tags}
            onChange={(e) => setTags(e.target.value)}
            placeholder="Comma-delimited tags"
          />

          {error && <div className="text-sm text-red-500">{error}</div>}

          <div className="flex justify-end gap-2 mt-2">
            <ButtonOutline color="gray" onClick={onClose} disabled={saving}>Cancel</ButtonOutline>
            <ButtonOutline onClick={handleSave} disabled={saving}>
              {saving ? <Spinner className="text-sm" /> : 'Save Changes'}
            </ButtonOutline>
          </div>
        </div>
      )}
    </Modal>
  );
}
