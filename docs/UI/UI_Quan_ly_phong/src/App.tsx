import React, { useState, useMemo } from 'react';
import type {
  Room,
  RoomFilters,
  UserRole,
  SimulationState,
  AppNavTab,
} from './types/index.ts';
import {
  INITIAL_ROOMS,
  INITIAL_PROPERTIES,
  INITIAL_CONTRACTS,
  INITIAL_TENANTS,
} from './data/initialData.ts';
import {
  filterRooms,
  calculateKpiMetrics,
  exportRoomsToCsv,
} from './utils/roomCalculations.ts';
import { Sidebar } from './components/Sidebar.tsx';
import { Header } from './components/Header.tsx';
import { KpiMetrics } from './components/KpiMetrics.tsx';
import { FilterToolbar } from './components/FilterToolbar.tsx';
import { RoomTable } from './components/RoomTable.tsx';
import { FloatingDemoPanel } from './components/FloatingDemoPanel.tsx';
import { AddEditRoomModal } from './components/AddEditRoomModal.tsx';
import { RoomDetailModal } from './components/RoomDetailModal.tsx';
import { ConfirmDeleteModal } from './components/ConfirmDeleteModal.tsx';
import { PropertiesView } from './components/PropertiesView.tsx';
import { ContractsView } from './components/ContractsView.tsx';
import { TenantsView } from './components/TenantsView.tsx';
import { TrashView } from './components/TrashView.tsx';

