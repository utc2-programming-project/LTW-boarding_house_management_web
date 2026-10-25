import React, { useState } from 'react';
import {
  Building2,
  Users,
  FileText,
  Receipt,
  BarChart3,
  Settings,
  Plus,
  Search,
  CheckCircle2,
  Clock,
  Printer,
  FileCheck,
  Zap,
  Droplet,
  ArrowUpRight,
  TrendingUp,
} from 'lucide-react';
import { Building, RoomRecord, Tenant, Invoice } from '../types';
import { formatCurrencyVND } from '../utils/billingCalculator';

/* -------------------------------------------------------
   OVERVIEW VIEW
------------------------------------------------------- */
export const OverviewView: React.FC<{
  buildings: Building[];
  rooms: RoomRecord[];
  onNavigateToServices: () => void;
  onNavigateToMeter: () => void;
}> = ({ buildings, rooms, onNavigateToServices, onNavigateToMeter }) => {
  const totalRooms = buildings.reduce((acc, b) => acc + b.totalRooms, 0);
  const occupiedRooms = buildings.reduce((acc, b) => acc + b.occupiedRooms, 0);
  const occupancyRate = Math.round((occupiedRooms / totalRooms) * 100);

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl md:text-2xl font-bold text-slate-900 tracking-tight">
            Tổng quan vận hành nhà trọ
          </h1>
          <p className="text-xs md:text-sm text-slate-500 mt-1">
            Theo dõi tỷ lệ lấp đầy, doanh thu định kỳ và trạng thái chỉ số điện nước toàn chuỗi.
          </p>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            onClick={onNavigateToMeter}
            className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-colors"
          >
            <Zap className="w-4 h-4" />
            <span>Ghi chỉ số tháng 10/2026</span>
          </button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs">
          <p className="text-xs font-medium text-slate-500">Tỷ lệ lấp đầy phòng</p>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-3xl font-black text-indigo-600">{occupancyRate}%</span>
            <span className="text-xs text-emerald-600 font-semibold flex items-center">
              <TrendingUp className="w-3.5 h-3.5 mr-0.5" /> +4.2%
            </span>
          </div>
          <p className="text-xs text-slate-400 mt-1">
            {occupiedRooms}/{totalRooms} phòng đang có khách ở
          </p>
        </div>

        <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs">
          <p className="text-xs font-medium text-slate-500">Doanh thu dự kiến tháng</p>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-2xl font-bold text-slate-900">76.450.000 đ</span>
          </div>
          <p className="text-xs text-slate-400 mt-1">Gồm tiền phòng + điện nước + dịch vụ</p>
        </div>

        <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs">
          <p className="text-xs font-medium text-slate-500">Tiêu thụ điện tháng này</p>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-2xl font-bold text-slate-900">2.480 kWh</span>
            <span className="text-xs text-slate-500">toàn chuỗi</span>
          </div>
          <p className="text-xs text-slate-400 mt-1">Đã chốt 18/20 phòng</p>
        </div>

        <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs">
          <p className="text-xs font-medium text-slate-500">Tiêu thụ nước sạch</p>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-2xl font-bold text-slate-900">124 m³</span>
            <span className="text-xs text-slate-500">toàn chuỗi</span>
          </div>
          <p className="text-xs text-slate-400 mt-1">Bình quân 6.2 m³ / phòng</p>
        </div>
      </div>

      {/* Quick shortcuts & Building Cards */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs space-y-4">
          <h3 className="font-bold text-sm text-slate-900">Danh sách cơ sở nhà trọ</h3>
          <div className="space-y-3">
            {buildings.map((b) => (
              <div
                key={b.id}
                className="p-3.5 border border-slate-100 rounded-xl bg-slate-50/60 flex items-center justify-between"
              >
                <div>
                  <h4 className="font-bold text-xs text-slate-800">{b.name}</h4>
                  <p className="text-[11px] text-slate-500 mt-0.5">{b.address}</p>
                </div>
                <div className="text-right">
                  <span className="text-xs font-bold text-indigo-700">
                    {b.occupiedRooms}/{b.totalRooms} phòng
                  </span>
                  <p className="text-[10px] text-slate-400">Đang hoạt động</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs space-y-4">
          <h3 className="font-bold text-sm text-slate-900">Truy cập nhanh dịch vụ</h3>
          <div className="grid grid-cols-2 gap-3">
            <button
              onClick={onNavigateToServices}
              className="p-4 border border-indigo-100 bg-indigo-50/50 hover:bg-indigo-50 rounded-xl text-left transition-colors"
            >
              <Zap className="w-5 h-5 text-indigo-600 mb-2" />
              <p className="font-bold text-xs text-slate-900">Danh mục đơn giá</p>
              <p className="text-[11px] text-slate-500 mt-0.5">Biểu giá điện, nước, phòng</p>
            </button>

            <button
              onClick={onNavigateToMeter}
              className="p-4 border border-blue-100 bg-blue-50/50 hover:bg-blue-50 rounded-xl text-left transition-colors"
            >
              <Droplet className="w-5 h-5 text-blue-600 mb-2" />
              <p className="font-bold text-xs text-slate-900">Chỉ số công tơ</p>
              <p className="text-[11px] text-slate-500 mt-0.5">Chốt số kWh & m³ tháng</p>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

/* -------------------------------------------------------
   BUILDINGS & ROOMS VIEW
------------------------------------------------------- */
export const BuildingsRoomsView: React.FC<{
  buildings: Building[];
  rooms: RoomRecord[];
}> = ({ buildings, rooms }) => {
  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl md:text-2xl font-bold text-slate-900 tracking-tight">
            Tòa nhà & Phòng trọ
          </h1>
          <p className="text-xs md:text-sm text-slate-500 mt-1">
            Quản lý sơ đồ phòng, giá thuê và tình trạng phòng trống.
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {rooms.map((room) => (
          <div
            key={room.id}
            className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs space-y-3"
          >
            <div className="flex items-center justify-between">
              <span className="font-black text-lg text-indigo-700">{room.roomCode}</span>
              <span className="px-2.5 py-0.5 bg-emerald-50 text-emerald-700 border border-emerald-200 rounded-full text-xs font-semibold">
                Đang thuê
              </span>
            </div>
            <div className="text-xs space-y-1 text-slate-600">
              <p>
                Khách thuê: <strong>{room.tenantName}</strong> ({room.tenantPhone})
              </p>
              <p>
                Cơ sở: <strong>{room.buildingName}</strong>
              </p>
              <p>
                Giá thuê: <strong>{formatCurrencyVND(room.roomRent)} / tháng</strong>
              </p>
              <p className="font-mono text-[11px] text-slate-400">
                Đồng hồ: {room.electricMeterCode} | {room.waterMeterCode}
              </p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

/* -------------------------------------------------------
   TENANTS VIEW
------------------------------------------------------- */
export const TenantsView: React.FC<{ tenants: Tenant[] }> = ({ tenants }) => {
  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl md:text-2xl font-bold text-slate-900 tracking-tight">
            Danh bạ khách thuê
          </h1>
          <p className="text-xs md:text-sm text-slate-500 mt-1">
            Thông tin liên hệ, căn cước công dân và hợp đồng thuê phòng.
          </p>
        </div>
      </div>

      <div className="bg-white border border-slate-200 rounded-2xl shadow-2xs overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse text-xs">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200 font-semibold text-slate-600">
                <th className="px-5 py-3.5">Họ và tên</th>
                <th className="px-5 py-3.5">Phòng / Cơ sở</th>
                <th className="px-5 py-3.5">Số điện thoại</th>
                <th className="px-5 py-3.5">Mã hợp đồng</th>
                <th className="px-5 py-3.5">Tiền cọc</th>
                <th className="px-5 py-3.5">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {tenants.map((t) => (
                <tr key={t.id} className="hover:bg-slate-50/60">
                  <td className="px-5 py-4 font-bold text-slate-900">{t.name}</td>
                  <td className="px-5 py-4 text-slate-700">
                    <span className="font-semibold text-indigo-700">{t.roomCode}</span> -{' '}
                    {t.buildingName}
                  </td>
                  <td className="px-5 py-4 font-mono text-slate-600">{t.phone}</td>
                  <td className="px-5 py-4 font-mono text-slate-500">{t.contractCode}</td>
                  <td className="px-5 py-4 font-semibold text-slate-800">
                    {formatCurrencyVND(t.depositAmount)}
                  </td>
                  <td className="px-5 py-4">
                    <span className="px-2.5 py-1 bg-emerald-50 text-emerald-700 rounded-full font-medium text-[11px]">
                      Đang ở
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

/* -------------------------------------------------------
   INVOICES VIEW
------------------------------------------------------- */
export const InvoicesView: React.FC<{ invoices: Invoice[] }> = ({ invoices }) => {
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl md:text-2xl font-bold text-slate-900 tracking-tight">
            Hóa đơn & Thu chi
          </h1>
          <p className="text-xs md:text-sm text-slate-500 mt-1">
            Quản lý phiếu thanh toán tiền phòng, điện nước và tiện ích hàng tháng.
          </p>
        </div>
      </div>

      <div className="bg-white border border-slate-200 rounded-2xl shadow-2xs overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse text-xs">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200 font-semibold text-slate-600">
                <th className="px-5 py-3.5">Mã hóa đơn</th>
                <th className="px-5 py-3.5">Phòng / Khách</th>
                <th className="px-5 py-3.5">Kỳ thanh toán</th>
                <th className="px-5 py-3.5">Tiền phòng</th>
                <th className="px-5 py-3.5">Điện + Nước</th>
                <th className="px-5 py-3.5">Tổng cộng</th>
                <th className="px-5 py-3.5">Trạng thái</th>
                <th className="px-5 py-3.5 text-right">Chi tiết</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {invoices.map((inv) => (
                <tr key={inv.id} className="hover:bg-slate-50/60">
                  <td className="px-5 py-4 font-mono font-bold text-slate-800">{inv.code}</td>
                  <td className="px-5 py-4 text-slate-700">
                    <strong>{inv.roomCode}</strong> - {inv.tenantName}
                  </td>
                  <td className="px-5 py-4 text-slate-600">{inv.period}</td>
                  <td className="px-5 py-4 font-mono">{formatCurrencyVND(inv.roomRent)}</td>
                  <td className="px-5 py-4 font-mono text-slate-600">
                    {formatCurrencyVND(inv.electricAmount + inv.waterAmount)}
                  </td>
                  <td className="px-5 py-4 font-bold text-indigo-700 text-sm">
                    {formatCurrencyVND(inv.totalAmount)}
                  </td>
                  <td className="px-5 py-4">
                    {inv.status === 'paid' ? (
                      <span className="px-2.5 py-1 bg-emerald-50 text-emerald-700 border border-emerald-200 rounded-full font-medium text-[11px]">
                        Đã thanh toán
                      </span>
                    ) : (
                      <span className="px-2.5 py-1 bg-amber-50 text-amber-700 border border-amber-200 rounded-full font-medium text-[11px]">
                        Chờ thanh toán
                      </span>
                    )}
                  </td>
                  <td className="px-5 py-4 text-right">
                    <button
                      onClick={() => setSelectedInvoice(inv)}
                      className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 rounded-lg text-slate-700 font-semibold"
                    >
                      Xem phiếu
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Invoice Detail Printable Preview Modal */}
      {selectedInvoice && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-fadeIn">
          <div className="w-full max-w-lg bg-white rounded-2xl shadow-2xl border border-slate-100 p-6 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2">
                <Receipt className="w-5 h-5 text-indigo-600" />
                <h3 className="font-bold text-base text-slate-900">
                  Phiếu thu tiền phòng & dịch vụ
                </h3>
              </div>
              <button
                onClick={() => setSelectedInvoice(null)}
                className="text-slate-400 hover:text-slate-600"
              >
                ✕
              </button>
            </div>

            <div className="p-4 bg-slate-50 rounded-xl space-y-2 text-xs">
              <div className="flex justify-between">
                <span className="text-slate-500">Mã hóa đơn:</span>
                <span className="font-mono font-bold text-slate-800">{selectedInvoice.code}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Phòng:</span>
                <span className="font-bold text-slate-800">{selectedInvoice.roomCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Khách thuê:</span>
                <span className="font-bold text-slate-800">{selectedInvoice.tenantName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Kỳ thanh toán:</span>
                <span className="text-slate-800">{selectedInvoice.period}</span>
              </div>
            </div>

            <div className="border border-slate-200 rounded-xl p-3 text-xs space-y-2">
              <div className="flex justify-between py-1 border-b border-slate-100">
                <span>Tiền thuê phòng</span>
                <span className="font-semibold">{formatCurrencyVND(selectedInvoice.roomRent)}</span>
              </div>
              <div className="flex justify-between py-1 border-b border-slate-100">
                <span>Tiền điện sinh hoạt</span>
                <span className="font-semibold">
                  {formatCurrencyVND(selectedInvoice.electricAmount)}
                </span>
              </div>
              <div className="flex justify-between py-1 border-b border-slate-100">
                <span>Tiền nước sinh hoạt</span>
                <span className="font-semibold">
                  {formatCurrencyVND(selectedInvoice.waterAmount)}
                </span>
              </div>
              <div className="flex justify-between py-1 border-b border-slate-100">
                <span>Dịch vụ chung (mạng, vệ sinh)</span>
                <span className="font-semibold">
                  {formatCurrencyVND(selectedInvoice.serviceAmount)}
                </span>
              </div>
              <div className="flex justify-between py-2 text-sm font-bold text-indigo-700">
                <span>TỔNG TIỀN PHẢI THU</span>
                <span>{formatCurrencyVND(selectedInvoice.totalAmount)}</span>
              </div>
            </div>

            <div className="pt-2 flex justify-end gap-2">
              <button
                onClick={() => setSelectedInvoice(null)}
                className="px-4 py-2 border border-slate-200 text-slate-700 rounded-xl text-xs font-semibold hover:bg-slate-50"
              >
                Đóng
              </button>
              <button
                onClick={() => {
                  window.print();
                }}
                className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 shadow-xs"
              >
                <Printer className="w-4 h-4" />
                <span>In phiếu thu</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
