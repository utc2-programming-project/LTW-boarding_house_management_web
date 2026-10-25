import React from 'react';
import { Menu, Sun, Moon, LogOut, ChevronDown, Check } from 'lucide-react';
import { UserRole } from '../types';

interface TopbarProps {
  onOpenMobileMenu: () => void;
  currentRole: UserRole;
  onRoleChange: (role: UserRole) => void;
  isDarkMode: boolean;
  onToggleDarkMode: () => void;
}

export const Topbar: React.FC<TopbarProps> = ({
  onOpenMobileMenu,
  currentRole,
  onRoleChange,
  isDarkMode,
  onToggleDarkMode,
}) => {
  const [langMenuOpen, setLangMenuOpen] = React.useState(false);
  const [currentLang, setCurrentLang] = React.useState<'VI' | 'EN'>('VI');
  const [profileOpen, setProfileOpen] = React.useState(false);

  return (
    <header className="sticky top-0 z-30 h-16 bg-white border-b border-slate-200 px-4 md:px-6 flex items-center justify-between">
      {/* Left: Mobile hamburger & title */}
      <div className="flex items-center gap-3">
        <button
          onClick={onOpenMobileMenu}
          className="p-2 -ml-2 text-slate-600 hover:text-slate-900 lg:hidden rounded-lg hover:bg-slate-100"
          aria-label="Mở menu"
        >
          <Menu className="w-5 h-5" />
        </button>
        <span className="font-semibold text-slate-800 text-base md:text-lg">
          Web Vận Hành Nhà Trọ
        </span>
      </div>

      {/* Right controls */}
      <div className="flex items-center gap-2 md:gap-3">
        {/* Role Switcher Pill Group */}
        <div className="hidden sm:flex items-center p-1 bg-slate-100 rounded-lg text-xs font-medium">
          <button
            onClick={() => onRoleChange('landlord')}
            className={`px-3 py-1.5 rounded-md transition-all ${
              currentRole === 'landlord'
                ? 'bg-white text-indigo-700 font-semibold shadow-xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Chủ trọ
          </button>
          <button
            onClick={() => onRoleChange('manager')}
            className={`px-3 py-1.5 rounded-md transition-all ${
              currentRole === 'manager'
                ? 'bg-white text-indigo-700 font-semibold shadow-xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Quản lý
          </button>
          <button
            onClick={() => onRoleChange('tenant')}
            className={`px-3 py-1.5 rounded-md transition-all ${
              currentRole === 'tenant'
                ? 'bg-white text-indigo-700 font-semibold shadow-xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Khách thuê
          </button>
        </div>

        {/* Theme toggle */}
        <button
          onClick={onToggleDarkMode}
          className="p-2 text-slate-500 hover:text-slate-700 rounded-lg hover:bg-slate-100 transition-colors"
          title="Chuyển chế độ sáng / tối"
          aria-label="Chuyển chế độ sáng / tối"
        >
          {isDarkMode ? <Moon className="w-4 h-4 text-amber-500" /> : <Sun className="w-4 h-4" />}
        </button>

        {/* Language dropdown */}
        <div className="relative">
          <button
            onClick={() => setLangMenuOpen(!langMenuOpen)}
            className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium text-slate-700 border border-slate-200 rounded-lg hover:bg-slate-50"
            aria-label="Chọn ngôn ngữ"
          >
            <span>{currentLang}</span>
            <ChevronDown className="w-3.5 h-3.5 text-slate-400" />
          </button>

          {langMenuOpen && (
            <div className="absolute right-0 mt-1 w-28 bg-white border border-slate-200 rounded-lg shadow-lg py-1 z-50 animate-fadeIn">
              <button
                onClick={() => {
                  setCurrentLang('VI');
                  setLangMenuOpen(false);
                }}
                className="w-full px-3 py-1.5 text-left text-xs flex items-center justify-between text-slate-700 hover:bg-slate-50"
              >
                <span>Tiếng Việt</span>
                {currentLang === 'VI' && <Check className="w-3.5 h-3.5 text-indigo-600" />}
              </button>
              <button
                onClick={() => {
                  setCurrentLang('EN');
                  setLangMenuOpen(false);
                }}
                className="w-full px-3 py-1.5 text-left text-xs flex items-center justify-between text-slate-700 hover:bg-slate-50"
              >
                <span>English</span>
                {currentLang === 'EN' && <Check className="w-3.5 h-3.5 text-indigo-600" />}
              </button>
            </div>
          )}
        </div>

        {/* User profile */}
        <div className="relative">
          <button
            onClick={() => setProfileOpen(!profileOpen)}
            className="flex items-center gap-2 pl-2 pr-1 py-1 rounded-full hover:bg-slate-100 transition-colors"
          >
            <div className="w-8 h-8 rounded-full bg-indigo-600 text-white flex items-center justify-center font-bold text-xs shadow-xs">
              NV
            </div>
            <span className="hidden md:inline-block text-sm font-medium text-slate-700">
              Nguyễn Văn An
            </span>
            <LogOut className="w-4 h-4 text-slate-400 hover:text-slate-600 ml-1 hidden sm:block" />
          </button>

          {profileOpen && (
            <div className="absolute right-0 mt-2 w-56 bg-white border border-slate-200 rounded-xl shadow-lg p-2 z-50 animate-fadeIn">
              <div className="p-2 border-b border-slate-100">
                <p className="font-semibold text-sm text-slate-800">Nguyễn Văn An</p>
                <p className="text-xs text-slate-500">dinhhoangphuc264@gmail.com</p>
                <span className="mt-1 inline-block px-2 py-0.5 text-[11px] font-medium bg-indigo-50 text-indigo-700 rounded-md">
                  {currentRole === 'landlord'
                    ? 'Chủ trọ (Toàn quyền)'
                    : currentRole === 'manager'
                    ? 'Quản lý vận hành'
                    : 'Khách thuê phòng'}
                </span>
              </div>
              <div className="pt-1">
                <button
                  onClick={() => setProfileOpen(false)}
                  className="w-full px-3 py-2 text-left text-xs text-rose-600 hover:bg-rose-50 rounded-lg flex items-center gap-2"
                >
                  <LogOut className="w-3.5 h-3.5" />
                  <span>Đăng xuất tài khoản</span>
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </header>
  );
};
