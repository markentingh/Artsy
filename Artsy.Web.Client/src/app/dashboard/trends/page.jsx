import React, { useEffect, useState, useRef, useCallback } from 'react';
import { useSession } from '@/context/session';
import { Trends, createTrendHubConnection } from '@/api/user/trends';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Input from '@/components/forms/input';
import Message from '@/components/ui/message';
import Sparkline from '@/components/ui/sparkline';

export default function DashboardTrends() {
  const session = useSession();
  const { getRecent, deleteTrend } = Trends(session);
  const [seedKeyword, setSeedKeyword] = useState('');
  const [trends, setTrends] = useState([]);
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState(null);
  const [showConsole, setShowConsole] = useState(false);
  const [consoleLines, setConsoleLines] = useState([]);
  const hubRef = useRef(null);
  const consoleRef = useRef(null);

  useEffect(() => {
    if (consoleRef.current) {
      consoleRef.current.scrollTop = consoleRef.current.scrollHeight;
    }
  }, [consoleLines]);

  const fetchRecent = useCallback(async () => {
    try {
      const response = await getRecent(20);
      if (response.data.success) {
        setTrends(response.data.data || []);
      }
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to load recent trends' });
    }
  }, [getRecent]);

  useEffect(() => {
    fetchRecent();
  }, []);

  const setupHub = useCallback(async () => {
    if (hubRef.current) return hubRef.current;

    const connection = createTrendHubConnection(session.token);

    connection.on('TrendResearchProgress', (progressEvent) => {
      const msg = typeof progressEvent.data === 'string'
        ? progressEvent.data
        : progressEvent.data?.message || JSON.stringify(progressEvent.data);
      setConsoleLines((prev) => [...prev, `[${progressEvent.stage}] ${msg}`]);
    });

    connection.on('TrendResearchComplete', (response) => {
      setLoading(false);
      if (response.success) {
        setResults(response.data || []);
        setMessage(null);
        setConsoleLines((prev) => [...prev, '[complete] Research complete.']);
        fetchRecent();
      } else {
        setMessage({ type: 'error', text: response.message || 'Research failed' });
        setConsoleLines((prev) => [...prev, `[error] ${response.message || 'Research failed'}`]);
      }
    });

    await connection.start();
    hubRef.current = connection;
    return connection;
  }, [session.token, fetchRecent]);

  const handleCollectResearch = async () => {
    if (!seedKeyword.trim()) {
      setMessage({ type: 'error', text: 'Please enter a seed keyword' });
      return;
    }

    setLoading(true);
    setResults([]);
    setMessage(null);
    setShowConsole(true);
    setConsoleLines([]);

    try {
      const connection = await setupHub();
      await connection.invoke('StartResearch', seedKeyword.trim());
    } catch (error) {
      setLoading(false);
      setMessage({ type: 'error', text: error?.message || 'Failed to start research' });
    }
  };

  const handleDeleteTrend = async (e, id) => {
    e.stopPropagation();
    try {
      const response = await deleteTrend({ id });
      if (response.data.success) {
        setTrends((prev) => prev.filter((t) => t.id !== id));
      }
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to delete trend' });
    }
  };

  const getSupplyBadge = (count) => {
    if (count === 0) {
      return <span className="px-2 py-1 rounded text-xs font-bold bg-red-500 text-white">0 Listings (Absolute Gap)</span>;
    }
    if (count <= 50) {
      return <span className="px-2 py-1 rounded text-xs font-bold bg-orange-500 text-white">1–50 Listings (Micro-Supply)</span>;
    }
    return null;
  };

  const renderTrendCard = (trend, isResult = false) => {
    const keyword = trend.keyword || trend.keyword;
    const etsyCount = trend.etsyListingCount ?? trend.etsyListingCount ?? 0;
    const interestData = isResult
      ? trend.interestDataPoints
      : trend.data ? (JSON.parse(trend.data).interestDataPoints || []) : [];

    return (
      <div
        key={trend.id || keyword}
        className="rounded-lg bg-white dark:bg-gray-800 shadow hover:shadow-md overflow-hidden transition p-4"
      >
        <div className="flex items-start justify-between mb-3">
          <div className="flex-1 min-w-0">
            <h3 className="font-bold text-lg truncate">{keyword}</h3>
            {trend.sector && (
              <span className="text-xs text-gray-500 dark:text-gray-400">{trend.sector}</span>
            )}
          </div>
          {getSupplyBadge(etsyCount)}
        </div>

        <div className="flex items-center justify-between">
          <div className="flex-1">
            <Sparkline data={interestData} width={140} height={40} />
          </div>
          <div className="flex flex-col items-end gap-2 ml-4">
            <span className="text-sm text-gray-600 dark:text-gray-300">
              {etsyCount} Etsy {etsyCount === 1 ? 'listing' : 'listings'}
            </span>
            <a
              href={`https://www.etsy.com/search?q=${encodeURIComponent(keyword)}`}
              target="_blank"
              rel="noopener noreferrer"
              className="text-primary-600 dark:text-primary-400 text-sm hover:underline flex items-center gap-1"
            >
              <Icon name="open_in_new" className="w-4 h-4" />
              Verify on Etsy
            </a>
          </div>
        </div>

        {!isResult && (
          <div className="flex justify-end mt-3">
            <ButtonIcon name="delete" color="red" onClick={(e) => handleDeleteTrend(e, trend.id)} title="Delete trend" />
          </div>
        )}
      </div>
    );
  };

  return (
    <div>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Trends</h1>
      </div>

      <div className="filters tool-bar mb-6">
        <div className="flex items-center gap-4">
          <div className="w-64">
            <Input
              name="seedKeyword"
              placeholder="Enter a seed keyword (e.g. home decor)"
              value={seedKeyword}
              onChange={(e) => setSeedKeyword(e.target.value)}
              disabled={loading}
            />
          </div>
          <div className="right-side">
            <ButtonOutline onClick={handleCollectResearch} disabled={loading || !seedKeyword.trim()}>
              <Icon name={loading ? 'progress_activity' : 'search'} className={loading ? 'animate-spin' : ''} />
              <span className="ml-2">{loading ? 'Researching...' : 'Collect Research'}</span>
            </ButtonOutline>
          </div>
        </div>
      </div>

      {showConsole && (
        <div
          ref={consoleRef}
          className="mb-6 bg-black text-green-400 font-mono text-xs p-3 rounded-lg overflow-y-auto"
          style={{ height: '5em' }}
        >
          {consoleLines.length === 0 ? (
            <span className="text-gray-500">Waiting for output...</span>
          ) : (
            consoleLines.map((line, i) => (
              <div key={i} className="whitespace-pre-wrap break-words">{line}</div>
            ))
          )}
        </div>
      )}

      {results.length > 0 && (
        <div className="mb-8">
          <h2 className="text-xl font-semibold mb-4">Latest Research Results</h2>
          <div className="grid grid-cols-[repeat(auto-fill,minmax(350px,1fr))] gap-4">
            {results.map((trend) => renderTrendCard(trend, true))}
          </div>
        </div>
      )}

      <div>
        <h2 className="text-xl font-semibold mb-4">Recent Trends</h2>
        {trends.length === 0 ? (
          <div className="p-12 text-center text-gray-600 dark:text-gray-400">
            No trends collected yet. Enter a seed keyword and click "Collect Research" to start finding market gaps.
          </div>
        ) : (
          <div className="grid grid-cols-[repeat(auto-fill,minmax(350px,1fr))] gap-4">
            {trends.map((trend) => renderTrendCard(trend, false))}
          </div>
        )}
      </div>
    </div>
  );
}
