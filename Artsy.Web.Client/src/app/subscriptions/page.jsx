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
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-950 text-white">
        <p className="text-white/60">Loading...</p>
      </div>
    );
  }

  const subscriptions = (data.subscriptions || []).sort((a, b) => a.sortIndex - b.sortIndex);

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-950 text-white">
      <div className="max-w-6xl mx-auto px-4 py-16">
        <h1 className="text-4xl font-bold text-center mb-10">Choose Your Plan</h1>

        <div className="flex justify-center mb-12">
          <div className="inline-flex rounded-full border border-white/20 overflow-hidden bg-white/5">
            <button
              onClick={() => setPlanType('monthly')}
              className={`px-8 py-2.5 text-sm font-medium transition rounded-l-full ${
                planType === 'monthly'
                  ? 'bg-purple-600 text-white'
                  : 'bg-transparent text-white/70 hover:bg-white/10'
              }`}
            >
              Monthly Plans
            </button>
            <button
              onClick={() => setPlanType('yearly')}
              className={`px-8 py-2.5 text-sm font-medium transition rounded-r-full ${
                planType === 'yearly'
                  ? 'bg-purple-600 text-white'
                  : 'bg-transparent text-white/70 hover:bg-white/10'
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
                className={`relative overflow-hidden border border-white/10 rounded-lg shadow-lg p-6 flex flex-col bg-[radial-gradient(circle_at_top_left,_rgb(53_15_125_/_95%)_0%,_rgb(38_14_72_/_70%)_40%,_rgba(15,23,42,0.98)_100%)] ${
                  s.featured ? 'ring-2 ring-purple-500' : ''
                }`}>

                <h2 className="text-xl font-semibold mb-4">{s.title}</h2>

                {s.featured && (
                  <span className="inline-block bg-purple-500/20 text-purple-300 text-xs font-medium px-2 py-1 rounded mb-4 self-start">
                    Featured
                  </span>
                )}

                <ul className="space-y-2 mb-6 flex-1">
                  {features.map((f, i) => (
                    <li key={i} className="flex items-start gap-2 text-sm text-white/70">
                      <Icon name={getIconName(f.icon)} className="text-xl flex-shrink-0" />
                      <span>{f.text}</span>
                    </li>
                  ))}
                </ul>

                <div className="mb-4">
                  <span className="text-4xl text-white">${Number(price).toLocaleString()}</span>
                  <span className="text-white/50 text-sm">/{planType === 'monthly' ? 'mo' : 'yr'}</span>
                </div>

                <button className="w-full py-2.5 bg-purple-600 text-white rounded-lg font-medium hover:bg-purple-700 transition">
                  Select Plan
                </button>
              </div>
            );
          })}
        </div>

        {subscriptions.length === 0 && (
          <p className="text-center text-white/60 mt-12">No subscriptions available.</p>
        )}
      </div>
    </div>
  );
}
