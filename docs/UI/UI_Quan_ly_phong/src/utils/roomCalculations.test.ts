import test from 'node:test';
import assert from 'node:assert/strict';
import {
  formatVndCurrency,
  filterRooms,
  calculateKpiMetrics,
  validateRoomForm,
} from './roomCalculations.ts';
import type { Room, RoomFilters } from '../types/index.ts';

const mockRooms: Room[] = [
  {
    id: '1',
    code: 'P.101',
    property: 'An Phú Building',
    floor: 1,
    area: 24,
    price: 3500000,
    status: 'Trống',
    activeStatus: 'Đang hoạt động',
    approvalStatus: 'Chờ duyệt',
  },
  {
    id: '2',
    code: 'P.102',
    property: 'An Phú Building',
    floor: 1,
    area: 28,
    price: 4000000,
    status: 'Đang thuê',
    activeStatus: 'Đang hoạt động',
  },
  {
    id: '3',
    code: 'P.201',
    property: 'Minh Khang Residence',
    floor: 2,
    area: 22,
    price: 3200000,
    status: 'Bảo trì',
    activeStatus: 'Đang hoạt động',
  },
  {
    id: '4',
    code: 'P.202',
    property: 'Minh Khang Residence',
    floor: null,
    area: null,
    price: 3000000,
    status: 'Trống',
    activeStatus: 'Không hoạt động',
  },
  {
    id: '5',
    code: 'P.301',
    property: 'Green House',
    floor: 3,
    area: 32,
    price: 4500000,
    status: 'Đang thuê',
    activeStatus: 'Đang hoạt động',
  },
  {
    id: '6',
    code: 'P.302',
    property: 'Green House',
    floor: 3,
    area: 26,
    price: 3800000,
    status: 'Trống',
    activeStatus: 'Đang hoạt động',
  },
];

test('formatVndCurrency correctly formats Vietnamese dong currency', () => {
  assert.equal(formatVndCurrency(3500000).includes('3.500.000'), true);
  assert.equal(formatVndCurrency(3500000).includes('₫'), true);
  assert.equal(formatVndCurrency(0).includes('0'), true);
});

test('calculateKpiMetrics computes accurate totals and percentages', () => {
  const metrics = calculateKpiMetrics(mockRooms);
  assert.equal(metrics.totalRooms, 6);
  assert.equal(metrics.totalProperties, 3);
  assert.equal(metrics.vacantRooms, 3); // P.101, P.202, P.302
  assert.equal(metrics.occupiedRooms, 2); // P.102, P.301
  assert.equal(metrics.maintenanceOrPendingRooms, 1); // P.201 (Bảo trì)
  assert.equal(metrics.vacantPercentage, 50.0);
});

test('filterRooms filters by search query on room code', () => {
  const baseFilters: RoomFilters = {
    searchQuery: 'P.101',
    property: '',
    status: '',
    activeStatus: '',
    minPrice: null,
    maxPrice: null,
    onlyAvailable: false,
    showLockedRooms: false,
  };

  const results = filterRooms(mockRooms, baseFilters);
  assert.equal(results.length, 1);
  assert.equal(results[0].code, 'P.101');
});

test('filterRooms filters by property', () => {
  const baseFilters: RoomFilters = {
    searchQuery: '',
    property: 'Green House',
    status: '',
    activeStatus: '',
    minPrice: null,
    maxPrice: null,
    onlyAvailable: false,
    showLockedRooms: false,
  };

  const results = filterRooms(mockRooms, baseFilters);
  assert.equal(results.length, 2);
  assert.equal(results[0].code, 'P.301');
  assert.equal(results[1].code, 'P.302');
});

test('filterRooms with onlyAvailable toggle shows only vacant rooms', () => {
  const baseFilters: RoomFilters = {
    searchQuery: '',
    property: '',
    status: 'Đang thuê', // Should be overridden by onlyAvailable
    activeStatus: '',
    minPrice: null,
    maxPrice: null,
    onlyAvailable: true,
    showLockedRooms: false,
  };

  const results = filterRooms(mockRooms, baseFilters);
  assert.equal(results.length, 3);
  assert.equal(results.every((r) => r.status === 'Trống'), true);
});

test('filterRooms filters by price range', () => {
  const baseFilters: RoomFilters = {
    searchQuery: '',
    property: '',
    status: '',
    activeStatus: '',
    minPrice: 3500000,
    maxPrice: 4000000,
    onlyAvailable: false,
    showLockedRooms: false,
  };

  const results = filterRooms(mockRooms, baseFilters);
  assert.equal(results.length, 3); // P.101 (3.5M), P.102 (4.0M), P.302 (3.8M)
});

test('validateRoomForm validates room creation data', () => {
  const invalid = validateRoomForm({ code: '', price: -10 });
  assert.equal(invalid.isValid, false);
  assert.equal(Boolean(invalid.errors.code), true);
  assert.equal(Boolean(invalid.errors.property), true);
  assert.equal(Boolean(invalid.errors.price), true);

  const valid = validateRoomForm({
    code: 'P.401',
    property: 'An Phú Building',
    price: 3500000,
    area: 25,
    floor: 4,
  });
  assert.equal(valid.isValid, true);
});

test('INITIAL_ROOMS data matches screenshot KPI metrics exactly', async () => {
  const { INITIAL_ROOMS } = await import('../data/initialData.ts');
  const metrics = calculateKpiMetrics(INITIAL_ROOMS);
  assert.equal(metrics.totalRooms, 42);
  assert.equal(metrics.totalProperties, 3);
  assert.equal(metrics.vacantRooms, 14);
  assert.equal(metrics.occupiedRooms, 24);
  assert.equal(metrics.maintenanceOrPendingRooms, 4);
});

