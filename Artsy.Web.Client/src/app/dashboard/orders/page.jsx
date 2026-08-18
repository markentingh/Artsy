import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Orders } from '@/api/user/orders';
import Button from '@/components/ui/button';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';
import OrderModal from './OrderModal';

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

const sumQty = (items) => (items || []).reduce((acc, i) => acc + (i.quantity || 0), 0);

const parseJson = (s) => {
  try {
    return JSON.parse(s || '{}');
  } catch {
    return {};
  }
};

export default function OrdersPage() {
  const session = useSession();
  const { getOrders, refreshOrders } = Orders(session);

  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [message, setMessage] = useState(null);
  const [selectedOrder, setSelectedOrder] = useState(null);

  const fetchOrders = async () => {
    try {
      const resp = await getOrders();
      if (resp.data?.success) {
        setOrders(resp.data.data || []);
      } else {
        setMessage({ type: 'error', text: resp.data?.message || 'Failed to load orders' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load orders' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchOrders();
  }, []);

  const handleRefresh = async () => {
    setRefreshing(true);
    setMessage(null);
    try {
      const resp = await refreshOrders();
      if (resp.data?.success) {
        setMessage({
          type: 'success',
          text: `${resp.data.newOrders} new orders found, ${resp.data.updatedOrders} existing orders updated`,
        });
        await fetchOrders();
      } else {
        setMessage({ type: 'error', text: resp.data?.message || 'Refresh failed' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Refresh failed' });
    } finally {
      setRefreshing(false);
    }
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-3xl">Orders</h1>
        <Button onClick={handleRefresh} disabled={refreshing}>
          {refreshing ? (
            <span className="inline-flex items-center gap-2">
              <Icon name="progress_activity" spin className="w-4 h-4" />
              Processing...
            </span>
          ) : (
            'Refresh Orders'
          )}
        </Button>
      </div>

      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      {loading ? (
        <div className="flex items-center gap-2 text-gray-600 dark:text-gray-400">
          <Icon name="progress_activity" spin className="w-5 h-5" />
          Loading orders...
        </div>
      ) : (
        <div className="overflow-x-auto bg-white dark:bg-gray-800 rounded-lg shadow">
          <table className="min-w-full text-sm text-left">
            <thead className="bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">
              <tr>
                <th className="px-4 py-3 font-medium">Customer</th>
                <th className="px-4 py-3 font-medium">Products</th>
                <th className="px-4 py-3 font-medium">Total</th>
                <th className="px-4 py-3 font-medium">Shipping</th>
                <th className="px-4 py-3 font-medium">Tax</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Created</th>
                <th className="px-4 py-3 font-medium">Fulfilled</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
              {orders.map((o) => {
                const addressTo = parseJson(o.order.addressTo);
                const customerName = `${addressTo.firstName || ''} ${addressTo.lastName || ''}`.trim();
                return (
                  <tr
                    key={o.order.id}
                    onClick={() => setSelectedOrder(o)}
                    className="hover:bg-gray-50 dark:hover:bg-gray-700 cursor-pointer"
                  >
                    <td className="px-4 py-3">{customerName}</td>
                    <td className="px-4 py-3">{sumQty(o.items)}</td>
                    <td className="px-4 py-3">${formatCents(o.order.totalPrice)}</td>
                    <td className="px-4 py-3">${formatCents(o.order.totalShipping)}</td>
                    <td className="px-4 py-3">${formatCents(o.order.totalTax)}</td>
                    <td className="px-4 py-3">{capitalize(o.order.status)}</td>
                    <td className="px-4 py-3">{formatDate(o.order.dateCreated)}</td>
                    <td className="px-4 py-3">{formatDate(o.order.dateFulfilled)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {orders.length === 0 && (
            <div className="p-6 text-center text-gray-500 dark:text-gray-400">No orders found.</div>
          )}
        </div>
      )}

      {selectedOrder && (
        <OrderModal
          order={selectedOrder}
          onClose={() => setSelectedOrder(null)}
        />
      )}
    </div>
  );
}

