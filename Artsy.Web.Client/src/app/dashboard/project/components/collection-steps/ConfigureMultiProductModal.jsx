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
  const [generatingTags, setGeneratingTags] = useState(false);
  const [error, setError] = useState(null);
  const [savedAt, setSavedAt] = useState(null);

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

  useEffect(() => {
    if (!savedAt) return;
    const timer = setTimeout(() => setSavedAt(null), 3000);
    return () => clearTimeout(timer);
  }, [savedAt]);

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

  const handleGenerateTags = useCallback(async () => {
    if (!collectionId) return;
    setGeneratingTags(true);
    setError(null);
    try {
      const res = await api.generateMultiProductInfo({ collectionId, tagsOnly: true, title: title.trim() });
      if (res.data.success) {
        setTags(res.data.data.tags || '');
      } else {
        setError(res.data.message || 'Failed to generate tags.');
      }
    } catch (err) {
      setError(err?.response?.data?.message || 'Failed to generate tags.');
    } finally {
      setGeneratingTags(false);
    }
  }, [collectionId, api, title]);

  const handleSave = useCallback(async () => {
    if (!collectionId) return;
    setSaving(true);
    setError(null);
    try {
      const json = JSON.stringify({ title: title.trim(), description: description.trim(), tags: tags.trim() });
      const res = await api.saveMultiProductJson({ collectionId, multiProductJson: json });
      if (res.data.success) {
        setSavedAt(Date.now());
      } else {
        setError(res.data.message || 'Failed to save.');
      }
    } catch (err) {
      setError(err?.response?.data?.message || 'Failed to save.');
    } finally {
      setSaving(false);
    }
  }, [collectionId, title, description, tags, api]);

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

          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">Tags</label>
              <ButtonOutline size="small" color="green" onClick={handleGenerateTags} disabled={generatingTags}>
                {generatingTags ? <Spinner className="text-sm" /> : 'Generate Tags'}
              </ButtonOutline>
            </div>
            <TextArea
              name="multiProductTags"
              value={tags}
              onChange={(e) => setTags(e.target.value)}
              placeholder="Comma-delimited tags"
              rows={10}
            />
          </div>

          {error && <div className="text-sm text-red-500">{error}</div>}

          <div className="flex justify-end items-center gap-2 mt-2">
            {savedAt && (
              <span className="text-xs font-bold text-green-600 dark:text-green-400 mr-auto">
                Saved!
              </span>
            )}
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
