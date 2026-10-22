import React, { useState } from 'react';
import type { Tenant, Language } from '../types/index.ts';
import { maskCCCD } from '../utils/tenantLogic.ts';

interface TenantPickerPopoverProps {
  isOpen: boolean;
  onClose: () => void;
  tenants: Tenant[];
  onSelect: (tenant: Tenant) => void;
  lang: Language;
}

export const TenantPickerPopover: React.FC<TenantPickerPopoverProps> = ({
  isOpen,
  onClose,
  tenants,
  onSelect,
  lang,
}) => {
  const [search, setSearch] = useState('');

  if (!isOpen) return null;

  const filtered = tenants.filter(
    (t) =>
      t.name.toLowerCase().includes(search.toLowerCase()) ||
      t.phone.includes(search) ||
      t.code.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div
      className="fixed inset-0 z-50 bg-black/30 backdrop-blur-[1px] flex items-center justify-center p-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-md bg-white dark:bg-[#191c1e] rounded-xl shadow-2xl border border-neutral-100 dark:border-neutral-800 p-4 flex flex-col gap-3 animate-in fade-in zoom-in-95 duration-150"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between pb-2 border-b border-neutral-100 dark:border-neutral-800">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[#3525cd] dark:text-indigo-400 text-[20px]">
              person_search
            </span>
            <span className="font-bold text-sm text-[#191c1e] dark:text-neutral-100">
              {lang === 'vi' ? 'Chọn khách thuê (Component Test)' : 'Select Tenant (Test Component)'}
            </span>
          </div>
          <button
            onClick={onClose}
            className="text-neutral-400 hover:text-neutral-600 dark:hover:text-neutral-200"
          >
            <span className="material-symbols-outlined text-[18px]">close</span>
          </button>
        </div>

        {/* Search */}
        <div className="relative">
          <span className="material-symbols-outlined absolute left-3 top-2.5 text-[#565e74] dark:text-neutral-400 text-[18px]">
            search
          </span>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={lang === 'vi' ? 'Tìm nhanh theo tên, SĐT...' : 'Search by name, phone...'}
            className="w-full h-9 pl-9 pr-3 rounded-lg bg-[#f2f4f6] dark:bg-neutral-800 text-xs text-[#191c1e] dark:text-neutral-100 focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
            autoFocus
          />
        </div>

        {/* List */}
        <div className="flex flex-col gap-1 max-h-60 overflow-y-auto">
          {filtered.slice(0, 8).map((tenant) => {
            const { last4 } = maskCCCD(tenant.cccd);
            return (
              <div
                key={tenant.id}
                onClick={() => {
                  onSelect(tenant);
                  onClose();
                }}
                className="flex items-center justify-between p-2 rounded-lg hover:bg-[#f2f4f6] dark:hover:bg-neutral-800/70 cursor-pointer transition-colors"
              >
                <div className="flex items-center gap-2.5">
                  <div className="w-7 h-7 rounded-full bg-[#4f46e5]/10 text-[#4f46e5] text-xs font-bold flex items-center justify-center">
                    {tenant.name.slice(0, 1).toUpperCase()}
                  </div>
                  <div className="flex flex-col">
                    <span className="text-xs font-semibold text-[#191c1e] dark:text-neutral-100">
                      {tenant.name}
                    </span>
                    <span className="text-[10px] text-[#565e74] dark:text-neutral-400">
                      {tenant.phone} • CCCD: •••• {last4}
                    </span>
                  </div>
                </div>
                <span className="text-[11px] font-mono text-[#3525cd] dark:text-indigo-400 font-medium">
                  {tenant.room}
                </span>
              </div>
            );
          })}
          {filtered.length === 0 && (
            <div className="text-center py-6 text-xs text-[#565e74] dark:text-neutral-400">
              {lang === 'vi' ? 'Không tìm thấy khách thuê' : 'No tenants found'}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
