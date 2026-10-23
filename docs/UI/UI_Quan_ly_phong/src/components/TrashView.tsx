import React from 'react';
import type { Room } from '../types/index.ts';
import { formatVndCurrency } from '../utils/roomCalculations.ts';

interface TrashViewProps {
  deletedRooms: Room[];
  onRestoreRoom: (roomId: string) => void;
  onPermanentDelete: (roomId: string) => void;
  onEmptyTrash: () => void;
}

export const TrashView: React.FC<TrashViewProps> = ({
  deletedRooms,
  onRestoreRoom,
  onPermanentDelete,
  onEmptyTrash,
}) => {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-[28px] font-bold text-on-surface tracking-tight">Thùng rác</h2>
          <p className="text-[14px] text-on-surface-variant">
            Danh sách các phòng đã bị xóa tạm thời. Bạn có thể khôi phục lại dữ liệu bất cứ lúc nào.
          </p>
        </div>

        {deletedRooms.length > 0 && (
          <button
            type="button"
            onClick={onEmptyTrash}
            className="px-4 py-2 rounded-lg bg-error-container text-on-error-container hover:bg-error/20 text-[13px] font-semibold transition-all flex items-center gap-1.5"
          >
            <span className="material-symbols-outlined text-[18px]">delete_forever</span>
            <span>Dọn sạch thùng rác</span>
          </button>
        )}
      </div>

      {deletedRooms.length === 0 ? (
        <div className="bg-surface-container-lowest rounded-2xl shadow-sm border border-surface-container p-16 flex flex-col items-center justify-center text-center gap-3">
          <div className="w-12 h-12 rounded-xl bg-surface-container flex items-center justify-center text-outline">
            <span className="material-symbols-outlined text-[28px]">delete</span>
          </div>
          <h3 className="text-[17px] font-semibold text-on-surface">Thùng rác đang trống</h3>
          <p className="text-[13px] text-on-surface-variant">
            Không có phòng nào đang nằm trong thùng rác.
          </p>
        </div>
      ) : (
        <div className="bg-surface-container-lowest rounded-xl shadow-sm border border-surface-container overflow-hidden">
          <div className="overflow-x-auto w-full">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-surface-container-low text-outline text-[11px] tracking-wider uppercase font-semibold border-b border-surface-container">
                  <th className="py-3 px-4">Mã phòng</th>
                  <th className="py-3 px-4">Nhà trọ</th>
                  <th className="py-3 px-4 text-center">Tầng</th>
                  <th className="py-3 px-4 text-right">Giá chuẩn</th>
                  <th className="py-3 px-4 text-center">Trạng thái trước khi xóa</th>
                  <th className="py-3 px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-container/50 text-[14px] text-on-surface">
                {deletedRooms.map((room) => (
                  <tr key={room.id} className="hover:bg-surface-container transition-colors">
                    <td className="py-3.5 px-4 font-semibold text-on-surface">{room.code}</td>
                    <td className="py-3.5 px-4 text-on-surface-variant text-[13px]">{room.property}</td>
                    <td className="py-3.5 px-4 text-center">
                      {room.floor !== null ? `Tầng ${room.floor}` : '—'}
                    </td>
                    <td className="py-3.5 px-4 text-right font-medium">
                      {formatVndCurrency(room.price)}
                    </td>
                    <td className="py-3.5 px-4 text-center">
                      <span className="px-2.5 py-0.5 rounded-full bg-surface-container text-on-surface-variant text-[11px] font-medium">
                        {room.status}
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => onRestoreRoom(room.id)}
                          className="px-3 py-1 rounded-lg bg-surface-container text-primary hover:bg-primary-container hover:text-on-primary text-[12px] font-semibold transition-all flex items-center gap-1"
                        >
                          <span className="material-symbols-outlined text-[16px]">restore</span>
                          <span>Khôi phục</span>
                        </button>
                        <button
                          type="button"
                          onClick={() => onPermanentDelete(room.id)}
                          className="p-1 rounded-lg text-outline hover:text-error hover:bg-error-container/40 transition-colors"
                          title="Xóa vĩnh viễn"
                        >
                          <span className="material-symbols-outlined text-[18px]">delete_forever</span>
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};
