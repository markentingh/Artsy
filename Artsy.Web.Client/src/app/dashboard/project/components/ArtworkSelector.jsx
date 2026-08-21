import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Modal from '@/components/ui/modal';
import Carousel from '@/components/ui/carousel';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';

export default function ArtworkSelector({ show, projectId, currentIndex, onSelect, onClose }) {
  const session = useSession();
  const { getItems, getItemPreviews, getItemPreviewUrl } = Projects(session);

  const [items, setItems] = useState([]);
  const [previewsByItem, setPreviewsByItem] = useState({});
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    if (!show || !projectId) return;
    setLoading(true);
    setMessage(null);
    setItems([]);
    setPreviewsByItem({});
    fetchItems();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [show, projectId]);

  const fetchItems = async () => {
    try {
      const resp = await getItems(projectId);
      if (resp.data.success) {
        const allItems = resp.data.data || [];
        const eligible = allItems.filter((i) => i.index < currentIndex);
        setItems(eligible);

        const previewMap = {};
        await Promise.all(eligible.map(async (item) => {
          try {
            const previewResp = await getItemPreviews(item.id);
            if (previewResp.data.success) {
              previewMap[item.id] = previewResp.data.data || [];
            }
          } catch {
            previewMap[item.id] = [];
          }
        }));
        setPreviewsByItem(previewMap);
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load artworks' });
    } finally {
      setLoading(false);
    }
  };

  const handleSelect = (item) => {
    if (onSelect) onSelect(item);
  };

  if (!show) return null;

  return (
    <Modal
      title="Select Artwork Reference"
      onClose={onClose}
      top
      className="min-w-[50em] max-w-full"
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Spinner className="text-4xl" />
        </div>
      ) : items.length > 0 ? (
        <div className="grid grid-cols-[repeat(auto-fill,200px)] gap-4 max-h-[60vh] overflow-y-auto">
          {items.map((item) => {
            const previews = previewsByItem[item.id] || [];
            const images = previews.map((p) => getItemPreviewUrl(item.id, p.id, true));
            return (
              <div
                key={item.id}
                className="border border-gray-300 dark:border-gray-600 rounded-lg overflow-hidden cursor-pointer hover:border-primary-500 hover:ring-2 hover:ring-primary-500 transition"
                onClick={() => handleSelect(item)}
              >
                <div className="flex items-center justify-center bg-gray-100 dark:bg-gray-700">
                  {images.length > 0 ? (
                    <Carousel
                      images={images}
                      alt={item.title}
                      singleImage
                      imageClassName="!max-w-[200px] !max-h-[200px] object-contain"
                    />
                  ) : (
                    <span className="text-sm text-gray-400 my-8">No preview</span>
                  )}
                </div>
                <div className="px-2 py-1 text-xs font-medium text-gray-700 dark:text-gray-300 truncate">
                  {item.title || `Artwork ${item.index}`}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <p className="text-sm text-gray-500 dark:text-gray-400">No artworks available to reference. Only artworks with a lower index can be referenced.</p>
      )}

      <div className="buttons flex justify-end gap-2 mt-4">
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>
          Cancel
        </ButtonOutline>
      </div>
    </Modal>
  );
}