export default function App() {
  // Navigation & Localization State
  const [currentTab, setCurrentTab] = useState<AppNavTab>('phong');
  const [language, setLanguage] = useState<'VI' | 'EN'>('VI');
  const [isDarkMode, setIsDarkMode] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  // Core Data State
  const [rooms, setRooms] = useState<Room[]>(INITIAL_ROOMS);
  const [properties] = useState(INITIAL_PROPERTIES);
  const [contracts] = useState(INITIAL_CONTRACTS);
  const [tenants] = useState(INITIAL_TENANTS);

  // Demo & Simulation States
  const [currentRole, setCurrentRole] = useState<UserRole>('Chủ trọ');
  const [sw1Locked, setSw1Locked] = useState(false);
  const [sw2PriceCheck, setSw2PriceCheck] = useState(true);
  const [sw3Compact, setSw3Compact] = useState(false);
  const [simulationState, setSimulationState] = useState<SimulationState>('normal');

  // Filter State
  const [filters, setFilters] = useState<RoomFilters>({
    searchQuery: '',
    property: '',
    status: '',
    activeStatus: '',
    minPrice: null,
    maxPrice: null,
    onlyAvailable: false,
    showLockedRooms: false,
  });

  // Modal States
  const [isAddEditModalOpen, setIsAddEditModalOpen] = useState(false);
  const [editingRoom, setEditingRoom] = useState<Room | null>(null);
  const [viewingRoom, setViewingRoom] = useState<Room | null>(null);
  const [deletingRoom, setDeletingRoom] = useState<Room | null>(null);

  // Toast Notification
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => {
      setToastMessage(null);
    }, 3200);
  };

  // Sync SW-01 toggle with filters
  const handleToggleSw1 = (val: boolean) => {
    setSw1Locked(val);
    setFilters((prev) => ({ ...prev, showLockedRooms: val }));
  };

  // Toggle Dark Mode
  const toggleDarkMode = () => {
    setIsDarkMode((prev) => {
      const next = !prev;
      if (next) {
        document.documentElement.classList.add('dark');
      } else {
        document.documentElement.classList.remove('dark');
      }
      return next;
    });
  };

  // Filtered rooms calculation
  const filteredRooms = useMemo(() => {
    return filterRooms(rooms, filters);
  }, [rooms, filters]);

  // KPI Metrics Calculation (calculated from active filtered rooms for live responsiveness)
  const kpiMetrics = useMemo(() => {
    // If no active property/search filter, show baseline metrics for all rooms
    const baseList =
      !filters.property && !filters.searchQuery && filters.minPrice === null && filters.maxPrice === null
        ? rooms.filter((r) => !r.isDeleted)
        : filteredRooms;
    return calculateKpiMetrics(baseList);
  }, [rooms, filteredRooms, filters]);

  // List of unique property names
  const propertyNames = useMemo(() => {
    return Array.from(new Set(rooms.map((r) => r.property).filter(Boolean)));
  }, [rooms]);

  // Deleted rooms count for trash badge
  const deletedRooms = useMemo(() => {
    return rooms.filter((r) => r.isDeleted);
  }, [rooms]);

  // Action Handlers
  const handleSaveRoom = (roomData: Partial<Room>) => {
    if (editingRoom) {
      setRooms((prev) =>
        prev.map((r) => (r.id === editingRoom.id ? { ...r, ...roomData } : r))
      );
      showToast(`Đã cập nhật thông tin phòng ${roomData.code}`);
    } else {
      const newRoom: Room = {
        id: `room-${Date.now()}`,
        code: roomData.code || 'P.000',
        property: roomData.property || 'An Phú Building',
        floor: roomData.floor ?? null,
        area: roomData.area ?? null,
        price: roomData.price ?? 3000000,
        status: roomData.status || 'Trống',
        activeStatus: roomData.activeStatus || 'Đang hoạt động',
        approvalStatus: roomData.approvalStatus,
        isLocked: roomData.isLocked,
        notes: roomData.notes,
        isDeleted: false,
      };
      setRooms((prev) => [newRoom, ...prev]);
      showToast(`Đã thêm mới phòng ${newRoom.code} thành công`);
    }
    setEditingRoom(null);
  };

  const handleConfirmDelete = (room: Room) => {
    setRooms((prev) =>
      prev.map((r) => (r.id === room.id ? { ...r, isDeleted: true } : r))
    );
    showToast(`Đã chuyển phòng ${room.code} vào Thùng rác`);
  };

  const handleRestoreRoom = (roomId: string) => {
    setRooms((prev) =>
      prev.map((r) => (r.id === roomId ? { ...r, isDeleted: false } : r))
    );
    showToast('Đã khôi phục phòng thành công');
  };

  const handlePermanentDelete = (roomId: string) => {
    setRooms((prev) => prev.filter((r) => r.id !== roomId));
    showToast('Đã xóa vĩnh viễn phòng khỏi hệ thống');
  };

  const handleEmptyTrash = () => {
    setRooms((prev) => prev.filter((r) => !r.isDeleted));
    showToast('Đã dọn sạch thùng rác');
  };

  // Export to Excel / CSV
  const handleExportCsv = () => {
    const csvData = exportRoomsToCsv(filteredRooms);
    const blob = new Blob([csvData], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `danh_sach_phong_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    showToast(`Đã xuất ${filteredRooms.length} phòng ra tệp Excel (.csv)`);
  };

  // Helper when clicking "Xem danh sách phòng" on a property card
  const handleSelectPropertyRooms = (propName: string) => {
    setFilters((prev) => ({ ...prev, property: propName }));
    setCurrentTab('phong');
  };

  const breadcrumbSub =
    currentTab === 'phong'
      ? 'Vận hành cơ sở'
      : currentTab === 'nha-tro'
      ? 'Quản lý cơ sở'
      : currentTab === 'hop-dong'
      ? 'Hợp đồng'
      : currentTab === 'khach-thue'
      ? 'Khách thuê'
      : 'Thùng rác';

  return (
    <div className="bg-surface font-sans text-on-surface min-h-screen antialiased flex flex-col">
      {/* Toast Notification */}
      {toastMessage && (
        <div
          role="status"
          aria-live="polite"
          className="fixed top-20 right-8 z-60 bg-inverse-surface text-inverse-on-surface px-4 py-3 rounded-xl shadow-xl flex items-center gap-2.5 text-[13px] font-medium border border-outline/30 animate-in fade-in slide-in-from-top-2"
        >
          <span className="material-symbols-outlined text-secondary-container text-[20px]">
            check_circle
          </span>
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Main Left Sidebar */}
      <Sidebar
        currentTab={currentTab}
        onTabChange={(tab) => {
          setCurrentTab(tab);
          setIsMobileMenuOpen(false);
        }}
        deletedCount={deletedRooms.length}
        isOpenMobile={isMobileMenuOpen}
        onCloseMobile={() => setIsMobileMenuOpen(false)}
      />

      {/* Main Workspace Frame */}
      <div className="lg:pl-64 pl-0 flex flex-col flex-1">
        {/* Top Header */}
        <Header
          currentRole={currentRole}
          language={language}
          onLanguageChange={setLanguage}
          isDarkMode={isDarkMode}
          onToggleDarkMode={toggleDarkMode}
          breadcrumbSub={breadcrumbSub}
          onOpenMobile={() => setIsMobileMenuOpen(true)}
        />

        {/* Content Body */}
        <main className="w-full pt-16 bg-surface min-h-screen px-4 sm:px-8 py-6 flex-1">
          <div className="flex flex-col w-full gap-6 relative pb-16">
            {/* TAB: PHÒNG (Room Management - Primary Screen) */}
            {currentTab === 'phong' && (
              <>
                {/* Top Context Header & Breadcrumb */}
                <div className="flex flex-col gap-1">
                  <nav
                    aria-label="Breadcrumb"
                    className="flex items-center gap-1.5 text-on-surface-variant text-[12px] font-medium"
                  >
                    <button
                      type="button"
                      onClick={() => setCurrentTab('nha-tro')}
                      className="hover:text-primary transition-colors cursor-pointer"
                    >
                      Trang chủ
                    </button>
                    <span className="text-outline">/</span>
                    <span className="text-on-surface font-semibold">Quản lý phòng</span>
                  </nav>

                  <div className="flex flex-wrap items-center justify-between gap-4 pt-1">
                    <div className="flex items-center gap-3">
                      <h1 className="text-[32px] font-bold text-on-surface tracking-tight leading-tight">
                        Quản lý phòng
                      </h1>
                      <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-surface-container-high text-on-primary-fixed-variant text-[12px] font-semibold border border-primary-fixed">
                        <svg className="w-3.5 h-3.5 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                          <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
                        </svg>
                        <span>{currentRole}</span>
                      </span>
                    </div>

                    <div className="flex items-center gap-2.5">
                      <button
                        type="button"
                        onClick={handleExportCsv}
                        className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg bg-surface-container-lowest text-on-surface text-[13px] font-medium shadow-xs hover:bg-surface-container transition-all border border-surface-container"
                      >
                        <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                          <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                          <polyline points="7 10 12 15 17 10" />
                          <line x1="12" x2="12" y1="15" y2="3" />
                        </svg>
                        <span>Xuất Excel</span>
                      </button>

                      {(currentRole === 'Chủ trọ' || currentRole === 'Admin' || currentRole === 'Quản lý') && (
                        <button
                          id="btn-add-room"
                          type="button"
                          onClick={() => {
                            setEditingRoom(null);
                            setIsAddEditModalOpen(true);
                          }}
                          className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-primary-container text-on-primary text-[13px] font-semibold shadow-sm hover:bg-primary transition-all cursor-pointer"
                        >
                          <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                            <line x1="12" x2="12" y1="5" y2="19" />
                            <line x1="5" x2="19" y1="12" y2="12" />
                          </svg>
                          <span>Thêm phòng</span>
                        </button>
                      )}
                    </div>
                  </div>
                </div>

                {/* KPI Metric Summary */}
                <KpiMetrics metrics={kpiMetrics} />

                {/* Filter & Search Toolbar */}
                <FilterToolbar
                  filters={filters}
                  onFilterChange={setFilters}
                  filteredCount={filteredRooms.length}
                  properties={propertyNames}
                />

                {/* Primary DataTable Section */}
                <RoomTable
                  rooms={filteredRooms}
                  totalFilteredCount={filteredRooms.length}
                  isCompact={sw3Compact}
                  userRole={currentRole}
                  simulationState={simulationState}
                  onResetSimulation={() => setSimulationState('normal')}
                  onResetFilters={() =>
                    setFilters({
                      searchQuery: '',
                      property: '',
                      status: '',
                      activeStatus: '',
                      minPrice: null,
                      maxPrice: null,
                      onlyAvailable: false,
                      showLockedRooms: sw1Locked,
                    })
                  }
                  onViewRoom={(room) => setViewingRoom(room)}
                  onEditRoom={(room) => {
                    setEditingRoom(room);
                    setIsAddEditModalOpen(true);
                  }}
                  onDeleteRoom={(room) => setDeletingRoom(room)}
                />

                {/* Discreet Sample Data Label in Corner */}
                <div className="flex items-center justify-between px-1 text-outline text-[12px] pt-1">
                  <div className="flex items-center gap-2">
                    <span className="w-2 h-2 rounded-full bg-secondary" />
                    <span>Hệ thống phân hệ VHT-16 P2 • Cơ sở dữ liệu mẫu</span>
                  </div>
                  <span>Phiên bản giao diện June.so UltraClean</span>
                </div>
              </>
            )}

            {/* TAB: NHÀ TRỌ (Properties) */}
            {currentTab === 'nha-tro' && (
              <PropertiesView
                properties={properties}
                onSelectPropertyRooms={handleSelectPropertyRooms}
              />
            )}

            {/* TAB: HỢP ĐỒNG (Contracts) */}
            {currentTab === 'hop-dong' && <ContractsView contracts={contracts} />}

            {/* TAB: KHÁCH THUÊ (Tenants) */}
            {currentTab === 'khach-thue' && <TenantsView tenants={tenants} />}

            {/* TAB: THÙNG RÁC (Trash) */}
            {currentTab === 'thung-rac' && (
              <TrashView
                deletedRooms={deletedRooms}
                onRestoreRoom={handleRestoreRoom}
                onPermanentDelete={handlePermanentDelete}
                onEmptyTrash={handleEmptyTrash}
              />
            )}
          </div>
        </main>
      </div>

      {/* Floating Demo Control Panel */}
      <FloatingDemoPanel
        currentRole={currentRole}
        onRoleChange={setCurrentRole}
        sw1Locked={sw1Locked}
        onToggleSw1={handleToggleSw1}
        sw2PriceCheck={sw2PriceCheck}
        onToggleSw2={setSw2PriceCheck}
        sw3Compact={sw3Compact}
        onToggleSw3={setSw3Compact}
        simulationState={simulationState}
        onSetSimulationState={setSimulationState}
      />

      {/* Modals */}
      <AddEditRoomModal
        isOpen={isAddEditModalOpen}
        onClose={() => {
          setIsAddEditModalOpen(false);
          setEditingRoom(null);
        }}
        onSave={handleSaveRoom}
        initialData={editingRoom}
        properties={propertyNames}
        enablePriceCheck={sw2PriceCheck}
      />

      <RoomDetailModal
        room={viewingRoom}
        onClose={() => setViewingRoom(null)}
        onEdit={(room) => {
          setViewingRoom(null);
          setEditingRoom(room);
          setIsAddEditModalOpen(true);
        }}
        userRole={currentRole}
      />

      <ConfirmDeleteModal
        room={deletingRoom}
        isOpen={Boolean(deletingRoom)}
        onClose={() => setDeletingRoom(null)}
        onConfirm={handleConfirmDelete}
      />
    </div>
  );
}
