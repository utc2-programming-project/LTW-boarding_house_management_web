import React from 'react';
import type { Language, MetricSummary } from '../types/index.ts';
import { translations } from '../utils/i18n.ts';

interface TenantStatsProps {
  metrics: MetricSummary;
  lang: Language;
}

export const TenantStats: React.FC<TenantStatsProps> = ({ metrics, lang }) => {
  const t = translations[lang];

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
      {/* 1. Tổng khách thuê */}
      <div className="p-4 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100/60 dark:border-neutral-800 flex flex-col justify-between transition-colors">
        <div className="flex items-center justify-between">
          <span className="text-[11px] font-semibold text-[#565e74] dark:text-neutral-400 uppercase tracking-wider">
            {t.stat_total}
          </span>
          <span className="material-symbols-outlined text-[20px] text-[#3525cd] dark:text-indigo-400">
            groups
          </span>
        </div>
        <div className="flex items-baseline gap-1 mt-2">
          <span className="text-3xl font-bold tracking-tight text-[#191c1e] dark:text-neutral-100 font-sans">
            {metrics.totalTenants}
          </span>
          <span className="text-xs text-[#565e74] dark:text-neutral-400">
            {t.stat_total_unit}
          </span>
        </div>
        <div className="mt-2 text-xs text-[#565e74] dark:text-neutral-400 flex items-center gap-1 font-medium">
          <span className="text-[#3525cd] dark:text-indigo-400 font-bold">
            +{metrics.totalThisMonthDelta}
          </span>
          <span>{t.stat_this_month}</span>
        </div>
      </div>

      {/* 2. Đang lưu trú */}
      <div className="p-4 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100/60 dark:border-neutral-800 flex flex-col justify-between transition-colors">
        <div className="flex items-center justify-between">
          <span className="text-[11px] font-semibold text-[#565e74] dark:text-neutral-400 uppercase tracking-wider">
            {t.stat_active}
          </span>
          <div className="w-2.5 h-2.5 rounded-full bg-[#4f46e5]"></div>
        </div>
        <div className="flex items-baseline gap-1 mt-2">
          <span className="text-3xl font-bold tracking-tight text-[#191c1e] dark:text-neutral-100 font-sans">
            {metrics.activeTenants}
          </span>
          <span className="text-xs text-[#565e74] dark:text-neutral-400">
            {t.stat_active_unit}
          </span>
        </div>
        <div className="mt-2 text-xs text-[#565e74] dark:text-neutral-400 flex items-center gap-1 font-medium">
          <span className="text-[#191c1e] dark:text-neutral-200 font-bold">
            {metrics.activeRatio}%
          </span>
          <span>{t.stat_active_ratio}</span>
        </div>
      </div>

      {/* 3. Đã kích hoạt app */}
      <div className="p-4 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100/60 dark:border-neutral-800 flex flex-col justify-between transition-colors">
        <div className="flex items-center justify-between">
          <span className="text-[11px] font-semibold text-[#565e74] dark:text-neutral-400 uppercase tracking-wider">
            {t.stat_app_user}
          </span>
          <span className="material-symbols-outlined text-[20px] text-[#004d70] dark:text-sky-400">
            smartphone
          </span>
        </div>
        <div className="flex items-baseline gap-1 mt-2">
          <span className="text-3xl font-bold tracking-tight text-[#191c1e] dark:text-neutral-100 font-sans">
            {metrics.appActivatedCount}
          </span>
          <span className="text-xs text-[#565e74] dark:text-neutral-400">
            {t.stat_app_user_unit}
          </span>
        </div>
        <div className="mt-2 text-xs text-[#565e74] dark:text-neutral-400 flex items-center gap-1 font-medium">
          <span className="text-[#004d70] dark:text-sky-400 font-bold">
            {metrics.appActivatedRatio}%
          </span>
          <span>{t.stat_conversion}</span>
        </div>
      </div>

      {/* 4. Xác thực CCCD */}
      <div className="p-4 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100/60 dark:border-neutral-800 flex flex-col justify-between transition-colors">
        <div className="flex items-center justify-between">
          <span className="text-[11px] font-semibold text-[#565e74] dark:text-neutral-400 uppercase tracking-wider">
            {t.stat_pending_docs}
          </span>
          <span className="material-symbols-outlined text-[20px] text-[#565e74] dark:text-neutral-400">
            verified_user
          </span>
        </div>
        <div className="flex items-baseline gap-1 mt-2">
          <span className="text-3xl font-bold tracking-tight text-[#191c1e] dark:text-neutral-100 font-sans">
            {metrics.cccdMaskingCoverage}%
          </span>
          <span className="text-xs text-[#565e74] dark:text-neutral-400">
            {t.stat_masked_unit}
          </span>
        </div>
        <div className="mt-2 text-xs text-[#565e74] dark:text-neutral-400 flex items-center gap-1 font-medium">
          <span className="material-symbols-outlined text-[14px] text-[#3525cd] dark:text-indigo-400">
            lock
          </span>
          <span>{t.stat_d9_spec}</span>
        </div>
      </div>
    </div>
  );
};
