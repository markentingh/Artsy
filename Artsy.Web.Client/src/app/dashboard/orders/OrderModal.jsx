import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Orders } from '@/api/orders';
import Modal from '@/components/ui/modal';
import { List, Item } from '@/components/ui/list';
import CarouselElements from '@/components/ui/carousel-elements';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import PersonalizeOrderItem from './PersonalizeOrderItem';

const formatCents = (cents) => (cents / 100).toFixed(2);

const formatDate = (d) => {
  if (!d) return '';
  try {
    return new Date(d).toISOString().slice(0, 10).replace(/-/g, '/');
  } catch {
    return '';
  }
};

const capitalize = (s) => {
  if (!s) return '';
  return s.charAt(0).toUpperCase() + s.slice(1);
};

const parseJson = (s) => {
  try {
    return JSON.parse(s || '{}');
  } catch {
    return {};
  }
};

export default function OrderModal({ order, onClose }) {
  const session = useSession();
  const { getOrderImages } = Orders(session);
  const [imagesByProduct, setImagesByProduct] = useState({});
  const [loadingImages, setLoadingImages] = useState(true);
  const [personalizingItem, setPersonalizingItem] = useState(null);

  useEffect(() => {
    let cancelled = false;
    const fetchImages = async () => {
      try {
        const resp = await getOrderImages(order.order.id);
        if (resp.data?.success && !cancelled) {
          setImagesByProduct(resp.data.images || {});
        }
      } catch (error) {
        // fail silently; the items will render without images
      } finally {
        if (!cancelled) setLoadingImages(false);
      }
    };
    fetchImages();
    return () => { cancelled = true; };
  }, [order.order.id]);

  const addressTo = parseJson(order.order.addressTo);
  const metadata = parseJson(order.order.metadata);
  const printifyConnect = parseJson(order.order.printifyConnect);

  return (
    <Modal
      title={`Order ${order.order.orderId}`}
      onClose={onClose}
      className="max-w-4xl"
    >
      <div className="flex justify-end mb-4">
        <a
          href={`https://printify.com/app/store/${order.order.printifyShopId}/order/${order.order.orderId}`}
          target="_blank"
          rel="noopener noreferrer"
          className="text-blue-600 dark:text-blue-400 hover:underline text-sm"
        >
          View Order on Printify
        </a>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm mb-6">
        <div className="bg-gray-50 dark:bg-gray-700 p-3 rounded">
          <h4 className="font-semibold mb-2">Customer</h4>
          <p>{addressTo.firstName || ''} {addressTo.lastName || ''}</p>
          <p>{addressTo.address1 || ''}</p>
          <p>{addressTo.city || ''}, {addressTo.zip || ''}</p>
          <p>{addressTo.country || ''}</p>
          <p className="mt-2 text-gray-600 dark:text-gray-400">{addressTo.email || ''}</p>
          <p className="text-gray-600 dark:text-gray-400">{addressTo.phone || ''}</p>
          {addressTo.company && <p>{addressTo.company}</p>}
        </div>
        <div className="bg-gray-50 dark:bg-gray-700 p-3 rounded">
          <h4 className="font-semibold mb-2">Order</h4>
          <p>Status: {capitalize(order.order.status)}</p>
          <p>Total: ${formatCents(order.order.totalPrice)}</p>
          <p>Shipping: ${formatCents(order.order.totalShipping)}</p>
          <p>Tax: ${formatCents(order.order.totalTax)}</p>
          <p>Express: {order.order.isExpress ? 'Yes' : 'No'}</p>
          <p>Economy: {order.order.isEconomyShipping ? 'Yes' : 'No'}</p>
          {printifyConnect.url && (
            <a
              href={printifyConnect.url}
              target="_blank"
              rel="noopener noreferrer"
              className="text-blue-600 dark:text-blue-400 hover:underline"
            >
              Printify Connect
            </a>
          )}
        </div>
        <div className="bg-gray-50 dark:bg-gray-700 p-3 rounded">
          <h4 className="font-semibold mb-2">Dates & Shipments</h4>
          <p>Created: {formatDate(order.order.dateCreated)}</p>
          <p>Sent to Production: {formatDate(order.order.dateSentToProduction)}</p>
          <p>Fulfilled: {formatDate(order.order.dateFulfilled)}</p>
          {order.shipments.length > 0 ? (
            <ul className="mt-2 space-y-1">
              {order.shipments.map((s) => (
                <li key={s.id}>
                  {s.carrier} {s.number}
                  {s.url && (
                    <a
                      href={s.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="ml-2 text-blue-600 dark:text-blue-400 hover:underline"
                    >
                      track
                    </a>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-gray-600 dark:text-gray-400">No shipments</p>
          )}
        </div>
      </div>

      <h3 className="font-semibold mb-2">Products</h3>
      {loadingImages ? (
        <div className="flex items-center gap-2 text-gray-600 dark:text-gray-400">
          <Icon name="progress_activity" spin className="w-5 h-5" />
          Loading product images...
        </div>
      ) : (
        <List>
          {order.items.map((item) => (
            <OrderItemRow
              key={item.id}
              order={order}
              item={item}
              imageUrls={imagesByProduct[item.id] || []}
              onPersonalize={() => setPersonalizingItem(item)}
            />
          ))}
        </List>
      )}
      {personalizingItem && (
        <PersonalizeOrderItem
          order={order}
          orderItem={personalizingItem}
          onClose={() => setPersonalizingItem(null)}
        />
      )}
    </Modal>
  );
}

function OrderItemRow({ order, item, imageUrls, onPersonalize }) {
  const meta = parseJson(item.metadata);
  const statusLabel = item.status?.toLowerCase() === 'on-hold'
    ? 'On Hold (Required Personalization)'
    : capitalize(item.status);
  const imageElements = imageUrls.length > 0
    ? imageUrls.map((url, i) => (
        <img
          key={i}
          src={url}
          alt={meta.title || 'Product image'}
          className="w-[150px] h-[150px] object-cover rounded"
          width="150"
          height="150"
        />
      ))
    : [];

  return (
    <Item inModal className="items-start gap-4">
      <div className="flex-shrink-0 w-[214px]">
        {imageElements.length > 0 ? (
          <CarouselElements elements={imageElements} gap={8} className="w-full px-8" />
        ) : (
          <div className="w-[150px] h-[150px] bg-gray-100 dark:bg-gray-700 flex items-center justify-center rounded text-gray-500 dark:text-gray-400 text-xs text-center p-2">
            No image
          </div>
        )}
        {item.status?.toLowerCase() === 'on-hold' && (
          <ButtonOutline onClick={onPersonalize} size="small" className="w-full mt-2">
            Personalize
          </ButtonOutline>
        )}
      </div>
      <div className="grid grid-cols-3 gap-x-4 gap-y-2 text-sm flex-1">
        <div>
          <span className="text-gray-500 dark:text-gray-400">Title</span>
          <p className="font-medium">{meta.title || ''}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Variant</span>
          <p className="font-medium">{meta.variantLabel || meta.variant_label || ''}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Product ID</span>
          <p className="font-medium break-all">{item.productId}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Quantity</span>
          <p className="font-medium">{item.quantity}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Cost</span>
          <p className="font-medium">${formatCents(item.cost)}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Shipping</span>
          <p className="font-medium">${formatCents(item.shippingCost)}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Print Provider</span>
          <p className="font-medium">{item.printProviderId}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Variant ID</span>
          <p className="font-medium">{item.variantId}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Status</span>
          <p className="font-medium">{statusLabel}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Sent to Production</span>
          <p className="font-medium">{formatDate(item.dateSentToProduction)}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Fulfilled</span>
          <p className="font-medium">{formatDate(item.dateFulfilled)}</p>
        </div>
        {meta.sku && (
          <div>
            <span className="text-gray-500 dark:text-gray-400">SKU</span>
            <p className="font-medium">{meta.sku}</p>
          </div>
        )}
      </div>
    </Item>
  );
}
