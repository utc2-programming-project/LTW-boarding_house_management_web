import React, { useState } from 'react';
import { Sidebar, MainTab, ServiceSubTab } from './components/Sidebar';
import { Topbar } from './components/Topbar';
import { BannerNotice } from './components/BannerNotice';
import { ServiceCatalogView } from './components/ServiceCatalogView';
import { PriceHistoryView } from './components/PriceHistoryView';
import { MeterReadingView } from './components/MeterReadingView';
import { UpdatePriceModal } from './components/UpdatePriceModal';
import {
  OverviewView,
  BuildingsRoomsView,
  TenantsView,
  InvoicesView,
} from './components/OtherViews';
import {
  INITIAL_BUILDINGS,
  INITIAL_SERVICES,
  INITIAL_PRICE_VERSIONS,
  INITIAL_ROOMS,
  INITIAL_TENANTS,
  INITIAL_INVOICES,
} from './data/mockData';
import { ServiceItem, PriceVersion, RoomRecord, UserRole } from './types';
import { calculateElectricityTiered, calculateWaterCost } from './utils/billingCalculator';

export default function App() {
  // Navigation State
  const [currentTab, setCurrentTab] = useState<MainTab>('services');
  const [currentSubTab, setCurrentSubTab] = useState<ServiceSubTab>('catalog');
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  // App Settings & Simulation State
  const [currentRole, setCurrentRole] = useState<UserRole>('landlord');
  const [isDarkMode, setIsDarkMode] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(
    'Dữ liệu mẫu đã cập nhật — Thao tác giả lập phiên làm việc — chưa lưu cơ sở dữ liệu SQL.'
  );

  // Core Data State
  const [buildings] = useState(INITIAL_BUILDINGS);
  const [services, setServices] = useState<ServiceItem[]>(INITIAL_SERVICES);
  const [priceVersions, setPriceVersions] = useState<PriceVersion[]>(INITIAL_PRICE_VERSIONS);
  const [rooms, setRooms] = useState<RoomRecord[]>(INITIAL_ROOMS);
  const [tenants] = useState(INITIAL_TENANTS);
  const [invoices, setInvoices] = useState(INITIAL_INVOICES);

  // Selected Service for Update Price Modal (Image 1)
  const [modalService, setModalService] = useState<ServiceItem | null>(null);

  // Notifications helper
  const showToast = (msg: string) => {
    setToastMessage(msg);
  };

  // Toggle active/inactive service
  const handleToggleActive = (serviceId: string) => {
    setServices((prev) =>
      prev.map((s) => {
        if (s.id === serviceId) {
          const nextActive = !s.active;
          showToast(
            `Đã ${nextActive ? 'kích hoạt' : 'tạm ngưng'} dịch vụ "${s.name}". Cập nhật phiên làm việc.`
          );
          return { ...s, active: nextActive };
        }
        return s;
      })
    );
  };

  // Save new price version from modal (Image 1)
  const handleSavePriceVersion = (data: {
    serviceId: string;
    newRate: number;
    unit: string;
    buildingScope: string;
    effectiveFrom: string;
    effectiveTo: string;
    autoActivate: boolean;
  }) => {
    const targetService = services.find((s) => s.id === data.serviceId);
    if (!targetService) return;

    const newVersion: PriceVersion = {
      id: `pv-0${priceVersions.length + 1}`,
      serviceId: data.serviceId,
      serviceName: targetService.name,
      unit: data.unit,
      rate: data.newRate,
      rateType: 'flat',
      effectiveFrom: data.effectiveFrom,
      effectiveTo: data.effectiveTo || undefined,
      status: 'upcoming',
      buildingScope: data.buildingScope,
      buildingName: data.buildingScope,
      autoActivate: data.autoActivate,
      createdAt: new Date().toISOString().split('T')[0],
      createdBy:
        currentRole === 'landlord'
          ? 'Nguyễn Văn An (Chủ trọ)'
          : 'Trần Văn Quản (Quản lý)',
      note: 'Tạo từ màn hình Cập nhật đơn giá UI',
    };

    setPriceVersions((prev) => [newVersion, ...prev]);

    // Update service display if applicable
    setServices((prev) =>
      prev.map((s) => {
        if (s.id === data.serviceId) {
          return {
            ...s,
            unit: data.unit,
            historyCount: s.historyCount + 1,
          };
        }
        return s;
      })
    );

    showToast(
      `Đã tạo phiên bản giá mới ${data.newRate.toLocaleString(
        'vi-VN'
      )} đ cho ${targetService.name} (Hiệu lực từ ${data.effectiveFrom})`
    );
  };

  // Add new custom service
  const handleAddNewService = (newSrv: Partial<ServiceItem>) => {
    const created: ServiceItem = {
      id: `srv-${Date.now()}`,
      name: newSrv.name || 'Dịch vụ mới',
      code: `SRV-${services.length + 1}`,
      type: newSrv.type || 'fixed_monthly',
      typeLabel: newSrv.typeLabel || 'Cố định hàng tháng',
      unit: newSrv.unit || 'Phòng',
      rateType: 'flat',
      currentRate: newSrv.currentRate || 50000,
      rateDisplay: newSrv.rateDisplay || '50.000 đ',
      effectiveFrom: '01/11/2026',
      effectiveTo: null,
      active: true,
      isReadOnly: false,
      buildingId: newSrv.buildingId || 'b1',
      buildingName: newSrv.buildingName || 'Tòa nhà An Gia 01',
      historyCount: 1,
    };

    setServices((prev) => [...prev, created]);
    showToast(`Đã thêm mới dịch vụ "${created.name}" vào danh mục.`);
  };

  // Save meter reading & audit log (Image 5)
  const handleSaveMeterReading = (updatedRoom: RoomRecord, logReason?: string) => {
    setRooms((prev) =>
      prev.map((r) => {
        if (r.id === updatedRoom.id) {
          const oldElec = r.electricEnd;
          const oldWater = r.waterEnd;
          const newLogs = [...r.auditLogs];

          if (logReason && (oldElec !== updatedRoom.electricEnd || oldWater !== updatedRoom.waterEnd)) {
            newLogs.unshift({
              id: `log-${Date.now()}`,
              modifier:
                currentRole === 'landlord'
                  ? 'Nguyễn Văn An (Chủ trọ)'
                  : 'Trần Văn Quản (Quản lý)',
              role: currentRole === 'landlord' ? 'Chủ trọ' : 'Quản lý',
              timestamp: new Date().toLocaleString('vi-VN', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              }),
              fieldChanged:
                oldElec !== updatedRoom.electricEnd
                  ? `Điện cuối kỳ ${updatedRoom.periodMonth}`
                  : `Nước cuối kỳ ${updatedRoom.periodMonth}`,
              oldValue: `${oldElec} kWh`,
              newValue: `${updatedRoom.electricEnd} kWh`,
              detailDiff: `${oldElec} kWh → ${updatedRoom.electricEnd} kWh`,
              reason: `“${logReason}”`,
            });
          }

          // Recalculate bill
          const elecCalc = calculateElectricityTiered(
            Math.max(0, updatedRoom.electricEnd - updatedRoom.electricStart)
          );
          const waterCalc = calculateWaterCost(
            Math.max(0, updatedRoom.waterEnd - updatedRoom.waterStart),
            updatedRoom.waterRate
          );
          const serviceCost =
            updatedRoom.internetCost + updatedRoom.cleaningCost + updatedRoom.parkingCost;
          const totalCost = updatedRoom.roomRent + elecCalc.total + waterCalc.total + serviceCost;

          // Also update or add invoice
          setInvoices((prevInv) =>
            prevInv.map((inv) =>
              inv.roomCode === updatedRoom.roomCode
                ? {
                    ...inv,
                    electricAmount: elecCalc.total,
                    waterAmount: waterCalc.total,
                    totalAmount: totalCost,
                  }
                : inv
            )
          );

          return {
            ...updatedRoom,
            auditLogs: newLogs,
          };
        }
        return r;
      })
    );

    showToast(
      `Đã lưu chốt chỉ số công tơ phòng ${updatedRoom.roomCode}! Chi phí dự thảo đã cập nhật.`
    );
  };

  return (
    <div className={`min-h-screen bg-slate-50 ${isDarkMode ? 'dark bg-slate-900 text-slate-100' : ''}`}>
      {/* Fixed Sidebar */}
      <Sidebar
        currentTab={currentTab}
        currentSubTab={currentSubTab}
        onSelectTab={(tab) => {
          setCurrentTab(tab);
          if (tab === 'services' && currentSubTab !== 'catalog') {
            setCurrentSubTab('catalog');
          }
        }}
        onSelectSubTab={(subTab) => {
          setCurrentTab('services');
          setCurrentSubTab(subTab);
        }}
        isMobileOpen={isMobileMenuOpen}
        onCloseMobile={() => setIsMobileMenuOpen(false)}
      />

      {/* Main Content Area */}
      <div className="lg:pl-64 flex flex-col min-h-screen">
        {/* Topbar */}
        <Topbar
          onOpenMobileMenu={() => setIsMobileMenuOpen(true)}
          currentRole={currentRole}
          onRoleChange={setCurrentRole}
          isDarkMode={isDarkMode}
          onToggleDarkMode={() => setIsDarkMode(!isDarkMode)}
        />

        {/* Page Content Body */}
        <main className="flex-1 p-4 md:p-6 lg:p-8 max-w-7xl w-full mx-auto">
          {/* Banner Notice matching mockups */}
          <BannerNotice
            currentRole={currentRole}
            toastMessage={toastMessage}
            onCloseToast={() => setToastMessage(null)}
          />

          {/* Active View Routing */}
          {currentTab === 'services' && (
            <>
              {currentSubTab === 'catalog' && (
                <ServiceCatalogView
                  services={services}
                  buildings={buildings}
                  currentRole={currentRole}
                  onOpenUpdateModal={(service) => setModalService(service)}
                  onToggleActive={handleToggleActive}
                  onNavigateToHistory={() => setCurrentSubTab('pricing-period')}
                  onAddNewService={handleAddNewService}
                />
              )}

              {currentSubTab === 'pricing-period' && (
                <PriceHistoryView
                  priceVersions={priceVersions}
                  services={services}
                  onOpenUpdateModal={(service) => setModalService(service)}
                  onBackToCatalog={() => setCurrentSubTab('catalog')}
                />
              )}

              {currentSubTab === 'meter-reading' && (
                <MeterReadingView
                  rooms={rooms}
                  currentRole={currentRole}
                  onSaveReading={handleSaveMeterReading}
                  onCancel={() => setCurrentSubTab('catalog')}
                  onNotify={showToast}
                />
              )}
            </>
          )}

          {currentTab === 'overview' && (
            <OverviewView
              buildings={buildings}
              rooms={rooms}
              onNavigateToServices={() => {
                setCurrentTab('services');
                setCurrentSubTab('catalog');
              }}
              onNavigateToMeter={() => {
                setCurrentTab('services');
                setCurrentSubTab('meter-reading');
              }}
            />
          )}

          {currentTab === 'buildings' && (
            <BuildingsRoomsView buildings={buildings} rooms={rooms} />
          )}

          {currentTab === 'tenants' && <TenantsView tenants={tenants} />}

          {currentTab === 'invoices' && <InvoicesView invoices={invoices} />}

          {currentTab === 'contracts' && (
            <div className="space-y-4">
              <h1 className="text-xl font-bold text-slate-900">Quản lý hợp đồng thuê</h1>
              <p className="text-xs text-slate-500">
                Theo dõi thời hạn kết thúc hợp đồng, gia hạn và thanh lý cọc.
              </p>
              <TenantsView tenants={tenants} />
            </div>
          )}

          {currentTab === 'reports' && (
            <OverviewView
              buildings={buildings}
              rooms={rooms}
              onNavigateToServices={() => {
                setCurrentTab('services');
                setCurrentSubTab('catalog');
              }}
              onNavigateToMeter={() => {
                setCurrentTab('services');
                setCurrentSubTab('meter-reading');
              }}
            />
          )}

          {currentTab === 'settings' && (
            <div className="p-6 bg-white border border-slate-200 rounded-2xl shadow-2xs space-y-4">
              <h2 className="text-lg font-bold text-slate-900">Cấu hình biểu giá & hệ số quy định</h2>
              <p className="text-xs text-slate-500">
                Thiết lập mức giá lũy tiến điện 3 bậc và định mức sử dụng nước sạch sinh hoạt.
              </p>
              <div className="p-4 bg-slate-50 rounded-xl text-xs space-y-2">
                <p>
                  <strong>Điện sinh hoạt:</strong> Bậc 1: 1.800 đ/kWh (0-50 kWh) | Bậc 2: 2.000 đ/kWh
                  (51-100 kWh) | Bậc 3: 3.000 đ/kWh (&gt;100 kWh)
                </p>
                <p>
                  <strong>Nước sạch:</strong> Đơn giá quy định cố định: 15.000 đ/m³
                </p>
              </div>
            </div>
          )}
        </main>
      </div>

      {/* Update Price Modal (Image 1) */}
      <UpdatePriceModal
        isOpen={!!modalService}
        service={modalService}
        onClose={() => setModalService(null)}
        onSavePriceVersion={handleSavePriceVersion}
      />
    </div>
  );
}
