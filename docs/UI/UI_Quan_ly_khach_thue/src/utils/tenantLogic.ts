import type { Tenant, FilterState, MetricSummary } from '../types/index.ts';

/**
 * Mask National ID (CCCD/CMND) according to D9 security standard.
 * Keeps the last 4 digits visible and replaces previous digits with dots.
 */
export function maskCCCD(cccd: string): { maskedPart: string; last4: string } {
  if (!cccd) {
    return { maskedPart: '••••••••', last4: '----' };
  }
  const clean = cccd.replace(/\s+/g, '');
  if (clean.length <= 4) {
    return { maskedPart: '••••••••', last4: clean.padStart(4, '0') };
  }
  const last4 = clean.slice(-4);
  return { maskedPart: '••••••••', last4 };
}

/**
 * Formats standard Vietnamese 10-digit phone number as "0xxx xxx xxx"
 */
export function formatPhoneNumber(phone: string): string {
  if (!phone) return '';
  const digits = phone.replace(/\D/g, '');
  if (digits.length === 10) {
    return `${digits.slice(0, 4)} ${digits.slice(4, 7)} ${digits.slice(7)}`;
  }
  if (digits.length === 11) {
    return `${digits.slice(0, 4)} ${digits.slice(4, 7)} ${digits.slice(7)}`;
  }
  return phone;
}

/**
 * Helper to normalize Vietnamese string for diacritic-insensitive search
 */
export function normalizeVietnamese(str: string): string {
  return str
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim();
}

/**
 * Filter and sort tenants based on search keyword and criteria
 */
export function filterTenants(tenants: Tenant[], filter: FilterState): Tenant[] {
  let result = [...tenants];

  // 1. Search filter
  const query = filter.search.trim();
  if (query.length > 0) {
    const normQuery = normalizeVietnamese(query);
    const digitQuery = query.replace(/\D/g, '');

    result = result.filter(tenant => {
      const normName = normalizeVietnamese(tenant.name);
      const codeMatch = tenant.code.toLowerCase().includes(query.toLowerCase());
      const nameMatch = normName.includes(normQuery);

      let phoneMatch = false;
      let cccdMatch = false;

      // Search hint rule: Match phone or CCCD only if 3 or more characters/digits
      if (digitQuery.length >= 3) {
        const cleanPhone = tenant.phone.replace(/\D/g, '');
        const cleanCCCD = tenant.cccd.replace(/\D/g, '');
        phoneMatch = cleanPhone.includes(digitQuery);
        cccdMatch = cleanCCCD.includes(digitQuery);
      } else if (digitQuery.length > 0 && query.length < 3) {
        // If user typed 1-2 digits, don't trigger phone/cccd search per UI hint
        phoneMatch = false;
        cccdMatch = false;
      }

      return nameMatch || codeMatch || phoneMatch || cccdMatch;
    });
  }

  // 2. Status filter
  if (filter.status === 'active') {
    result = result.filter(t => t.status === 'active');
  } else if (filter.status === 'inactive') {
    result = result.filter(t => t.status === 'inactive');
  }

  // 3. Account filter
  if (filter.account === 'has_acc') {
    result = result.filter(t => t.hasApp);
  } else if (filter.account === 'no_acc') {
    result = result.filter(t => !t.hasApp);
  }

  // 4. Sort
  result.sort((a, b) => {
    if (filter.sortBy === 'name_asc') {
      return a.name.localeCompare(b.name, 'vi');
    }
    if (filter.sortBy === 'name_desc') {
      return b.name.localeCompare(a.name, 'vi');
    }
    // 'date_desc' (default): newest first by createdAt / startDate
    return new Date(b.startDate).getTime() - new Date(a.startDate).getTime();
  });

  return result;
}

/**
 * Computes dashboard bento metric stats
 */
export function calculateMetrics(tenants: Tenant[]): MetricSummary {
  const total = tenants.length;
  const activeCount = tenants.filter(t => t.status === 'active').length;
  const appCount = tenants.filter(t => t.hasApp).length;

  const activeRatio = total > 0 ? Math.round((activeCount / total) * 1000) / 10 : 0;
  const appRatio = total > 0 ? Math.round((appCount / total) * 1000) / 10 : 0;

  return {
    totalTenants: total,
    totalThisMonthDelta: 4,
    activeTenants: activeCount,
    activeRatio: activeRatio,
    appActivatedCount: appCount,
    appActivatedRatio: appRatio,
    cccdMaskingCoverage: 100,
  };
}

/**
 * Generate next tenant code (e.g. T-2023-132)
 */
export function generateTenantCode(existing: Tenant[]): string {
  const currentYear = new Date().getFullYear();
  let maxNum = 131;

  for (const t of existing) {
    const match = t.code.match(/T-\d{4}-(\d+)/);
    if (match) {
      const num = parseInt(match[1], 10);
      if (num > maxNum) maxNum = num;
    }
  }

  const nextNum = String(maxNum + 1).padStart(3, '0');
  return `T-${currentYear}-${nextNum}`;
}
