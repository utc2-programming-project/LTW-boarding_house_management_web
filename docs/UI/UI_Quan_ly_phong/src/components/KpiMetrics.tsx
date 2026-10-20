import React from 'react';
import type { KpiSummary } from '../types/index.ts';

interface KpiMetricsProps {
  metrics: KpiSummary;
}

export const KpiMetrics: React.FC<KpiMetricsProps> = ({ metrics }) => {
  return (
    <div className="flex flex-col gap-2">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Total Rooms Card */}
        <div className="bg-surface-container-lowest rounded-xl p-4 shadow-sm flex flex-col justify-between border border-surface-container hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between text-on-surface-variant text-[13px] font-medium">
            <span>Tổng số phòng</span>
            <div className="w-7 h-7 rounded-lg bg-surface-container flex items-center justify-center text-on-surface">
              <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                <polyline points="9 22 9 12 15 12 15 22" />
              </svg>
            </div>
          </div>
          <div className="flex items-baseline justify-between mt-3">
            <span className="text-[32px] font-bold text-on-surface leading-none tracking-tight">
              {metrics.totalRooms}
            </span>
            <span className="text-on-surface-variant text-[12px] font-medium">
              {metrics.totalProperties} cơ sở
            </span>
          </div>
        </div>

        {/* Vacant Rooms Card */}
        <div className="bg-surface-container-lowest rounded-xl p-4 shadow-sm flex flex-col justify-between border border-surface-container hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between text-on-surface-variant text-[13px] font-medium">
            <span>Phòng trống</span>
            <div className="w-7 h-7 rounded-lg bg-surface-container flex items-center justify-center text-on-surface-variant">
              <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                <rect height="18" rx="2" width="18" x="3" y="3" />
                <path d="M9 3v18" />
              </svg>
            </div>
          </div>
          <div className="flex items-baseline justify-between mt-3">
            <div className="flex items-baseline gap-1.5">
              <span className="text-[32px] font-bold text-on-surface leading-none tracking-tight">
                {metrics.vacantRooms}
              </span>
              <span className="text-[13px] text-on-surface-variant font-medium">
                / {metrics.totalRooms}
              </span>
            </div>
            <span className="px-2 py-0.5 rounded bg-surface-container text-[11px] text-on-surface-variant font-medium">
              {metrics.vacantPercentage}%
            </span>
          </div>
        </div>

        {/* Occupied Rooms Card */}
        <div className="bg-surface-container-lowest rounded-xl p-4 shadow-sm flex flex-col justify-between border border-surface-container hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between text-on-surface-variant text-[13px] font-medium">
            <span>Đang thuê</span>
            <div className="w-7 h-7 rounded-lg bg-surface-container flex items-center justify-center text-secondary">
              <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
                <circle cx="9" cy="7" r="4" />
                <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
                <path d="M16 3.13a4 4 0 0 1 0 7.75" />
              </svg>
            </div>
          </div>
          <div className="flex items-baseline justify-between mt-3">
            <div className="flex items-baseline gap-1.5">
              <span className="text-[32px] font-bold text-on-surface leading-none tracking-tight">
                {metrics.occupiedRooms}
              </span>
              <span className="text-[13px] text-on-surface-variant font-medium">
                / {metrics.totalRooms}
              </span>
            </div>
            <span className="px-2 py-0.5 rounded bg-secondary-fixed text-on-secondary-fixed-variant text-[11px] font-semibold">
              {metrics.occupiedPercentage}%
            </span>
          </div>
        </div>

        {/* Maintenance / Pending Rooms Card */}
        <div className="bg-surface-container-lowest rounded-xl p-4 shadow-sm flex flex-col justify-between border border-surface-container hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between text-on-surface-variant text-[13px] font-medium">
            <span>Bảo trì / Chờ duyệt</span>
            <div className="w-7 h-7 rounded-lg bg-surface-container flex items-center justify-center text-tertiary">
              <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
              </svg>
            </div>
          </div>
          <div className="flex items-baseline justify-between mt-3">
            <div className="flex items-baseline gap-1.5">
              <span className="text-[32px] font-bold text-on-surface leading-none tracking-tight">
                {metrics.maintenanceOrPendingRooms}
              </span>
              <span className="text-[13px] text-on-surface-variant font-medium">
                / {metrics.totalRooms}
              </span>
            </div>
            <span className="px-2 py-0.5 rounded bg-tertiary-fixed text-on-tertiary-fixed-variant text-[11px] font-semibold">
              {metrics.maintenancePercentage}%
            </span>
          </div>
        </div>
      </div>

      {/* Metric caption note */}
      <div className="flex items-center gap-1.5 text-outline text-[12px] px-1">
        <svg className="w-3.5 h-3.5 stroke-current fill-none stroke-2 shrink-0" viewBox="0 0 24 24">
          <circle cx="12" cy="12" r="10" />
          <line x1="12" x2="12" y1="16" y2="12" />
          <line x1="12" x2="12.01" y1="8" y2="8" />
        </svg>
        <span>Số liệu tính theo nhà trọ, từ khóa, trạng thái hoạt động và khoảng giá đang chọn.</span>
      </div>
    </div>
  );
};
