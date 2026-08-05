import React, { useState, useEffect } from 'react';
import { Subscriptions } from '@/api/public/subscriptions';
import Icon from '@/components/ui/icon';
import { getIconName } from '@/helpers/icons';

export default function SubscriptionsPage() {
  const [data, setData] = useState({ subscriptions: [], products: [] });
  const [planType, setPlanType] = useState('monthly');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Subscriptions({}).getActiveSubscriptions().then(res => {
      if (res.data.success) {
        setData(res.data.data);
      }
      setLoading(false);
    }).catch(() => setLoading(false));
  }, []);

  const productLookup = (data.products || []).reduce((acc, p) => {
    acc[p.id] = p;
    return acc;
  }, {});

  const getFeatures = (featuresJson, type) => {
    try {
      const parsed = featuresJson ? JSON.parse(featuresJson) : {};
      return Array.isArray(parsed[type]) ? parsed[type] : [];
    } catch {
      return [];
    }
  };

  const getPrice = (subscription) => {
    const productId = planType === 'monthly' ? subscription.monthlyProductId : subscription.yearlyProductId;
    const product = productLookup[productId];
    if (!product) return null;
    return (product.price / 100).toFixed(2);
  };

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900">
        <p className="text-gray-500">Loading...</p>
      </div>
    );
  }

  const subscriptions = (data.subscriptions || []).sort((a, b) => a.sortIndex - b.sortIndex);

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-gray-100">
      <div className="max-w-6xl mx-auto px-4 py-16">
        <h1 className="text-4xl font-bold text-center mb-10">Choose Your Plan</h1>

        <div className="flex justify-center mb-12">
          <div className="inline-flex rounded-full border border-gray-300 dark:border-gray-600 overflow-hidden">
            <button
              onClick={() => setPlanType('monthly')}
              className={`px-8 py-2.5 text-sm font-medium transition rounded-l-full ${
                planType === 'monthly'
                  ? 'bg-blue-600 text-white'
                  : 'bg-transparent text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800'
              }`}
            >
              Monthly Plans
            </button>
            <button
              onClick={() => setPlanType('yearly')}
              className={`px-8 py-2.5 text-sm font-medium transition rounded-r-full ${
                planType === 'yearly'
                  ? 'bg-blue-600 text-white'
                  : 'bg-transparent text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800'
              }`}
            >
              Yearly Plans
            </button>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {subscriptions.map(s => {
            const features = getFeatures(s.featuresJson, planType);
            const price = getPrice(s);
            const productId = planType === 'monthly' ? s.monthlyProductId : s.yearlyProductId;

            if (!productId) return null;

            return (
              <div
                key={s.id}
                className={`bg-white dark:bg-gray-800 rounded-lg shadow-lg p-6 flex flex-col ${
                  s.featured ? 'ring-2 ring-blue-500' : ''
                }`}
              >
                <h2 className="text-xl font-semibold mb-4">{s.title}</h2>

                {s.featured && (
                  <span className="inline-block bg-blue-100 text-blue-700 text-xs font-medium px-2 py-1 rounded mb-4 self-start">
                    Featured
                  </span>
                )}

                <ul className="space-y-2 mb-6 flex-1">
                  {features.map((f, i) => (
                    <li key={i} className="flex items-start gap-2 text-sm text-gray-600 dark:text-gray-300">
                      <Icon name={getIconName(f.icon)} className="text-xl flex-shrink-0" />
                      <span>{f.text}</span>
                    </li>
                  ))}
                </ul>

                <div className="mb-4">
                  <span className="text-4xl text-lime-400">${Number(price).toLocaleString()}</span>
                  <span className="text-gray-500 text-sm">/{planType === 'monthly' ? 'mo' : 'yr'}</span>
                </div>

                <button className="w-full py-2.5 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition">
                  Select Plan
                </button>
              </div>
            );
          })}
        </div>

        {subscriptions.length === 0 && (
          <p className="text-center text-gray-500 mt-12">No subscriptions available.</p>
        )}
      </div>
    </div>
  );
}
