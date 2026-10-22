import React from 'react';
import type { TenantItem } from '../types/index.ts';

interface TenantsViewProps {
  tenants: TenantItem[];
}

export const TenantsView: React.FC<TenantsViewProps> = ({ tenants }) => {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h2 className="text-[28px] font-bold text-on-surface tracking-tight">Danh bạ Khách thuê</h2>
        <p className="text-[14px] text-on-surface-variant">
          Thông tin cư dân lưu trú, định danh CCCD và lịch sử thuê phòng trọ.
        </p>
      </div>

      <div className="bg-surface-container-lowest rounded-xl shadow-sm border border-surface-container overflow-hidden">
        <div className="overflow-x-auto w-full">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-surface-container-low text-outline text-[11px] tracking-wider uppercase font-semibold border-b border-surface-container">
                <th className="py-3 px-4">Họ và tên</th>
                <th className="py-3 px-4">Số điện thoại</th>
                <th className="py-3 px-4">CCCD / Định danh</th>
                <th className="py-3 px-4">Phòng</th>
                <th className="py-3 px-4">Cơ sở / Tòa nhà</th>
                <th className="py-3 px-4">Ngày bắt đầu</th>
                <th className="py-3 px-4 text-center">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-container/50 text-[14px] text-on-surface">
              {tenants.map((t) => (
                <tr key={t.id} className="hover:bg-surface-container transition-colors">
                  <td className="py-3.5 px-4 font-semibold text-on-surface flex items-center gap-2.5">
                    <div className="w-8 h-8 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold text-[12px]">
                      {t.name.split(' ').pop()?.charAt(0) || 'K'}
                    </div>
                    <span>{t.name}</span>
                  </td>
                  <td className="py-3.5 px-4 text-primary font-medium">{t.phone}</td>
                  <td className="py-3.5 px-4 font-mono text-[13px] text-on-surface-variant">{t.cccd}</td>
                  <td className="py-3.5 px-4 font-semibold">{t.roomCode}</td>
                  <td className="py-3.5 px-4 text-on-surface-variant text-[13px]">{t.propertyName}</td>
                  <td className="py-3.5 px-4 text-[13px]">{t.startDate}</td>
                  <td className="py-3.5 px-4 text-center">
                    <span className="px-2.5 py-0.5 rounded-full bg-secondary-fixed text-on-secondary-fixed-variant text-[12px] font-semibold">
                      {t.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
