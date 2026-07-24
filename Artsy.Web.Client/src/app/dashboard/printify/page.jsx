import React, { useEffect, useState, useRef, useCallback } from 'react';
import { useSession } from '@/context/session';
import { Printify } from '@/api/admin/printify';
import Input from '@/components/forms/input';
import Select from '@/components/forms/select';
import Icon from '@/components/ui/icon';
import Spinner from '@/components/ui/spinner';
import Message from '@/components/ui/message';
import Carousel from '@/components/ui/carousel';
import ConfigurePrintifyBlueprint from './components/ConfigurePrintifyBlueprint';

const STORAGE_KEY = 'printifyDashboardFilter';

export default function DashboardPrintify() {
  const session = useSession();
  const { searchBlueprints, getBrands, getBlueprintImageUrl } = Printify(session);

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
  const [selectedBlueprint, setSelectedBlueprint] = useState(null);
  const scrollRef = useRef(null);
  const [scrollMaxHeight, setScrollMaxHeight] = useState('none');

  useEffect(() => {
    let savedSearch = '';
    let savedBrand = 'all';
    try {
      const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
      savedSearch = saved.search || '';
      savedBrand = saved.brand || 'all';
    } catch { /* ignore */ }

    setSearch(savedSearch);
    setBrand(savedBrand);

    getBrands()
      .then((resp) => {
        if (resp.data.success) {
          setBrands(resp.data.data.brands || []);
        }
      })
      .catch(() => {});

    handleSearch(savedSearch, savedBrand);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const saveFilter = (searchVal, brandVal) => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ search: searchVal, brand: brandVal }));
  };

  useEffect(() => {
    const updateMaxHeight = () => {
      if (scrollRef.current) {
        const rect = scrollRef.current.getBoundingClientRect();
        setScrollMaxHeight(`calc(100vh - ${rect.top + 40}px)`);
      }
    };
    updateMaxHeight();
    window.addEventListener('resize', updateMaxHeight);
    setTimeout(updateMaxHeight, 10);
    return () => window.removeEventListener('resize', updateMaxHeight);
  }, []);

  const handleSearch = useCallback((keyword, brandVal, append = false) => {
    if (!append) {
      setSearching(true);
      setSearchInitiated(true);
    } else {
      setLoadingMore(true);
    }
    setMessage(null);
    const start = append ? results.length : 0;
    searchBlueprints(keyword, brandVal, start, 20)
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
  }, [searchBlueprints, results.length]);

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
    setSelectedBlueprint(bp);
  };

  const handleConfigClose = () => {
    setSelectedBlueprint(null);
  };

  const handleConfigSave = () => {
    setSelectedBlueprint(null);
    handleSearch(search, brand);
  };

  return (
    <div>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Printify</h1>
      </div>

      <div className="filters tool-bar flex items-center gap-4 mb-6">
        <div className="flex items-center gap-2 flex-1 min-w-0">
          <Icon name="search" className="text-gray-400 shrink-0" />
          <Input
            name="printifySearch"
            value={search}
            onChange={handleSearchInput}
            placeholder="Search Printify blueprints..."
            className="flex-1 mb-0"
          />
        </div>
        <div className="right-side shrink-0">
          <Select
            name="printifyBrand"
            placeholder="All brands"
            options={brands.map((b) => ({ value: b, label: b }))}
            value={brand === 'all' ? '' : brand}
            onChange={handleBrandChange}
            className="mb-0 w-[10em]"
          />
        </div>
      </div>

      <div ref={scrollRef} className="overflow-y-auto" style={{ maxHeight: scrollMaxHeight }} onScroll={handleScroll}>
        {searching ? (
          <div className="flex items-center justify-center py-12">
            <Spinner className="text-4xl" />
          </div>
        ) : results.length > 0 ? (
          <>
            <div className="grid grid-cols-[repeat(auto-fill,300px)] gap-4">
              {results.map((bp) => (
                <div
                  key={bp.id}
                  className="cursor-pointer rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 hover:border-primary-500 transition"
                  onClick={() => handleBlueprintClick(bp)}
                >
                  {bp.imageCount > 0 ? (
                    <Carousel
                      images={Array.from({ length: bp.imageCount }, (_, i) => getBlueprintImageUrl(bp.id, i))}
                      alt={bp.title}
                      singleImage
                    />
                  ) : (
                    <div className="w-full aspect-square flex items-center justify-center bg-gray-100 dark:bg-gray-700">
                      <span className="text-xs text-gray-400">No image</span>
                    </div>
                  )}
                  <div className="p-3">
                    <div className="flex items-start justify-between gap-2">
                      <p className="text-sm font-medium truncate flex-1">{bp.title}</p>
                      {bp.published && (
                        <span className="px-2 py-0.5 rounded text-xs font-bold bg-green-500 text-white whitespace-nowrap">
                          Published
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-gray-500 dark:text-gray-400 truncate">{bp.brand}</p>
                  </div>
                </div>
              ))}
              {hasMore && (
                <div
                  className="cursor-pointer rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 hover:border-primary-500 transition flex flex-col items-center justify-center"
                  onClick={() => !loadingMore && handleSearch(search, brand, true)}
                >
                  <div className="w-full aspect-square flex items-center justify-center bg-gray-100 dark:bg-gray-700">
                    {loadingMore ? (
                      <Spinner className="text-3xl" />
                    ) : (
                      <span className="text-sm text-gray-500 dark:text-gray-400">View More</span>
                    )}
                  </div>
                  <div className="p-3">
                    <p className="text-sm font-medium text-center text-gray-500 dark:text-gray-400">View More</p>
                  </div>
                </div>
              )}
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

      {selectedBlueprint && (
        <ConfigurePrintifyBlueprint
          show={!!selectedBlueprint}
          blueprint={selectedBlueprint}
          onClose={handleConfigClose}
          onSave={handleConfigSave}
        />
      )}
    </div>
  );
}
