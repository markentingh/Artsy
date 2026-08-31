import React, { useState, useRef, useCallback } from 'react';
import Modal from '@/components/ui/modal';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import Icon from '@/components/ui/icon';

export default function ReplaceMockupModal({ show, onClose, mockup, projectId, collectionId, onReplaced }) {
  const [selectedFile, setSelectedFile] = useState(null);
  const [previewUrl, setPreviewUrl] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState(null);
  const [dragging, setDragging] = useState(false);
  const fileInputRef = useRef(null);

  const handleFileSelect = useCallback((file) => {
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/jpg'].includes(file.type)) {
      setError('Only JPG and PNG files are allowed.');
      return;
    }
    setError(null);
    setSelectedFile(file);
    const url = URL.createObjectURL(file);
    setPreviewUrl(url);
  }, []);

  const handleFileChange = useCallback((e) => {
    const file = e.target.files?.[0];
    handleFileSelect(file);
  }, [handleFileSelect]);

  const handleDrop = useCallback((e) => {
    e.preventDefault();
    setDragging(false);
    const file = e.dataTransfer.files?.[0];
    handleFileSelect(file);
  }, [handleFileSelect]);

  const handleDragOver = useCallback((e) => {
    e.preventDefault();
    setDragging(true);
  }, []);

  const handleDragLeave = useCallback((e) => {
    e.preventDefault();
    setDragging(false);
  }, []);

  const handleUpload = useCallback(async () => {
    if (!selectedFile) {
      setError('Please select a file first.');
      return;
    }
    setUploading(true);
    setError(null);
    try {
      const formData = new FormData();
      formData.append('ProjectId', projectId);
      formData.append('CollectionId', collectionId);
      formData.append('MockupId', mockup.id);
      formData.append('File', selectedFile);

      const apiBase = import.meta.env.VITE_API_URL || '';
      const response = await fetch(`${apiBase}/api/printify-products/replace-mockup-image`, {
        method: 'POST',
        body: formData,
        credentials: 'include',
      });

      if (!response.ok) {
        const data = await response.json().catch(() => ({}));
        throw new Error(data.message || 'Failed to upload mockup image.');
      }

      const data = await response.json();
      if (!data.success) {
        throw new Error(data.message || 'Failed to upload mockup image.');
      }

      const cacheBust = Math.floor(Math.random() * 1000000);
      const newImageUrl = `${data.data.imageUrl}&r=${cacheBust}`;
      onReplaced(mockup.id, newImageUrl);
      handleClose();
    } catch (err) {
      setError(err.message || 'Failed to upload mockup image.');
    } finally {
      setUploading(false);
    }
  }, [selectedFile, projectId, collectionId, mockup, onReplaced]);

  const handleClose = useCallback(() => {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setSelectedFile(null);
    setPreviewUrl(null);
    setError(null);
    setDragging(false);
    onClose();
  }, [previewUrl, onClose]);

  if (!show || !mockup) return null;

  const apiBase = import.meta.env.VITE_API_URL || '';
  const currentThumbUrl = `${apiBase}${mockup.imageUrl}`;

  return (
    <Modal show={show} onClose={handleClose} title="Replace Mockup Image" className="w-[500px] max-w-[95vw]">
      <div className="flex flex-col gap-4 p-4">
        {/* Current mockup image */}
        <div>
          <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Current Mockup</label>
          <div className="w-full max-w-[200px] mx-auto rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600">
            <img src={currentThumbUrl} alt="Current mockup" className="w-full h-auto object-contain" />
          </div>
        </div>

        {/* Upload area */}
        <div>
          <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Upload New Image</label>
          <div
            onClick={() => !uploading && fileInputRef.current?.click()}
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            className={`cursor-pointer border-2 border-dashed rounded-lg p-8 text-center transition ${
              dragging
                ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
                : 'border-gray-300 dark:border-gray-600 hover:border-gray-400 dark:hover:border-gray-500'
            } ${uploading ? 'opacity-50 pointer-events-none' : ''}`}
          >
            {previewUrl ? (
              <div className="flex flex-col items-center gap-2">
                <img src={previewUrl} alt="Preview" className="max-h-[150px] object-contain rounded" />
                <span className="text-sm text-gray-500 dark:text-gray-400">{selectedFile?.name}</span>
                <span className="text-xs text-blue-500">Click to change</span>
              </div>
            ) : (
              <div className="flex flex-col items-center gap-2">
                <Icon name="upload" className="w-8 h-8 text-gray-400" />
                <span className="text-sm text-gray-500 dark:text-gray-400">
                  Click to upload or drag &amp; drop
                </span>
                <span className="text-xs text-gray-400">JPG or PNG</span>
              </div>
            )}
          </div>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/jpeg,image/png,image/jpg"
            onChange={handleFileChange}
            className="hidden"
          />
        </div>

        {error && <div className="text-sm text-red-500">{error}</div>}

        {/* Buttons */}
        <div className="flex justify-end gap-2 mt-2">
          <ButtonOutline color="gray" onClick={handleClose} disabled={uploading}>Cancel</ButtonOutline>
          <ButtonOutline onClick={handleUpload} disabled={!selectedFile || uploading}>
            {uploading ? <Spinner className="text-sm" /> : 'Update Mockup'}
          </ButtonOutline>
        </div>
      </div>
    </Modal>
  );
}
