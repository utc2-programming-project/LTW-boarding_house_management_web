import React, { useState } from 'react';
import {
  CalendarDays,
  Plus,
  ArrowRight,
  Clock,
  CheckCircle2,
  AlertTriangle,
  History,
  Building,
  ChevronRight,
  Filter,
} from 'lucide-react';
import { PriceVersion, ServiceItem } from '../types';

interface PriceHistoryViewProps {
  priceVersions: PriceVersion[];
  services: ServiceItem[];
  onOpenUpdateModal: (service: ServiceItem) => void;
  onBackToCatalog: () => void;
}

export const PriceHistoryView: React.FC<PriceHistoryViewProps> = ({
  priceVersions,
  services,
  onOpenUpdateModal,
  onBackToCatalog,
}) => {
  const [selectedServiceId, setSelectedServiceId] = useState<string>('srv-3'); // Internet

  const currentService = services.find((s) => s.id === selectedServiceId) || services[0];
  const filteredVersions = priceVersions.filter(
    (pv) => pv.serviceId === selectedServiceId || selectedServiceId === 'all'
  );

  return (
    <div className="space-y-6">
      {/* Breadcrumb */}
      <div>
        <div className="flex items-center gap-1.5 text-xs text-slate-500 mb-2">
          <span>Tổng quan</span>
          <ChevronRight className="w-3.5 h-3.5 text-slate-400" />
          <span>Điện nước & Dịch vụ</span>
          <ChevronRight className="w-3.5 h-3.5 text-slate-400" />
          <span className="text-slate-800 font-medium">Bảng đơn giá theo kỳ</span>
        </div>

        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-bold text-slate-900 tracking-tight">
              Bảng đơn giá theo kỳ
            </h1>
            <p className="text-xs md:text-sm text-slate-500 mt-1">
              Quản lý các phiên bản giá dịch vụ có hiệu lực theo dòng thời gian và chính sách áp dụng.
            </p>
          </div>

          <div className="flex items-center gap-2.5">
            <button
              onClick={onBackToCatalog}
              className="px-3.5 py-2 border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 rounded-xl text-xs font-semibold shadow-2xs transition-colors"
            >
              Về danh mục dịch vụ
            </button>
            <button
              onClick={() => currentService && onOpenUpdateModal(currentService)}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-colors"
            >
              <Plus className="w-4 h-4" />
              <span>Cập nhật mốc giá mới</span>
            </button>
          </div>
        </div>
      </div>

      {/* Warning banner matching Image 1 background */}
      <div className="p-3.5 bg-amber-50 border border-amber-200/90 rounded-xl text-xs text-amber-900 flex items-start gap-2.5">
        <AlertTriangle className="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
        <div className="space-y-0.5">
          <p className="font-semibold text-amber-950">
            Dữ liệu mô phỏng theo kỳ — chưa đồng bộ hóa đơn cũ
          </p>
          <p className="text-amber-800 text-xs">
            Phiên bản giá mới chỉ áp dụng từ mốc thời gian bắt đầu hiệu lực; hóa đơn các tháng trước đó
            được bảo toàn đơn giá lịch sử.
          </p>
        </div>
      </div>

      {/* Service Selector Tabs */}
      <div className="flex items-center gap-2 overflow-x-auto pb-1">
        {services.map((srv) => (
          <button
            key={srv.id}
            onClick={() => setSelectedServiceId(srv.id)}
            className={`px-4 py-2 rounded-xl text-xs font-medium whitespace-nowrap transition-all ${
              selectedServiceId === srv.id
                ? 'bg-indigo-600 text-white font-semibold shadow-xs'
                : 'bg-white border border-slate-200 text-slate-700 hover:bg-slate-50'
            }`}
          >
            {srv.name}
          </button>
        ))}
      </div>

      {/* Featured Service Card Overview */}
      {currentService && (
        <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span className="font-bold text-slate-900 text-base">
                {currentService.name}
              </span>
              <span className="text-xs px-2.5 py-0.5 bg-blue-50 text-blue-700 rounded-md font-medium">
                Đơn vị tính: {currentService.unit}
              </span>
            </div>
            <p className="text-xs text-slate-500">
              Giá hiện tại:{' '}
              <strong className="text-indigo-700 font-bold text-sm">
                {currentService.rateDisplay}
              </strong>{' '}
              / {currentService.unit} (Áp dụng từ {currentService.effectiveFrom})
            </p>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => onOpenUpdateModal(currentService)}
              className="px-4 py-2 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 font-semibold rounded-xl text-xs transition-colors"
            >
              Cập nhật phiên bản giá
            </button>
          </div>
        </div>
      )}

      {/* Timeline Versions Table */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-2xs overflow-hidden">
        <div className="p-4 border-b border-slate-100 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Clock className="w-4 h-4 text-indigo-600" />
            <h3 className="font-bold text-sm text-slate-800">
              Các phiên bản giá của {currentService ? currentService.name : 'dịch vụ'}
            </h3>
          </div>
          <span className="text-xs text-slate-400 font-mono">
            {filteredVersions.length} mốc giá ghi nhận
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse text-xs">
            <thead>
              <tr className="bg-slate-50/70 border-b border-slate-200 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                <th className="px-5 py-3.5">Mã phiên bản</th>
                <th className="px-5 py-3.5">Đơn giá</th>
                <th className="px-5 py-3.5">Phạm vi áp dụng</th>
                <th className="px-5 py-3.5">Ngày bắt đầu</th>
                <th className="px-5 py-3.5">Ngày kết thúc</th>
                <th className="px-5 py-3.5 text-center">Trạng thái</th>
                <th className="px-5 py-3.5">Người tạo</th>
                <th className="px-5 py-3.5">Ghi chú</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredVersions.map((version) => (
                <tr key={version.id} className="hover:bg-slate-50/60 transition-colors">
                  <td className="px-5 py-4 font-mono font-medium text-slate-700">
                    {version.id}
                  </td>
                  <td className="px-5 py-4 font-bold text-slate-900 text-sm">
                    {version.rate.toLocaleString('vi-VN')} đ{' '}
                    <span className="text-xs font-normal text-slate-500">
                      / {version.unit}
                    </span>
                  </td>
                  <td className="px-5 py-4 text-slate-600 font-medium">
                    {version.buildingName}
                  </td>
                  <td className="px-5 py-4 font-mono text-slate-700">
                    {version.effectiveFrom}
                  </td>
                  <td className="px-5 py-4 font-mono text-slate-400">
                    {version.effectiveTo || 'Không thời hạn'}
                  </td>
                  <td className="px-5 py-4 text-center">
                    {version.status === 'active' && (
                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
                        <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                        Đang áp dụng
                      </span>
                    )}
                    {version.status === 'upcoming' && (
                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-medium bg-blue-50 text-blue-700 border border-blue-200">
                        <span className="w-1.5 h-1.5 rounded-full bg-blue-500" />
                        Sắp áp dụng
                      </span>
                    )}
                    {version.status === 'expired' && (
                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-medium bg-slate-100 text-slate-600">
                        Hết hiệu lực
                      </span>
                    )}
                  </td>
                  <td className="px-5 py-4 text-slate-600">{version.createdBy}</td>
                  <td className="px-5 py-4 text-slate-500 max-w-xs truncate">
                    {version.note || '—'}
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
