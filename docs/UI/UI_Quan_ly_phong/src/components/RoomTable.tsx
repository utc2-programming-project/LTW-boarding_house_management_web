import React, { useState } from 'react';
import type { Room, UserRole, SimulationState } from '../types/index.ts';
import { formatVndCurrency } from '../utils/roomCalculations.ts';

interface RoomTableProps {
  rooms: Room[];
  totalFilteredCount: number;
  isCompact: boolean;
  userRole: UserRole;
  simulationState: SimulationState;
  onResetSimulation: () => void;
  onResetFilters: () => void;
  onViewRoom: (room: Room) => void;
  onEditRoom: (room: Room) => void;
  onDeleteRoom: (room: Room) => void;
}

export const RoomTable: React.FC<RoomTableProps> = ({
  rooms,
  totalFilteredCount,
  isCompact,
  userRole,
  simulationState,
  onResetSimulation,
  onResetFilters,
  onViewRoom,
  onEditRoom,
  onDeleteRoom,
}) => {
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;
  const totalPages = Math.max(1, Math.ceil(rooms.length / pageSize));

  // Safe pagination slice
  const startIndex = (currentPage - 1) * pageSize;
  const paginatedRooms = rooms.slice(startIndex, startIndex + pageSize);

  const canEditOrDelete = userRole === 'Chủ trọ' || userRole === 'Admin' || userRole === 'Quản lý';

  // Forced Simulation State Overlays
  if (simulationState === 'loading') {
    return (
      <div
        id="state-loading"
        className="bg-surface-container-lowest rounded-xl shadow-sm p-16 flex flex-col items-center justify-center gap-4 min-h-[420px] border border-surface-container"
      >
        <div className="w-9 h-9 rounded-full border-3 border-primary-container border-t-transparent animate-spin" />
        <span className="text-[14px] text-on-surface-variant font-medium">
          Đang tải danh sách phòng...
        </span>
      </div>
    );
  }

  if (simulationState === 'empty' || (simulationState === 'normal' && rooms.length === 0)) {
    return (
      <div
        id="state-empty"
        className="bg-surface-container-lowest rounded-xl shadow-sm p-16 flex flex-col items-center justify-center text-center gap-3 min-h-[420px] border border-surface-container"
      >
        <div className="w-12 h-12 rounded-xl bg-surface-container flex items-center justify-center text-outline">
          <svg className="w-6 h-6 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
            <rect height="18" rx="2" width="18" x="3" y="3" />
            <line x1="9" x2="15" y1="9" y2="15" />
            <line x1="15" x2="9" y1="9" y2="15" />
          </svg>
        </div>
        <h3 className="text-[18px] font-semibold text-on-surface">Không tìm thấy phòng phù hợp</h3>
        <p className="text-[14px] text-on-surface-variant max-w-sm">
          Thử thay đổi bộ lọc, xóa từ khóa tìm kiếm hoặc bấm 'Đặt lại' để xem toàn bộ danh sách phòng.
        </p>
        <button
          type="button"
          onClick={() => {
            onResetSimulation();
            onResetFilters();
          }}
          className="mt-2 px-4 py-2 rounded-lg bg-surface-container text-on-surface text-[13px] font-medium hover:bg-surface-container-high transition-all"
        >
          Khôi phục bộ lọc
        </button>
      </div>
    );
  }

  if (simulationState === 'error') {
    return (
      <div
        id="state-error"
        className="bg-surface-container-lowest rounded-xl shadow-sm p-16 flex flex-col items-center justify-center text-center gap-3 min-h-[420px] border border-surface-container"
      >
        <div className="w-12 h-12 rounded-xl bg-error-container text-on-error-container flex items-center justify-center">
          <svg className="w-6 h-6 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" x2="12" y1="8" y2="12" />
            <line x1="12" x2="12.01" y1="16" y2="16" />
          </svg>
        </div>
        <h3 className="text-[18px] font-semibold text-on-surface">Không thể tải dữ liệu cơ sở</h3>
        <p className="text-[14px] text-on-surface-variant max-w-sm">
          Có lỗi kết nối đến máy chủ quản lý bất động sản. Vui lòng kiểm tra lại đường truyền mạng.
        </p>
        <button
          type="button"
          onClick={onResetSimulation}
          className="mt-2 px-4 py-2 rounded-lg bg-primary-container text-on-primary text-[13px] font-medium hover:bg-primary transition-all shadow-sm"
        >
          Thử lại ngay
        </button>
      </div>
    );
  }

  if (simulationState === 'unauthorized') {
    return (
      <div
        id="state-unauthorized"
        className="bg-surface-container-lowest rounded-xl shadow-sm p-16 flex flex-col items-center justify-center text-center gap-3 min-h-[420px] border border-surface-container"
      >
        <div className="w-12 h-12 rounded-xl bg-surface-container text-outline flex items-center justify-center">
          <svg className="w-6 h-6 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
            <rect height="11" rx="2" width="18" x="3" y="11" />
            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          </svg>
        </div>
        <h3 className="text-[18px] font-semibold text-on-surface">Không có quyền truy cập (403)</h3>
        <p className="text-[14px] text-on-surface-variant max-w-sm">
          Tài khoản với vai trò hiện tại không được cấp phép quản lý danh mục phòng này.
        </p>
        <button
          type="button"
          onClick={onResetSimulation}
          className="mt-2 px-4 py-2 rounded-lg bg-surface-container text-on-surface text-[13px] font-medium hover:bg-surface-container-high transition-all"
        >
          Quay lại chế độ xem thường
        </button>
      </div>
    );
  }

  const cellPadding = isCompact ? 'py-1.5 px-4' : 'py-3.5 px-4';

  return (
    <div
      id="data-container"
      className="bg-surface-container-lowest rounded-xl shadow-sm overflow-hidden flex flex-col relative min-h-[420px] border border-surface-container"
    >
      <div className="overflow-x-auto w-full" id="table-wrapper">
        <table className="w-full text-left border-collapse" id="main-room-table">
          <thead>
            <tr className="bg-surface-container-low text-outline text-[11px] tracking-wider uppercase select-none font-semibold border-b border-surface-container">
              <th className="py-3 px-4">Mã phòng</th>
              <th className="py-3 px-4">Nhà trọ</th>
              <th className="py-3 px-4 text-center">Tầng</th>
              <th className="py-3 px-4 text-right">Diện tích</th>
              <th className="py-3 px-4 text-right">Giá chuẩn</th>
              <th className="py-3 px-4 text-center">Trạng thái phòng</th>
              <th className="py-3 px-4 text-center">Hoạt động</th>
              <th className="py-3 px-4 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-surface-container/50 text-on-surface text-[14px]" id="room-tbody">
            {paginatedRooms.map((room) => {
              const isInactive = room.activeStatus === 'Không hoạt động';

              return (
                <tr
                  key={room.id}
                  data-code={room.code}
                  data-prop={room.property}
                  data-status={room.status}
                  data-active={room.activeStatus}
                  data-price={room.price}
                  className={`hover:bg-surface-container transition-colors group ${
                    isInactive ? 'opacity-85' : ''
                  }`}
                >
                  {/* Mã phòng */}
                  <td className={cellPadding}>
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-[15px] text-on-surface group-hover:text-primary transition-colors">
                        {room.code}
                      </span>
                      {room.approvalStatus === 'Chờ duyệt' && (
                        <span className="px-1.5 py-0.5 rounded bg-tertiary-fixed text-on-tertiary-fixed-variant text-[11px] font-medium">
                          Chờ duyệt
                        </span>
                      )}
                      {room.isLocked && (
                        <span className="px-1.5 py-0.5 rounded bg-error-container text-on-error-container text-[11px] font-medium flex items-center gap-0.5">
                          <svg className="w-2.5 h-2.5 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                            <rect height="11" rx="2" width="18" x="3" y="11" />
                            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                          </svg>
                          Khóa
                        </span>
                      )}
                    </div>
                  </td>

                  {/* Nhà trọ */}
                  <td className={cellPadding}>
                    <span className="text-[13px] text-on-surface font-medium">{room.property}</span>
                  </td>

                  {/* Tầng */}
                  <td className={`${cellPadding} text-center`}>
                    {room.floor !== null ? (
                      <span className="px-2 py-0.5 rounded bg-surface-container text-[11px] text-on-surface-variant font-medium">
                        Tầng {room.floor}
                      </span>
                    ) : (
                      <span className="text-outline text-[13px]">—</span>
                    )}
                  </td>

                  {/* Diện tích */}
                  <td className={`${cellPadding} text-right`}>
                    {room.area !== null ? (
                      <span className="text-[14px]">{room.area} m²</span>
                    ) : (
                      <span className="text-outline text-[13px]">—</span>
                    )}
                  </td>

                  {/* Giá chuẩn */}
                  <td className={`${cellPadding} text-right font-semibold text-[15px]`}>
                    {formatVndCurrency(room.price)}
                  </td>

                  {/* Trạng thái phòng */}
                  <td className={`${cellPadding} text-center`}>
                    {room.status === 'Trống' && (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-surface-container text-on-surface-variant text-[12px] font-medium">
                        <span className="w-1.5 h-1.5 rounded-full bg-outline" />
                        Trống
                      </span>
                    )}
                    {room.status === 'Đang thuê' && (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-secondary-fixed text-on-secondary-fixed-variant text-[12px] font-semibold">
                        <span className="w-1.5 h-1.5 rounded-full bg-secondary" />
                        Đang thuê
                      </span>
                    )}
                    {room.status === 'Bảo trì' && (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-tertiary-fixed text-on-tertiary-fixed-variant text-[12px] font-semibold">
                        <span className="w-1.5 h-1.5 rounded-full bg-tertiary" />
                        Bảo trì
                      </span>
                    )}
                  </td>

                  {/* Hoạt động */}
                  <td className={`${cellPadding} text-center`}>
                    {room.activeStatus === 'Đang hoạt động' ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-secondary-fixed text-on-secondary-fixed-variant text-[11px] font-medium">
                        <svg className="w-3 h-3 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                          <polyline points="20 6 9 17 4 12" />
                        </svg>
                        Đang hoạt động
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-surface-container text-outline text-[11px] font-medium">
                        <svg className="w-3 h-3 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                          <line x1="18" x2="6" y1="6" y2="18" />
                          <line x1="6" x2="18" y1="6" y2="18" />
                        </svg>
                        Không hoạt động
                      </span>
                    )}
                  </td>

                  {/* Thao tác */}
                  <td className={`${cellPadding} text-right`}>
                    <div className="flex items-center justify-end gap-1">
                      <button
                        type="button"
                        aria-label={`Xem chi tiết phòng ${room.code}`}
                        title="Xem chi tiết"
                        onClick={() => onViewRoom(room)}
                        className="p-1.5 rounded-lg text-on-surface-variant hover:text-primary hover:bg-surface-container-high transition-colors"
                      >
                        <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                          <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                          <circle cx="12" cy="12" r="3" />
                        </svg>
                      </button>

                      {canEditOrDelete && (
                        <>
                          <button
                            type="button"
                            aria-label={`Chỉnh sửa phòng ${room.code}`}
                            title="Chỉnh sửa"
                            onClick={() => onEditRoom(room)}
                            className="p-1.5 rounded-lg text-on-surface-variant hover:text-primary hover:bg-surface-container-high transition-colors"
                          >
                            <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                              <path d="M12 20h9" />
                              <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
                            </svg>
                          </button>

                          <button
                            type="button"
                            aria-label={`Xóa phòng ${room.code}`}
                            title="Xóa"
                            onClick={() => onDeleteRoom(room)}
                            className="p-1.5 rounded-lg text-on-surface-variant hover:text-error hover:bg-error-container/40 transition-colors"
                          >
                            <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                              <polyline points="3 6 5 6 21 6" />
                              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                            </svg>
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Pagination Footer */}
      <div
        id="table-footer"
        className="p-4 bg-surface-container-low flex flex-wrap items-center justify-between gap-3 text-[13px] text-on-surface-variant border-t border-surface-container"
      >
        <div className="flex items-center gap-1.5">
          <span>Hiển thị</span>
          <span id="pagination-info" className="font-semibold text-on-surface">
            {rooms.length > 0 ? `${startIndex + 1}–${Math.min(startIndex + pageSize, rooms.length)}` : '0'}
          </span>
          <span>/ {totalFilteredCount} phòng</span>
        </div>

        <div className="flex items-center gap-1 select-none">
          <button
            type="button"
            aria-label="Trang trước"
            disabled={currentPage <= 1}
            onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
            className={`p-1.5 rounded-lg transition-colors ${
              currentPage <= 1
                ? 'text-outline opacity-40 cursor-not-allowed'
                : 'text-on-surface hover:bg-surface-container'
            }`}
          >
            <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
              <polyline points="15 18 9 12 15 6" />
            </svg>
          </button>

          {Array.from({ length: totalPages }).map((_, i) => {
            const pageNum = i + 1;
            const isActive = pageNum === currentPage;
            return (
              <button
                key={pageNum}
                type="button"
                aria-label={`Trang ${pageNum}`}
                aria-current={isActive ? 'page' : undefined}
                onClick={() => setCurrentPage(pageNum)}
                className={`w-8 h-8 rounded-lg text-[13px] font-semibold flex items-center justify-center transition-all ${
                  isActive
                    ? 'bg-primary-container text-on-primary shadow-sm'
                    : 'text-on-surface hover:bg-surface-container'
                }`}
              >
                {pageNum}
              </button>
            );
          })}

          <button
            type="button"
            aria-label="Trang tiếp"
            disabled={currentPage >= totalPages}
            onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
            className={`p-1.5 rounded-lg transition-colors ${
              currentPage >= totalPages
                ? 'text-outline opacity-40 cursor-not-allowed'
                : 'text-on-surface hover:bg-surface-container'
            }`}
          >
            <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
              <polyline points="9 18 15 12 9 6" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  );
};
