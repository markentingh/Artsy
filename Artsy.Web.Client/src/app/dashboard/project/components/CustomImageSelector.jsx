import React, { useEffect, useRef, useState, lazy, Suspense } from 'react';
import { useSession } from '@/context/session';
import { CustomImages } from '@/api/user/customImages';
import Modal from '@/components/ui/modal';
import ButtonOutline from '@/components/ui/button-outline';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
const ConfirmModal = lazy(() => import('@/components/ui/confirm-modal'));

const PAGE_SIZE = 10;

export default function CustomImageSelector({ show, selectedImageId, onSelect, onClose }) {
  const session = useSession();
  const { getCustomImages, uploadCustomImage, deleteCustomImage, getCustomImageUrl } = CustomImages(session);

  const [images, setImages] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [message, setMessage] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const fileInputRef = useRef(null);

  useEffect(() => {
    if (!show) return;
    setLoading(true);
    setMessage(null);
    setImages([]);
    setTotalCount(0);
    fetchImages(0);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [show]);

  const fetchImages = async (offset) => {
    try {
      const resp = await getCustomImages(PAGE_SIZE, offset);
      if (resp.data.success) {
        const newImages = resp.data.data.images || [];
        setTotalCount(resp.data.data.totalCount || 0);
        if (offset === 0) {
          setImages(newImages);
        } else {
          setImages((prev) => [...prev, ...newImages]);
        }
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load images' });
    } finally {
      setLoading(false);
    }
  };

  const handleLoadMore = () => {
    fetchImages(images.length);
  };

  const handleFileSelect = async (e) => {
    const files = Array.from(e.target.files || []);
    if (files.length === 0) return;
    setUploading(true);
    setMessage(null);
    try {
      let lastUploaded = null;
      for (const file of files) {
        const response = await uploadCustomImage(file);
        if (response.data.success) {
          lastUploaded = response.data.data;
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to upload image' });
        }
      }
      if (lastUploaded) {
        fetchImages(0);
        if (onSelect) onSelect(lastUploaded);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to upload image' });
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const handleImageClick = (img) => {
    if (onSelect) {
      onSelect(img);
    }
  };

  const handleDeleteImage = (img, e) => {
    e.stopPropagation();
    setDeleteTarget(img);
  };

  const handleConfirmDelete = async () => {
    if (!deleteTarget) return;
    try {
      const resp = await deleteCustomImage({ id: deleteTarget.id });
      if (resp.data.success) {
        setImages((prev) => prev.filter((i) => i.id !== deleteTarget.id));
        setTotalCount((prev) => prev - 1);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete image' });
    } finally {
      setDeleteTarget(null);
    }
  };

  if (!show) return null;

  return (
    <Modal
      title="Select Custom Image"
      onClose={onClose}
      top
      className="min-w-[40em] max-w-full"
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300">Custom Images</h3>
        <div>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/jpeg,image/png"
            multiple
            onChange={handleFileSelect}
            className="hidden"
          />
          <ButtonOutline onClick={() => fileInputRef.current?.click()}>
            {uploading ? (
              <Icon name="progress_activity" spin className="mr-2" />
            ) : (
              <Icon name="add" className="mr-2" />
            )}
            <span>Upload Images</span>
          </ButtonOutline>
        </div>
      </div>

      {loading && images.length === 0 ? (
        <div className="flex items-center justify-center py-12">
          <Spinner className="text-4xl" />
        </div>
      ) : images.length > 0 ? (
        <div className="grid grid-cols-[repeat(auto-fill,120px)] gap-2 max-h-[50vh] overflow-y-auto">
          {images.map((img) => (
            <div
              key={img.id}
              className={`relative group rounded-lg overflow-hidden border cursor-pointer transition w-[120px] h-[120px] flex items-center justify-center ${
                selectedImageId === img.id
                  ? 'border-primary-500 ring-2 ring-primary-500'
                  : 'border-gray-300 dark:border-gray-600 hover:border-primary-500'
              }`}
              onClick={() => handleImageClick(img)}
            >
              <img
                src={getCustomImageUrl(img.id, true)}
                alt={img.fileName}
                className="max-w-full max-h-full object-contain"
              />
              <button
                type="button"
                onClick={(e) => handleDeleteImage(img, e)}
                className="absolute top-1 right-1 w-6 h-6 flex items-center justify-center bg-black/60 text-white rounded opacity-0 group-hover:opacity-100 transition"
                title="Delete image"
              >
                <Icon name="close" />
              </button>
            </div>
          ))}
          {images.length < totalCount && (
            <ButtonOutline onClick={handleLoadMore} className="w-[120px] h-[120px] flex items-center justify-center">
              Load More
            </ButtonOutline>
          )}
        </div>
      ) : (
        <p className="text-sm text-gray-500 dark:text-gray-400">No custom images uploaded. Click "Upload Images" to add some.</p>
      )}

      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>
          Cancel
        </ButtonOutline>
      </div>

      {deleteTarget && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ConfirmModal
            show={!!deleteTarget}
            title="Delete Image"
            message="Do you really want to delete this image? This cannot be undone."
            onConfirm={handleConfirmDelete}
            onClose={() => setDeleteTarget(null)}
          />
        </Suspense>
      )}
    </Modal>
  );
}
