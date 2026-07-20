import React, { useEffect, useRef, useState } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/user/printify';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import Select from '@/components/forms/select';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';

const STORAGE_KEY = 'findPrintifyBlueprintFilter';

export default function FindPrintifyBlueprintModal({ show, onSelect, onClose }) {
  const session = useSession();
  const { getBlueprints, getBrands, getBlueprintImageUrl } = Printify(session);

  const [search, setSearch] = useState('');
  const [brand, setBrand] = useState('all');
  const [brands, setBrands] = useState([]);
  const [results, setResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [message, setMessage] = useState(null);
  const [debounceTimer, setDebounceTimer] = useState(null);
  const [searchInitiated, setSearchInitiated] = useState(false);
  const scrollRef = useRef(null);

  useEffect(() => {
    if (!show) return;

    let savedSearch = '';
    let savedBrand = 'all';
    try {
      const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
      savedSearch = saved.search || '';
      savedBrand = saved.brand || 'all';
    } catch { /* ignore */ }

    setSearch(savedSearch);
    setBrand(savedBrand);
    setResults([]);
    setSearching(false);
    setLoadingMore(false);
    setHasMore(false);
    setMessage(null);
    setSearchInitiated(false);

    getBrands()
      .then((resp) => {
        if (resp.data.success) {
          setBrands(resp.data.data.brands || []);
        }
      })
      .catch(() => {});

    handleSearch(savedSearch, savedBrand);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [show]);

  const saveFilter = (searchVal, brandVal) => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ search: searchVal, brand: brandVal }));
  };

  const handleSearch = (keyword, brandVal, append = false) => {
    if (!append) {
      setSearching(true);
      setSearchInitiated(true);
    } else {
      setLoadingMore(true);
    }
    setMessage(null);
    const start = append ? results.length : 0;
    getBlueprints(keyword, brandVal, start, 20)
      .then((resp) => {
        if (resp.data.success) {
          const newResults = resp.data.data.blueprints || [];
          if (append) {
            setResults((prev) => [...prev, ...newResults]);
          } else {
            setResults(newResults);
          }
          setHasMore(resp.data.data.hasMore || false);
        } else {
          setMessage({ type: 'error', text: resp.data.message || 'Failed to search blueprints' });
        }
      })
      .catch((error) => {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to search blueprints' });
      })
      .finally(() => {
        setSearching(false);
        setLoadingMore(false);
      });
  };

  const handleScroll = (e) => {
    const el = e.target;
    if (el.scrollTop + el.clientHeight >= el.scrollHeight - 50 && hasMore && !loadingMore) {
      handleSearch(search, brand, true);
    }
  };

  const handleSearchInput = (e) => {
    const value = e.target.value;
    setSearch(value);
    saveFilter(value, brand);
    if (debounceTimer) clearTimeout(debounceTimer);
    const timer = setTimeout(() => {
      handleSearch(value, brand);
    }, 400);
    setDebounceTimer(timer);
  };

  const handleBrandChange = (e) => {
    const value = e.target.value;
    setBrand(value);
    saveFilter(search, value);
    handleSearch(search, value);
  };

  const handleBlueprintClick = (bp) => {
    if (onSelect) {
      onSelect(bp);
    }
  };

  if (!show) return null;

  return (
    <Modal
      title="Find Blueprint"
      onClose={onClose}
      top
      className="min-w-[50em] max-w-full"
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <div className="filters tool-bar mb-4">
        <div className="flex items-center gap-2 flex-1">
          <Icon name="search" className="text-gray-400" />
          <Input
            name="blueprintSearch"
            value={search}
            onChange={handleSearchInput}
            placeholder="Search Printify blueprints..."
            className="flex-1 mb-0"
          />
        </div>
        <div className="right-side">
          <Select
            name="blueprintBrand"
            placeholder="All brands"
            options={brands.map((b) => ({ value: b, label: b }))}
            value={brand === 'all' ? '' : brand}
            onChange={handleBrandChange}
            className="mb-0 min-w-[10em]"
          />
        </div>
      </div>

      <div ref={scrollRef} className="max-h-[50vh] overflow-y-auto" onScroll={handleScroll}>
        {searching ? (
          <div className="flex items-center justify-center py-12">
            <Spinner className="text-4xl" />
          </div>
        ) : results.length > 0 ? (
          <>
            <div className="grid grid-cols-4 gap-2">
              {results.map((bp) => (
                <div
                  key={bp.id}
                  className="cursor-pointer rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 hover:border-primary-500 transition"
                  onClick={() => handleBlueprintClick(bp)}
                >
                  {bp.imageCount > 0 ? (
                    <img
                      src={getBlueprintImageUrl(bp.id, 0, true)}
                      alt={bp.title}
                      className="w-full aspect-square object-cover"
                    />
                  ) : (
                    <div className="w-full aspect-square flex items-center justify-center bg-gray-100 dark:bg-gray-700">
                      <span className="text-xs text-gray-400">No image</span>
                    </div>
                  )}
                  <div className="p-2">
                    <p className="text-xs font-medium truncate">{bp.title}</p>
                    <p className="text-xs text-gray-500 dark:text-gray-400 truncate">{bp.brand}</p>
                  </div>
                </div>
              ))}
            </div>
            {loadingMore && (
              <div className="flex items-center justify-center py-4">
                <Spinner className="text-2xl" />
              </div>
            )}
          </>
        ) : searchInitiated ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No blueprints found.</p>
        ) : (
          <p className="text-sm text-gray-500 dark:text-gray-400">Search for product blueprints from Printify.</p>
        )}
      </div>
    </Modal>
  );
}
