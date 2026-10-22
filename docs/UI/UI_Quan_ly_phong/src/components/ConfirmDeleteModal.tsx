import React from 'react';
import type { Room } from '../types/index.ts';

interface ConfirmDeleteModalProps {
  room: Room | null;
  isOpen: boolean;
  onClose: () => void;
  onConfirm: (room: Room) => void;
}

export const ConfirmDeleteModal: React.FC<ConfirmDeleteModalProps> = ({
  room,
  isOpen,
  onClose,
  onConfirm,
}) => {
  if (!isOpen || !room) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-delete-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-inverse-surface/40 backdrop-blur-xs animate-in fade-in duration-150"
    >
      <div className="bg-surface-container-lowest rounded-2xl shadow-2xl max-w-md w-full border border-surface-container p-6 flex flex-col gap-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-error-container text-on-error-container flex items-center justify-center shrink-0">
            <svg className="w-5 h-5 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
              <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
              <line x1="12" x2="12" y1="9" y2="13" />
              <line x1="12" x2="12.01" y1="17" y2="17" />
            </svg>
          </div>
          <div>
            <h3 id="modal-delete-title" className="text-[17px] font-bold text-on-surface">
              Xóa phòng {room.code}?
            </h3>
            <span className="text-[12px] text-on-surface-variant">{room.property}</span>
          </div>
        </div>

        <p className="text-[14px] text-on-surface-variant leading-relaxed">
          Phòng <strong className="text-on-surface">{room.code}</strong> sẽ được chuyển vào mục{' '}
          <strong className="text-on-surface">Thùng rác</strong>. Bạn có thể khôi phục lại bất kỳ lúc
          nào hoặc xóa vĩnh viễn trong tab Thùng rác.
        </p>

        <div className="flex items-center justify-end gap-3 pt-3 border-t border-surface-container">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-lg text-on-surface-variant hover:bg-surface-container text-[13px] font-medium transition-all"
          >
            Hủy thao tác
          </button>
          <button
            type="button"
            onClick={() => {
              onConfirm(room);
              onClose();
            }}
            className="px-4 py-2 rounded-lg bg-error text-on-error hover:bg-error/90 text-[13px] font-semibold transition-all shadow-sm"
          >
            Chuyển vào thùng rác
          </button>
        </div>
      </div>
    </div>
  );
};
