import test from 'node:test';
import assert from 'node:assert/strict';
import {
  maskCCCD,
  formatPhoneNumber,
  filterTenants,
  calculateMetrics,
  generateTenantCode,
  normalizeVietnamese,
} from './tenantLogic.ts';
import type { Tenant, FilterState } from '../types/index.ts';

const sampleTenants: Tenant[] = [
  {
    id: '1',
    code: 'T-2023-089',
    name: 'Nguyễn Văn An',
    phone: '0912345678',
    cccd: '079201005678',
    status: 'active',
    hasApp: true,
    room: 'P.101',
    gender: 'male',
    startDate: '2023-05-10',
  },
  {
    id: '2',
    code: 'T-2023-094',
    name: 'Trần Thị Bích',
    phone: '0988776543',
    cccd: '079201009012',
    status: 'active',
    hasApp: true,
    room: 'P.102',
    gender: 'female',
    startDate: '2023-06-15',
  },
  {
    id: '3',
    code: 'T-2023-102',
    name: 'Lê Quang Cường',
    phone: '0903112233',
    cccd: '079201003344',
    status: 'inactive',
    hasApp: false,
    room: 'P.201',
    gender: 'male',
    startDate: '2023-08-01',
  },
];

test('maskCCCD masks all but last 4 digits per D9 standard', () => {
  const result = maskCCCD('079201005678');
  assert.equal(result.maskedPart, '••••••••');
  assert.equal(result.last4, '5678');
});

test('maskCCCD handles short strings gracefully', () => {
  const result = maskCCCD('12');
  assert.equal(result.maskedPart, '••••••••');
  assert.equal(result.last4, '0012');
});

test('formatPhoneNumber formats 10-digit number with spaces', () => {
  assert.equal(formatPhoneNumber('0912345678'), '0912 345 678');
});

test('normalizeVietnamese strips diacritics and converts to lower case', () => {
  assert.equal(normalizeVietnamese('Nguyễn Văn An'), 'nguyen van an');
  assert.equal(normalizeVietnamese('Đặng'), 'dang');
});

test('filterTenants filters by name search case- and diacritic-insensitively', () => {
  const filter: FilterState = {
    search: 'nguyen',
    status: 'all',
    account: 'all',
    sortBy: 'date_desc',
  };
  const filtered = filterTenants(sampleTenants, filter);
  assert.equal(filtered.length, 1);
  assert.equal(filtered[0].name, 'Nguyễn Văn An');
});

test('filterTenants enforces minimum 3 characters for phone/CCCD match', () => {
  // Query with 2 digits should not match
  const filter2: FilterState = {
    search: '12',
    status: 'all',
    account: 'all',
    sortBy: 'date_desc',
  };
  assert.equal(filterTenants(sampleTenants, filter2).length, 0);

  // Query with 3 digits should match
  const filter3: FilterState = {
    search: '678',
    status: 'all',
    account: 'all',
    sortBy: 'date_desc',
  };
  const res = filterTenants(sampleTenants, filter3);
  assert.equal(res.length, 1);
  assert.equal(res[0].name, 'Nguyễn Văn An');
});

test('filterTenants filters by status and account properly', () => {
  const activeOnly: FilterState = {
    search: '',
    status: 'active',
    account: 'all',
    sortBy: 'date_desc',
  };
  assert.equal(filterTenants(sampleTenants, activeOnly).length, 2);

  const noAppOnly: FilterState = {
    search: '',
    status: 'all',
    account: 'no_acc',
    sortBy: 'date_desc',
  };
  const res = filterTenants(sampleTenants, noAppOnly);
  assert.equal(res.length, 1);
  assert.equal(res[0].name, 'Lê Quang Cường');
});

test('calculateMetrics computes correct totals and ratios', () => {
  const metrics = calculateMetrics(sampleTenants);
  assert.equal(metrics.totalTenants, 3);
  assert.equal(metrics.activeTenants, 2);
  assert.equal(metrics.activeRatio, 66.7);
  assert.equal(metrics.appActivatedCount, 2);
  assert.equal(metrics.appActivatedRatio, 66.7);
  assert.equal(metrics.cccdMaskingCoverage, 100);
});

test('generateTenantCode increments code sequence correctly', () => {
  const code = generateTenantCode(sampleTenants);
  const currentYear = new Date().getFullYear();
  assert.equal(code, `T-${currentYear}-132`);
});
