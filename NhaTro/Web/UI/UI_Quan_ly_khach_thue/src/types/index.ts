export type TenantStatus = 'active' | 'inactive';

export interface Tenant {
  id: string;
  code: string; // e.g. T-2023-089
  name: string;
  phone: string;
  cccd: string; // 12-digit national ID
  status: TenantStatus;
  hasApp: boolean;
  room: string;
  gender: 'male' | 'female' | 'other';
  birthDate?: string;
  hometown?: string;
  startDate: string;
  emergencyContact?: string;
  depositAmount?: number;
  monthlyRent?: number;
  notes?: string;
  avatarBg?: string;
  avatarColor?: string;
}

export type UserRole = 'landlord' | 'manager' | 'tenant' | 'admin';

export type SimulationState = 'normal' | 'loading' | 'empty' | 'error' | 'conflict' | 'offline';

export type Language = 'vi' | 'en';

export type SortOption = 'date_desc' | 'name_asc' | 'name_desc';

export interface FilterState {
  search: string;
  status: 'all' | 'active' | 'inactive';
  account: 'all' | 'has_acc' | 'no_acc';
  sortBy: SortOption;
}

export interface MetricSummary {
  totalTenants: number;
  totalThisMonthDelta: number;
  activeTenants: number;
  activeRatio: number;
  appActivatedCount: number;
  appActivatedRatio: number;
  cccdMaskingCoverage: number;
}
