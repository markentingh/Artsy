import React, { useState, useEffect, useCallback } from 'react';
import ButtonIcon from '@/components/ui/button-icon';

export default function UserSubscriptionsTab({ api, showMessage }) {
  const [subscriptions, setSubscriptions] = useState([]);

  const load = useCallback(async () => {
    const res = await api.getUserSubscriptions();
    if (res.data.success) setSubscriptions(res.data.data);
  }, [api]);

  useEffect(() => { load(); }, [load]);

  const handleCancel = async (id) => {
    const res = await api.cancelUserSubscription(id);
    if (res.data.success) {
      showMessage('info', 'User subscription cancelled.');
      load();
    }
  };

  return (
    <div>
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
        <table className="w-full text-left border-collapse">
          <thead className="bg-gray-100 dark:bg-gray-700">
            <tr>
              <th className="px-4 py-3">Email</th>
              <th className="px-4 py-3">Subscription ID</th>
              <th className="px-4 py-3">Start Date</th>
              <th className="px-4 py-3">End Date</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 w-24"></th>
            </tr>
          </thead>
          <tbody>
            {subscriptions.map(s => (
              <tr key={s.id} className="border-b border-gray-200 dark:border-gray-700">
                <td className="px-4 py-3 text-sm">{s.email || s.appUserId}</td>
                <td className="px-4 py-3">{s.subscriptionId}</td>
                <td className="px-4 py-3">{new Date(s.startDate).toLocaleDateString()}</td>
                <td className="px-4 py-3">{s.endDate ? new Date(s.endDate).toLocaleDateString() : '-'}</td>
                <td className="px-4 py-3">
                  {s.cancelled
                    ? <span className="text-red-600">Cancelled</span>
                    : <span className="text-green-600">Active</span>}
                </td>
                <td className="px-4 py-3">
                  {!s.cancelled && (
                    <ButtonIcon name="delete" color="red" onClick={() => handleCancel(s.id)} title="Cancel" />
                  )}
                </td>
              </tr>
            ))}
            {subscriptions.length === 0 && (
              <tr>
                <td colSpan="6" className="text-center py-8 text-gray-600 dark:text-gray-400">
                  No user subscriptions found.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
