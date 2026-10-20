import React, { useEffect, useRef } from 'react';
import type { FilterState, Language } from '../types/index.ts';
import { translations } from '../utils/i18n.ts';

interface TenantFiltersProps {
  filter: FilterState;
  setFilter: React.Dispatch<React.SetStateAction<FilterState>>;
  lang: Language;
}

export const TenantFilters: React.FC<TenantFiltersProps> = ({ filter, setFilter, lang }) => {
  const t = translations[lang];
  const searchInputRef = useRef<HTMLInputElement>(null);

  // Keyboard shortcut: ⌘K or Ctrl+K to focus search input
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  const handleReset = () => {
    setFilter({
      search: '',
      status: 'all',
      account: 'all',
      sortBy: 'date_desc',
    });
    if (searchInputRef.current) {
      searchInputRef.current.value = '';
    }
  };

  const getStatusChipLabel = () => {
    if (filter.status === 'active') return t.chip_status_active;
    if (filter.status === 'inactive') return t.chip_status_inactive;
    return t.chip_all_status;
  };

  const getAccountChipLabel = () => {
    if (filter.account === 'has_acc') return t.chip_acc_has;
    if (filter.account === 'no_acc') return t.chip_acc_none;
    return t.chip_all_acc;
  };

  return (
    <div className="p-4 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100/60 dark:border-neutral-800 flex flex-col gap-4 transition-colors">
      <div className="flex flex-col lg:flex-row gap-3 items-stretch lg:items-center justify-between">
        {/* Search Input */}
        <div className="relative flex-1 min-w-[280px]">
          <span className="absolute inset-y-0 left-3 flex items-center pointer-events-none text-[#565e74] dark:text-neutral-400">
            <span className="material-symbols-outlined text-[18px]">search</span>
          </span>
          <input
            ref={searchInputRef}
            type="text"
            value={filter.search}
            onChange={(e) => setFilter((prev) => ({ ...prev, search: e.target.value }))}
            placeholder={t.search_placeholder}
            className="w-full h-10 pl-9 pr-14 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm placeholder-[#565e74]/70 dark:placeholder-neutral-500 focus:outline-none focus:bg-white dark:focus:bg-neutral-800 focus:ring-1 focus:ring-[#4f46e5] transition-all"
          />
          <div className="absolute right-2.5 top-2.5 hidden sm:flex items-center">
            <kbd className="px-1.5 py-0.5 rounded bg-[#eceef0] dark:bg-neutral-700 text-[11px] font-mono text-[#565e74] dark:text-neutral-300">
              ⌘K
            </kbd>
          </div>
        </div>

        {/* Quick Dropdowns & Sorter */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Status Filter */}
          <div className="relative">
            <select
              value={filter.status}
              onChange={(e) =>
                setFilter((prev) => ({
                  ...prev,
                  status: e.target.value as FilterState['status'],
                }))
              }
              className="h-10 pl-3 pr-8 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-200 text-xs font-semibold appearance-none cursor-pointer focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
            >
              <option value="all">{t.filter_status_all}</option>
              <option value="active">{t.filter_status_active}</option>
              <option value="inactive">{t.filter_status_inactive}</option>
            </select>
            <span className="material-symbols-outlined absolute right-2.5 top-3 pointer-events-none text-[#565e74] dark:text-neutral-400 text-[16px]">
              expand_more
            </span>
          </div>

          {/* Account Filter */}
          <div className="relative">
            <select
              value={filter.account}
              onChange={(e) =>
                setFilter((prev) => ({
                  ...prev,
                  account: e.target.value as FilterState['account'],
                }))
              }
              className="h-10 pl-3 pr-8 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-200 text-xs font-semibold appearance-none cursor-pointer focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
            >
              <option value="all">{t.filter_account_all}</option>
              <option value="has_acc">{t.filter_account_has}</option>
              <option value="no_acc">{t.filter_account_none}</option>
            </select>
            <span className="material-symbols-outlined absolute right-2.5 top-3 pointer-events-none text-[#565e74] dark:text-neutral-400 text-[16px]">
              expand_more
            </span>
          </div>

          {/* Sort */}
          <div className="relative">
            <select
              value={filter.sortBy}
              onChange={(e) =>
                setFilter((prev) => ({
                  ...prev,
                  sortBy: e.target.value as FilterState['sortBy'],
                }))
              }
              className="h-10 pl-3 pr-8 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-200 text-xs font-semibold appearance-none cursor-pointer focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
            >
              <option value="date_desc">{t.sort_date_desc}</option>
              <option value="name_asc">{t.sort_name_asc}</option>
              <option value="name_desc">{t.sort_name_desc}</option>
            </select>
            <span className="material-symbols-outlined absolute right-2.5 top-3 pointer-events-none text-[#565e74] dark:text-neutral-400 text-[16px]">
              sort
            </span>
          </div>

          {/* Reset Button */}
          <button
            onClick={handleReset}
            type="button"
            title={t.btn_reset}
            className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#565e74] dark:text-neutral-400 hover:text-[#191c1e] dark:hover:text-white hover:bg-[#eceef0] dark:hover:bg-neutral-700 text-xs font-semibold inline-flex items-center gap-1 transition-colors cursor-pointer"
          >
            <span className="material-symbols-outlined text-[16px]">restart_alt</span>
            <span>{t.btn_reset}</span>
          </button>
        </div>
      </div>

      {/* Active Filter Badges & Search Hint */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-2 pt-2 border-t border-neutral-100 dark:border-neutral-800">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs text-[#565e74] dark:text-neutral-400 font-medium">
            {t.active_filters}
          </span>
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-[#dae2fd] dark:bg-indigo-950/70 text-xs font-medium text-[#3525cd] dark:text-indigo-300">
            <span>{getStatusChipLabel()}</span>
            {filter.status !== 'all' && (
              <button
                onClick={() => setFilter((prev) => ({ ...prev, status: 'all' }))}
                className="hover:text-[#ba1a1a] flex items-center cursor-pointer"
                type="button"
              >
                <span className="material-symbols-outlined text-[14px]">close</span>
              </button>
            )}
          </span>
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-[#dae2fd] dark:bg-indigo-950/70 text-xs font-medium text-[#3525cd] dark:text-indigo-300">
            <span>{getAccountChipLabel()}</span>
            {filter.account !== 'all' && (
              <button
                onClick={() => setFilter((prev) => ({ ...prev, account: 'all' }))}
                className="hover:text-[#ba1a1a] flex items-center cursor-pointer"
                type="button"
              >
                <span className="material-symbols-outlined text-[14px]">close</span>
              </button>
            )}
          </span>
          {filter.search && (
            <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-[#dae2fd] dark:bg-indigo-950/70 text-xs font-medium text-[#3525cd] dark:text-indigo-300">
              <span>{filter.search}</span>
              <button
                onClick={() => setFilter((prev) => ({ ...prev, search: '' }))}
                className="hover:text-[#ba1a1a] flex items-center cursor-pointer"
                type="button"
              >
                <span className="material-symbols-outlined text-[14px]">close</span>
              </button>
            </span>
          )}
        </div>

        <div className="flex items-center gap-1 text-[#565e74] dark:text-neutral-400 text-xs font-mono">
          <span className="material-symbols-outlined text-[15px] text-[#3525cd] dark:text-indigo-400">
            info
          </span>
          <span>{t.search_hint}</span>
        </div>
      </div>
    </div>
  );
};
