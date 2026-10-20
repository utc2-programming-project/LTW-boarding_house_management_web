import React from 'react';
import type { ContractItem } from '../types/index.ts';
import { formatVndCurrency } from '../utils/roomCalculations.ts';

interface ContractsViewProps {
  contracts: ContractItem[];
}

export const ContractsView: React.FC<ContractsViewProps> = ({ contracts }) => {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h2 className="text-[28px] font-bold text-on-surface tracking-tight">Quản lý Hợp đồng thuê</h2>
        <p className="text-[14px] text-on-surface-variant">
          Theo dõi hợp đồng còn hạn, tiền đặt cọc và chu kỳ tái ký của từng phòng trọ.
        </p>
      </div>

      <div className="bg-surface-container-lowest rounded-xl shadow-sm border border-surface-container overflow-hidden">
        <div className="overflow-x-auto w-full">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-surface-container-low text-outline text-[11px] tracking-wider uppercase font-semibold border-b border-surface-container">
                <th className="py-3 px-4">Mã Hợp Đồng</th>
                <th className="py-3 px-4">Phòng</th>
                <th className="py-3 px-4">Nhà trọ</th>
                <th className="py-3 px-4">Khách thuê</th>
                <th className="py-3 px-4 text-right">Tiền cọc</th>
                <th className="py-3 px-4 text-right">Tiền thuê / tháng</th>
                <th className="py-3 px-4">Thời hạn HĐ</th>
                <th className="py-3 px-4 text-center">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-container/50 text-[14px] text-on-surface">
              {contracts.map((c) => (
                <tr key={c.id} className="hover:bg-surface-container transition-colors">
                  <td className="py-3.5 px-4 font-semibold text-primary">{c.contractCode}</td>
                  <td className="py-3.5 px-4 font-semibold">{c.roomCode}</td>
                  <td className="py-3.5 px-4 text-on-surface-variant text-[13px]">{c.propertyName}</td>
                  <td className="py-3.5 px-4">
                    <div className="flex flex-col">
                      <span className="font-semibold text-on-surface">{c.tenantName}</span>
                      <span className="text-[11px] text-on-surface-variant">{c.tenantPhone}</span>
                    </div>
                  </td>
                  <td className="py-3.5 px-4 text-right font-medium">
                    {formatVndCurrency(c.deposit)}
                  </td>
                  <td className="py-3.5 px-4 text-right font-semibold">
                    {formatVndCurrency(c.monthlyRent)}
                  </td>
                  <td className="py-3.5 px-4 text-[13px]">
                    {c.startDate} → {c.endDate}
                  </td>
                  <td className="py-3.5 px-4 text-center">
                    {c.status === 'Hiệu lực' ? (
                      <span className="px-2.5 py-0.5 rounded-full bg-secondary-fixed text-on-secondary-fixed-variant text-[12px] font-semibold">
                        Hiệu lực
                      </span>
                    ) : (
                      <span className="px-2.5 py-0.5 rounded-full bg-tertiary-fixed text-on-tertiary-fixed-variant text-[12px] font-semibold">
                        {c.status}
                      </span>
                    )}
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
