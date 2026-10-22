import React from 'react';
import type { Language } from '../types/index.ts';
import { translations } from '../utils/i18n.ts';

interface SidebarProps {
  activeTab: 'tenants' | 'profile' | 'reports' | 'settings';
  setActiveTab: (tab: 'tenants' | 'profile' | 'reports' | 'settings') => void;
  lang: Language;
  isOpenMobile: boolean;
  setIsOpenMobile: (open: boolean) => void;
}

export const Sidebar: React.FC<SidebarProps> = ({
  activeTab,
  setActiveTab,
  lang,
  isOpenMobile,
  setIsOpenMobile,
}) => {
  const t = translations[lang];

  const navItems = [
    { id: 'tenants', label: t.nav_tenants, icon: 'group' },
    { id: 'profile', label: t.nav_profile, icon: 'badge' },
    { id: 'reports', label: t.nav_reports, icon: 'analytics' },
    { id: 'settings', label: t.nav_settings, icon: 'tune' },
  ] as const;

  const handleNavClick = (id: 'tenants' | 'profile' | 'reports' | 'settings') => {
    setActiveTab(id);
    setIsOpenMobile(false);
  };

  return (
    <>
      {/* Mobile backdrop */}
      {isOpenMobile && (
        <div
          className="fixed inset-0 z-40 bg-black/40 backdrop-blur-xs md:hidden"
          onClick={() => setIsOpenMobile(false)}
        />
      )}

      <aside
        className={`fixed left-0 top-0 h-full w-[240px] bg-white dark:bg-[#191c1e] shadow-[0_1px_8px_rgba(0,0,0,0.04)] dark:shadow-none dark:border-r dark:border-neutral-800 z-50 flex flex-col justify-between transition-transform duration-200 ${
          isOpenMobile ? 'translate-x-0' : '-translate-x-full md:translate-x-0'
        }`}
      >
        <div className="flex flex-col">
          {/* Brand Header */}
          <div className="h-16 px-6 flex items-center justify-between border-b border-transparent">
            <div
              className="flex items-center gap-2 cursor-pointer"
              onClick={() => handleNavClick('tenants')}
            >
              <div className="w-7 h-7 rounded-lg bg-[#4f46e5] flex items-center justify-center text-white font-semibold text-base shadow-sm">
                R
              </div>
              <div className="flex flex-col">
                <span className="font-semibold text-sm text-[#191c1e] dark:text-neutral-100 leading-tight tracking-tight">
                  {t.brand_name}
                </span>
                <span className="text-[12px] text-[#565e74] dark:text-neutral-400">
                  {t.brand_tagline}
                </span>
              </div>
            </div>

            {/* Mobile close button */}
            <button
              onClick={() => setIsOpenMobile(false)}
              className="md:hidden text-[#565e74] hover:text-[#191c1e] dark:hover:text-white p-1 rounded-lg"
            >
              <span className="material-symbols-outlined text-[20px]">close</span>
            </button>
          </div>

          {/* Section title */}
          <div className="px-4 py-2">
            <div className="px-2 py-1 text-[11px] font-semibold text-[#565e74] dark:text-neutral-400 uppercase tracking-wider">
              {t.nav_section_rooms}
            </div>
          </div>

          {/* Nav Items */}
          <nav className="flex flex-col gap-1 px-3">
            {navItems.map((item) => {
              const isActive = activeTab === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => handleNavClick(item.id)}
                  className={`flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors text-left cursor-pointer ${
                    isActive
                      ? 'bg-[#4f46e5] text-white shadow-sm'
                      : 'text-[#464555] dark:text-neutral-300 hover:bg-[#f2f4f6] dark:hover:bg-neutral-800/80 hover:text-[#191c1e] dark:hover:text-white'
                  }`}
                >
                  <span
                    className={`material-symbols-outlined text-[19px] ${
                      isActive ? 'text-white' : 'text-[#565e74] dark:text-neutral-400'
                    }`}
                  >
                    {item.icon}
                  </span>
                  <span>{item.label}</span>
                </button>
              );
            })}
          </nav>
        </div>

        {/* Bottom Profile & Plan Card */}
        <div className="p-4 flex flex-col gap-2 bg-white dark:bg-[#191c1e] border-t border-neutral-100 dark:border-neutral-800/60">
          <div className="flex items-center justify-between px-2 py-1">
            <span className="text-xs text-[#565e74] dark:text-neutral-400 font-medium">
              {t.plan_tier}
            </span>
            <span className="px-2 py-0.5 rounded-full bg-[#dae2fd] dark:bg-indigo-950/80 text-[11px] font-semibold text-[#3525cd] dark:text-indigo-300">
              {t.plan_name}
            </span>
          </div>

          <div
            onClick={() => handleNavClick('profile')}
            className="flex items-center gap-2.5 p-2 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800/70 hover:bg-[#e6e8ea] dark:hover:bg-neutral-800 transition-colors cursor-pointer"
          >
            <div className="w-8 h-8 rounded-full bg-[#3525cd] flex items-center justify-center shrink-0 shadow-sm">
              <span className="material-symbols-outlined text-white text-[18px]">person</span>
            </div>
            <div className="flex flex-col min-w-0 flex-1">
              <span className="text-xs font-semibold text-[#191c1e] dark:text-neutral-100 truncate">
                {t.user_name}
              </span>
              <span className="text-[11px] text-[#565e74] dark:text-neutral-400 truncate">
                {t.user_role}
              </span>
            </div>
          </div>
        </div>
      </aside>
    </>
  );
};
