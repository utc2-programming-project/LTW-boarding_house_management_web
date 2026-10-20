import React, { useState, useEffect } from 'react';
import type { Room, RoomStatus, ActiveStatus } from '../types/index.ts';
import { validateRoomForm } from '../utils/roomCalculations.ts';

interface AddEditRoomModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (room: Partial<Room>) => void;
  initialData?: Room | null;
  properties: string[];
  enablePriceCheck: boolean;
}

export const AddEditRoomModal: React.FC<AddEditRoomModalProps> = ({
  isOpen,
  onClose,
  onSave,
  initialData,
  properties,
  enablePriceCheck,
}) => {
  const [code, setCode] = useState('');
  const [property, setProperty] = useState(properties[0] || 'An Phú Building');
  const [floor, setFloor] = useState<number | ''>(1);
  const [area, setArea] = useState<number | ''>(25);
  const [price, setPrice] = useState<number | ''>(3500000);
  const [status, setStatus] = useState<RoomStatus>('Trống');
  const [activeStatus, setActiveStatus] = useState<ActiveStatus>('Đang hoạt động');
  const [isPendingApproval, setIsPendingApproval] = useState(false);
  const [isLocked, setIsLocked] = useState(false);
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (initialData) {
      setCode(initialData.code);
      setProperty(initialData.property);
      setFloor(initialData.floor !== null ? initialData.floor : '');
      setArea(initialData.area !== null ? initialData.area : '');
      setPrice(initialData.price);
      setStatus(initialData.status);
      setActiveStatus(initialData.activeStatus);
      setIsPendingApproval(initialData.approvalStatus === 'Chờ duyệt');
      setIsLocked(Boolean(initialData.isLocked));
      setNotes(initialData.notes || '');
    } else {
      setCode('');
      setProperty(properties[0] || 'An Phú Building');
      setFloor(1);
      setArea(25);
      setPrice(3500000);
      setStatus('Trống');
      setActiveStatus('Đang hoạt động');
      setIsPendingApproval(false);
      setIsLocked(false);
      setNotes('');
    }
    setErrors({});
  }, [initialData, isOpen, properties]);

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const roomData: Partial<Room> = {
      code: code.trim(),
      property,
      floor: floor === '' ? null : Number(floor),
      area: area === '' ? null : Number(area),
      price: price === '' ? 0 : Number(price),
      status,
      activeStatus,
      approvalStatus: isPendingApproval ? 'Chờ duyệt' : 'Đã duyệt',
      isLocked,
      notes: notes.trim(),
    };

    const validation = validateRoomForm(roomData, enablePriceCheck);
    if (!validation.isValid) {
      setErrors(validation.errors);
      return;
    }

    onSave(roomData);
    onClose();
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-room-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-inverse-surface/40 backdrop-blur-xs animate-in fade-in duration-150"
    >
      <div className="bg-surface-container-lowest rounded-2xl shadow-2xl max-w-lg w-full max-h-[90vh] overflow-y-auto border border-surface-container flex flex-col">
        {/* Modal Header */}
        <div className="px-6 py-4 border-b border-surface-container flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[24px]">
              {initialData ? 'edit' : 'add_circle'}
            </span>
            <h2 id="modal-room-title" className="text-[18px] font-bold text-on-surface">
              {initialData ? `Chỉnh sửa phòng ${initialData.code}` : 'Thêm phòng mới'}
            </h2>
          </div>
          <button
            type="button"
            aria-label="Đóng hộp thoại"
            onClick={onClose}
            className="p-1 rounded-lg text-outline hover:text-on-surface hover:bg-surface-container transition-colors"
          >
            <svg className="w-5 h-5 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
              <line x1="18" x2="6" y1="6" y2="18" />
              <line x1="6" x2="18" y1="6" y2="18" />
            </svg>
          </button>
        </div>

        {/* Modal Form */}
        <form onSubmit={handleSubmit} className="p-6 flex flex-col gap-4 text-[14px]">
          {/* Mã phòng & Nhà trọ */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="modal-code" className="font-medium text-on-surface text-[13px]">
                Mã phòng <span className="text-error">*</span>
              </label>
              <input
                id="modal-code"
                type="text"
                required
                placeholder="VD: P.105"
                value={code}
                onChange={(e) => {
                  setCode(e.target.value);
                  if (errors.code) setErrors({ ...errors, code: '' });
                }}
                className={`px-3 py-2 rounded-lg outline-none ring-1 transition-all ${
                  errors.code
                    ? 'ring-error bg-error-container/20 text-on-error-container'
                    : 'ring-outline-variant focus:ring-2 focus:ring-primary-container bg-surface-container-lowest text-on-surface'
                }`}
              />
              {errors.code && <span className="text-error text-[11px]">{errors.code}</span>}
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="modal-property" className="font-medium text-on-surface text-[13px]">
                Nhà trọ / Cơ sở <span className="text-error">*</span>
              </label>
              <select
                id="modal-property"
                value={property}
                onChange={(e) => setProperty(e.target.value)}
                className="px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container bg-surface-container-lowest text-on-surface cursor-pointer"
              >
                {properties.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Tầng & Diện tích */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="modal-floor" className="font-medium text-on-surface text-[13px]">
                Tầng
              </label>
              <input
                id="modal-floor"
                type="number"
                min="0"
                placeholder="VD: 1, 2, 3..."
                value={floor}
                onChange={(e) => setFloor(e.target.value ? Number(e.target.value) : '')}
                className="px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container bg-surface-container-lowest text-on-surface"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="modal-area" className="font-medium text-on-surface text-[13px]">
                Diện tích (m²)
              </label>
              <input
                id="modal-area"
                type="number"
                min="1"
                placeholder="VD: 25"
                value={area}
                onChange={(e) => setArea(e.target.value ? Number(e.target.value) : '')}
                className="px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container bg-surface-container-lowest text-on-surface"
              />
            </div>
          </div>

          {/* Giá chuẩn */}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="modal-price" className="font-medium text-on-surface text-[13px]">
              Giá chuẩn (VNĐ / tháng) <span className="text-error">*</span>
            </label>
            <input
              id="modal-price"
              type="number"
              step="50000"
              required
              placeholder="VD: 3500000"
              value={price}
              onChange={(e) => {
                setPrice(e.target.value ? Number(e.target.value) : '');
                if (errors.price) setErrors({ ...errors, price: '' });
              }}
              className={`px-3 py-2 rounded-lg outline-none ring-1 transition-all ${
                errors.price
                  ? 'ring-error bg-error-container/20 text-on-error-container'
                  : 'ring-outline-variant focus:ring-2 focus:ring-primary-container bg-surface-container-lowest text-on-surface'
              }`}
            />
            {errors.price && <span className="text-error text-[11px]">{errors.price}</span>}
          </div>

          {/* Trạng thái & Hoạt động */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="modal-status" className="font-medium text-on-surface text-[13px]">
                Trạng thái phòng
              </label>
              <select
                id="modal-status"
                value={status}
                onChange={(e) => setStatus(e.target.value as RoomStatus)}
                className="px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container bg-surface-container-lowest text-on-surface cursor-pointer"
              >
                <option value="Trống">Trống</option>
                <option value="Đang thuê">Đang thuê</option>
                <option value="Bảo trì">Bảo trì</option>
              </select>
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="modal-active" className="font-medium text-on-surface text-[13px]">
                Hoạt động
              </label>
              <select
                id="modal-active"
                value={activeStatus}
                onChange={(e) => setActiveStatus(e.target.value as ActiveStatus)}
                className="px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container bg-surface-container-lowest text-on-surface cursor-pointer"
              >
                <option value="Đang hoạt động">Đang hoạt động</option>
                <option value="Không hoạt động">Không hoạt động</option>
              </select>
            </div>
          </div>

          {/* Operational Checkboxes */}
          <div className="flex flex-wrap items-center gap-6 pt-1">
            <label className="flex items-center gap-2 cursor-pointer select-none text-[13px] text-on-surface">
              <input
                type="checkbox"
                checked={isPendingApproval}
                onChange={(e) => setIsPendingApproval(e.target.checked)}
                className="w-4 h-4 accent-primary-container rounded"
              />
              <span>Đánh dấu "Chờ duyệt"</span>
            </label>

            <label className="flex items-center gap-2 cursor-pointer select-none text-[13px] text-on-surface">
              <input
                type="checkbox"
                checked={isLocked}
                onChange={(e) => setIsLocked(e.target.checked)}
                className="w-4 h-4 accent-error rounded"
              />
              <span>Tạm khóa phòng (SW-01)</span>
            </label>
          </div>

          {/* Ghi chú */}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="modal-notes" className="font-medium text-on-surface text-[13px]">
              Ghi chú thêm
            </label>
            <textarea
              id="modal-notes"
              rows={2}
              placeholder="Tiện ích kèm theo, tình trạng phòng..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className="px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container bg-surface-container-lowest text-on-surface resize-none"
            />
          </div>

          {/* Action buttons */}
          <div className="flex items-center justify-end gap-3 pt-4 border-t border-surface-container">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-lg text-on-surface-variant hover:bg-surface-container hover:text-on-surface transition-all text-[13px] font-medium"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-5 py-2 rounded-lg bg-primary-container text-on-primary hover:bg-primary transition-all text-[13px] font-semibold shadow-sm"
            >
              {initialData ? 'Lưu thay đổi' : 'Thêm phòng'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
