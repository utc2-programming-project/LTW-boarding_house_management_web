import React, { useState } from 'react';
import type { Tenant, Language, UserRole } from '../types/index.ts';
import { maskCCCD } from '../utils/tenantLogic.ts';
import { translations } from '../utils/i18n.ts';

interface TenantTableProps {
  tenants: Tenant[];
  totalTenantsCount: number;
  lang: Language;
  role: UserRole;
  onViewTenant: (tenant: Tenant) => void;
  onEditTenant: (tenant: Tenant) => void;
  onToggleStatus: (tenant: Tenant) => void;
}

export const TenantTable: React.FC<TenantTableProps> = ({
  tenants,
  totalTenantsCount,
  lang,
  role,
  onViewTenant,
  onEditTenant,
  onToggleStatus,
}) => {
  const t = translations[lang];
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  const totalPages = Math.max(1, Math.ceil(tenants.length / pageSize));
  const startIndex = (currentPage - 1) * pageSize;
  const endIndex = Math.min(startIndex + pageSize, tenants.length);
  const currentTenants = tenants.slice(startIndex, endIndex);

  // Helper to extract first initial letter for avatar
  const getInitial = (name: string) => {
    const parts = name.trim().split(/\s+/);
    const lastWord = parts[parts.length - 1] || 'A';
    return lastWord.charAt(0).toUpperCase();
  };

  const canEdit = role === 'landlord' || role === 'manager' || role === 'admin';

  return (
    <div className="rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100/60 dark:border-neutral-800 overflow-hidden flex flex-col transition-colors">
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse" id="tenants-table">
          <thead>
            <tr className="bg-[#f2f4f6] dark:bg-neutral-800/80 text-[#565e74] dark:text-neutral-400 text-xs font-semibold uppercase tracking-wider select-none border-b border-neutral-200/60 dark:border-neutral-800">
              <th className="py-3 px-5 text-left font-medium">{t.col_name}</th>
              <th className="py-3 px-5 text-left font-medium">{t.col_phone}</th>
              <th className="py-3 px-5 text-left font-medium">{t.col_cccd}</th>
              <th className="py-3 px-5 text-left font-medium">{t.col_status}</th>
              <th className="py-3 px-5 text-left font-medium">{t.col_account}</th>
              <th className="py-3 px-5 text-right font-medium">{t.col_actions}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-neutral-100 dark:divide-neutral-800 text-sm text-[#191c1e] dark:text-neutral-200">
            {currentTenants.map((tenant) => {
              const { maskedPart, last4 } = maskCCCD(tenant.cccd);
              const initial = getInitial(tenant.name);

              return (
                <tr
                  key={tenant.id}
                  className="hover:bg-[#f2f4f6]/60 dark:hover:bg-neutral-800/50 transition-colors group cursor-pointer"
                  onClick={() => onViewTenant(tenant)}
                >
                  {/* Name & Code */}
                  <td className="py-3.5 px-5">
                    <div className="flex items-center gap-3">
                      <div
                        className={`w-9 h-9 rounded-full ${
                          tenant.avatarBg || 'bg-[#4f46e5]/10'
                        } ${
                          tenant.avatarColor || 'text-[#4f46e5]'
                        } text-sm flex items-center justify-center font-bold shrink-0 shadow-xs`}
                      >
                        {initial}
                      </div>
                      <div className="flex flex-col">
                        <span className="text-xs font-semibold text-[#191c1e] dark:text-neutral-100 group-hover:text-[#4f46e5] dark:group-hover:text-indigo-400 transition-colors">
                          {tenant.name}
                        </span>
                        <span className="text-[11px] font-mono text-[#565e74] dark:text-neutral-400">
                          {tenant.code}
                        </span>
                      </div>
                    </div>
                  </td>

                  {/* Phone */}
                  <td className="py-3.5 px-5 font-mono text-xs tabular-nums text-[#191c1e] dark:text-neutral-200">
                    {tenant.phone}
                  </td>

                  {/* CCCD (D9) */}
                  <td className="py-3.5 px-5">
                    <div className="flex items-center gap-1.5 font-mono text-xs text-[#565e74] dark:text-neutral-400">
                      <span className="tracking-widest font-mono text-[#777587] dark:text-neutral-500">
                        {maskedPart}
                      </span>
                      <span className="text-[#191c1e] dark:text-neutral-100 font-bold">
                        {last4}
                      </span>
                    </div>
                  </td>

                  {/* Status */}
                  <td className="py-3.5 px-5">
                    {tenant.status === 'active' ? (
                      <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-[#ECFDF5] dark:bg-emerald-950/70 text-[#065F46] dark:text-emerald-300 text-xs font-medium">
                        <span className="w-1.5 h-1.5 rounded-full bg-[#059669]"></span>
                        <span>{t.status_active}</span>
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-[#eceef0] dark:bg-neutral-800 text-[#565e74] dark:text-neutral-400 text-xs font-medium">
                        <span className="w-1.5 h-1.5 rounded-full bg-[#777587]"></span>
                        <span>{t.status_inactive}</span>
                      </span>
                    )}
                  </td>

                  {/* App Account */}
                  <td className="py-3.5 px-5">
                    {tenant.hasApp ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-[#e2dfff] dark:bg-indigo-950/70 text-[#0f0069] dark:text-indigo-300 text-xs font-medium">
                        <span className="material-symbols-outlined text-[13px]">smartphone</span>
                        <span>{t.acc_yes}</span>
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-[#eceef0] dark:bg-neutral-800 text-[#565e74] dark:text-neutral-400 text-xs font-medium">
                        <span className="material-symbols-outlined text-[13px]">
                          {tenant.status === 'inactive' ? 'block' : 'hourglass_empty'}
                        </span>
                        <span>{t.acc_none}</span>
                      </span>
                    )}
                  </td>

                  {/* Actions */}
                  <td
                    className="py-3.5 px-5 text-right"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <div className="inline-flex items-center gap-1">
                      <button
                        onClick={() => onViewTenant(tenant)}
                        className="p-1.5 rounded-lg text-[#565e74] dark:text-neutral-400 hover:text-[#4f46e5] dark:hover:text-indigo-400 hover:bg-[#eceef0] dark:hover:bg-neutral-800 transition-colors cursor-pointer"
                        title={t.action_view}
                        type="button"
                      >
                        <span className="material-symbols-outlined text-[18px]">visibility</span>
                      </button>

                      {canEdit && (
                        <>
                          <button
                            onClick={() => onEditTenant(tenant)}
                            className="p-1.5 rounded-lg text-[#565e74] dark:text-neutral-400 hover:text-[#4f46e5] dark:hover:text-indigo-400 hover:bg-[#eceef0] dark:hover:bg-neutral-800 transition-colors cursor-pointer"
                            title={t.action_edit}
                            type="button"
                          >
                            <span className="material-symbols-outlined text-[18px]">edit</span>
                          </button>

                          <button
                            onClick={() => onToggleStatus(tenant)}
                            className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                              tenant.status === 'active'
                                ? 'text-[#565e74] dark:text-neutral-400 hover:text-[#ba1a1a] hover:bg-[#eceef0] dark:hover:bg-neutral-800'
                                : 'text-[#565e74] dark:text-neutral-400 hover:text-[#059669] hover:bg-[#eceef0] dark:hover:bg-neutral-800'
                            }`}
                            title={
                              tenant.status === 'active'
                                ? t.action_deactivate
                                : t.action_reactivate
                            }
                            type="button"
                          >
                            <span className="material-symbols-outlined text-[18px]">
                              {tenant.status === 'active' ? 'person_off' : 'lock_reset'}
                            </span>
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Pagination Footer */}
      <div className="px-5 py-3 bg-[#f2f4f6] dark:bg-neutral-800/80 border-t border-neutral-200/60 dark:border-neutral-800 flex flex-col sm:flex-row items-center justify-between gap-3">
        <div className="flex items-center gap-4">
          <span className="text-xs text-[#565e74] dark:text-neutral-400 font-medium">
            {t.pagination_showing}{' '}
            <span className="font-bold text-[#191c1e] dark:text-neutral-100">
              {tenants.length > 0 ? startIndex + 1 : 0}
              {t.pagination_to}
              {endIndex}
            </span>{' '}
            {t.pagination_of}{' '}
            <span className="font-bold text-[#191c1e] dark:text-neutral-100">
              {totalTenantsCount}
            </span>{' '}
            {t.pagination_tenants}
          </span>
          <div className="hidden sm:flex items-center gap-1.5 text-[#565e74] dark:text-neutral-400 text-xs font-medium">
            <span>{t.rows_per_page}</span>
            <span className="px-2 py-0.5 rounded bg-[#eceef0] dark:bg-neutral-700 font-mono text-xs text-[#191c1e] dark:text-neutral-200 font-bold">
              {pageSize}
            </span>
          </div>
        </div>

        {/* Pagination buttons */}
        <div className="flex items-center gap-1">
          <button
            onClick={() => setCurrentPage(1)}
            disabled={currentPage === 1}
            className={`w-8 h-8 rounded-lg flex items-center justify-center transition-colors ${
              currentPage === 1
                ? 'text-neutral-400 dark:text-neutral-600 cursor-not-allowed'
                : 'text-[#565e74] dark:text-neutral-300 hover:bg-[#eceef0] dark:hover:bg-neutral-700 cursor-pointer'
            }`}
            type="button"
          >
            <span className="material-symbols-outlined text-[18px]">first_page</span>
          </button>
          <button
            onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
            disabled={currentPage === 1}
            className={`w-8 h-8 rounded-lg flex items-center justify-center transition-colors ${
              currentPage === 1
                ? 'text-neutral-400 dark:text-neutral-600 cursor-not-allowed'
                : 'text-[#565e74] dark:text-neutral-300 hover:bg-[#eceef0] dark:hover:bg-neutral-700 cursor-pointer'
            }`}
            type="button"
          >
            <span className="material-symbols-outlined text-[18px]">chevron_left</span>
          </button>

          {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
            <button
              key={page}
              onClick={() => setCurrentPage(page)}
              className={`w-8 h-8 rounded-lg flex items-center justify-center text-xs font-bold transition-all cursor-pointer ${
                currentPage === page
                  ? 'bg-[#4f46e5] text-white shadow-xs'
                  : 'text-[#565e74] dark:text-neutral-300 hover:bg-[#eceef0] dark:hover:bg-neutral-700'
              }`}
              type="button"
            >
              {page}
            </button>
          ))}

          <button
            onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
            disabled={currentPage === totalPages}
            className={`w-8 h-8 rounded-lg flex items-center justify-center transition-colors ${
              currentPage === totalPages
                ? 'text-neutral-400 dark:text-neutral-600 cursor-not-allowed'
                : 'text-[#565e74] dark:text-neutral-300 hover:bg-[#eceef0] dark:hover:bg-neutral-700 cursor-pointer'
            }`}
            type="button"
          >
            <span className="material-symbols-outlined text-[18px]">chevron_right</span>
          </button>
          <button
            onClick={() => setCurrentPage(totalPages)}
            disabled={currentPage === totalPages}
            className={`w-8 h-8 rounded-lg flex items-center justify-center transition-colors ${
              currentPage === totalPages
                ? 'text-neutral-400 dark:text-neutral-600 cursor-not-allowed'
                : 'text-[#565e74] dark:text-neutral-300 hover:bg-[#eceef0] dark:hover:bg-neutral-700 cursor-pointer'
            }`}
            type="button"
          >
            <span className="material-symbols-outlined text-[18px]">last_page</span>
          </button>
        </div>
      </div>
    </div>
  );
};
