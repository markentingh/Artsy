import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Hangfire } from '@/api/admin/hangfire';
import BarChart from '@/components/ui/bar-chart';
import Icon from '@/components/ui/icon';

const RANGE_OPTIONS = [
  { value: '24h', label: 'Last 24 Hours' },
  { value: '7d', label: 'Last 7 Days' },
  { value: '30d', label: 'Last 30 Days' },
  { value: '12m', label: 'Last 12 Months' },
  { value: 'ytd', label: 'Year To Date' },
];

export default function HangfirePage() {
  const session = useSession();
  const { getOrdersHistory } = Hangfire(session);
  const [range, setRange] = useState('24h');
  const [history, setHistory] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    const fetchHistory = async () => {
      setLoading(true);
      setError(null);
      try {
        const resp = await getOrdersHistory(range);
        if (resp.data?.success && !cancelled) {
          setHistory(resp.data.data || []);
        } else if (!cancelled) {
          setError(resp.data?.message || 'Failed to load order history');
        }
      } catch (err) {
        if (!cancelled) setError(err?.response?.data?.message || 'Failed to load order history');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    fetchHistory();
    return () => { cancelled = true; };
  }, [range]);

  const chartData = (history || []).map((d) => ({
    label: d.label,
    title: d.title,
    value: (d.newOrders || 0) + (d.updatedOrders || 0),
    upscaleCost: d.updatedOrders || 0,
  }));

  const showLabels = range !== 'ytd';

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)]">
      <div className="flex items-center justify-between mb-4 flex-shrink-0">
        <h1 className="text-3xl">Hangfire</h1>
        <select
          value={range}
          onChange={(e) => setRange(e.target.value)}
          className="px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 focus:outline-none focus:ring-2 focus:ring-[#003cbf]"
        >
          {RANGE_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>{opt.label}</option>
          ))}
        </select>
      </div>

      {loading && (
        <div className="flex items-center gap-2 text-gray-600 dark:text-gray-400 mb-4 flex-shrink-0">
          <Icon name="progress_activity" spin className="w-5 h-5" />
          Loading...
        </div>
      )}

      {error && (
        <div className="mb-4 p-3 bg-red-50 dark:bg-red-900/20 text-red-600 dark:text-red-400 rounded flex-shrink-0">
          {error}
        </div>
      )}

      {!loading && !error && history !== null && (
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 mb-4 flex-shrink-0">
          <div className="flex items-center justify-between mb-2">
            <div className="text-sm font-bold text-gray-500 dark:text-gray-400">
              Order Syncs — {RANGE_OPTIONS.find((o) => o.value === range)?.label}
            </div>
            <div className="flex items-center gap-4 text-xs text-gray-500 dark:text-gray-400">
              <div className="flex items-center gap-1.5">
                <span className="inline-block w-3 h-3 rounded-sm" style={{ backgroundColor: '#003cbf' }} />
                New Orders
              </div>
              <div className="flex items-center gap-1.5">
                <span className="inline-block w-3 h-3 rounded-sm" style={{ backgroundColor: '#e91e63' }} />
                Updated Orders
              </div>
            </div>
          </div>
          <BarChart
            data={chartData}
            formatValue={(v) => v.toLocaleString()}
            height={200}
            showXAxisLabels={showLabels}
            primaryLabel="New Orders"
            secondaryLabel="Updated Orders"
          />
        </div>
      )}

      <div className="flex-1 min-h-0 rounded-lg overflow-hidden border border-gray-200 dark:border-gray-700 shadow bg-white dark:bg-gray-800">
        <iframe
          src="/hangfire"
          title="Hangfire Dashboard"
          className="w-full h-full border-0"
        />
      </div>
    </div>
  );
}
