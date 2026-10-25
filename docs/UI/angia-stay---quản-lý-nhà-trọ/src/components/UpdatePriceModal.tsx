import React, { useState } from 'react';
import {
  X,
  Info,
  Calendar,
  AlertCircle,
  RefreshCw,
  Receipt,
  Check,
} from 'lucide-react';
import { ServiceItem } from '../types';
import { validatePriceInput } from '../utils/billingCalculator';

interface UpdatePriceModalProps {
  isOpen: boolean;
  service: ServiceItem | null;
  onClose: () => void;
  onSavePriceVersion: (updatedData: {
    serviceId: string;
    newRate: number;
    unit: string;
    buildingScope: string;
    effectiveFrom: string;
    effectiveTo: string;
    autoActivate: boolean;
  }) => void;
}

export const UpdatePriceModal: React.FC<UpdatePriceModalProps> = ({
  isOpen,
  service,
  onClose,
  onSavePriceVersion,
}) => {
  if (!isOpen || !service) return null;

  const [serviceName, setServiceName] = useState(service.name);
  const [serviceType, setServiceType] = useState(service.typeLabel);
  const [unit, setUnit] = useState(service.unit);
  const [buildingScope, setBuildingScope] = useState('Tất cả nhà trọ');
  const [rateString, setRateString] = useState('110000');
  const [effectiveFrom, setEffectiveFrom] = useState('2026-12-01');
  const [effectiveTo, setEffectiveTo] = useState('');
  const [autoActivate, setAutoActivate] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const numericRate = parseFloat(rateString.replace(/\D/g, '')) || 0;

  const handleRateChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const rawVal = e.target.value.replace(/\D/g, '');
    setRateString(rawVal);
    const parsed = parseFloat(rawVal) || 0;
    const validation = validatePriceInput(parsed);
    if (!validation.valid) {
      setErrorMessage(validation.error || 'Đơn giá không hợp lệ');
    } else {
      setErrorMessage(null);
    }
  };

  const handleSave = () => {
    const validation = validatePriceInput(numericRate);
    if (!validation.valid) {
      setErrorMessage(validation.error || 'Đơn giá phải lớn hơn 0.');
      return;
    }

    setIsSaving(true);
    setTimeout(() => {
      onSavePriceVersion({
        serviceId: service.id,
        newRate: numericRate,
        unit,
        buildingScope,
        effectiveFrom: effectiveFrom || '01/12/2026',
        effectiveTo,
        autoActivate,
      });
      setIsSaving(false);
      onClose();
    }, 450);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-fadeIn">
      <div
        className="w-full max-w-xl bg-white rounded-2xl shadow-2xl border border-slate-100 overflow-hidden transform transition-all"
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-headline"
      >
        {/* Modal Header */}
        <div className="flex items-start justify-between p-6 pb-4 border-b border-slate-100">
          <div className="flex items-center gap-3.5">
            <div className="w-11 h-11 rounded-xl bg-indigo-50 border border-indigo-100 text-indigo-600 flex items-center justify-center shrink-0">
              <Receipt className="w-6 h-6" />
            </div>
            <div>
              <h2 id="modal-headline" className="text-lg font-bold text-slate-900 leading-snug">
                Cập nhật đơn giá dịch vụ: {service.name}
              </h2>
              <p className="text-xs text-slate-500 font-normal">
                Tạo phiên bản giá mới áp dụng theo mốc thời gian
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors"
            aria-label="Đóng"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
          {/* Blue explanation notice */}
          <div className="p-3.5 bg-blue-50 border border-blue-200/80 rounded-xl flex items-start gap-3">
            <Info className="w-5 h-5 text-blue-600 shrink-0 mt-0.5" />
            <p className="text-xs text-blue-900 leading-relaxed font-normal">
              Đơn giá mới tạo một phiên bản giá có hiệu lực từ ngày chọn; hợp đồng/hóa đơn cũ giữ nguyên
              giá đã chốt.
            </p>
          </div>

          {/* Row 1: Tên dịch vụ & Loại dịch vụ */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                Tên dịch vụ <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                value={serviceName}
                onChange={(e) => setServiceName(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-800 font-medium focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                Loại dịch vụ <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                value={serviceType}
                onChange={(e) => setServiceType(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-800 font-medium focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white"
              />
            </div>
          </div>

          {/* Row 2: Đơn vị tính & Áp dụng cho nhà trọ */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                Đơn vị tính <span className="text-rose-500">*</span>
              </label>
              <select
                value={unit}
                onChange={(e) => setUnit(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-800 font-medium focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white"
              >
                <option value="Phòng">Phòng</option>
                <option value="Người">Người</option>
                <option value="Xe">Xe</option>
                <option value="kWh">kWh</option>
                <option value="m³">m³</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                Áp dụng cho nhà trọ <span className="text-rose-500">*</span>
              </label>
              <select
                value={buildingScope}
                onChange={(e) => setBuildingScope(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-800 font-medium focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white"
              >
                <option value="Tất cả nhà trọ">Tất cả nhà trọ</option>
                <option value="Tòa nhà An Gia 01">Tòa nhà An Gia 01</option>
                <option value="Nhà trọ Bình Minh">Nhà trọ Bình Minh</option>
              </select>
            </div>
          </div>

          {/* Row 3: Đơn giá (đ) with formatted suffix & example */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="block text-xs font-semibold text-slate-700">
                Đơn giá (đ) <span className="text-rose-500">*</span>
              </label>
              <span className="text-xs text-slate-400 font-mono">Ví dụ: 100000</span>
            </div>

            <div className="relative flex items-center">
              <input
                type="text"
                value={numericRate > 0 ? numericRate.toLocaleString('vi-VN') : rateString}
                onChange={handleRateChange}
                placeholder="Nhập đơn giá..."
                className={`w-full text-right pr-24 pl-4 py-2.5 text-base font-bold rounded-xl border transition-all ${
                  errorMessage
                    ? 'border-rose-300 bg-rose-50/40 text-rose-900 focus:ring-rose-400'
                    : 'border-slate-200 bg-white text-slate-900 focus:ring-indigo-500'
                } focus:outline-none focus:ring-2`}
              />
              <span className="absolute right-3.5 text-xs font-medium text-slate-500 bg-slate-100 px-2 py-1 rounded-md pointer-events-none">
                đ / {unit.toLowerCase() === 'phòng' ? 'tháng' : unit.toLowerCase()}
              </span>
            </div>
          </div>

          {/* Validation Error Banner */}
          {errorMessage && (
            <div className="p-3 bg-rose-50 border border-rose-200 rounded-xl flex items-center gap-2.5 text-xs text-rose-700 font-medium animate-fadeIn">
              <AlertCircle className="w-4 h-4 shrink-0 text-rose-600" />
              <span>{errorMessage}</span>
            </div>
          )}

          {/* Row 4: Ngày bắt đầu hiệu lực & Ngày hết hiệu lực */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                Ngày bắt đầu hiệu lực <span className="text-rose-500">*</span>
              </label>
              <div className="relative">
                <input
                  type="date"
                  value={effectiveFrom}
                  onChange={(e) => setEffectiveFrom(e.target.value)}
                  className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-800 font-medium focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                Ngày hết hiệu lực
              </label>
              <div className="relative">
                <input
                  type="date"
                  value={effectiveTo}
                  onChange={(e) => setEffectiveTo(e.target.value)}
                  className="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-800 font-medium focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white"
                />
              </div>
              <p className="mt-1 text-[11px] text-slate-400">
                Để trống nếu chưa có ngày kết thúc
              </p>
            </div>
          </div>

          {/* Row 5: Kích hoạt ngay khi đến hạn Toggle card */}
          <div className="p-3.5 bg-slate-50/80 border border-slate-200/80 rounded-xl flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-slate-800">
                Kích hoạt ngay khi đến hạn
              </p>
              <p className="text-[11px] text-slate-500 mt-0.5">
                Hệ thống sẽ chuyển trạng thái Đang áp dụng vào 00:00 của ngày bắt đầu
              </p>
            </div>
            <button
              type="button"
              onClick={() => setAutoActivate(!autoActivate)}
              className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                autoActivate ? 'bg-indigo-600' : 'bg-slate-300'
              }`}
              role="switch"
              aria-checked={autoActivate}
            >
              <span
                className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-xs ring-0 transition duration-200 ease-in-out ${
                  autoActivate ? 'translate-x-5' : 'translate-x-0'
                }`}
              />
            </button>
          </div>
        </div>

        {/* Modal Footer */}
        <div className="p-4 px-6 bg-slate-50/80 border-t border-slate-100 flex flex-col sm:flex-row items-center justify-between gap-3">
          <div className="flex items-center gap-1.5 text-xs text-slate-500 font-medium">
            <RefreshCw className="w-3.5 h-3.5 text-slate-400" />
            <span>Phiên bản UI A3 • Mock SQL</span>
          </div>

          <div className="flex items-center gap-3 w-full sm:w-auto justify-end">
            <button
              onClick={onClose}
              className="px-4 py-2 border border-slate-200 text-slate-700 hover:bg-slate-100 rounded-xl text-xs font-semibold transition-colors"
            >
              Hủy
            </button>
            <button
              onClick={handleSave}
              disabled={isSaving}
              className="px-5 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-colors"
            >
              {isSaving ? (
                <>
                  <span className="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  <span>Đang lưu... (chưa lưu SQL)</span>
                </>
              ) : (
                <>
                  <Check className="w-3.5 h-3.5" />
                  <span>Lưu cập nhật</span>
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
