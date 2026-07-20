import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import { Printify } from '@/api/user/printify';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import Carousel from '@/components/ui/carousel';
import Message from '@/components/ui/message';
import FindPrintifyBlueprintModal from './FindPrintifyBlueprintModal';
import ConfigureProductBlueprint from './ConfigureProductBlueprint';

export default function ProductsSection({ projectId, onProductsChanged }) {
  const session = useSession();
  const { getBlueprints, createBlueprint, deleteBlueprint, updateBlueprint, getItems, getItemPreviews, getItemPreviewUrl } = Projects(session);
  const { getBlueprintImageUrl } = Printify(session);

  const [blueprints, setBlueprints] = useState([]);
  const [mount, setMount] = useState(false);
  const [showFindBlueprint, setShowFindBlueprint] = useState(false);
  const [configBlueprint, setConfigBlueprint] = useState(null);
  const [editingBlueprint, setEditingBlueprint] = useState(null);
  const [message, setMessage] = useState(null);

  const fetchBlueprints = async () => {
    try {
      const response = await getBlueprints(projectId);
      if (response.data.success) {
        setBlueprints(response.data.data || []);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to load products' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load products' });
    } finally {
      setMount(true);
    }
  };

  useEffect(() => {
    fetchBlueprints();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  const handleFindBlueprint = (bp) => {
    setConfigBlueprint(bp);
    setEditingBlueprint(null);
  };

  const handleEditBlueprint = (bp) => {
    setEditingBlueprint(bp);
    setConfigBlueprint({ id: bp.blueprintId, title: bp.name });
  };

  const handleDeleteBlueprint = async (bp) => {
    try {
      const resp = await deleteBlueprint({ id: bp.id });
      if (resp.data.success) {
        await fetchBlueprints();
        if (onProductsChanged) onProductsChanged();
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to delete product' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete product' });
    }
  };

  const handleSaveBlueprintConfig = async (config) => {
    if (editingBlueprint) {
      try {
        const resp = await updateBlueprint({
          id: editingBlueprint.id,
          blueprintId: config.blueprintId,
          name: config.name,
          blueprintJson: config.blueprintJson,
          placementJson: config.placementJson || '',
        });
        if (resp.data.success) {
          await fetchBlueprints();
          setConfigBlueprint(null);
          setEditingBlueprint(null);
          if (onProductsChanged) onProductsChanged();
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to update product' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to update product' });
      }
    } else {
      try {
        const resp = await createBlueprint({
          projectId,
          blueprintId: config.blueprintId,
          name: config.name,
          blueprintJson: config.blueprintJson,
          placementJson: config.placementJson || '',
        });
        if (resp.data.success) {
          await fetchBlueprints();
          setConfigBlueprint(null);
          if (onProductsChanged) onProductsChanged();
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to save product' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to save product' });
      }
    }
  };

  if (!mount) {
    return (
      <div className="p-8 text-center">
        <Icon name="progress_activity" spin className="w-6 h-6 mx-auto mb-2" />
        Loading products...
      </div>
    );
  }

  return (
    <div>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold">Products</h2>
        <ButtonOutline onClick={() => setShowFindBlueprint(true)}>
          <Icon name="search" />
          <span className="ml-2">Find Product</span>
        </ButtonOutline>
      </div>
      {blueprints.length === 0 ? (
        <div className="p-12 text-center text-gray-600 dark:text-gray-400">
          No Products configured for this project
        </div>
      ) : (
        <div className="grid grid-cols-[repeat(auto-fill,250px)] gap-4 mb-8">
          {blueprints.map((bp) => (
            <div
              key={bp.id}
              onClick={() => handleEditBlueprint(bp)}
              className="rounded-lg bg-white dark:bg-gray-800 shadow hover:shadow-md cursor-pointer overflow-hidden transition"
            >
              <div className="aspect-square w-full">
                <Carousel
                  images={bp.imageCount > 0
                    ? Array.from({ length: bp.imageCount }, (_, i) => getBlueprintImageUrl(bp.blueprintId, i, true))
                    : []}
                  alt={bp.name}
                  singleImage
                  infiniteScroll
                  placeholder="No Image"
                  imageClassName="!max-h-none w-full h-full object-cover"
                />
              </div>
              <div className="p-3">
                <p className="text-sm font-medium truncate">{bp.name}</p>
                <div className="flex items-center justify-between mt-1">
                  <span className="text-xs text-gray-500 dark:text-gray-400">Blueprint #{bp.blueprintId}</span>
                  <button
                    type="button"
                    onClick={(e) => { e.stopPropagation(); handleDeleteBlueprint(bp); }}
                    className="text-red-500 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300"
                    title="Remove product"
                  >
                    <Icon name="delete" />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      <FindPrintifyBlueprintModal
        show={showFindBlueprint}
        onSelect={handleFindBlueprint}
        onClose={() => setShowFindBlueprint(false)}
      />

      {configBlueprint && (
        <ConfigureProductBlueprint
          show={!!configBlueprint}
          blueprint={configBlueprint}
          existingConfig={editingBlueprint}
          projectId={projectId}
          onSave={handleSaveBlueprintConfig}
          onClose={() => { setConfigBlueprint(null); setEditingBlueprint(null); }}
        />
      )}
    </div>
  );
}
