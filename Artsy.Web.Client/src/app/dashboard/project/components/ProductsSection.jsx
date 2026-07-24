import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import { Printify } from '@/api/user/printify';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Carousel from '@/components/ui/carousel';
import Tooltip from '@/components/ui/tooltip';
import Message from '@/components/ui/message';
import Checked from '@/components/ui/checked';
import ConfirmModal from '@/components/ui/confirm-modal';
import FindPrintifyBlueprintModal from './FindPrintifyBlueprintModal';
import ConfigureProductBlueprint from './ConfigureProductBlueprint';

export default function ProductsSection({ projectId, onProductsChanged }) {
  const session = useSession();
  const { getBlueprints, createBlueprint, deleteBlueprint, updateBlueprint, getItems, getItemPreviews, getItemPreviewUrl } = Projects(session);
  const { getBlueprintImageUrl, getBlueprintImages } = Printify(session);

  const [blueprints, setBlueprints] = useState([]);
  const [blueprintImageMap, setBlueprintImageMap] = useState({});
  const [mount, setMount] = useState(false);
  const [showFindBlueprint, setShowFindBlueprint] = useState(false);
  const [configBlueprint, setConfigBlueprint] = useState(null);
  const [editingBlueprint, setEditingBlueprint] = useState(null);
  const [message, setMessage] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const fetchBlueprints = async () => {
    try {
      const response = await getBlueprints(projectId);
      if (response.data.success) {
        const bps = response.data.data || [];
        setBlueprints(bps);

        const imgMap = {};
        for (const bp of bps) {
          try {
            const imgResp = await getBlueprintImages(bp.blueprintId);
            if (imgResp.data.success) {
              imgMap[bp.blueprintId] = imgResp.data.data || [];
            }
          } catch { /* ignore */ }
        }
        setBlueprintImageMap(imgMap);
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

  const handleDeleteBlueprint = (bp, e) => {
    e.stopPropagation();
    setDeleteTarget(bp);
  };

  const handleConfirmDelete = async () => {
    if (!deleteTarget) return;
    try {
      const resp = await deleteBlueprint({ id: deleteTarget.id });
      if (resp.data.success) {
        await fetchBlueprints();
        if (onProductsChanged) onProductsChanged();
      } else {
        setMessage({ type: 'error', text: resp.data.message || 'Failed to delete product' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete product' });
    } finally {
      setDeleteTarget(null);
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

  const isProductComplete = (bp) => {
    if (!bp.placementJson) return false;
    try {
      const placements = JSON.parse(bp.placementJson);
      if (!placements || Object.keys(placements).length === 0) return false;
      return Object.values(placements).some(p => {
        if (!p.source) return false;
        if (p.source === 'item' && p.itemId) return true;
        if (p.source === 'custom' && p.customImageId) return true;
        return false;
      });
    } catch { return false; }
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
        <div className="flex items-center gap-1">
          <h2 className="text-xl font-semibold">Products</h2>
          <Tooltip text="Products are the physical items you'll sell, sourced from print-on-demand providers. Find a product blueprint, configure its variants and placements, and assign artworks to each print area." />
        </div>
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
              <div className="aspect-square w-full relative">
                <Carousel
                  images={(() => {
                    const cfg = (() => {
                      try { return JSON.parse(bp.blueprintJson || '{}'); } catch { return {}; }
                    })();
                    const selectedVariantIds = new Set((cfg.variantIds || []).map(String));
                    const imgData = blueprintImageMap[bp.blueprintId] || [];
                    const matchingIndices = imgData
                      .filter(img => (img.variants || []).some(vId => selectedVariantIds.has(String(vId))))
                      .map(img => img.imageIndex);
                    const indices = matchingIndices.length > 0
                      ? matchingIndices
                      : Array.from({ length: bp.imageCount }, (_, i) => i);
                    return indices.map(i => getBlueprintImageUrl(bp.blueprintId, i, true));
                  })()}
                  alt={bp.name}
                  singleImage
                  infiniteScroll
                  placeholder="No Image"
                  imageClassName="!max-h-none w-full h-full object-cover"
                />
                <div className="absolute bottom-2 right-2">
                  <Checked checked={isProductComplete(bp)} />
                </div>
              </div>
              <div className="p-3">
                <p className="text-sm font-medium truncate">{bp.name}</p>
                <div className="flex items-center justify-between mt-1">
                  <span className="text-xs text-gray-500 dark:text-gray-400">Blueprint #{bp.blueprintId}</span>
                  <ButtonIcon name="delete" color="red" onClick={(e) => handleDeleteBlueprint(bp, e)} title="Remove product" />
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

      <ConfirmModal
        show={!!deleteTarget}
        title="Delete Product"
        message={`Do you really want to delete this product${deleteTarget ? ` (${deleteTarget.name})` : ''}? This cannot be undone.`}
        onConfirm={handleConfirmDelete}
        onClose={() => setDeleteTarget(null)}
      />
    </div>
  );
}
