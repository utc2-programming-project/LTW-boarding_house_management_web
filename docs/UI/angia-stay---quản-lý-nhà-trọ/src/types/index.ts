export type UserRole = 'landlord' | 'manager' | 'tenant';

export interface Building {
  id: string;
  name: string;
  address: string;
  totalRooms: number;
  occupiedRooms: number;
}

export type ServiceType = 'meter' | 'fixed_monthly' | 'custom';

export interface ServiceItem {
  id: string;
  name: string;
  code: string;
  type: ServiceType;
  typeLabel: string;
  unit: string;
  rateType: 'flat' | 'tiered';
  currentRate: number; // for flat rate
  rateDisplay: string; // e.g. "100.000 đ" or "Bậc thang · 3 bậc"
  effectiveFrom: string; // "01/01/2026"
  effectiveTo: string | null; // "31/12/2026" or null
  active: boolean;
  isReadOnly: boolean;
  buildingId: string; // 'all' or building id
  buildingName: string; // 'Tất cả nhà trọ' or specific
  description?: string;
  historyCount: number;
}

export interface PriceVersion {
  id: string;
  serviceId: string;
  serviceName: string;
  unit: string;
  rate: number;
  rateType: 'flat' | 'tiered';
  effectiveFrom: string;
  effectiveTo?: string;
  status: 'active' | 'upcoming' | 'expired';
  buildingScope: string;
  buildingName: string;
  autoActivate: boolean;
  createdAt: string;
  createdBy: string;
  note?: string;
}

export interface AuditLogItem {
  id: string;
  modifier: string;
  role: string;
  timestamp: string;
  fieldChanged: string;
  oldValue: string;
  newValue: string;
  detailDiff: string;
  reason: string;
}

export interface RoomRecord {
  id: string;
  roomCode: string;
  roomName: string;
  buildingId: string;
  buildingName: string;
  contractCode: string;
  tenantName: string;
  tenantPhone: string;
  roomRent: number;
  periodMonth: string; // e.g. "Tháng 10/2026"
  periodStart: string; // "10/01/2026"
  periodEnd: string; // "10/31/2026"
  status: 'open' | 'locked' | 'billed';
  electricMeterCode: string;
  electricStart: number;
  electricEnd: number;
  waterMeterCode: string;
  waterStart: number;
  waterEnd: number;
  waterRate: number;
  internetCost: number;
  cleaningCost: number;
  parkingCost: number;
  auditLogs: AuditLogItem[];
  refCode: string;
}

export interface Tenant {
  id: string;
  name: string;
  phone: string;
  email: string;
  roomCode: string;
  buildingName: string;
  contractCode: string;
  startDate: string;
  endDate: string;
  depositAmount: number;
  monthlyRent: number;
  idCard: string;
  status: 'active' | 'expiring' | 'ended';
}

export interface Invoice {
  id: string;
  code: string;
  roomCode: string;
  buildingName: string;
  tenantName: string;
  period: string;
  roomRent: number;
  electricAmount: number;
  waterAmount: number;
  serviceAmount: number;
  totalAmount: number;
  status: 'paid' | 'pending' | 'overdue';
  dueDate: string;
  createdAt: string;
}
