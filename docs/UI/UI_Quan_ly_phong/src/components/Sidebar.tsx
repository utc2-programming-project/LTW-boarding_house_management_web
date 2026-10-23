import React from 'react';
import type { AppNavTab } from '../types/index.ts';

interface SidebarProps {
  currentTab: AppNavTab;
  onTabChange: (tab: AppNavTab) => void;
  deletedCount: number;
  isOpenMobile?: boolean;
  onCloseMobile?: () => void;
}

export const Sidebar: React.FC<SidebarProps> = ({
  currentTab,
  onTabChange,
  deletedCount,
  isOpenMobile,
  onCloseMobile,
}) => {
  return (
    <>
      {/* Mobile backdrop */}
      {isOpenMobile && (
        <div
          role="presentation"
          onClick={onCloseMobile}
          className="fixed inset-0 bg-black/40 z-45 lg:hidden backdrop-blur-xs"
        />
      )}

      <aside
        aria-label="Thanh điều hướng chính"
        className={`fixed left-0 top-0 bottom-0 w-64 bg-surface-container-lowest shadow-[0_1px_8px_rgba(0,0,0,0.04)] z-50 flex flex-col justify-between py-6 px-4 border-r border-surface-container transition-transform duration-200 ${
          isOpenMobile ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
        }`}
      >
      <div className="flex flex-col gap-6">
        {/* Brand Logo & Title */}
        <div className="flex items-center gap-3 px-1">
          <img
            alt="VHT Logo"
            className="h-8 w-auto object-contain"
            src="https://lh3.googleusercontent.com/aida/AEtjO1UZafK_n-Uva0ffZvY1SBXryENwQo2N-B2hs-sO41-fhIc-a73VEYO6EzX3aK2-5Q9m3B-lUEREMjam5wEOFtb_0ixnn0RJgFC24ey2eP0eJoLARQxzrlubvEpplSm2TkBuWVahcxTBdC-Dhm15rbXqpkgsbtm-O_F42MNDopsawj0K4F97dfboP2UXQVLO2m4-j1sN3vTHNUdB3Gw2ml3o5vp3HN3u69LmGuvHsLCz8aUxRynYHtONXw"
          />
          <div className="flex flex-col">
            <span className="font-semibold text-[16px] text-on-surface leading-tight">VHT Quản Lý Trọ</span>
            <span className="text-[11px] font-semibold text-on-surface-variant uppercase tracking-wider">
              Phân hệ vận hành
            </span>
          </div>
        </div>

        <div className="h-px w-full bg-surface-container-high" />

        {/* Primary Navigation */}
        <nav className="flex flex-col gap-1.5" aria-label="Menu chức năng">
          <button
            type="button"
            onClick={() => onTabChange('nha-tro')}
            className={`flex items-center gap-3 px-3.5 py-2.5 rounded-lg text-left text-[14px] font-medium transition-all ${
              currentTab === 'nha-tro'
                ? 'bg-primary text-on-primary font-semibold shadow-sm'
                : 'text-on-surface-variant hover:bg-surface-container hover:text-on-surface'
            }`}
          >
            <span className="material-symbols-outlined text-[20px]">apartment</span>
            <span>Nhà trọ</span>
          </button>

          <button
            type="button"
            onClick={() => onTabChange('phong')}
            className={`flex items-center gap-3 px-3.5 py-2.5 rounded-lg text-left text-[14px] font-medium transition-all ${
              currentTab === 'phong'
                ? 'bg-primary text-on-primary font-semibold shadow-sm'
                : 'text-on-surface-variant hover:bg-surface-container hover:text-on-surface'
            }`}
          >
            <span className="material-symbols-outlined text-[20px]">meeting_room</span>
            <span>Phòng</span>
          </button>

          {/* Expanded / Secondary Section */}
          <div className="my-1.5 pt-2 border-t border-surface-container-high">
            <span className="px-3.5 text-[11px] font-semibold text-outline uppercase tracking-wider block mb-1.5">
              Mở rộng
            </span>
          </div>

          <button
            type="button"
            onClick={() => onTabChange('hop-dong')}
            className={`flex items-center justify-between px-3.5 py-2.5 rounded-lg text-left text-[14px] font-medium transition-all ${
              currentTab === 'hop-dong'
                ? 'bg-primary text-on-primary font-semibold shadow-sm'
                : 'text-on-surface-variant hover:bg-surface-container hover:text-on-surface'
            }`}
          >
            <div className="flex items-center gap-3">
              <span className="material-symbols-outlined text-[20px]">description</span>
              <span>Hợp đồng</span>
            </div>
            <span className="text-[11px] font-medium bg-surface-container text-on-surface-variant px-2 py-0.5 rounded">
              3 HĐ
            </span>
          </button>

          <button
            type="button"
            onClick={() => onTabChange('khach-thue')}
            className={`flex items-center justify-between px-3.5 py-2.5 rounded-lg text-left text-[14px] font-medium transition-all ${
              currentTab === 'khach-thue'
                ? 'bg-primary text-on-primary font-semibold shadow-sm'
                : 'text-on-surface-variant hover:bg-surface-container hover:text-on-surface'
            }`}
          >
            <div className="flex items-center gap-3">
              <span className="material-symbols-outlined text-[20px]">group</span>
              <span>Khách thuê</span>
            </div>
            <span className="text-[11px] font-medium bg-surface-container text-on-surface-variant px-2 py-0.5 rounded">
              24 khách
            </span>
          </button>

          <button
            type="button"
            onClick={() => onTabChange('thung-rac')}
            className={`flex items-center justify-between px-3.5 py-2.5 rounded-lg text-left text-[14px] font-medium transition-all ${
              currentTab === 'thung-rac'
                ? 'bg-primary text-on-primary font-semibold shadow-sm'
                : 'text-on-surface-variant hover:bg-surface-container hover:text-on-surface'
            }`}
          >
            <div className="flex items-center gap-3">
              <span className="material-symbols-outlined text-[20px]">delete</span>
              <span>Thùng rác</span>
            </div>
            {deletedCount > 0 ? (
              <span className="text-[11px] font-semibold bg-error-container text-on-error-container px-2 py-0.5 rounded-full">
                {deletedCount}
              </span>
            ) : (
              <span className="text-[11px] font-medium bg-surface-container text-outline px-2 py-0.5 rounded">
                0
              </span>
            )}
          </button>
        </nav>
      </div>

      {/* System Status Footer */}
      <div className="flex flex-col gap-1.5 pt-4 border-t border-surface-container-high">
        <div className="flex items-center gap-2.5 px-3 py-2 rounded-lg bg-surface-container-low">
          <span className="material-symbols-outlined text-secondary text-[20px]">check_circle</span>
          <div className="flex flex-col">
            <span className="text-[12px] font-semibold text-on-surface">Hệ thống sẵn sàng</span>
            <span className="text-[11px] text-on-surface-variant">v1.2.0-stable</span>
          </div>
        </div>
      </div>
    </aside>
    </>
  );
};
