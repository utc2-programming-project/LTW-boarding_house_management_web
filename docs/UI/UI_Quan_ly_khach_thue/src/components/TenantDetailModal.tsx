import React, { useState } from 'react';
import type { Tenant, Language } from '../types/index.ts';
import { translations } from '../utils/i18n.ts';
import { maskCCCD } from '../utils/tenantLogic.ts';

interface TenantDetailModalProps {
  tenant: Tenant | null;
  isOpen: boolean;
  onClose: () => void;
  onEdit: (tenant: Tenant) => void;
  lang: Language;
}

export const TenantDetailModal: React.FC<TenantDetailModalProps> = ({
  tenant,
  isOpen,
  onClose,
  onEdit,
  lang,
}) => {
  const t = translations[lang];
  const [revealCCCD, setRevealCCCD] = useState(false);

  if (!isOpen || !tenant) return null;

  const { maskedPart, last4 } = maskCCCD(tenant.cccd);

  return (
    <div
      className="fixed inset-0 z-50 bg-[#191c1e]/50 backdrop-blur-[2px] flex items-center justify-center p-4 overflow-y-auto"
      role="dialog"
      aria-modal="true"
    >
      <div className="bg-white dark:bg-[#191c1e] w-full max-w-lg rounded-xl shadow-2xl flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-150 border border-neutral-100 dark:border-neutral-800 my-8">
        {/* Header */}
        <div className="px-6 py-4 bg-[#f2f4f6] dark:bg-neutral-800 flex items-center justify-between border-b border-neutral-200/60 dark:border-neutral-700">
          <div className="flex items-center gap-2.5">
            <span className="material-symbols-outlined text-[#3525cd] dark:text-indigo-400 text-[22px]">
              badge
            </span>
            <span className="text-base font-bold text-[#191c1e] dark:text-neutral-100">
              {t.modal_detail_title}
            </span>
          </div>
          <button
            onClick={onClose}
            aria-label={t.btn_close}
            className="text-[#565e74] hover:text-[#191c1e] dark:hover:text-white p-1 rounded-lg transition-colors cursor-pointer"
            type="button"
          >
            <span className="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        {/* Content */}
        <div className="p-6 flex flex-col gap-5">
          {/* Tenant top summary card */}
          <div className="flex items-center gap-4 p-4 rounded-xl bg-[#f2f4f6]/80 dark:bg-neutral-800/60 border border-neutral-200/40 dark:border-neutral-700/60">
            <div
              className={`w-14 h-14 rounded-full ${
                tenant.avatarBg || 'bg-[#4f46e5]/10'
              } ${
                tenant.avatarColor || 'text-[#4f46e5]'
              } text-xl flex items-center justify-center font-bold shrink-0 shadow-sm`}
            >
              {tenant.name.slice(0, 1).toUpperCase()}
            </div>
            <div className="flex flex-col flex-1 min-w-0">
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold text-[#191c1e] dark:text-neutral-100 truncate">
                  {tenant.name}
                </h3>
                <span className="text-xs font-mono text-[#565e74] dark:text-neutral-400">
                  {tenant.code}
                </span>
              </div>
              <div className="flex flex-wrap items-center gap-2 mt-1.5">
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
                {tenant.hasApp ? (
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-[#e2dfff] dark:bg-indigo-950/70 text-[#0f0069] dark:text-indigo-300 text-xs font-medium">
                    <span className="material-symbols-outlined text-[13px]">smartphone</span>
                    <span>{t.acc_yes}</span>
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-[#eceef0] dark:bg-neutral-800 text-[#565e74] dark:text-neutral-400 text-xs font-medium">
                    <span className="material-symbols-outlined text-[13px]">hourglass_empty</span>
                    <span>{t.acc_none}</span>
                  </span>
                )}
              </div>
            </div>
          </div>

          {/* Details Grid */}
          <div className="grid grid-cols-2 gap-4 text-xs">
            <div className="p-3 rounded-xl bg-[#f2f4f6]/60 dark:bg-neutral-800/40 border border-neutral-100 dark:border-neutral-800">
              <span className="text-[#565e74] dark:text-neutral-400 font-medium">{t.col_phone}</span>
              <p className="text-sm font-semibold text-[#191c1e] dark:text-neutral-100 font-mono mt-0.5">
                {tenant.phone}
              </p>
            </div>

            <div className="p-3 rounded-xl bg-[#f2f4f6]/60 dark:bg-neutral-800/40 border border-neutral-100 dark:border-neutral-800">
              <span className="text-[#565e74] dark:text-neutral-400 font-medium">{t.input_room}</span>
              <p className="text-sm font-semibold text-[#191c1e] dark:text-neutral-100 mt-0.5">
                {tenant.room}
              </p>
            </div>

            {/* CCCD with D9 unmask toggle */}
            <div className="col-span-2 p-3 rounded-xl bg-[#f2f4f6]/60 dark:bg-neutral-800/40 border border-neutral-100 dark:border-neutral-800 flex items-center justify-between">
              <div>
                <span className="text-[#565e74] dark:text-neutral-400 font-medium">
                  {t.col_cccd} ({lang === 'vi' ? 'Bảo mật D9' : 'D9 Security'})
                </span>
                <p className="text-sm font-bold text-[#191c1e] dark:text-neutral-100 font-mono mt-0.5 tracking-wider">
                  {revealCCCD ? tenant.cccd : `${maskedPart} ${last4}`}
                </p>
              </div>
              <button
                onClick={() => setRevealCCCD(!revealCCCD)}
                className="px-2.5 py-1 rounded-lg bg-white dark:bg-neutral-700 text-[#3525cd] dark:text-indigo-300 font-medium text-xs shadow-xs hover:bg-neutral-50 dark:hover:bg-neutral-600 transition-colors flex items-center gap-1 cursor-pointer"
                type="button"
              >
                <span className="material-symbols-outlined text-[15px]">
                  {revealCCCD ? 'visibility_off' : 'visibility'}
                </span>
                <span>{revealCCCD ? (lang === 'vi' ? 'Che số' : 'Mask') : (lang === 'vi' ? 'Xem đầy đủ' : 'Reveal')}</span>
              </button>
            </div>

            <div className="p-3 rounded-xl bg-[#f2f4f6]/60 dark:bg-neutral-800/40 border border-neutral-100 dark:border-neutral-800">
              <span className="text-[#565e74] dark:text-neutral-400 font-medium">{t.input_hometown}</span>
              <p className="text-sm font-semibold text-[#191c1e] dark:text-neutral-100 mt-0.5">
                {tenant.hometown || (lang === 'vi' ? 'Chưa cập nhật' : 'Not set')}
              </p>
            </div>

            <div className="p-3 rounded-xl bg-[#f2f4f6]/60 dark:bg-neutral-800/40 border border-neutral-100 dark:border-neutral-800">
              <span className="text-[#565e74] dark:text-neutral-400 font-medium">{t.input_emergency}</span>
              <p className="text-sm font-semibold text-[#191c1e] dark:text-neutral-100 mt-0.5">
                {tenant.emergencyContact || (lang === 'vi' ? 'Chưa cập nhật' : 'Not set')}
              </p>
            </div>

            <div className="p-3 rounded-xl bg-[#f2f4f6]/60 dark:bg-neutral-800/40 border border-neutral-100 dark:border-neutral-800">
              <span className="text-[#565e74] dark:text-neutral-400 font-medium">{t.input_rent}</span>
              <p className="text-sm font-semibold text-[#191c1e] dark:text-neutral-100 font-mono mt-0.5">
                {tenant.monthlyRent ? tenant.monthlyRent.toLocaleString('vi-VN') + ' đ' : '4.500.000 đ'}
              </p>
            </div>

            <div className="p-3 rounded-xl bg-[#f2f4f6]/60 dark:bg-neutral-800/40 border border-neutral-100 dark:border-neutral-800">
              <span className="text-[#565e74] dark:text-neutral-400 font-medium">{t.input_deposit}</span>
              <p className="text-sm font-semibold text-[#191c1e] dark:text-neutral-100 font-mono mt-0.5">
                {tenant.depositAmount ? tenant.depositAmount.toLocaleString('vi-VN') + ' đ' : '4.500.000 đ'}
              </p>
            </div>

            {tenant.notes && (
              <div className="col-span-2 p-3 rounded-xl bg-[#f2f4f6]/60 dark:bg-neutral-800/40 border border-neutral-100 dark:border-neutral-800">
                <span className="text-[#565e74] dark:text-neutral-400 font-medium">{t.input_notes}</span>
                <p className="text-xs text-[#191c1e] dark:text-neutral-200 mt-0.5 leading-relaxed">
                  {tenant.notes}
                </p>
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-3.5 bg-[#f2f4f6] dark:bg-neutral-800 border-t border-neutral-200/60 dark:border-neutral-700 flex items-center justify-end gap-2">
          <button
            onClick={() => {
              onClose();
              onEdit(tenant);
            }}
            type="button"
            className="px-4 py-2 rounded-xl bg-[#4f46e5] text-white hover:bg-[#3525cd] text-xs font-semibold shadow-xs flex items-center gap-1.5 transition-colors cursor-pointer"
          >
            <span className="material-symbols-outlined text-[16px]">edit</span>
            <span>{t.action_edit}</span>
          </button>
          <button
            onClick={onClose}
            type="button"
            className="px-4 py-2 rounded-xl text-xs font-semibold text-[#565e74] dark:text-neutral-300 hover:text-[#191c1e] dark:hover:text-white hover:bg-[#eceef0] dark:hover:bg-neutral-700 transition-colors cursor-pointer"
          >
            {t.btn_close}
          </button>
        </div>
      </div>
    </div>
  );
};
