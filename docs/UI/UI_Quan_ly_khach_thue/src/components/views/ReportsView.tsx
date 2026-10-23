import React from 'react';
import type { Language, MetricSummary } from '../../types/index.ts';

interface ReportsViewProps {
  metrics: MetricSummary;
  lang: Language;
}

export const ReportsView: React.FC<ReportsViewProps> = ({ metrics, lang }) => {
  return (
    <div className="flex flex-col gap-6 max-w-5xl">
      <div>
        <h1 className="text-2xl font-bold text-[#191c1e] dark:text-neutral-100 tracking-tight">
          {lang === 'vi' ? 'Báo cáo & Thống kê lưu trú' : 'Reports & Occupancy Analytics'}
        </h1>
        <p className="text-sm text-[#565e74] dark:text-neutral-400 mt-1">
          {lang === 'vi'
            ? 'Tổng hợp hiệu suất cho thuê phòng, tỉ lệ kích hoạt ứng dụng và dòng tiền'
            : 'Summary of occupancy rates, app adoption, and financial performance'}
        </p>
      </div>

      {/* Highlights */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="p-5 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100 dark:border-neutral-800">
          <span className="text-xs font-semibold text-[#565e74] dark:text-neutral-400 uppercase tracking-wider">
            {lang === 'vi' ? 'Tỉ lệ lấp đầy' : 'Occupancy Rate'}
          </span>
          <div className="flex items-baseline gap-2 mt-2">
            <span className="text-3xl font-bold text-[#191c1e] dark:text-neutral-100 font-sans">
              {metrics.activeRatio}%
            </span>
            <span className="text-xs text-[#059669] font-semibold">+3.2% vs Q2</span>
          </div>
          <div className="w-full bg-[#f2f4f6] dark:bg-neutral-800 h-2 rounded-full mt-3 overflow-hidden">
            <div
              className="bg-[#4f46e5] h-full rounded-full transition-all"
              style={{ width: `${metrics.activeRatio}%` }}
            ></div>
          </div>
        </div>

        <div className="p-5 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100 dark:border-neutral-800">
          <span className="text-xs font-semibold text-[#565e74] dark:text-neutral-400 uppercase tracking-wider">
            {lang === 'vi' ? 'Tỉ lệ số hóa App' : 'App Digitalization Rate'}
          </span>
          <div className="flex items-baseline gap-2 mt-2">
            <span className="text-3xl font-bold text-[#191c1e] dark:text-neutral-100 font-sans">
              {metrics.appActivatedRatio}%
            </span>
            <span className="text-xs text-[#059669] font-semibold">+12.5%</span>
          </div>
          <div className="w-full bg-[#f2f4f6] dark:bg-neutral-800 h-2 rounded-full mt-3 overflow-hidden">
            <div
              className="bg-[#004d70] dark:bg-sky-500 h-full rounded-full transition-all"
              style={{ width: `${metrics.appActivatedRatio}%` }}
            ></div>
          </div>
        </div>

        <div className="p-5 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100 dark:border-neutral-800">
          <span className="text-xs font-semibold text-[#565e74] dark:text-neutral-400 uppercase tracking-wider">
            {lang === 'vi' ? 'Doanh thu tháng này' : 'Estimated Revenue'}
          </span>
          <div className="flex items-baseline gap-2 mt-2">
            <span className="text-3xl font-bold text-[#191c1e] dark:text-neutral-100 font-mono">
              134.4 tr
            </span>
            <span className="text-xs text-[#565e74] dark:text-neutral-400">VND</span>
          </div>
          <p className="text-xs text-[#059669] font-medium mt-2 flex items-center gap-1">
            <span className="material-symbols-outlined text-[15px]">trending_up</span>
            {lang === 'vi' ? 'Đã thu 92% tiền phòng' : '92% collected on time'}
          </p>
        </div>
      </div>

      {/* Occupancy by Building Chart representation */}
      <div className="p-6 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100 dark:border-neutral-800">
        <h3 className="text-sm font-bold text-[#191c1e] dark:text-neutral-100 mb-4">
          {lang === 'vi' ? 'Phân bố khách thuê theo toà nhà' : 'Tenant Distribution by Building'}
        </h3>
        <div className="flex flex-col gap-3">
          {[
            { label: 'Toà A (8 phòng)', count: 8, max: 8, color: 'bg-[#4f46e5]' },
            { label: 'Toà B (10 phòng)', count: 9, max: 10, color: 'bg-[#3525cd]' },
            { label: 'Toà C (8 phòng)', count: 7, max: 8, color: 'bg-[#004d70]' },
            { label: 'Toà D (6 phòng)', count: 4, max: 6, color: 'bg-[#565e74]' },
          ].map((item, i) => (
            <div key={i} className="flex flex-col gap-1">
              <div className="flex justify-between text-xs">
                <span className="font-medium text-[#191c1e] dark:text-neutral-200">{item.label}</span>
                <span className="font-mono text-[#565e74] dark:text-neutral-400">
                  {item.count}/{item.max} ({Math.round((item.count / item.max) * 100)}%)
                </span>
              </div>
              <div className="w-full bg-[#f2f4f6] dark:bg-neutral-800 h-3 rounded-full overflow-hidden">
                <div
                  className={`${item.color} h-full rounded-full transition-all`}
                  style={{ width: `${(item.count / item.max) * 100}%` }}
                ></div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
