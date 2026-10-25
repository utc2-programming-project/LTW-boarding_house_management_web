export interface ElectricTier {
  id: number;
  name: string;
  maxKwh: number | null; // null for highest tier (infinity)
  rate: number;
}

export const DEFAULT_ELECTRIC_TIERS: ElectricTier[] = [
  { id: 1, name: 'Bậc 1 (0-50 kWh)', maxKwh: 50, rate: 1800 },
  { id: 2, name: 'Bậc 2 (51-100 kWh)', maxKwh: 50, rate: 2000 },
  { id: 3, name: 'Bậc 3 (>100 kWh)', maxKwh: null, rate: 3000 },
];

export interface TierApplied {
  tierName: string;
  units: number;
  rate: number;
  amount: number;
}

export interface ElectricityCalculationResult {
  total: number;
  breakdown: string;
  tiersApplied: TierApplied[];
}

/**
 * Calculates tiered electricity cost and produces human-readable breakdown
 * e.g., 120 kWh with default tiers => 50*1800 + 50*2000 + 20*3000 = 250,000 đ
 */
export function calculateElectricityTiered(
  usageKwh: number,
  tiers: ElectricTier[] = DEFAULT_ELECTRIC_TIERS
): ElectricityCalculationResult {
  const safeUsage = Math.max(0, usageKwh);
  if (safeUsage === 0) {
    return {
      total: 0,
      breakdown: '0 kWh × 0 đ',
      tiersApplied: [],
    };
  }

  let remaining = safeUsage;
  let total = 0;
  const tiersApplied: TierApplied[] = [];
  const formulaParts: string[] = [];

  for (let i = 0; i < tiers.length; i++) {
    const tier = tiers[i];
    if (remaining <= 0) break;

    const unitsInTier = tier.maxKwh === null ? remaining : Math.min(remaining, tier.maxKwh);
    const cost = unitsInTier * tier.rate;
    total += cost;
    remaining -= unitsInTier;

    tiersApplied.push({
      tierName: tier.name,
      units: unitsInTier,
      rate: tier.rate,
      amount: cost,
    });

    formulaParts.push(`${unitsInTier}×${tier.rate.toLocaleString('vi-VN')}`);
  }

  const breakdown = `Bậc thang ${tiersApplied.length} bậc: ${formulaParts.join(' + ')}`;

  return {
    total,
    breakdown,
    tiersApplied,
  };
}

/**
 * Calculates water cost based on fixed rate per m³
 */
export function calculateWaterCost(
  usageM3: number,
  unitRate: number = 15000
): { total: number; breakdown: string } {
  const safeUsage = Math.max(0, usageM3);
  const total = safeUsage * unitRate;
  const breakdown = `Đơn giá cố định: ${safeUsage} m³ × ${unitRate.toLocaleString('vi-VN')} đ`;
  return { total, breakdown };
}

/**
 * Calculates total bill breakdown
 */
export function calculateTotalMonthlyBill(params: {
  roomRent: number;
  electricityUsageKwh: number;
  waterUsageM3: number;
  waterRate?: number;
  internetCost?: number;
  cleaningCost?: number;
  parkingCost?: number;
}): {
  roomRent: number;
  electricityCost: number;
  electricityBreakdown: string;
  waterCost: number;
  waterBreakdown: string;
  serviceCost: number;
  totalCost: number;
} {
  const {
    roomRent,
    electricityUsageKwh,
    waterUsageM3,
    waterRate = 15000,
    internetCost = 100000,
    cleaningCost = 30000,
    parkingCost = 0,
  } = params;

  const elecResult = calculateElectricityTiered(electricityUsageKwh);
  const waterResult = calculateWaterCost(waterUsageM3, waterRate);
  const serviceCost = internetCost + cleaningCost + parkingCost;
  const totalCost = roomRent + elecResult.total + waterResult.total + serviceCost;

  return {
    roomRent,
    electricityCost: elecResult.total,
    electricityBreakdown: elecResult.breakdown,
    waterCost: waterResult.total,
    waterBreakdown: waterResult.breakdown,
    serviceCost,
    totalCost,
  };
}

/**
 * Formats currency in Vietnamese Dong style: e.g. 3.470.000 đ
 */
export function formatCurrencyVND(amount: number, suffix: string = ' đ'): string {
  const formatted = Math.round(amount).toLocaleString('vi-VN');
  return `${formatted}${suffix}`;
}

/**
 * Validates meter readings
 */
export function validateMeterReading(
  startReading: number,
  endReading: number
): { valid: boolean; usage: number; errorMessage?: string } {
  if (isNaN(endReading)) {
    return { valid: false, usage: 0, errorMessage: 'Vui lòng nhập chỉ số hợp lệ' };
  }
  if (endReading < startReading) {
    return {
      valid: false,
      usage: 0,
      errorMessage: 'Chỉ số cuối kỳ phải lớn hơn hoặc bằng chỉ số đầu kỳ.',
    };
  }
  return { valid: true, usage: Math.round((endReading - startReading) * 100) / 100 };
}

/**
 * Validates price input
 */
export function validatePriceInput(price: number): { valid: boolean; error?: string } {
  if (isNaN(price) || price <= 0) {
    return {
      valid: false,
      error: 'Đơn giá phải lớn hơn 0. / Price must be greater than 0.',
    };
  }
  return { valid: true };
}
