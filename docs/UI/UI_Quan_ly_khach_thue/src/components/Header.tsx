import React, { useState } from 'react';
import type { Language } from '../types/index.ts';
import { translations } from '../utils/i18n.ts';

interface HeaderProps {
  activeTab: 'tenants' | 'profile' | 'reports' | 'settings';
  setActiveTab: (tab: 'tenants' | 'profile' | 'reports' | 'settings') => void;
  lang: Language;
  setLang: (lang: Language) => void;
  isDark: boolean;
  setIsDark: (dark: boolean) => void;
  onToggleMobileSidebar: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  activeTab,
  setActiveTab,
  lang,
  setLang,
  isDark,
  setIsDark,
  onToggleMobileSidebar,
}) => {
  const t = translations[lang];
  const [showNotifications, setShowNotifications] = useState(false);
  const [unreadCount, setUnreadCount] = useState(2);

  const getBreadcrumbTitle = () => {
    switch (activeTab) {
      case 'profile':
        return t.breadcrumb_profile;
      case 'reports':
        return t.breadcrumb_reports;
      case 'settings':
        return t.breadcrumb_settings;
      default:
        return t.breadcrumb_tenants;
    }
  };

  const notifications = [
    {
      id: 'n1',
      title: lang === 'vi' ? 'Hợp đồng sắp hết hạn' : 'Lease expiring soon',
      desc: lang === 'vi' ? 'Phòng P.201 (Lê Quang Cường) hết hạn ngày 30/10' : 'Room P.201 (Le Quang Cuong) expires Oct 30',
      time: '10m',
      unread: true,
    },
    {
      id: 'n2',
      title: lang === 'vi' ? 'Khách kích hoạt app thành công' : 'Tenant app activated',
      desc: lang === 'vi' ? 'Đặng Hoàng Khánh (P.302) vừa đăng nhập tài khoản' : 'Dang Hoang Khanh (P.302) just logged in',
      time: '1h',
      unread: true,
    },
    {
      id: 'n3',
      title: lang === 'vi' ? 'Bảo mật D9' : 'D9 Security Check',
      desc: lang === 'vi' ? '100% hồ sơ CCCD đã được mã hóa chuẩn D9' : '100% tenant IDs masked per D9 standard',
      time: '2h',
      unread: false,
    },
  ];

  return (
    <header className="fixed top-0 left-0 md:left-[240px] right-0 h-16 bg-white/95 dark:bg-[#191c1e]/95 backdrop-blur-xl shadow-[0_1px_8px_rgba(0,0,0,0.04)] dark:border-b dark:border-neutral-800 z-40 flex items-center justify-between px-4 sm:px-8 transition-colors">
      {/* Mobile hamburger & Breadcrumb Navigation */}
      <div className="flex items-center gap-2">
        <button
          onClick={onToggleMobileSidebar}
          className="p-1.5 rounded-lg md:hidden text-[#565e74] hover:bg-[#f2f4f6] dark:hover:bg-neutral-800"
          type="button"
          aria-label="Toggle Menu"
        >
          <span className="material-symbols-outlined text-[22px]">menu</span>
        </button>

        <nav className="flex items-center gap-1.5 text-xs text-[#565e74] dark:text-neutral-400 font-medium">
          <button
            onClick={() => setActiveTab('tenants')}
            className="hover:text-[#191c1e] dark:hover:text-white transition-colors cursor-pointer hidden sm:inline"
          >
            {t.breadcrumb_home}
          </button>
          <span className="material-symbols-outlined text-[15px] text-[#777587] hidden sm:inline">
            chevron_right
          </span>
          <span className="text-[#191c1e] dark:text-neutral-100 font-semibold">{getBreadcrumbTitle()}</span>
        </nav>
      </div>

      {/* Header Actions */}
      <div className="flex items-center gap-2 sm:gap-3">
        {/* Language switch */}
        <div className="flex items-center p-1 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800/80">
          <button
            onClick={() => setLang('vi')}
            type="button"
            className={`px-2.5 py-1 rounded-lg text-xs font-semibold transition-all cursor-pointer ${
              lang === 'vi'
                ? 'bg-white dark:bg-neutral-700 text-[#191c1e] dark:text-white shadow-sm'
                : 'text-[#565e74] dark:text-neutral-400 hover:text-[#191c1e] dark:hover:text-white'
            }`}
          >
            VI
          </button>
          <button
            onClick={() => setLang('en')}
            type="button"
            className={`px-2.5 py-1 rounded-lg text-xs font-semibold transition-all cursor-pointer ${
              lang === 'en'
                ? 'bg-white dark:bg-neutral-700 text-[#191c1e] dark:text-white shadow-sm'
                : 'text-[#565e74] dark:text-neutral-400 hover:text-[#191c1e] dark:hover:text-white'
            }`}
          >
            EN
          </button>
        </div>

        {/* Theme mode toggle */}
        <button
          onClick={() => setIsDark(!isDark)}
          aria-label={lang === 'vi' ? 'Đổi giao diện' : 'Toggle theme'}
          className="p-2 rounded-xl text-[#464555] dark:text-neutral-300 hover:bg-[#f2f4f6] dark:hover:bg-neutral-800 transition-colors flex items-center justify-center cursor-pointer"
          type="button"
        >
          <span className="material-symbols-outlined text-[18px]">
            {isDark ? 'dark_mode' : 'light_mode'}
          </span>
        </button>

        {/* Notifications button */}
        <div className="relative">
          <button
            onClick={() => {
              setShowNotifications(!showNotifications);
              if (unreadCount > 0) setUnreadCount(0);
            }}
            aria-label={lang === 'vi' ? 'Thông báo' : 'Notifications'}
            className="p-2 rounded-xl text-[#464555] dark:text-neutral-300 hover:bg-[#f2f4f6] dark:hover:bg-neutral-800 transition-colors flex items-center justify-center relative cursor-pointer"
            type="button"
          >
            <span className="material-symbols-outlined text-[19px]">notifications</span>
            {unreadCount > 0 && (
              <span className="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-[#ba1a1a] ring-2 ring-white dark:ring-[#191c1e]"></span>
            )}
          </button>

          {/* Notifications Dropdown */}
          {showNotifications && (
            <div className="absolute right-0 mt-2 w-80 bg-white dark:bg-[#191c1e] rounded-xl shadow-xl border border-neutral-100 dark:border-neutral-800 p-3 z-50 animate-in fade-in duration-150">
              <div className="flex items-center justify-between pb-2 border-b border-neutral-100 dark:border-neutral-800">
                <span className="font-semibold text-xs text-[#191c1e] dark:text-neutral-100">
                  {lang === 'vi' ? 'Thông báo hệ thống' : 'Notifications'}
                </span>
                <span className="text-[11px] text-[#3525cd] dark:text-indigo-400 font-medium cursor-pointer">
                  {lang === 'vi' ? 'Đã xem tất cả' : 'Mark all read'}
                </span>
              </div>
              <div className="flex flex-col gap-2 mt-2">
                {notifications.map((n) => (
                  <div
                    key={n.id}
                    className="p-2 rounded-lg hover:bg-[#f2f4f6] dark:hover:bg-neutral-800 transition-colors flex flex-col gap-0.5 cursor-pointer text-left"
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-semibold text-[#191c1e] dark:text-neutral-100">
                        {n.title}
                      </span>
                      <span className="text-[10px] text-neutral-400">{n.time}</span>
                    </div>
                    <p className="text-[11px] text-[#565e74] dark:text-neutral-400">{n.desc}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* User identification badge */}
        <div
          onClick={() => setActiveTab('profile')}
          className="flex items-center gap-2.5 pl-1 sm:pl-2 cursor-pointer hover:opacity-85 transition-opacity"
        >
          <div className="w-8 h-8 rounded-full bg-[#3525cd] flex items-center justify-center shrink-0 shadow-sm">
            <span className="material-symbols-outlined text-white text-[18px]">person</span>
          </div>
          <div className="hidden md:flex flex-col text-left">
            <span className="text-xs font-semibold text-[#191c1e] dark:text-neutral-100 leading-none">
              nguyen.an@demo.vn
            </span>
            <span className="text-[11px] font-mono text-[#565e74] dark:text-neutral-400 mt-1">
              #LANDLORD-01
            </span>
          </div>
        </div>
      </div>
    </header>
  );
};
