import React from 'react';
import {
  LayoutDashboard,
  Building2,
  Users,
  FileText,
  Zap,
  Receipt,
  BarChart3,
  Settings,
  ChevronDown,
  ChevronRight,
  List,
  CalendarDays,
  Gauge,
  X,
  Layers,
} from 'lucide-react';

export type MainTab =
  | 'overview'
  | 'buildings'
  | 'tenants'
  | 'contracts'
  | 'services'
  | 'invoices'
  | 'reports'
  | 'settings';

export type ServiceSubTab = 'catalog' | 'pricing-period' | 'meter-reading';

interface SidebarProps {
  currentTab: MainTab;
  currentSubTab: ServiceSubTab;
  onSelectTab: (tab: MainTab) => void;
  onSelectSubTab: (subTab: ServiceSubTab) => void;
  isMobileOpen: boolean;
  onCloseMobile: () => void;
}

export const Sidebar: React.FC<SidebarProps> = ({
  currentTab,
  currentSubTab,
  onSelectTab,
  onSelectSubTab,
  isMobileOpen,
  onCloseMobile,
}) => {
  const [servicesExpanded, setServicesExpanded] = React.useState(true);

  const handleServicesClick = () => {
    onSelectTab('services');
    setServicesExpanded(!servicesExpanded);
  };

  const navItemClass = (isActive: boolean) =>
    `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
      isActive
        ? 'bg-indigo-50 text-indigo-700 font-semibold'
        : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
    }`;

  const subNavItemClass = (isActive: boolean) =>
    `flex items-center gap-2 pl-9 pr-3 py-2 rounded-lg text-xs font-medium transition-colors ${
      isActive
        ? 'bg-indigo-50/80 text-indigo-700 font-semibold'
        : 'text-slate-500 hover:text-slate-800 hover:bg-slate-50'
    }`;

  return (
    <>
      {/* Mobile backdrop */}
      {isMobileOpen && (
        <div
          className="fixed inset-0 z-40 bg-slate-900/50 backdrop-blur-xs lg:hidden"
          onClick={onCloseMobile}
        />
      )}

      {/* Sidebar container */}
      <aside
        className={`fixed inset-y-0 left-0 z-50 w-64 bg-white border-r border-slate-200 flex flex-col transition-transform duration-200 ease-in-out lg:translate-x-0 ${
          isMobileOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        {/* Brand Header */}
        <div className="flex items-center justify-between p-4 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-indigo-600 text-white flex items-center justify-center shadow-xs">
              <Layers className="w-5 h-5" />
            </div>
            <div>
              <h1 className="font-bold text-slate-900 text-base leading-tight tracking-tight">
                AnGia Stay
              </h1>
              <p className="text-xs text-slate-500 font-normal">Quản lý nhà trọ</p>
            </div>
          </div>
          <button
            onClick={onCloseMobile}
            className="p-1.5 text-slate-400 hover:text-slate-600 lg:hidden rounded-lg hover:bg-slate-100"
            aria-label="Đóng menu"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Navigation list */}
        <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-1">
          <button
            onClick={() => onSelectTab('overview')}
            className={`w-full ${navItemClass(currentTab === 'overview')}`}
          >
            <LayoutDashboard className="w-4 h-4" />
            <span>Tổng quan</span>
          </button>

          <button
            onClick={() => onSelectTab('buildings')}
            className={`w-full ${navItemClass(currentTab === 'buildings')}`}
          >
            <Building2 className="w-4 h-4" />
            <span>Tòa nhà & Phòng</span>
          </button>

          <button
            onClick={() => onSelectTab('tenants')}
            className={`w-full ${navItemClass(currentTab === 'tenants')}`}
          >
            <Users className="w-4 h-4" />
            <span>Khách thuê</span>
          </button>

          <button
            onClick={() => onSelectTab('contracts')}
            className={`w-full ${navItemClass(currentTab === 'contracts')}`}
          >
            <FileText className="w-4 h-4" />
            <span>Hợp đồng</span>
          </button>

          {/* Điện nước & Dịch vụ with sub-items */}
          <div>
            <button
              onClick={handleServicesClick}
              className={`w-full justify-between ${navItemClass(currentTab === 'services')}`}
            >
              <div className="flex items-center gap-3">
                <Zap className="w-4 h-4 text-indigo-600" />
                <span>Điện nước & Dịch vụ</span>
              </div>
              {servicesExpanded ? (
                <ChevronDown className="w-4 h-4 text-slate-400" />
              ) : (
                <ChevronRight className="w-4 h-4 text-slate-400" />
              )}
            </button>

            {servicesExpanded && (
              <div className="mt-1 space-y-0.5 animate-fadeIn">
                <button
                  onClick={() => {
                    onSelectTab('services');
                    onSelectSubTab('catalog');
                  }}
                  className={`w-full text-left ${subNavItemClass(
                    currentTab === 'services' && currentSubTab === 'catalog'
                  )}`}
                >
                  <List className="w-3.5 h-3.5" />
                  <span>Danh mục dịch vụ</span>
                </button>

                <button
                  onClick={() => {
                    onSelectTab('services');
                    onSelectSubTab('pricing-period');
                  }}
                  className={`w-full text-left ${subNavItemClass(
                    currentTab === 'services' && currentSubTab === 'pricing-period'
                  )}`}
                >
                  <CalendarDays className="w-3.5 h-3.5" />
                  <span>Bảng đơn giá theo kỳ</span>
                </button>

                <button
                  onClick={() => {
                    onSelectTab('services');
                    onSelectSubTab('meter-reading');
                  }}
                  className={`w-full text-left ${subNavItemClass(
                    currentTab === 'services' && currentSubTab === 'meter-reading'
                  )}`}
                >
                  <Gauge className="w-3.5 h-3.5" />
                  <span>Ghi chỉ số & Chi phí</span>
                </button>
              </div>
            )}
          </div>

          <button
            onClick={() => onSelectTab('invoices')}
            className={`w-full ${navItemClass(currentTab === 'invoices')}`}
          >
            <Receipt className="w-4 h-4" />
            <span>Hóa đơn & Thu chi</span>
          </button>

          <button
            onClick={() => onSelectTab('reports')}
            className={`w-full ${navItemClass(currentTab === 'reports')}`}
          >
            <BarChart3 className="w-4 h-4" />
            <span>Báo cáo</span>
          </button>

          <button
            onClick={() => onSelectTab('settings')}
            className={`w-full ${navItemClass(currentTab === 'settings')}`}
          >
            <Settings className="w-4 h-4" />
            <span>Cài đặt</span>
          </button>
        </nav>

        {/* Footer info badge */}
        <div className="p-3 border-t border-slate-100 bg-slate-50/50">
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
            <span>Hệ thống trực tuyến v2.4</span>
          </div>
        </div>
      </aside>
    </>
  );
};
