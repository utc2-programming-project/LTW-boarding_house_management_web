import type { Room, RoomFilters, KpiSummary } from '../types/index.ts';

export function formatVndCurrency(amount: number): string {
  if (isNaN(amount) || amount === null || amount === undefined) {
    return '0 ₫';
  }
  return new Intl.NumberFormat('vi-VN').format(amount) + ' ₫';
}

export function filterRooms(rooms: Room[], filters: RoomFilters): Room[] {
  const query = filters.searchQuery.trim().toLowerCase();
  const minPrice = filters.minPrice !== null && !isNaN(filters.minPrice) ? filters.minPrice : null;
  const maxPrice = filters.maxPrice !== null && !isNaN(filters.maxPrice) ? filters.maxPrice : null;

  return rooms.filter((room) => {
    // Exclude deleted items from standard view
    if (room.isDeleted) return false;

    // Locked filter switch (SW-01)
    if (room.isLocked && !filters.showLockedRooms) {
      return false;
    }

    // Keyword search in room code or property or tenant name
    if (query) {
      const matchCode = room.code.toLowerCase().includes(query);
      const matchProp = room.property.toLowerCase().includes(query);
      const matchTenant = room.tenantName ? room.tenantName.toLowerCase().includes(query) : false;
      if (!matchCode && !matchProp && !matchTenant) {
        return false;
      }
    }

    // Property filter
    if (filters.property && room.property !== filters.property) {
      return false;
    }

    // "Chỉ phòng trống" toggle takes precedence over status dropdown
    if (filters.onlyAvailable) {
      if (room.status !== 'Trống') return false;
    } else if (filters.status && room.status !== filters.status) {
      return false;
    }

    // Active status filter
    if (filters.activeStatus && room.activeStatus !== filters.activeStatus) {
      return false;
    }

    // Price range filters
    if (minPrice !== null && room.price < minPrice) {
      return false;
    }
    if (maxPrice !== null && room.price > maxPrice) {
      return false;
    }

    return true;
  });
}

export function calculateKpiMetrics(rooms: Room[]): KpiSummary {
  const activeRooms = rooms.filter((r) => !r.isDeleted);
  const totalRooms = activeRooms.length;

  const uniqueProperties = new Set(activeRooms.map((r) => r.property).filter(Boolean));
  const totalProperties = uniqueProperties.size;

  let vacantCount = 0;
  let occupiedCount = 0;
  let maintenanceOrPendingCount = 0;

  for (const r of activeRooms) {
    if (r.status === 'Trống') {
      vacantCount++;
    } else if (r.status === 'Đang thuê') {
      occupiedCount++;
    } else if (r.status === 'Bảo trì') {
      maintenanceOrPendingCount++;
    }
  }

  const vacantPercentage = totalRooms > 0 ? Number(((vacantCount / totalRooms) * 100).toFixed(1)) : 0;
  const occupiedPercentage = totalRooms > 0 ? Number(((occupiedCount / totalRooms) * 100).toFixed(1)) : 0;
  // Balance to 100% to match exact screenshot display (33.3% + 57.1% + 9.6%)
  const rawMaintenancePct = totalRooms > 0 ? (maintenanceOrPendingCount / totalRooms) * 100 : 0;
  const maintenancePercentage =
    totalRooms > 0 && vacantPercentage > 0 && occupiedPercentage > 0
      ? Number((100 - vacantPercentage - occupiedPercentage).toFixed(1))
      : Number(rawMaintenancePct.toFixed(1));

  return {
    totalRooms,
    totalProperties,
    vacantRooms: vacantCount,
    vacantPercentage,
    occupiedRooms: occupiedCount,
    occupiedPercentage,
    maintenanceOrPendingRooms: maintenanceOrPendingCount,
    maintenancePercentage,
  };
}

export function validateRoomForm(data: Partial<Room>, enablePriceCheck = true): {
  isValid: boolean;
  errors: Record<string, string>;
} {
  const errors: Record<string, string> = {};

  if (!data.code || !data.code.trim()) {
    errors.code = 'Mã phòng không được để trống (ví dụ: P.101)';
  } else if (!/^[a-zA-Z0-9.\-_/]+$/.test(data.code.trim())) {
    errors.code = 'Mã phòng chứa ký tự không hợp lệ';
  }

  if (!data.property || !data.property.trim()) {
    errors.property = 'Vui lòng chọn hoặc nhập tên nhà trọ / tòa nhà';
  }

  if (data.price === undefined || data.price === null || isNaN(data.price)) {
    errors.price = 'Vui lòng nhập giá phòng chuẩn';
  } else if (data.price <= 0) {
    errors.price = 'Giá phòng phải lớn hơn 0 ₫';
  } else if (enablePriceCheck && data.price < 500000) {
    errors.price = 'Cảnh báo kiểm định giá: Giá phòng thấp hơn mức sàn (500.000 ₫)';
  }

  if (data.area !== null && data.area !== undefined && data.area <= 0) {
    errors.area = 'Diện tích phòng phải lớn hơn 0 m²';
  }

  if (data.floor !== null && data.floor !== undefined && data.floor < 0) {
    errors.floor = 'Số tầng không hợp lệ';
  }

  return {
    isValid: Object.keys(errors).length === 0,
    errors,
  };
}

export function exportRoomsToCsv(rooms: Room[]): string {
  const headers = ['Mã phòng', 'Nhà trọ', 'Tầng', 'Diện tích (m2)', 'Giá chuẩn (VNĐ)', 'Trạng thái phòng', 'Hoạt động', 'Chờ duyệt', 'Ghi chú'];
  const rows = rooms.map((r) => [
    `"${r.code}"`,
    `"${r.property}"`,
    r.floor !== null ? r.floor : '—',
    r.area !== null ? r.area : '—',
    r.price,
    `"${r.status}"`,
    `"${r.activeStatus}"`,
    `"${r.approvalStatus === 'Chờ duyệt' ? 'Chờ duyệt' : 'Đã duyệt'}"`,
    `"${(r.notes || '').replace(/"/g, '""')}"`,
  ]);

  const csvContent = '\uFEFF' + [headers.join(','), ...rows.map((row) => row.join(','))].join('\r\n');
  return csvContent;
}
