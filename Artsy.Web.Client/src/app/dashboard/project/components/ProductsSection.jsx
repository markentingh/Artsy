import React, { useEffect, useState, lazy, Suspense } from 'react';
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
import CarouselElements from '@/components/ui/carousel-elements';
import Spinner from '@/components/ui/spinner';
const ConfirmModal = lazy(() => import('@/components/ui/confirm-modal'));
const FindPrintifyBlueprintModal = lazy(() => import('./FindPrintifyBlueprintModal'));
const ConfigureProductBlueprint = lazy(() => import('./ConfigureProductBlueprint'));

export default function ProductsSection({ projectId, onProductsChanged }) {
  const session = useSession();
  const { getBlueprints, getBlueprintsList, deleteBlueprint, getItems, getItemPreviews, getItemPreviewUrl } = Projects(session);
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
      const response = await getBlueprintsList(projectId);
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

  const handleEditBlueprint = async (bp) => {
    try {
      const resp = await getBlueprints(projectId);
      if (resp.data.success) {
        const fullBp = (resp.data.data || []).find(b => b.id === bp.id);
        setEditingBlueprint(fullBp || bp);
      } else {
        setEditingBlueprint(bp);
      }
    } catch {
      setEditingBlueprint(bp);
    }
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

  const handleSaveBlueprintConfig = async () => {
    await fetchBlueprints();
    if (onProductsChanged) onProductsChanged();
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
          <h2 className="text-xl font-semibold">Product Blueprints</h2>
          <Tooltip text="Products are the physical items you'll sell, sourced from print-on-demand providers. Find a product blueprint, configure its variants and placements, and assign artworks to each print area. Your products will then be created for each collection you publish." />
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
        <CarouselElements
          className="mb-8"
          elements={blueprints.map((bp) => (
            <div
              key={bp.id}
              onClick={() => handleEditBlueprint(bp)}
              className="w-[300px] bg-white dark:bg-gray-800 rounded-lg shadow p-4 hover:shadow-md transition cursor-pointer"
            >
              <div className="w-full mb-3 rounded-lg overflow-hidden relative">
                <Carousel
                  images={(() => {
                    const cfg = (() => {
                      try { return JSON.parse(bp.blueprintJson || '{}'); } catch { return {}; }
                    })();
                    const selectedColors = new Set((cfg.variantColors || []).map(String));
                    const imgData = blueprintImageMap[bp.blueprintId] || [];
                    const matchingIndices = imgData
                      .filter(img => (img.variantColors || []).some(c => selectedColors.has(String(c))))
                      .map(img => img.imageIndex);
                    return matchingIndices.map(i => getBlueprintImageUrl(bp.blueprintId, i, true));
                  })()}
                  alt={bp.name}
                  singleImage
                  infiniteScroll
                  placeholder="No Image"
                  imageClassName="!max-w-[260px] object-contain"
                  maxHeight="260px"
                />
                <div className="absolute bottom-2 right-4">
                  <Checked checked={bp.configured} />
                </div>
              </div>
              <div>
                <p className="text-sm font-medium truncate" title={bp.name}>{bp.name}</p>
                <div className="flex items-center justify-between mt-1">
                  <span className="text-gray-500 dark:text-gray-400">
                    {bp.minPrice != null ? `$${Number(bp.minPrice).toFixed(2)}` : 'No price set'}
                  </span>
                  <ButtonIcon name="delete" color="red" onClick={(e) => handleDeleteBlueprint(bp, e)} title="Remove product" />
                </div>
              </div>
            </div>
          ))}
        />
      )}

      {showFindBlueprint && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <FindPrintifyBlueprintModal
            show={showFindBlueprint}
            onSelect={handleFindBlueprint}
            onClose={() => setShowFindBlueprint(false)}
          />
        </Suspense>
      )}

      {configBlueprint && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ConfigureProductBlueprint
            show={!!configBlueprint}
            blueprint={configBlueprint}
            existingConfig={editingBlueprint}
            projectId={projectId}
            onSave={handleSaveBlueprintConfig}
            onClose={() => { setConfigBlueprint(null); setEditingBlueprint(null); }}
          />
        </Suspense>
      )}

      {deleteTarget && (
        <Suspense fallback={<div className="fixed inset-0 z-50 flex items-center justify-center"><Spinner className="text-4xl" /></div>}>
          <ConfirmModal
            show={!!deleteTarget}
            title="Delete Product"
            message={`Do you really want to delete this product${deleteTarget ? ` (${deleteTarget.name})` : ''}? This cannot be undone.`}
            onConfirm={handleConfirmDelete}
            onClose={() => setDeleteTarget(null)}
          />
        </Suspense>
      )}
    </div>
  );
}
