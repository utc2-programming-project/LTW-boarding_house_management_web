import React from 'react';
import type { Room, UserRole } from '../types/index.ts';
import { formatVndCurrency } from '../utils/roomCalculations.ts';

interface RoomDetailModalProps {
  room: Room | null;
  onClose: () => void;
  onEdit: (room: Room) => void;
  userRole: UserRole;
}

export const RoomDetailModal: React.FC<RoomDetailModalProps> = ({
  room,
  onClose,
  onEdit,
  userRole,
}) => {
  if (!room) return null;

  const canEdit = userRole === 'Chủ trọ' || userRole === 'Admin' || userRole === 'Quản lý';

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-detail-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-inverse-surface/40 backdrop-blur-xs animate-in fade-in duration-150"
    >
      <div className="bg-surface-container-lowest rounded-2xl shadow-2xl max-w-lg w-full max-h-[90vh] overflow-y-auto border border-surface-container flex flex-col">
        {/* Header */}
        <div className="px-6 py-5 border-b border-surface-container flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-primary-fixed flex items-center justify-center text-primary">
              <span className="material-symbols-outlined text-[24px]">meeting_room</span>
            </div>
            <div className="flex flex-col">
              <div className="flex items-center gap-2">
                <h2 id="modal-detail-title" className="text-[20px] font-bold text-on-surface">
                  {room.code}
                </h2>
                {room.approvalStatus === 'Chờ duyệt' && (
                  <span className="px-2 py-0.5 rounded bg-tertiary-fixed text-on-tertiary-fixed-variant text-[11px] font-semibold">
                    Chờ duyệt
                  </span>
                )}
                {room.isLocked && (
                  <span className="px-2 py-0.5 rounded bg-error-container text-on-error-container text-[11px] font-semibold">
                    Tạm khóa
                  </span>
                )}
              </div>
              <span className="text-[13px] text-on-surface-variant font-medium">
                {room.property}
              </span>
            </div>
          </div>
          <button
            type="button"
            aria-label="Đóng chi tiết phòng"
            onClick={onClose}
            className="p-1.5 rounded-lg text-outline hover:text-on-surface hover:bg-surface-container transition-colors"
          >
            <svg className="w-5 h-5 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
              <line x1="18" x2="6" y1="6" y2="18" />
              <line x1="6" x2="18" y1="6" y2="18" />
            </svg>
          </button>
        </div>

        {/* Content Body */}
        <div className="p-6 flex flex-col gap-5 text-[14px]">
          {/* Key Metric Highlights */}
          <div className="grid grid-cols-2 gap-3">
            <div className="p-3.5 rounded-xl bg-surface-container-low border border-surface-container flex flex-col gap-1">
              <span className="text-[12px] text-on-surface-variant font-medium">Giá thuê chuẩn</span>
              <span className="text-[18px] font-bold text-on-surface text-primary">
                {formatVndCurrency(room.price)}
              </span>
            </div>
            <div className="p-3.5 rounded-xl bg-surface-container-low border border-surface-container flex flex-col gap-1">
              <span className="text-[12px] text-on-surface-variant font-medium">Diện tích & Tầng</span>
              <span className="text-[16px] font-bold text-on-surface">
                {room.area ? `${room.area} m²` : '—'} • {room.floor ? `Tầng ${room.floor}` : 'Chưa phân tầng'}
              </span>
            </div>
          </div>

          {/* Status Breakdown */}
          <div className="flex flex-col gap-2.5">
            <span className="text-[12px] font-semibold uppercase tracking-wider text-outline">
              Trạng thái & Vận hành
            </span>
            <div className="grid grid-cols-2 gap-3">
              <div className="flex items-center justify-between p-2.5 rounded-lg bg-surface-container-lowest border border-surface-container">
                <span className="text-on-surface-variant text-[13px]">Trạng thái:</span>
                {room.status === 'Trống' && (
                  <span className="px-2.5 py-0.5 rounded-full bg-surface-container text-on-surface-variant text-[12px] font-medium">
                    Trống
                  </span>
                )}
                {room.status === 'Đang thuê' && (
                  <span className="px-2.5 py-0.5 rounded-full bg-secondary-fixed text-on-secondary-fixed-variant text-[12px] font-semibold">
                    Đang thuê
                  </span>
                )}
                {room.status === 'Bảo trì' && (
                  <span className="px-2.5 py-0.5 rounded-full bg-tertiary-fixed text-on-tertiary-fixed-variant text-[12px] font-semibold">
                    Bảo trì
                  </span>
                )}
              </div>

              <div className="flex items-center justify-between p-2.5 rounded-lg bg-surface-container-lowest border border-surface-container">
                <span className="text-on-surface-variant text-[13px]">Hoạt động:</span>
                <span
                  className={`px-2.5 py-0.5 rounded-full text-[12px] font-medium ${
                    room.activeStatus === 'Đang hoạt động'
                      ? 'bg-secondary-fixed text-on-secondary-fixed-variant'
                      : 'bg-surface-container text-outline'
                  }`}
                >
                  {room.activeStatus}
                </span>
              </div>
            </div>
          </div>

          {/* Tenant Info if Occupied */}
          {room.status === 'Đang thuê' && (
            <div className="flex flex-col gap-2.5 p-4 rounded-xl bg-secondary-fixed/30 border border-secondary/20">
              <div className="flex items-center gap-2 text-secondary font-semibold text-[13px]">
                <span className="material-symbols-outlined text-[18px]">person</span>
                <span>Thông tin khách đang thuê</span>
              </div>
              <div className="grid grid-cols-2 gap-2 text-[13px]">
                <div>
                  <span className="text-on-surface-variant block text-[11px]">Họ và tên:</span>
                  <span className="font-semibold text-on-surface">{room.tenantName || 'Khách vãng lai'}</span>
                </div>
                <div>
                  <span className="text-on-surface-variant block text-[11px]">Điện thoại:</span>
                  <span className="font-semibold text-on-surface">{room.tenantPhone || 'Chưa cập nhật'}</span>
                </div>
                {room.contractEnd && (
                  <div className="col-span-2">
                    <span className="text-on-surface-variant block text-[11px]">Hạn hợp đồng:</span>
                    <span className="font-medium text-on-surface">{room.contractEnd}</span>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Amenities & Notes */}
          <div className="flex flex-col gap-2">
            <span className="text-[12px] font-semibold uppercase tracking-wider text-outline">
              Trang bị tiện nghi & Ghi chú
            </span>
            {room.amenities && room.amenities.length > 0 ? (
              <div className="flex flex-wrap gap-1.5">
                {room.amenities.map((item, idx) => (
                  <span
                    key={idx}
                    className="px-2.5 py-1 rounded-lg bg-surface-container text-on-surface-variant text-[12px]"
                  >
                    {item}
                  </span>
                ))}
              </div>
            ) : (
              <span className="text-[13px] text-outline italic">Chưa gắn danh mục tiện nghi cụ thể</span>
            )}

            {room.notes && (
              <div className="mt-1 p-3 rounded-lg bg-surface-container-low text-[13px] text-on-surface-variant">
                {room.notes}
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-surface-container flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-lg text-on-surface-variant hover:bg-surface-container text-[13px] font-medium transition-all"
          >
            Đóng
          </button>
          {canEdit && (
            <button
              type="button"
              onClick={() => {
                onClose();
                onEdit(room);
              }}
              className="px-4 py-2 rounded-lg bg-primary-container text-on-primary hover:bg-primary transition-all text-[13px] font-semibold shadow-sm flex items-center gap-1.5"
            >
              <svg className="w-3.5 h-3.5 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                <path d="M12 20h9" />
                <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
              </svg>
              <span>Chỉnh sửa phòng</span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
