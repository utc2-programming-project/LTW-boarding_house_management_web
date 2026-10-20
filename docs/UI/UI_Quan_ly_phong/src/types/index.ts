export type RoomStatus = 'Trống' | 'Đang thuê' | 'Bảo trì';
export type ActiveStatus = 'Đang hoạt động' | 'Không hoạt động';
export type ApprovalStatus = 'Chờ duyệt' | 'Đã duyệt';

export interface Room {
  id: string;
  code: string;
  property: string;
  floor: number | null;
  area: number | null;
  price: number;
  status: RoomStatus;
  activeStatus: ActiveStatus;
  approvalStatus?: ApprovalStatus;
  isLocked?: boolean;
  tenantName?: string;
  tenantPhone?: string;
  contractEnd?: string;
  amenities?: string[];
  notes?: string;
  isDeleted?: boolean;
}

export interface RoomFilters {
  searchQuery: string;
  property: string;
  status: string;
  activeStatus: string;
  minPrice: number | null;
  maxPrice: number | null;
  onlyAvailable: boolean;
  showLockedRooms: boolean;
}

export interface KpiSummary {
  totalRooms: number;
  totalProperties: number;
  vacantRooms: number;
  vacantPercentage: number;
  occupiedRooms: number;
  occupiedPercentage: number;
  maintenanceOrPendingRooms: number;
  maintenancePercentage: number;
}

export type UserRole = 'Chủ trọ' | 'Quản lý' | 'Khách thuê' | 'Admin';
export type SimulationState = 'normal' | 'loading' | 'empty' | 'error' | 'unauthorized';
export type AppNavTab = 'phong' | 'nha-tro' | 'hop-dong' | 'khach-thue' | 'thung-rac';

export interface PropertyItem {
  id: string;
  name: string;
  address: string;
  totalRooms: number;
  occupiedRooms: number;
  managerName: string;
  managerPhone: string;
  description: string;
}

export interface ContractItem {
  id: string;
  contractCode: string;
  roomCode: string;
  propertyName: string;
  tenantName: string;
  tenantPhone: string;
  deposit: number;
  monthlyRent: number;
  startDate: string;
  endDate: string;
  status: 'Hiệu lực' | 'Sắp hết hạn' | 'Đã chấm dứt';
}

export interface TenantItem {
  id: string;
  name: string;
  phone: string;
  email: string;
  cccd: string;
  roomCode: string;
  propertyName: string;
  startDate: string;
  status: 'Đang thuê' | 'Đã trả phòng';
}
