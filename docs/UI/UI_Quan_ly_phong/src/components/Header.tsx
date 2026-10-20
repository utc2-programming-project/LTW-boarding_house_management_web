import React from 'react';
import type { UserRole } from '../types/index.ts';

interface HeaderProps {
  currentRole: UserRole;
  language: 'VI' | 'EN';
  onLanguageChange: (lang: 'VI' | 'EN') => void;
  isDarkMode: boolean;
  onToggleDarkMode: () => void;
  breadcrumbSub: string;
  onOpenMobile?: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  currentRole,
  language,
  onLanguageChange,
  isDarkMode,
  onToggleDarkMode,
  breadcrumbSub,
  onOpenMobile,
}) => {
  return (
    <header className="fixed top-0 left-0 lg:left-64 right-0 h-16 bg-surface-container-lowest/90 backdrop-blur-md shadow-[0_1px_8px_rgba(0,0,0,0.04)] z-40 flex items-center justify-between px-4 sm:px-8 border-b border-surface-container transition-all">
      {/* Breadcrumb & Subsystem */}
      <div className="flex items-center gap-3">
        {/* Mobile Hamburger Button */}
        <button
          type="button"
          aria-label="Mở menu điều hướng"
          onClick={onOpenMobile}
          className="p-1.5 rounded-lg text-on-surface-variant hover:bg-surface-container lg:hidden"
        >
          <span className="material-symbols-outlined text-[24px]">menu</span>
        </button>

        <img
          alt="VHT Logo"
          className="h-7 sm:h-8 w-auto object-contain hidden xs:block"
          src="https://lh3.googleusercontent.com/aida/AEtjO1UZafK_n-Uva0ffZvY1SBXryENwQo2N-B2hs-sO41-fhIc-a73VEYO6EzX3aK2-5Q9m3B-lUEREMjam5wEOFtb_0ixnn0RJgFC24ey2eP0eJoLARQxzrlubvEpplSm2TkBuWVahcxTBdC-Dhm15rbXqpkgsbtm-O_F42MNDopsawj0K4F97dfboP2UXQVLO2m4-j1sN3vTHNUdB3Gw2ml3o5vp3HN3u69LmGuvHsLCz8aUxRynYHtONXw"
        />
        <div className="flex items-center gap-2 text-on-surface-variant text-[13px]">
          <span className="hover:text-on-surface transition-colors cursor-pointer font-normal">
            Tổng quan
          </span>
          <span className="material-symbols-outlined text-[16px] text-outline">chevron_right</span>
          <span className="text-on-surface text-[14px] font-medium">{breadcrumbSub}</span>
        </div>
      </div>

      {/* Right controls: Language, Theme mode, User badge */}
      <div className="flex items-center gap-4">
        {/* Language selector toggle */}
        <div
          role="group"
          aria-label="Chọn ngôn ngữ"
          className="flex items-center bg-surface-container-low rounded-lg p-1 border border-surface-container"
        >
          <button
            type="button"
            aria-pressed={language === 'VI'}
            onClick={() => onLanguageChange('VI')}
            className={`px-2.5 py-0.5 rounded text-[12px] font-semibold transition-all ${
              language === 'VI'
                ? 'text-on-surface bg-surface-container-lowest shadow-sm'
                : 'text-on-surface-variant hover:text-on-surface'
            }`}
          >
            VI
          </button>
          <button
            type="button"
            aria-pressed={language === 'EN'}
            onClick={() => onLanguageChange('EN')}
            className={`px-2.5 py-0.5 rounded text-[12px] font-semibold transition-all ${
              language === 'EN'
                ? 'text-on-surface bg-surface-container-lowest shadow-sm'
                : 'text-on-surface-variant hover:text-on-surface'
            }`}
          >
            EN
          </button>
        </div>

        {/* Theme mode toggle */}
        <button
          type="button"
          aria-label="Chuyển chế độ giao diện sáng tối"
          onClick={onToggleDarkMode}
          className="w-9 h-9 rounded-lg flex items-center justify-center text-on-surface-variant hover:bg-surface-container hover:text-on-surface transition-colors border border-transparent hover:border-surface-container"
        >
          <span className="material-symbols-outlined text-[20px]">
            {isDarkMode ? 'light_mode' : 'dark_mode'}
          </span>
        </button>

        <div className="h-5 w-px bg-surface-container-high" />

        {/* User Profile Badge */}
        <div
          className="flex items-center gap-3 pl-1 cursor-pointer group"
          title={`Đăng nhập với vai trò: ${currentRole}`}
        >
          <div className="flex flex-col text-right hidden sm:flex">
            <span className="text-[13px] font-semibold text-on-surface group-hover:text-primary transition-colors">
              Ban Quản Trị
            </span>
            <span className="text-[11px] text-on-surface-variant">admin@vht.vn</span>
          </div>
          <div className="w-8 h-8 rounded-full bg-primary flex items-center justify-center text-on-primary shadow-sm ring-2 ring-primary/20">
            <span className="material-symbols-outlined text-[18px]">person</span>
          </div>
        </div>
      </div>
    </header>
  );
};
