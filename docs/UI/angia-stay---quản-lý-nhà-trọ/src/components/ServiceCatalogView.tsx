import React, { useState } from 'react';
import {
  History,
  Plus,
  Search,
  RotateCcw,
  Lock,
  Eye,
  Edit2,
  Package,
  CheckCircle2,
  PauseCircle,
  ShieldCheck,
  X,
  ChevronRight,
  Info,
} from 'lucide-react';
import { ServiceItem, Building, UserRole } from '../types';
import { DEFAULT_ELECTRIC_TIERS } from '../utils/billingCalculator';

interface ServiceCatalogViewProps {
  services: ServiceItem[];
  buildings: Building[];
  currentRole: UserRole;
  onOpenUpdateModal: (service: ServiceItem) => void;
  onToggleActive: (serviceId: string) => void;
  onNavigateToHistory: () => void;
  onAddNewService: (newService: Partial<ServiceItem>) => void;
}

export const ServiceCatalogView: React.FC<ServiceCatalogViewProps> = ({
  services,
  buildings,
  currentRole,
  onOpenUpdateModal,
  onToggleActive,
  onNavigateToHistory,
  onAddNewService,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedBuilding, setSelectedBuilding] = useState<string>('b1'); // Tòa nhà An Gia 01
  const [selectedType, setSelectedType] = useState<string>('all');
  const [selectedStatus, setSelectedStatus] = useState<string>('all');
  const [tierModalOpen, setTierModalOpen] = useState(false);
  const [tierModalService, setTierModalService] = useState<string>('Điện sinh hoạt');
  const [addModalOpen, setAddModalOpen] = useState(false);

  // New service state
  const [newServiceName, setNewServiceName] = useState('');
  const [newServiceUnit, setNewServiceUnit] = useState('Phòng');
  const [newServiceRate, setNewServiceRate] = useState('50000');
  const [newServiceType, setNewServiceType] = useState<'fixed_monthly' | 'custom'>('fixed_monthly');

  // Filter logic
  const filteredServices = services.filter((srv) => {
    const matchesSearch = srv.name.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesBuilding =
      selectedBuilding === 'all' || srv.buildingId === selectedBuilding || srv.buildingId === 'all';
    const matchesType = selectedType === 'all' || srv.type === selectedType;
    const matchesStatus =
      selectedStatus === 'all' ||
      (selectedStatus === 'active' && srv.active) ||
      (selectedStatus === 'inactive' && !srv.active);
    return matchesSearch && matchesBuilding && matchesType && matchesStatus;
  });

  // Calculate stats
  const totalCount = services.length;
  const activeCount = services.filter((s) => s.active).length;
  const pausedCount = services.filter((s) => !s.active).length;
  const readOnlyCount = services.filter((s) => s.isReadOnly).length;

  const currentBuildingObj = buildings.find((b) => b.id === selectedBuilding);
  const currentBuildingName = currentBuildingObj ? currentBuildingObj.name : 'Tất cả nhà trọ';

  const handleCreateService = () => {
    if (!newServiceName.trim()) return;
    const rateNum = parseFloat(newServiceRate.replace(/\D/g, '')) || 0;
    onAddNewService({
      name: newServiceName,
      type: newServiceType,
      typeLabel: newServiceType === 'fixed_monthly' ? 'Cố định hàng tháng' : 'Tùy chọn',
      unit: newServiceUnit,
      rateType: 'flat',
      currentRate: rateNum,
      rateDisplay: `${rateNum.toLocaleString('vi-VN')} đ`,
      effectiveFrom: '01/11/2026',
      effectiveTo: null,
      active: true,
      isReadOnly: false,
      buildingId: selectedBuilding,
      buildingName: currentBuildingName,
      historyCount: 1,
    });
    setNewServiceName('');
    setAddModalOpen(false);
  };

  const resetFilters = () => {
    setSearchTerm('');
    setSelectedBuilding('b1');
    setSelectedType('all');
    setSelectedStatus('all');
  };

  return (
    <div className="space-y-6">
      {/* Breadcrumb & Subtitle */}
      <div>
        <div className="flex items-center gap-1.5 text-xs text-slate-500 mb-2">
          <span>Tổng quan</span>
          <ChevronRight className="w-3.5 h-3.5 text-slate-400" />
          <span className="text-slate-800 font-medium">Dịch vụ & Đơn giá</span>
        </div>

        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-bold text-slate-900 tracking-tight">
              Danh mục dịch vụ & đơn giá
            </h1>
            <p className="text-xs md:text-sm text-slate-500 mt-1">
              Thiết lập dịch vụ khác và xem biểu giá áp dụng để tính hóa đơn định kỳ.
            </p>
          </div>

          <div className="flex items-center gap-2.5">
            <button
              onClick={onNavigateToHistory}
              className="px-3.5 py-2 border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 rounded-xl text-xs font-semibold flex items-center gap-2 shadow-2xs transition-colors"
            >
              <History className="w-4 h-4 text-slate-500" />
              <span>Lịch sử giá</span>
            </button>
            <button
              onClick={() => setAddModalOpen(true)}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-colors"
            >
              <Plus className="w-4 h-4" />
              <span>Thêm dịch vụ mới</span>
            </button>
          </div>
        </div>
      </div>

      {/* 4 Stat Cards Grid matching Image 3 */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Card 1: Tổng dịch vụ */}
        <div className="p-4 bg-white border border-slate-200/90 rounded-2xl shadow-2xs flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Tổng dịch vụ</p>
            <div className="mt-2 flex items-baseline gap-1.5">
              <span className="text-2xl font-bold text-slate-900">{totalCount}</span>
              <span className="text-xs text-slate-500 font-normal">loại</span>
            </div>
            <p className="text-xs text-slate-400 mt-1">Toàn bộ danh mục hoạt động</p>
          </div>
          <div className="w-10 h-10 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center shrink-0">
            <Package className="w-5 h-5" />
          </div>
        </div>

        {/* Card 2: Đang kích hoạt */}
        <div className="p-4 bg-white border border-slate-200/90 rounded-2xl shadow-2xs flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Đang kích hoạt</p>
            <div className="mt-2 flex items-baseline gap-1.5">
              <span className="text-2xl font-bold text-slate-900">{activeCount}</span>
              <span className="text-xs text-slate-500 font-normal">dịch vụ</span>
            </div>
            <p className="text-xs text-slate-400 mt-1">Đang tự động áp vào hóa đơn</p>
          </div>
          <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
            <CheckCircle2 className="w-5 h-5" />
          </div>
        </div>

        {/* Card 3: Tạm ngưng */}
        <div className="p-4 bg-white border border-slate-200/90 rounded-2xl shadow-2xs flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Tạm ngưng</p>
            <div className="mt-2 flex items-baseline gap-1.5">
              <span className="text-2xl font-bold text-slate-900">{pausedCount}</span>
              <span className="text-xs text-slate-500 font-normal">dịch vụ</span>
            </div>
            <p className="text-xs text-slate-400 mt-1">Gửi xe (đang dừng tính phí)</p>
          </div>
          <div className="w-10 h-10 rounded-xl bg-slate-100 text-slate-500 flex items-center justify-center shrink-0">
            <PauseCircle className="w-5 h-5" />
          </div>
        </div>

        {/* Card 4: Biểu giá chỉ đọc */}
        <div className="p-4 bg-white border border-slate-200/90 rounded-2xl shadow-2xs flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Biểu giá chỉ đọc</p>
            <div className="mt-2 flex items-baseline gap-1.5">
              <span className="text-2xl font-bold text-slate-900">{readOnlyCount}</span>
              <span className="text-xs text-slate-500 font-normal">dịch vụ</span>
            </div>
            <p className="text-xs text-slate-400 mt-1">Điện, Nước (hệ thống cố định)</p>
          </div>
          <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
            <ShieldCheck className="w-5 h-5" />
          </div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="space-y-3">
        <div className="flex flex-col lg:flex-row items-stretch lg:items-center gap-3">
          {/* Search box */}
          <div className="relative flex-1">
            <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              placeholder="Tìm theo tên dịch vụ..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs text-slate-800 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 shadow-2xs"
            />
          </div>

          {/* Building select */}
          <select
            value={selectedBuilding}
            onChange={(e) => setSelectedBuilding(e.target.value)}
            className="px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-medium text-slate-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 shadow-2xs"
          >
            <option value="all">Tất cả nhà trọ</option>
            {buildings.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>

          {/* Type select */}
          <select
            value={selectedType}
            onChange={(e) => setSelectedType(e.target.value)}
            className="px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-medium text-slate-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 shadow-2xs"
          >
            <option value="all">Tất cả loại dịch vụ</option>
            <option value="meter">Theo chỉ số</option>
            <option value="fixed_monthly">Cố định hàng tháng</option>
          </select>

          {/* Status select */}
          <select
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
            className="px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-medium text-slate-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 shadow-2xs"
          >
            <option value="all">Tất cả trạng thái</option>
            <option value="active">Đang kích hoạt</option>
            <option value="inactive">Tạm ngưng</option>
          </select>

          {/* Reset button */}
          <button
            onClick={resetFilters}
            className="px-3.5 py-2.5 border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 rounded-xl text-xs font-medium flex items-center justify-center gap-1.5 shadow-2xs transition-colors"
          >
            <RotateCcw className="w-3.5 h-3.5 text-slate-500" />
            <span>Đặt lại</span>
          </button>
        </div>

        {/* Active Condition Tag */}
        {selectedBuilding !== 'all' && (
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <span>Điều kiện đang áp dụng:</span>
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 bg-slate-100 text-slate-700 rounded-lg text-xs font-medium">
              <span>
                Nhà trọ: <strong>{currentBuildingName}</strong>
              </span>
              <button
                onClick={() => setSelectedBuilding('all')}
                className="text-slate-400 hover:text-slate-600 rounded-full"
                aria-label="Xóa bộ lọc tòa nhà"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            </span>
          </div>
        )}
      </div>

      {/* Services Table */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-2xs overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-slate-50/70 border-b border-slate-200 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                <th className="px-5 py-3.5">Tên dịch vụ</th>
                <th className="px-5 py-3.5">Loại dịch vụ</th>
                <th className="px-5 py-3.5">Đơn vị tính</th>
                <th className="px-5 py-3.5">Đơn giá áp dụng</th>
                <th className="px-5 py-3.5">Bắt đầu hiệu lực</th>
                <th className="px-5 py-3.5">Hết hiệu lực</th>
                <th className="px-5 py-3.5 text-center">Kích hoạt</th>
                <th className="px-5 py-3.5 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 text-xs">
              {filteredServices.map((service) => (
                <tr key={service.id} className="hover:bg-slate-50/60 transition-colors">
                  {/* Tên dịch vụ */}
                  <td className="px-5 py-4">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-indigo-900 text-sm">
                        {service.name}
                      </span>
                      {service.isReadOnly && (
                        <span title="Chỉ đọc">
                          <Lock className="w-3.5 h-3.5 text-slate-400" />
                        </span>
                      )}
                    </div>
                  </td>

                  {/* Loại dịch vụ */}
                  <td className="px-5 py-4">
                    <span
                      className={`inline-block px-2.5 py-1 rounded-md font-medium text-[11px] ${
                        service.type === 'meter'
                          ? 'bg-blue-50 text-blue-700'
                          : 'bg-sky-50 text-sky-700'
                      }`}
                    >
                      {service.typeLabel}
                    </span>
                  </td>

                  {/* Đơn vị tính */}
                  <td className="px-5 py-4 text-slate-600 font-medium">
                    {service.unit}
                  </td>

                  {/* Đơn giá áp dụng */}
                  <td className="px-5 py-4">
                    {service.rateType === 'tiered' ? (
                      <button
                        onClick={() => {
                          setTierModalService(service.name);
                          setTierModalOpen(true);
                        }}
                        className="inline-flex items-center gap-1.5 text-indigo-600 hover:text-indigo-800 font-medium group"
                      >
                        <span>{service.rateDisplay}</span>
                        <Eye className="w-3.5 h-3.5 text-indigo-400 group-hover:text-indigo-600" />
                      </button>
                    ) : (
                      <span className="font-bold text-slate-900">
                        {service.rateDisplay}
                      </span>
                    )}
                  </td>

                  {/* Bắt đầu hiệu lực */}
                  <td className="px-5 py-4 text-slate-600 font-mono">
                    {service.effectiveFrom}
                  </td>

                  {/* Hết hiệu lực */}
                  <td className="px-5 py-4 text-slate-400 italic">
                    {service.effectiveTo ? (
                      <span className="font-mono text-slate-600 not-italic">
                        {service.effectiveTo}
                      </span>
                    ) : (
                      'Chưa có dữ liệu'
                    )}
                  </td>

                  {/* Kích hoạt (Toggle Switch) */}
                  <td className="px-5 py-4 text-center">
                    {service.isReadOnly ? (
                      <div className="inline-flex items-center justify-center opacity-40 cursor-not-allowed">
                        <div className="w-9 h-5 bg-slate-300 rounded-full flex items-center p-0.5">
                          <div className="w-4 h-4 bg-white rounded-full shadow-xs" />
                        </div>
                      </div>
                    ) : (
                      <button
                        type="button"
                        onClick={() => onToggleActive(service.id)}
                        className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                          service.active ? 'bg-indigo-600' : 'bg-slate-300'
                        }`}
                        role="switch"
                        aria-checked={service.active}
                      >
                        <span
                          className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-xs ring-0 transition duration-200 ease-in-out ${
                            service.active ? 'translate-x-4' : 'translate-x-0'
                          }`}
                        />
                      </button>
                    )}
                  </td>

                  {/* Thao tác */}
                  <td className="px-5 py-4 text-right">
                    <div className="flex items-center justify-end gap-2">
                      <button
                        onClick={() => {
                          if (service.rateType === 'tiered') {
                            setTierModalService(service.name);
                            setTierModalOpen(true);
                          } else {
                            onOpenUpdateModal(service);
                          }
                        }}
                        className="p-1.5 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg transition-colors"
                        title="Xem chi tiết"
                        aria-label={`Xem chi tiết ${service.name}`}
                      >
                        <Eye className="w-4 h-4" />
                      </button>

                      {service.isReadOnly ? (
                        <button
                          onClick={onNavigateToHistory}
                          className="p-1.5 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg transition-colors"
                          title="Lịch sử giá"
                          aria-label={`Lịch sử giá ${service.name}`}
                        >
                          <History className="w-4 h-4" />
                        </button>
                      ) : (
                        <button
                          onClick={() => onOpenUpdateModal(service)}
                          className="p-1.5 text-indigo-500 hover:text-indigo-700 hover:bg-indigo-50 rounded-lg transition-colors"
                          title="Chỉnh sửa đơn giá"
                          aria-label={`Chỉnh sửa đơn giá ${service.name}`}
                        >
                          <Edit2 className="w-4 h-4" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Table Footer */}
        <div className="px-5 py-3.5 bg-slate-50/70 border-t border-slate-200 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-slate-500">
          <div>
            Hiển thị <strong>1–{filteredServices.length}</strong> trong tổng số{' '}
            <strong>{filteredServices.length}</strong> dịch vụ
          </div>

          <div className="flex items-center gap-3">
            <div className="flex items-center gap-1.5">
              <span>Dòng mỗi trang:</span>
              <select className="px-2 py-1 bg-white border border-slate-200 rounded-md text-xs text-slate-700 focus:outline-none">
                <option value="10">10</option>
                <option value="20">20</option>
                <option value="50">50</option>
              </select>
            </div>

            <div className="flex items-center gap-1">
              <button
                disabled
                className="w-7 h-7 flex items-center justify-center rounded-md border border-slate-200 text-slate-300 cursor-not-allowed"
              >
                &lt;
              </button>
              <button className="w-7 h-7 flex items-center justify-center rounded-md bg-indigo-600 text-white font-semibold shadow-2xs">
                1
              </button>
              <button
                disabled
                className="w-7 h-7 flex items-center justify-center rounded-md border border-slate-200 text-slate-300 cursor-not-allowed"
              >
                &gt;
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Tier Details Modal (for Electricity / Water 3-tier view) */}
      {tierModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-fadeIn">
          <div className="w-full max-w-lg bg-white rounded-2xl shadow-2xl border border-slate-100 p-6 overflow-hidden">
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <div className="flex items-center gap-2.5">
                <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
                  <ShieldCheck className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 text-base">
                    Biểu giá bậc thang: {tierModalService}
                  </h3>
                  <p className="text-xs text-slate-500">
                    Áp dụng cố định theo quy định ban quản lý
                  </p>
                </div>
              </div>
              <button
                onClick={() => setTierModalOpen(false)}
                className="p-1 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="py-4 space-y-3">
              <div className="p-3 bg-blue-50 border border-blue-200 rounded-xl text-xs text-blue-900 flex items-start gap-2">
                <Info className="w-4 h-4 shrink-0 text-blue-600 mt-0.5" />
                <span>
                  Biểu giá này được áp dụng tự động trong mục <strong>Ghi chỉ số & Chi phí</strong> theo
                  lũy tiến tiêu thụ.
                </span>
              </div>

              <div className="border border-slate-200 rounded-xl overflow-hidden">
                <table className="w-full text-xs">
                  <thead className="bg-slate-50 border-b border-slate-200 font-semibold text-slate-600">
                    <tr>
                      <th className="px-4 py-2.5 text-left">Bậc thang</th>
                      <th className="px-4 py-2.5 text-left">Định mức</th>
                      <th className="px-4 py-2.5 text-right">Đơn giá (đ / kWh)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {DEFAULT_ELECTRIC_TIERS.map((tier) => (
                      <tr key={tier.id}>
                        <td className="px-4 py-3 font-medium text-slate-800">{tier.name}</td>
                        <td className="px-4 py-3 text-slate-600">
                          {tier.maxKwh ? `Tối đa ${tier.maxKwh} kWh` : 'Không giới hạn'}
                        </td>
                        <td className="px-4 py-3 text-right font-bold text-indigo-700">
                          {tier.rate.toLocaleString('vi-VN')} đ
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="pt-3 border-t border-slate-100 flex justify-end">
              <button
                onClick={() => setTierModalOpen(false)}
                className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl text-xs font-semibold"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Add New Service Modal */}
      {addModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-fadeIn">
          <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-100 p-6 overflow-hidden">
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <div className="flex items-center gap-2.5">
                <div className="w-10 h-10 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center">
                  <Plus className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 text-base">Thêm dịch vụ mới</h3>
                  <p className="text-xs text-slate-500">Tạo dịch vụ bổ sung tính phí định kỳ</p>
                </div>
              </div>
              <button
                onClick={() => setAddModalOpen(false)}
                className="p-1 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="py-4 space-y-3.5">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Tên dịch vụ <span className="text-rose-500">*</span>
                </label>
                <input
                  type="text"
                  placeholder="Ví dụ: Giặt sấy tự động, Thang máy..."
                  value={newServiceName}
                  onChange={(e) => setNewServiceName(e.target.value)}
                  className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Đơn vị tính
                  </label>
                  <select
                    value={newServiceUnit}
                    onChange={(e) => setNewServiceUnit(e.target.value)}
                    className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none"
                  >
                    <option value="Phòng">Phòng</option>
                    <option value="Người">Người</option>
                    <option value="Xe">Xe</option>
                    <option value="Lần">Lần</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Đơn giá (đ)
                  </label>
                  <input
                    type="number"
                    value={newServiceRate}
                    onChange={(e) => setNewServiceRate(e.target.value)}
                    className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Loại thu phí
                </label>
                <select
                  value={newServiceType}
                  onChange={(e) => setNewServiceType(e.target.value as any)}
                  className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none"
                >
                  <option value="fixed_monthly">Cố định hàng tháng</option>
                  <option value="custom">Tùy biến theo sử dụng</option>
                </select>
              </div>
            </div>

            <div className="pt-3 border-t border-slate-100 flex items-center justify-end gap-2.5">
              <button
                onClick={() => setAddModalOpen(false)}
                className="px-4 py-2 border border-slate-200 text-slate-600 rounded-xl text-xs font-semibold hover:bg-slate-50"
              >
                Hủy
              </button>
              <button
                onClick={handleCreateService}
                disabled={!newServiceName.trim()}
                className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white rounded-xl text-xs font-semibold shadow-xs"
              >
                Tạo dịch vụ
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
