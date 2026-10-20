import { useState, useMemo, useEffect } from 'react';
import type {
  Tenant,
  FilterState,
  UserRole,
  SimulationState,
  Language,
} from './types/index.ts';
import { initialTenants } from './data/mockTenants.ts';
import { filterTenants, calculateMetrics, maskCCCD } from './utils/tenantLogic.ts';
import { translations } from './utils/i18n.ts';

import { Sidebar } from './components/Sidebar.tsx';
import { Header } from './components/Header.tsx';
import { TenantStats } from './components/TenantStats.tsx';
import { TenantFilters } from './components/TenantFilters.tsx';
import { TenantTable } from './components/TenantTable.tsx';
import { TenantAdmissionModal } from './components/TenantAdmissionModal.tsx';
import { TenantDetailModal } from './components/TenantDetailModal.tsx';
import { TenantEditModal } from './components/TenantEditModal.tsx';
import { SandboxPanel } from './components/SandboxPanel.tsx';
import { TenantPickerPopover } from './components/TenantPickerPopover.tsx';
import { MyProfileView } from './components/views/MyProfileView.tsx';
import { ReportsView } from './components/views/ReportsView.tsx';
import { SettingsView } from './components/views/SettingsView.tsx';

export default function App() {
  const [tenants, setTenants] = useState<Tenant[]>(initialTenants);
  const [activeTab, setActiveTab] = useState<'tenants' | 'profile' | 'reports' | 'settings'>('tenants');
  const [lang, setLang] = useState<Language>('vi');
  const [isDark, setIsDark] = useState(false);
  const [role, setRole] = useState<UserRole>('landlord');
  const [simulation, setSimulation] = useState<SimulationState>('normal');

  // Mobile sidebar state
  const [isOpenMobile, setIsOpenMobile] = useState(false);

  // Filter state
  const [filter, setFilter] = useState<FilterState>({
    search: '',
    status: 'all',
    account: 'all',
    sortBy: 'date_desc',
  });

  // Modal states
  const [isAdmissionOpen, setIsAdmissionOpen] = useState(false);
  const [detailTenant, setDetailTenant] = useState<Tenant | null>(null);
  const [editTenant, setEditTenant] = useState<Tenant | null>(null);
  const [isPickerOpen, setIsPickerOpen] = useState(false);

  // Toast feedback
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3500);
  };

  // Sync dark class on document element
  useEffect(() => {
    if (isDark) {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }, [isDark]);

  const t = translations[lang];

  // Filtered tenants calculation
  const filteredTenants = useMemo(() => {
    if (simulation === 'empty') return [];
    return filterTenants(tenants, filter);
  }, [tenants, filter, simulation]);

  // Overall metrics calculation
  const metrics = useMemo(() => {
    return calculateMetrics(tenants);
  }, [tenants]);

  // Handlers
  const handleAddTenant = (newTenant: Tenant) => {
    setTenants((prev) => [newTenant, ...prev]);
    showToast(
      lang === 'vi'
        ? `Đã tiếp nhận hồ sơ khách thuê ${newTenant.name} (${newTenant.code}) thành công`
        : `Admitted tenant ${newTenant.name} (${newTenant.code}) successfully`
    );
  };

  const handleUpdateTenant = (updated: Tenant) => {
    setTenants((prev) => prev.map((item) => (item.id === updated.id ? updated : item)));
    showToast(
      lang === 'vi'
        ? `Đã cập nhật hồ sơ khách thuê ${updated.name}`
        : `Updated tenant profile ${updated.name}`
    );
  };

  const handleToggleStatus = (tenant: Tenant) => {
    const newStatus = tenant.status === 'active' ? 'inactive' : 'active';
    setTenants((prev) =>
      prev.map((item) => (item.id === tenant.id ? { ...item, status: newStatus } : item))
    );
    showToast(
      lang === 'vi'
        ? `Đã chuyển trạng thái ${tenant.name} sang ${
            newStatus === 'active' ? 'Đang sử dụng' : 'Ngừng sử dụng'
          }`
        : `Status of ${tenant.name} updated to ${newStatus}`
    );
  };

  // Export CSV handler
  const handleExportCSV = () => {
    const headers = ['Mã khách', 'Họ tên', 'Số điện thoại', 'CCCD (D9)', 'Phòng', 'Trạng thái', 'Tài khoản app'];
    const rows = filteredTenants.map((item) => {
      const { maskedPart, last4 } = maskCCCD(item.cccd);
      return [
        item.code,
        `"${item.name}"`,
        item.phone,
        `"${maskedPart}${last4}"`,
        `"${item.room}"`,
        item.status === 'active' ? 'Đang sử dụng' : 'Ngừng sử dụng',
        item.hasApp ? 'Có tài khoản' : 'Chưa có tài khoản',
      ].join(',');
    });

    const csvContent = 'data:text/csv;charset=utf-8,\uFEFF' + [headers.join(','), ...rows].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `danh_sach_khach_thue_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    showToast(
      lang === 'vi'
        ? `Đã xuất ${filteredTenants.length} hồ sơ khách thuê thành công (CSV)`
        : `Exported ${filteredTenants.length} tenant records (CSV)`
    );
  };

  return (
    <div className="bg-[#f7f9fb] dark:bg-[#121416] text-[#191c1e] dark:text-neutral-100 min-h-screen antialiased transition-colors flex">
      {/* Toast popup */}
      {toastMessage && (
        <div className="fixed top-20 right-8 z-50 bg-[#191c1e] dark:bg-white text-white dark:text-[#191c1e] px-4 py-2.5 rounded-xl shadow-xl flex items-center gap-2.5 text-xs font-semibold animate-in fade-in slide-in-from-top-3 duration-200">
          <span className="material-symbols-outlined text-[#059669] text-[18px]">
            check_circle
          </span>
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Sidebar Navigation */}
      <Sidebar
        activeTab={activeTab}
        setActiveTab={setActiveTab}
        lang={lang}
        isOpenMobile={isOpenMobile}
        setIsOpenMobile={setIsOpenMobile}
      />

      <div className="md:pl-[240px] pl-0 flex-1 flex flex-col min-w-0">
        {/* Fixed Header */}
        <Header
          activeTab={activeTab}
          setActiveTab={setActiveTab}
          lang={lang}
          setLang={setLang}
          isDark={isDark}
          setIsDark={setIsDark}
          onToggleMobileSidebar={() => setIsOpenMobile(!isOpenMobile)}
        />

        {/* Offline notification banner if simulated */}
        {simulation === 'offline' && (
          <div className="mt-16 bg-[#ba1a1a] text-white px-8 py-2 text-xs font-semibold flex items-center justify-center gap-2">
            <span className="material-symbols-outlined text-[16px]">wifi_off</span>
            <span>{t.offline_banner}</span>
          </div>
        )}

        {/* Conflict simulation banner */}
        {simulation === 'conflict' && (
          <div className="mt-16 bg-amber-500 text-neutral-950 px-8 py-2 text-xs font-bold flex items-center justify-center gap-2">
            <span className="material-symbols-outlined text-[16px]">warning</span>
            <span>
              {t.conflict_title}: {t.conflict_desc}
            </span>
            <button
              onClick={() => setSimulation('normal')}
              className="underline ml-2 cursor-pointer font-bold"
            >
              Tải lại phiên mới
            </button>
          </div>
        )}

        {/* Main Content View Container */}
        <main
          className={`relative w-full px-8 py-8 ${
            simulation === 'offline' || simulation === 'conflict' ? 'pt-6' : 'pt-24'
          }`}
        >
          {activeTab === 'profile' && <MyProfileView lang={lang} />}
          {activeTab === 'reports' && <ReportsView metrics={metrics} lang={lang} />}
          {activeTab === 'settings' && <SettingsView lang={lang} />}

          {activeTab === 'tenants' && (
            <div className="flex flex-col w-full gap-6">
              {/* Top Banner / Metric Overview Strip */}
              <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-4">
                <div className="flex flex-col gap-1">
                  <div className="flex items-center gap-2">
                    <h1
                      className="text-2xl font-bold text-[#191c1e] dark:text-neutral-100 tracking-tight"
                      data-i18n="title"
                    >
                      {t.title}
                    </h1>
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-[#eceef0] dark:bg-neutral-800 text-[11px] font-mono text-[#565e74] dark:text-neutral-400 font-medium">
                      <span className="w-1.5 h-1.5 rounded-full bg-[#777587]"></span>
                      <span data-i18n="sample_data">{t.sample_data}</span>
                    </span>
                  </div>
                  <p className="text-sm text-[#565e74] dark:text-neutral-400" data-i18n="subtitle">
                    {t.subtitle}
                  </p>
                </div>

                {/* Action Bar */}
                <div className="flex items-center gap-2 self-stretch sm:self-auto justify-end">
                  <button
                    onClick={handleExportCSV}
                    className="inline-flex items-center justify-center gap-1.5 px-3.5 py-2 rounded-xl bg-white dark:bg-[#191c1e] text-[#191c1e] dark:text-neutral-100 hover:bg-[#f2f4f6] dark:hover:bg-neutral-800 border border-neutral-100 dark:border-neutral-800 shadow-sm text-xs font-semibold transition-colors cursor-pointer"
                    type="button"
                  >
                    <span className="material-symbols-outlined text-[18px] text-[#565e74] dark:text-neutral-400">
                      file_download
                    </span>
                    <span data-i18n="export">{t.export}</span>
                  </button>

                  {(role === 'landlord' || role === 'manager' || role === 'admin') && (
                    <button
                      onClick={() => setIsAdmissionOpen(true)}
                      className="inline-flex items-center justify-center gap-1.5 px-4 py-2 rounded-xl bg-[#4f46e5] text-white hover:bg-[#3525cd] shadow-sm text-xs font-semibold transition-all active:scale-[0.98] cursor-pointer"
                      id="btn-add-tenant"
                      type="button"
                    >
                      <span className="material-symbols-outlined text-[18px]">person_add</span>
                      <span data-i18n="btn_admit">{t.btn_admit}</span>
                    </button>
                  )}
                </div>
              </div>

              {/* Bento Metric Cards */}
              <TenantStats metrics={metrics} lang={lang} />

              {/* Filter & Search Command Panel */}
              <TenantFilters filter={filter} setFilter={setFilter} lang={lang} />

              {/* Loading State Simulation */}
              {simulation === 'loading' && (
                <div className="p-8 rounded-xl bg-white dark:bg-[#191c1e] border border-neutral-100 dark:border-neutral-800 flex flex-col gap-4 animate-pulse">
                  <div className="h-6 bg-neutral-200 dark:bg-neutral-800 rounded-md w-1/4"></div>
                  <div className="space-y-3">
                    {Array.from({ length: 6 }).map((_, i) => (
                      <div key={i} className="h-12 bg-neutral-100 dark:bg-neutral-800/60 rounded-xl"></div>
                    ))}
                  </div>
                </div>
              )}

              {/* Error State Simulation */}
              {simulation === 'error' && (
                <div className="p-8 rounded-xl bg-white dark:bg-[#191c1e] border border-neutral-100 dark:border-neutral-800 flex flex-col items-center justify-center gap-3 text-center py-12">
                  <div className="w-12 h-12 rounded-full bg-[#ba1a1a]/10 text-[#ba1a1a] flex items-center justify-center">
                    <span className="material-symbols-outlined text-[28px]">error</span>
                  </div>
                  <h3 className="text-base font-bold text-[#191c1e] dark:text-neutral-100">
                    {t.error_title}
                  </h3>
                  <p className="text-xs text-[#565e74] dark:text-neutral-400 max-w-sm">
                    {t.error_desc}
                  </p>
                  <button
                    onClick={() => setSimulation('normal')}
                    className="mt-2 px-4 py-2 rounded-xl bg-[#4f46e5] text-white text-xs font-semibold hover:bg-[#3525cd] transition-colors cursor-pointer"
                  >
                    {t.btn_retry}
                  </button>
                </div>
              )}

              {/* Empty State Simulation or No Results */}
              {simulation !== 'loading' && simulation !== 'error' && filteredTenants.length === 0 && (
                <div className="p-12 rounded-xl bg-white dark:bg-[#191c1e] border border-neutral-100 dark:border-neutral-800 flex flex-col items-center justify-center gap-3 text-center">
                  <div className="w-14 h-14 rounded-full bg-[#f2f4f6] dark:bg-neutral-800 flex items-center justify-center text-[#565e74] dark:text-neutral-400">
                    <span className="material-symbols-outlined text-[32px]">folder_off</span>
                  </div>
                  <h3 className="text-base font-bold text-[#191c1e] dark:text-neutral-100">
                    {t.empty_title}
                  </h3>
                  <p className="text-xs text-[#565e74] dark:text-neutral-400 max-w-md">
                    {t.empty_desc}
                  </p>
                  <div className="flex items-center gap-2 mt-2">
                    <button
                      onClick={() =>
                        setFilter({
                          search: '',
                          status: 'all',
                          account: 'all',
                          sortBy: 'date_desc',
                        })
                      }
                      className="px-3.5 py-2 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-xs font-semibold text-[#191c1e] dark:text-neutral-100 hover:bg-[#e6e8ea] dark:hover:bg-neutral-700 transition-colors cursor-pointer"
                    >
                      {t.btn_reset}
                    </button>
                    {(role === 'landlord' || role === 'manager' || role === 'admin') && (
                      <button
                        onClick={() => setIsAdmissionOpen(true)}
                        className="px-4 py-2 rounded-xl bg-[#4f46e5] text-white text-xs font-semibold hover:bg-[#3525cd] transition-colors cursor-pointer"
                      >
                        {t.btn_admit}
                      </button>
                    )}
                  </div>
                </div>
              )}

              {/* Main Tenants Table (Normal state) */}
              {simulation !== 'loading' && simulation !== 'error' && filteredTenants.length > 0 && (
                <TenantTable
                  tenants={filteredTenants}
                  totalTenantsCount={tenants.length}
                  lang={lang}
                  role={role}
                  onViewTenant={(tenant) => setDetailTenant(tenant)}
                  onEditTenant={(tenant) => setEditTenant(tenant)}
                  onToggleStatus={handleToggleStatus}
                />
              )}
            </div>
          )}
        </main>
      </div>

      {/* Floating Sandbox State & RBAC Panel */}
      <SandboxPanel
        role={role}
        setRole={setRole}
        simulation={simulation}
        setSimulation={setSimulation}
        onOpenPicker={() => setIsPickerOpen(true)}
        lang={lang}
      />

      {/* Admission Modal */}
      <TenantAdmissionModal
        isOpen={isAdmissionOpen}
        onClose={() => setIsAdmissionOpen(false)}
        onSave={handleAddTenant}
        existingTenants={tenants}
        lang={lang}
      />

      {/* Detail Modal */}
      <TenantDetailModal
        tenant={detailTenant}
        isOpen={detailTenant !== null}
        onClose={() => setDetailTenant(null)}
        onEdit={(tenant) => setEditTenant(tenant)}
        lang={lang}
      />

      {/* Edit Modal */}
      <TenantEditModal
        tenant={editTenant}
        isOpen={editTenant !== null}
        onClose={() => setEditTenant(null)}
        onSave={handleUpdateTenant}
        lang={lang}
      />

      {/* Quick Tenant Picker Popover */}
      <TenantPickerPopover
        isOpen={isPickerOpen}
        onClose={() => setIsPickerOpen(false)}
        tenants={tenants}
        onSelect={(tenant) => {
          setDetailTenant(tenant);
          showToast(
            lang === 'vi'
              ? `Đã chọn khách ${tenant.name} (${tenant.code})`
              : `Selected tenant ${tenant.name} (${tenant.code})`
          );
        }}
        lang={lang}
      />
    </div>
  );
}
