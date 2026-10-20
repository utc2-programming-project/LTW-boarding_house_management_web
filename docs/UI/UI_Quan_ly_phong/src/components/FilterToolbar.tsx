import React from 'react';
import type { RoomFilters } from '../types/index.ts';

interface FilterToolbarProps {
  filters: RoomFilters;
  onFilterChange: (filters: RoomFilters) => void;
  filteredCount: number;
  properties: string[];
}

export const FilterToolbar: React.FC<FilterToolbarProps> = ({
  filters,
  onFilterChange,
  filteredCount,
  properties,
}) => {
  const handleOnlyAvailableChange = (checked: boolean) => {
    onFilterChange({
      ...filters,
      onlyAvailable: checked,
      status: checked ? 'Trống' : filters.status === 'Trống' ? '' : filters.status,
    });
  };

  const handleReset = () => {
    onFilterChange({
      searchQuery: '',
      property: '',
      status: '',
      activeStatus: '',
      minPrice: null,
      maxPrice: null,
      onlyAvailable: false,
      showLockedRooms: filters.showLockedRooms,
    });
  };

  return (
    <div className="bg-surface-container-lowest rounded-xl p-4 shadow-sm flex flex-col gap-4 border border-surface-container">
      {/* Primary Row: Search & Filters */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-12 gap-3 items-center">
        {/* Search Input */}
        <div className="lg:col-span-3 relative">
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-outline">
            <svg className="w-4 h-4 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" x2="16.65" y1="21" y2="16.65" />
            </svg>
          </div>
          <input
            id="filter-search"
            type="text"
            maxLength={50}
            value={filters.searchQuery}
            onChange={(e) => onFilterChange({ ...filters, searchQuery: e.target.value })}
            placeholder="Tìm theo mã phòng..."
            className="w-full bg-surface-container-lowest text-on-surface placeholder:text-outline text-[14px] pl-9 pr-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container transition-all"
          />
        </div>

        {/* Property Select */}
        <div className="lg:col-span-2">
          <select
            id="filter-property"
            value={filters.property}
            onChange={(e) => onFilterChange({ ...filters, property: e.target.value })}
            className="w-full bg-surface-container-lowest text-on-surface text-[14px] px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container transition-all cursor-pointer"
          >
            <option value="">Tất cả nhà trọ</option>
            {properties.map((prop) => (
              <option key={prop} value={prop}>
                {prop}
              </option>
            ))}
          </select>
        </div>

        {/* Room Status Select */}
        <div className="lg:col-span-2 relative">
          <select
            id="filter-status"
            disabled={filters.onlyAvailable}
            value={filters.onlyAvailable ? 'Trống' : filters.status}
            onChange={(e) => onFilterChange({ ...filters, status: e.target.value })}
            className="w-full bg-surface-container-lowest text-on-surface text-[14px] px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container transition-all cursor-pointer disabled:bg-surface-container-low disabled:text-outline disabled:cursor-not-allowed"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="Trống">Trống</option>
            <option value="Đang thuê">Đang thuê</option>
            <option value="Bảo trì">Bảo trì</option>
          </select>
        </div>

        {/* Active Status Select */}
        <div className="lg:col-span-2">
          <select
            id="filter-active"
            value={filters.activeStatus}
            onChange={(e) => onFilterChange({ ...filters, activeStatus: e.target.value })}
            className="w-full bg-surface-container-lowest text-on-surface text-[14px] px-3 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container transition-all cursor-pointer"
          >
            <option value="">Tất cả hoạt động</option>
            <option value="Đang hoạt động">Đang hoạt động</option>
            <option value="Không hoạt động">Không hoạt động</option>
          </select>
        </div>

        {/* Price Range Inputs */}
        <div className="lg:col-span-3 flex items-center gap-2">
          <input
            id="filter-price-min"
            type="number"
            placeholder="Giá từ"
            value={filters.minPrice ?? ''}
            onChange={(e) =>
              onFilterChange({
                ...filters,
                minPrice: e.target.value ? Number(e.target.value) : null,
              })
            }
            className="w-1/2 bg-surface-container-lowest text-on-surface placeholder:text-outline text-[14px] px-2.5 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container transition-all"
          />
          <span className="text-outline text-[13px] font-medium">-</span>
          <input
            id="filter-price-max"
            type="number"
            placeholder="Đến"
            value={filters.maxPrice ?? ''}
            onChange={(e) =>
              onFilterChange({
                ...filters,
                maxPrice: e.target.value ? Number(e.target.value) : null,
              })
            }
            className="w-1/2 bg-surface-container-lowest text-on-surface placeholder:text-outline text-[14px] px-2.5 py-2 rounded-lg outline-none ring-1 ring-outline-variant focus:ring-2 focus:ring-primary-container transition-all"
          />
        </div>
      </div>

      {/* Secondary Row: Quick Switches & Reset Action */}
      <div className="flex flex-wrap items-center justify-between gap-3 pt-1 border-t border-surface-container">
        <div className="flex items-center gap-4">
          {/* Switch: Only Available */}
          <label className="group relative flex items-center gap-2 cursor-pointer select-none">
            <input
              id="toggle-only-available"
              type="checkbox"
              checked={filters.onlyAvailable}
              onChange={(e) => handleOnlyAvailableChange(e.target.checked)}
              className="sr-only peer"
            />
            <div className="w-8 h-4.5 bg-surface-container-high rounded-full peer peer-checked:bg-primary-container transition-colors relative after:content-[''] after:absolute after:top-0.5 after:left-0.5 after:bg-surface-container-lowest after:rounded-full after:h-3.5 after:w-3.5 after:transition-all peer-checked:after:translate-x-3.5" />
            <span className="text-[13px] font-medium text-on-surface group-hover:text-primary transition-colors">
              Chỉ phòng trống
            </span>

            {/* Tooltip on hover when locked */}
            {filters.onlyAvailable && (
              <div className="hidden group-hover:flex absolute left-0 bottom-full mb-2 px-2.5 py-1 bg-inverse-surface text-inverse-on-surface rounded text-[11px] whitespace-nowrap shadow-md z-30 items-center gap-1.5">
                <svg className="w-3 h-3 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
                  <rect height="11" rx="2" ry="2" width="18" x="3" y="11" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
                <span>Khóa bộ lọc trạng thái vì đang chỉ định xem phòng trống</span>
              </div>
            )}
          </label>

          <div className="h-4 w-px bg-surface-container-high" />

          <span className="text-on-surface-variant text-[13px]">
            Đang lọc:{' '}
            <strong id="filter-active-count" className="text-on-surface font-semibold">
              {filteredCount} phòng
            </strong>
          </span>
        </div>

        {/* Reset Action */}
        <div className="flex items-center gap-2">
          <button
            id="btn-reset-filter"
            type="button"
            onClick={handleReset}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-on-surface-variant text-[13px] font-medium hover:bg-surface-container hover:text-on-surface transition-all border border-transparent hover:border-surface-container"
          >
            <svg className="w-3.5 h-3.5 stroke-current fill-none stroke-2" viewBox="0 0 24 24">
              <polyline points="1 4 1 10 7 10" />
              <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
            </svg>
            <span>Đặt lại</span>
          </button>
        </div>
      </div>
    </div>
  );
};
