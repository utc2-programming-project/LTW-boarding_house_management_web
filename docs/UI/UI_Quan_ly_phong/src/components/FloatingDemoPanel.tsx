import React, { useState } from 'react';
import type { UserRole, SimulationState } from '../types/index.ts';

interface FloatingDemoPanelProps {
  currentRole: UserRole;
  onRoleChange: (role: UserRole) => void;
  sw1Locked: boolean;
  onToggleSw1: (val: boolean) => void;
  sw2PriceCheck: boolean;
  onToggleSw2: (val: boolean) => void;
  sw3Compact: boolean;
  onToggleSw3: (val: boolean) => void;
  simulationState: SimulationState;
  onSetSimulationState: (state: SimulationState) => void;
}

export const FloatingDemoPanel: React.FC<FloatingDemoPanelProps> = ({
  currentRole,
  onRoleChange,
  sw1Locked,
  onToggleSw1,
  sw2PriceCheck,
  onToggleSw2,
  sw3Compact,
  onToggleSw3,
  simulationState,
  onSetSimulationState,
}) => {
  const [isCollapsed, setIsCollapsed] = useState(() => {
    if (typeof window !== 'undefined') {
      return window.innerWidth < 640;
    }
    return false;
  });

  return (
    <aside
      id="floating-demo-panel"
      aria-label="Bảng điều khiển Demo"
      className="fixed right-4 bottom-4 sm:right-6 sm:bottom-6 z-50 bg-surface-container-lowest/95 shadow-2xl rounded-xl p-4 w-72 sm:w-80 text-on-surface flex flex-col gap-3 border border-outline-variant/60 backdrop-blur-md transition-all"
    >
      {/* Panel Header */}
      <div
        className="flex items-center justify-between cursor-pointer select-none"
        onClick={() => setIsCollapsed(!isCollapsed)}
      >
        <div className="flex items-center gap-2 font-semibold text-[15px] text-on-surface">
          <svg className="w-4 h-4 stroke-primary-container fill-none stroke-2" viewBox="0 0 24 24">
            <circle cx="12" cy="12" r="3" />
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
          </svg>
          <span>Bảng điều khiển Demo</span>
        </div>
        <button
          type="button"
          aria-label={isCollapsed ? 'Mở rộng bảng điều khiển' : 'Thu nhỏ bảng điều khiển'}
          className="text-on-surface-variant hover:text-on-surface transition-transform"
        >
          <svg
            className={`w-4 h-4 stroke-current fill-none stroke-2 transition-transform duration-200 ${
              isCollapsed ? 'rotate-180' : 'rotate-0'
            }`}
            viewBox="0 0 24 24"
          >
            <polyline points="6 9 12 15 18 9" />
          </svg>
        </button>
      </div>

      {!isCollapsed && (
        <div id="demo-panel-content" className="flex flex-col gap-3 pt-1 text-[12px]">
          {/* Role Switcher */}
          <div className="flex flex-col gap-1">
            <span className="text-outline uppercase tracking-wider font-semibold text-[11px]">
              Vai trò người dùng
            </span>
            <select
              id="demo-role"
              value={currentRole}
              onChange={(e) => onRoleChange(e.target.value as UserRole)}
              className="bg-surface-container text-on-surface p-2 rounded-lg outline-none ring-1 ring-outline-variant text-[12px] cursor-pointer"
            >
              <option value="Chủ trọ">Chủ trọ (Toàn quyền)</option>
              <option value="Quản lý">Quản lý cơ sở</option>
              <option value="Khách thuê">Khách thuê (Chỉ xem)</option>
              <option value="Admin">Hệ thống Admin</option>
            </select>
          </div>

          {/* Test Switches SW-01 / SW-02 / SW-03 */}
          <div className="flex flex-col gap-2 pt-1 border-t border-surface-container">
            <span className="text-outline uppercase tracking-wider font-semibold text-[11px]">
              Công tắc kiểm thử
            </span>

            <div className="flex items-center justify-between">
              <label htmlFor="demo-sw1" className="text-on-surface cursor-pointer select-none">
                SW-01: Bật phòng tạm khóa
              </label>
              <input
                id="demo-sw1"
                type="checkbox"
                checked={sw1Locked}
                onChange={(e) => onToggleSw1(e.target.checked)}
                className="w-4 h-4 accent-primary-container cursor-pointer"
              />
            </div>

            <div className="flex items-center justify-between">
              <label htmlFor="demo-sw2" className="text-on-surface cursor-pointer select-none">
                SW-02: Bật kiểm định giá
              </label>
              <input
                id="demo-sw2"
                type="checkbox"
                checked={sw2PriceCheck}
                onChange={(e) => onToggleSw2(e.target.checked)}
                className="w-4 h-4 accent-primary-container cursor-pointer"
              />
            </div>

            <div className="flex items-center justify-between">
              <label htmlFor="demo-sw3" className="text-on-surface cursor-pointer select-none">
                SW-03: Chế độ gọn (Compact)
              </label>
              <input
                id="demo-sw3"
                type="checkbox"
                checked={sw3Compact}
                onChange={(e) => onToggleSw3(e.target.checked)}
                className="w-4 h-4 accent-primary-container cursor-pointer"
              />
            </div>
          </div>

          {/* Force UI View States */}
          <div className="flex flex-col gap-1.5 pt-1 border-t border-surface-container">
            <span className="text-outline uppercase tracking-wider font-semibold text-[11px]">
              Ép trạng thái UI
            </span>
            <div className="grid grid-cols-2 gap-1.5">
              <button
                type="button"
                onClick={() => onSetSimulationState('normal')}
                className={`px-2 py-1.5 rounded transition-colors ${
                  simulationState === 'normal'
                    ? 'bg-primary-container text-on-primary font-semibold'
                    : 'bg-surface-container hover:bg-surface-container-high text-on-surface'
                }`}
              >
                Bình thường
              </button>

              <button
                type="button"
                onClick={() => onSetSimulationState('loading')}
                className={`px-2 py-1.5 rounded transition-colors ${
                  simulationState === 'loading'
                    ? 'bg-primary-container text-on-primary font-semibold'
                    : 'bg-surface-container hover:bg-surface-container-high text-on-surface'
                }`}
              >
                Đang tải
              </button>

              <button
                type="button"
                onClick={() => onSetSimulationState('empty')}
                className={`px-2 py-1.5 rounded transition-colors ${
                  simulationState === 'empty'
                    ? 'bg-primary-container text-on-primary font-semibold'
                    : 'bg-surface-container hover:bg-surface-container-high text-on-surface'
                }`}
              >
                Rỗng (Empty)
              </button>

              <button
                type="button"
                onClick={() => onSetSimulationState('error')}
                className={`px-2 py-1.5 rounded transition-colors ${
                  simulationState === 'error'
                    ? 'bg-error text-on-error font-semibold'
                    : 'bg-error-container text-on-error-container hover:bg-error/20'
                }`}
              >
                Lỗi máy chủ
              </button>

              <button
                type="button"
                onClick={() => onSetSimulationState('unauthorized')}
                className={`col-span-2 px-2 py-1.5 rounded transition-colors ${
                  simulationState === 'unauthorized'
                    ? 'bg-outline text-surface font-semibold'
                    : 'bg-surface-container hover:bg-surface-container-high text-outline'
                }`}
              >
                Không quyền (403)
              </button>
            </div>
          </div>

          {/* Footer note */}
          <div className="pt-1 flex items-center justify-between text-outline text-[11px] border-t border-surface-container">
            <span>Trạng thái: Hoạt động tốt</span>
            <span className="font-mono text-[10px] bg-surface-container px-1 py-0.5 rounded">
              VHT-DEMO-V2
            </span>
          </div>
        </div>
      )}
    </aside>
  );
};
