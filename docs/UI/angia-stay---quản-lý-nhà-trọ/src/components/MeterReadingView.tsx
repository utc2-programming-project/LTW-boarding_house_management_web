import React, { useState } from 'react';
import {
  ChevronRight,
  Zap,
  Droplet,
  Home,
  Wifi,
  Lock,
  Save,
  RotateCcw,
  AlertTriangle,
  X,
  History,
  CheckCircle2,
  Calendar,
  Sparkles,
} from 'lucide-react';
import { RoomRecord, UserRole } from '../types';
import {
  calculateElectricityTiered,
  calculateWaterCost,
  formatCurrencyVND,
  validateMeterReading,
} from '../utils/billingCalculator';

interface MeterReadingViewProps {
  rooms: RoomRecord[];
  currentRole: UserRole;
  onSaveReading: (updatedRoom: RoomRecord, logReason?: string) => void;
  onCancel: () => void;
  onNotify: (msg: string) => void;
}

export const MeterReadingView: React.FC<MeterReadingViewProps> = ({
  rooms,
  currentRole,
  onSaveReading,
  onCancel,
  onNotify,
}) => {
  const [selectedRoomId, setSelectedRoomId] = useState<string>(rooms[0]?.id || 'room-101');
  const [showYellowBanner, setShowYellowBanner] = useState(true);

  // Active room data
  const activeRoom = rooms.find((r) => r.id === selectedRoomId) || rooms[0];

  // Editable form state initialized with active room values
  const [electricEndInput, setElectricEndInput] = useState<string>(
    activeRoom.electricEnd.toString()
  );
  const [waterEndInput, setWaterEndInput] = useState<string>(
    activeRoom.waterEnd.toString()
  );
  const [periodStart, setPeriodStart] = useState<string>('2026-10-01');
  const [periodEnd, setPeriodEnd] = useState<string>('2026-10-31');
  const [editReasonModal, setEditReasonModal] = useState(false);
  const [editReason, setEditReason] = useState('Ghi nhầm số đồng hồ thực tế kiểm tra lại');

  // Handle room switch
  const handleRoomChange = (roomId: string) => {
    setSelectedRoomId(roomId);
    const targetRoom = rooms.find((r) => r.id === roomId);
    if (targetRoom) {
      setElectricEndInput(targetRoom.electricEnd.toString());
      setWaterEndInput(targetRoom.waterEnd.toString());
    }
  };

  // Parse numeric values
  const numElectricEnd = parseFloat(electricEndInput) || 0;
  const numWaterEnd = parseFloat(waterEndInput) || 0;

  // Validate readings
  const electricValidation = validateMeterReading(activeRoom.electricStart, numElectricEnd);
  const waterValidation = validateMeterReading(activeRoom.waterStart, numWaterEnd);

  // Calculated usage
  const electricUsage = electricValidation.valid ? electricValidation.usage : 0;
  const waterUsage = waterValidation.valid ? waterValidation.usage : 0;

  // Real-time calculations
  const electricCalc = calculateElectricityTiered(electricUsage);
  const waterCalc = calculateWaterCost(waterUsage, activeRoom.waterRate);

  const roomRent = activeRoom.roomRent;
  const generalServicesCost = activeRoom.internetCost + activeRoom.cleaningCost + activeRoom.parkingCost;
  const totalCost = roomRent + electricCalc.total + waterCalc.total + generalServicesCost;

  const handleSaveClick = () => {
    if (!electricValidation.valid || !waterValidation.valid) {
      onNotify('Chỉ số cuối kỳ không hợp lệ. Vui lòng kiểm tra lại!');
      return;
    }

    // Check if values changed from original
    const hasChanged =
      numElectricEnd !== activeRoom.electricEnd || numWaterEnd !== activeRoom.waterEnd;

    if (hasChanged) {
      setEditReasonModal(true);
    } else {
      performSave();
    }
  };

  const performSave = (reason?: string) => {
    const updated: RoomRecord = {
      ...activeRoom,
      electricEnd: numElectricEnd,
      waterEnd: numWaterEnd,
    };
    onSaveReading(updated, reason);
    setEditReasonModal(false);
  };

  const isTenantRole = currentRole === 'tenant';

  return (
    <div className="space-y-6">
      {/* Top Breadcrumb & Phase Tag */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
        <div className="flex items-center gap-1.5 text-xs text-slate-500">
          <span>Tổng quan</span>
          <ChevronRight className="w-3.5 h-3.5 text-slate-400" />
          <span>Điện nước</span>
          <ChevronRight className="w-3.5 h-3.5 text-slate-400" />
          <span className="text-slate-800 font-medium">Kỳ & chỉ số</span>
        </div>

        <div className="flex items-center gap-2">
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
            Phiên bản v2.4 (Phase B / VHT-38)
          </span>
        </div>
      </div>

      {/* Yellow Simulation Warning Banner matching Image 5 */}
      {showYellowBanner && (
        <div className="p-3.5 bg-amber-50/90 border border-amber-200/90 rounded-2xl text-xs text-amber-900 flex items-start justify-between gap-3 shadow-2xs">
          <div className="flex items-start gap-2.5">
            <AlertTriangle className="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
            <div className="space-y-1">
              <div className="flex items-center gap-2">
                <span className="font-bold text-amber-950">
                  Dữ liệu mẫu — chưa lưu SQL (bản thử nghiệm giao diện)
                </span>
                <span className="px-2 py-0.5 bg-amber-100 text-amber-800 rounded font-medium text-[11px]">
                  Chế độ xem trước
                </span>
              </div>
              <p className="text-amber-800 leading-relaxed">
                Thao tác dùng dữ liệu giả lập, chỉ nằm trong phiên giao diện hiện tại; chưa có yêu cầu nào
                được gửi hoặc lưu vào cơ sở dữ liệu.
              </p>
            </div>
          </div>
          <button
            onClick={() => setShowYellowBanner(false)}
            className="text-amber-600 hover:text-amber-800 p-1 rounded-lg"
            aria-label="Đóng thông báo"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Title & Action Buttons */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl md:text-2xl font-bold text-slate-900 tracking-tight">
            Ghi chỉ số & Chi phí mẫu
          </h1>
          <p className="text-xs md:text-sm text-slate-500 mt-1">
            Chốt chỉ số điện nước theo kỳ và tính trước chi phí dịch vụ cho từng phòng.
          </p>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            onClick={onCancel}
            className="px-4 py-2 border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 rounded-xl text-xs font-semibold shadow-2xs flex items-center gap-1.5 transition-colors"
          >
            <X className="w-4 h-4 text-slate-500" />
            <span>Hủy</span>
          </button>
          <button
            onClick={handleSaveClick}
            disabled={isTenantRole}
            className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-colors"
          >
            <Save className="w-4 h-4" />
            <span>Lưu nhập chỉ số</span>
          </button>
        </div>
      </div>

      {/* SECTION 1: Chọn kỳ & Thông tin phòng */}
      <div className="bg-white border border-slate-200 rounded-2xl p-5 shadow-2xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-2.5">
            <span className="w-2.5 h-2.5 rounded-full bg-indigo-600" />
            <h2 className="font-bold text-sm text-slate-800">
              1. Chọn kỳ & Thông tin phòng
            </h2>
          </div>
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold bg-blue-50 text-indigo-700 border border-blue-200">
            <span className="w-1.5 h-1.5 rounded-full bg-indigo-600 animate-pulse" />
            Mở (Đang ghi chỉ số)
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          {/* Hợp đồng / Phòng */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              Hợp đồng / Phòng <span className="text-rose-500">*</span>
            </label>
            <select
              value={selectedRoomId}
              onChange={(e) => handleRoomChange(e.target.value)}
              className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-all shadow-2xs"
            >
              {rooms.map((room) => (
                <option key={room.id} value={room.id}>
                  {room.roomName}
                </option>
              ))}
            </select>
            <p className="mt-1 text-[11px] text-slate-400">
              Áp dụng cho kỳ tính hóa đơn {activeRoom.periodMonth}
            </p>
          </div>

          {/* Kỳ tính phí (từ - đến) */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              Kỳ tính phí (từ – đến) <span className="text-rose-500">*</span>
            </label>
            <div className="grid grid-cols-2 gap-2">
              <div className="relative">
                <input
                  type="date"
                  value={periodStart}
                  onChange={(e) => setPeriodStart(e.target.value)}
                  className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-medium text-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white"
                />
              </div>
              <div className="relative">
                <input
                  type="date"
                  value={periodEnd}
                  onChange={(e) => setPeriodEnd(e.target.value)}
                  className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-medium text-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white"
                />
              </div>
            </div>
            <p className="mt-1 text-[11px] text-slate-400">
              Chu kỳ trọn tháng (31 ngày sử dụng dịch vụ)
            </p>
          </div>
        </div>
      </div>

      {/* SECTION 2: Chỉ số công tơ Điện & Nước */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <span className="w-2.5 h-2.5 rounded-full bg-indigo-600" />
            <h2 className="font-bold text-sm text-slate-800">
              2. Chỉ số công tơ Điện & Nước
            </h2>
          </div>
          <span className="inline-flex items-center gap-1.5 text-xs text-indigo-600 font-semibold">
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Tự động tính chênh lệch</span>
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          {/* Electricity Meter Card */}
          <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs space-y-4">
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center shrink-0">
                  <Zap className="w-5 h-5 fill-amber-500" />
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 text-sm">
                    Công tơ Điện sinh hoạt
                  </h3>
                  <p className="text-xs text-slate-400">
                    Mã đồng hồ: {activeRoom.electricMeterCode}
                  </p>
                </div>
              </div>
              <span className="px-2.5 py-1 bg-slate-100 text-slate-600 rounded-lg text-xs font-medium">
                Đơn vị: kWh
              </span>
            </div>

            <div className="grid grid-cols-2 gap-4 pt-1">
              <div>
                <p className="text-xs text-slate-500 mb-1">Chỉ số đầu kỳ</p>
                <div className="p-2.5 bg-slate-50 border border-slate-200 rounded-xl font-bold text-base text-slate-800 flex items-baseline justify-between">
                  <span>{activeRoom.electricStart.toLocaleString('vi-VN')}</span>
                  <span className="text-xs text-slate-400 font-normal">kWh</span>
                </div>
                <p className="mt-1 text-[11px] text-slate-400">Cuối kỳ 09/2026</p>
              </div>

              <div>
                <p className="text-xs text-slate-700 font-semibold mb-1">
                  Chỉ số cuối kỳ <span className="text-rose-500">*</span>
                </p>
                <div className="relative flex items-center">
                  <input
                    type="number"
                    value={electricEndInput}
                    onChange={(e) => setElectricEndInput(e.target.value)}
                    disabled={isTenantRole}
                    className="w-full p-2.5 pr-12 bg-white border border-slate-300 rounded-xl font-bold text-base text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 shadow-2xs"
                  />
                  <span className="absolute right-3 text-xs text-slate-400 font-normal pointer-events-none">
                    kWh
                  </span>
                </div>
                <p className="mt-1 text-[11px] text-slate-400">Nhập tay công tơ</p>
              </div>
            </div>

            {!electricValidation.valid && (
              <p className="text-xs text-rose-600 font-medium bg-rose-50 p-2 rounded-lg">
                {electricValidation.errorMessage}
              </p>
            )}

            {/* Calculated Usage Banner */}
            <div className="p-3 bg-blue-50/70 border border-blue-100 rounded-xl flex items-center justify-between text-xs">
              <span className="text-slate-600 font-medium">Tiêu thụ tính toán:</span>
              <span className="text-base font-bold text-indigo-700">
                {electricUsage} <span className="text-xs font-medium">kWh</span>
              </span>
            </div>
          </div>

          {/* Water Meter Card */}
          <div className="p-5 bg-white border border-slate-200 rounded-2xl shadow-2xs space-y-4">
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
                  <Droplet className="w-5 h-5 fill-blue-500" />
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 text-sm">Đồng hồ Nước sạch</h3>
                  <p className="text-xs text-slate-400">
                    Mã đồng hồ: {activeRoom.waterMeterCode}
                  </p>
                </div>
              </div>
              <span className="px-2.5 py-1 bg-slate-100 text-slate-600 rounded-lg text-xs font-medium">
                Đơn vị: m³
              </span>
            </div>

            <div className="grid grid-cols-2 gap-4 pt-1">
              <div>
                <p className="text-xs text-slate-500 mb-1">Chỉ số đầu kỳ</p>
                <div className="p-2.5 bg-slate-50 border border-slate-200 rounded-xl font-bold text-base text-slate-800 flex items-baseline justify-between">
                  <span>{activeRoom.waterStart.toLocaleString('vi-VN')}</span>
                  <span className="text-xs text-slate-400 font-normal">m³</span>
                </div>
                <p className="mt-1 text-[11px] text-slate-400">Cuối kỳ 09/2026</p>
              </div>

              <div>
                <p className="text-xs text-slate-700 font-semibold mb-1">
                  Chỉ số cuối kỳ <span className="text-rose-500">*</span>
                </p>
                <div className="relative flex items-center">
                  <input
                    type="number"
                    value={waterEndInput}
                    onChange={(e) => setWaterEndInput(e.target.value)}
                    disabled={isTenantRole}
                    className="w-full p-2.5 pr-12 bg-white border border-slate-300 rounded-xl font-bold text-base text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 shadow-2xs"
                  />
                  <span className="absolute right-3 text-xs text-slate-400 font-normal pointer-events-none">
                    m³
                  </span>
                </div>
                <p className="mt-1 text-[11px] text-slate-400">Nhập tay công tơ</p>
              </div>
            </div>

            {!waterValidation.valid && (
              <p className="text-xs text-rose-600 font-medium bg-rose-50 p-2 rounded-lg">
                {waterValidation.errorMessage}
              </p>
            )}

            {/* Calculated Usage Banner */}
            <div className="p-3 bg-blue-50/70 border border-blue-100 rounded-xl flex items-center justify-between text-xs">
              <span className="text-slate-600 font-medium">Tiêu thụ tính toán:</span>
              <span className="text-base font-bold text-indigo-700">
                {waterUsage} <span className="text-xs font-medium">m³</span>
              </span>
            </div>
          </div>
        </div>

        {/* Lock note alert */}
        <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl flex items-center gap-2 text-xs text-slate-600">
          <Lock className="w-4 h-4 text-slate-400 shrink-0" />
          <span>Biểu giá điện/nước áp dụng hiệu lực từ 01/01/2026 (Biểu giá quy định cố định).</span>
        </div>
      </div>

      {/* SECTION 3: Bảng tính chi phí xem trước (Dự thảo) */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-2xs overflow-hidden">
        <div className="p-5 border-b border-slate-100 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="w-2.5 h-2.5 rounded-full bg-indigo-600" />
            <h2 className="font-bold text-sm text-slate-800">
              Bảng tính chi phí xem trước (Dự thảo)
            </h2>
          </div>
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-semibold bg-indigo-50 text-indigo-700">
            <Sparkles className="w-3.5 h-3.5" />
            Tự động tính
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse text-xs">
            <thead>
              <tr className="bg-slate-50/70 border-b border-slate-200 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                <th className="px-5 py-3.5">Loại chi phí</th>
                <th className="px-5 py-3.5">Diễn giải công thức</th>
                <th className="px-5 py-3.5">Số lượng</th>
                <th className="px-5 py-3.5">Đơn giá</th>
                <th className="px-5 py-3.5 text-right">Thành tiền</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 font-medium">
              {/* Row 1: Tiền thuê phòng */}
              <tr className="hover:bg-slate-50/60 transition-colors">
                <td className="px-5 py-3.5">
                  <div className="flex items-center gap-2.5">
                    <Home className="w-4 h-4 text-slate-400" />
                    <span className="font-semibold text-slate-800">Tiền thuê phòng</span>
                  </div>
                </td>
                <td className="px-5 py-3.5 text-slate-500">
                  Giá thuê cố định theo hợp đồng
                </td>
                <td className="px-5 py-3.5 text-slate-700">1 tháng</td>
                <td className="px-5 py-3.5 text-slate-700">
                  {roomRent.toLocaleString('vi-VN')} đ
                </td>
                <td className="px-5 py-3.5 text-right font-bold text-slate-900">
                  {roomRent.toLocaleString('vi-VN')} đ
                </td>
              </tr>

              {/* Row 2: Tiền điện sinh hoạt */}
              <tr className="hover:bg-slate-50/60 transition-colors">
                <td className="px-5 py-3.5">
                  <div className="flex items-center gap-2.5">
                    <Zap className="w-4 h-4 text-amber-500" />
                    <span className="font-semibold text-slate-800">Tiền điện sinh hoạt</span>
                  </div>
                </td>
                <td className="px-5 py-3.5 text-slate-500 font-mono text-[11px]">
                  {electricCalc.breakdown}
                </td>
                <td className="px-5 py-3.5 text-slate-700">{electricUsage} kWh</td>
                <td className="px-5 py-3.5 text-slate-500">Tính theo bậc</td>
                <td className="px-5 py-3.5 text-right font-bold text-slate-900">
                  {electricCalc.total.toLocaleString('vi-VN')} đ
                </td>
              </tr>

              {/* Row 3: Tiền nước sinh hoạt */}
              <tr className="hover:bg-slate-50/60 transition-colors">
                <td className="px-5 py-3.5">
                  <div className="flex items-center gap-2.5">
                    <Droplet className="w-4 h-4 text-blue-500" />
                    <span className="font-semibold text-slate-800">Tiền nước sinh hoạt</span>
                  </div>
                </td>
                <td className="px-5 py-3.5 text-slate-500 font-mono text-[11px]">
                  {waterCalc.breakdown}
                </td>
                <td className="px-5 py-3.5 text-slate-700">{waterUsage} m³</td>
                <td className="px-5 py-3.5 text-slate-700">
                  {activeRoom.waterRate.toLocaleString('vi-VN')} đ
                </td>
                <td className="px-5 py-3.5 text-right font-bold text-slate-900">
                  {waterCalc.total.toLocaleString('vi-VN')} đ
                </td>
              </tr>

              {/* Row 4: Dịch vụ chung */}
              <tr className="hover:bg-slate-50/60 transition-colors">
                <td className="px-5 py-3.5">
                  <div className="flex items-center gap-2.5">
                    <Wifi className="w-4 h-4 text-indigo-500" />
                    <span className="font-semibold text-slate-800">Dịch vụ chung</span>
                  </div>
                </td>
                <td className="px-5 py-3.5 text-slate-500">
                  Internet ({activeRoom.internetCost.toLocaleString('vi-VN')} đ/phòng) + Vệ sinh (
                  {activeRoom.cleaningCost.toLocaleString('vi-VN')} đ/người)
                </td>
                <td className="px-5 py-3.5 text-slate-700">2 khoản</td>
                <td className="px-5 py-3.5 text-slate-500">Theo quy định</td>
                <td className="px-5 py-3.5 text-right font-bold text-slate-900">
                  {generalServicesCost.toLocaleString('vi-VN')} đ
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* Total Cost Card matching Image 5 */}
        <div className="p-6 bg-slate-50/90 border-t border-slate-200 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="space-y-1">
            <h3 className="font-bold text-sm text-slate-900">
              Tổng chi phí kỳ (chưa bao gồm tiền cọc)
            </h3>
            <p className="text-xs text-slate-500">
              Bao gồm 01 tháng tiền phòng và các tiện ích điện, nước, vệ sinh, mạng
            </p>
          </div>

          <div className="text-right">
            <span className="text-2xl md:text-3xl font-black text-indigo-600 tracking-tight">
              {formatCurrencyVND(totalCost)}
            </span>
          </div>
        </div>

        {/* Reference Code Footer */}
        <div className="px-6 py-2.5 bg-slate-100/60 border-t border-slate-200/80 flex flex-col sm:flex-row items-center justify-between text-[11px] text-slate-500">
          <span>Số minh họa, không phải biểu giá thật; tiền cọc không tính vào tổng kỳ.</span>
          <span className="font-mono text-slate-600">
            Mã tham chiếu: <strong>{activeRoom.refCode}</strong>
          </span>
        </div>
      </div>

      {/* SECTION 4: Lịch sử điều chỉnh chỉ số (Audit Log) matching Image 5 */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-2xs overflow-hidden">
        <div className="p-4 px-5 border-b border-slate-100 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <History className="w-4 h-4 text-indigo-600" />
            <h3 className="font-bold text-sm text-slate-800">
              Lịch sử điều chỉnh chỉ số (Audit Log - Chỉ Chủ trọ / Quản lý xem)
            </h3>
          </div>
          <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-100 text-slate-700">
            {activeRoom.auditLogs.length} lượt sửa
          </span>
        </div>

        {activeRoom.auditLogs.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-slate-50/70 border-b border-slate-200 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                  <th className="px-5 py-3">Người sửa</th>
                  <th className="px-5 py-3">Thời gian</th>
                  <th className="px-5 py-3">Trường thay đổi</th>
                  <th className="px-5 py-3">Chi tiết thay đổi</th>
                  <th className="px-5 py-3">Lý do điều chỉnh</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {activeRoom.auditLogs.map((log) => (
                  <tr key={log.id} className="hover:bg-slate-50/60 transition-colors">
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <div className="w-6 h-6 rounded-full bg-indigo-600 text-white font-bold text-[10px] flex items-center justify-center">
                          A
                        </div>
                        <span className="font-semibold text-slate-800">{log.modifier}</span>
                      </div>
                    </td>
                    <td className="px-5 py-3.5 text-slate-600 font-mono">{log.timestamp}</td>
                    <td className="px-5 py-3.5">
                      <code className="px-2 py-0.5 bg-slate-100 text-slate-800 rounded font-mono text-[11px]">
                        {log.fieldChanged}
                      </code>
                    </td>
                    <td className="px-5 py-3.5 font-mono text-slate-700 font-medium">
                      {log.detailDiff}
                    </td>
                    <td className="px-5 py-3.5 text-slate-600 italic">{log.reason}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="p-6 text-center text-xs text-slate-400">
            Chưa có lượt sửa đổi thủ công nào cho phòng này trong kỳ hiện tại.
          </div>
        )}
      </div>

      {/* SECTION 5: Quy tắc bảo vệ kỳ kế toán đã khóa banner matching Image 5 */}
      <div className="p-4 bg-slate-50 border border-slate-200 rounded-2xl flex items-start gap-3">
        <div className="p-2 bg-white border border-slate-200 rounded-xl text-slate-600 shrink-0">
          <Lock className="w-5 h-5 text-indigo-600" />
        </div>
        <div className="space-y-1 text-xs">
          <h4 className="font-bold text-slate-900">
            Quy tắc bảo vệ kỳ kế toán đã khóa:
          </h4>
          <p className="text-slate-600 leading-relaxed">
            Kỳ đã phát hành hóa đơn, không thể sửa chỉ số. Khi cần điều chỉnh, vui lòng thực hiện hủy phát
            hành hóa đơn hoặc tạo phiếu bù trừ trong mục Hóa đơn & Thu chi.
          </p>
        </div>
      </div>

      {/* Edit Reason Modal */}
      {editReasonModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-fadeIn">
          <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-100 p-6 overflow-hidden">
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <h3 className="font-bold text-slate-900 text-base">
                Xác nhận lưu chỉ số công tơ
              </h3>
              <button
                onClick={() => setEditReasonModal(false)}
                className="p-1 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="py-4 space-y-3">
              <p className="text-xs text-slate-600">
                Bạn đã thay đổi chỉ số công tơ cho{' '}
                <strong>{activeRoom.roomCode}</strong>. Vui lòng nhập lý do điều chỉnh để ghi nhận vào
                hồ sơ kiểm toán (Audit Log):
              </p>
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Lý do điều chỉnh
                </label>
                <input
                  type="text"
                  value={editReason}
                  onChange={(e) => setEditReason(e.target.value)}
                  className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>
            </div>

            <div className="pt-3 border-t border-slate-100 flex items-center justify-end gap-2.5">
              <button
                onClick={() => setEditReasonModal(false)}
                className="px-4 py-2 border border-slate-200 text-slate-600 rounded-xl text-xs font-semibold hover:bg-slate-50"
              >
                Hủy
              </button>
              <button
                onClick={() => performSave(editReason)}
                className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-semibold shadow-xs"
              >
                Xác nhận lưu
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
