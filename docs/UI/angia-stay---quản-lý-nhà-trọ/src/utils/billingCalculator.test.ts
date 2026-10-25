import test from 'node:test';
import assert from 'node:assert/strict';
import {
  calculateElectricityTiered,
  calculateWaterCost,
  calculateTotalMonthlyBill,
  formatCurrencyVND,
  validateMeterReading,
  validatePriceInput,
} from './billingCalculator';

test('tiered electricity calculates 120 kWh correctly matching screenshot', () => {
  const result = calculateElectricityTiered(120);
  // 50 * 1800 (90,000) + 50 * 2000 (100,000) + 20 * 3000 (60,000) = 250,000
  assert.equal(result.total, 250000);
  assert.equal(
    result.breakdown,
    'Bậc thang 3 bậc: 50×1.800 + 50×2.000 + 20×3.000'
  );
  assert.equal(result.tiersApplied.length, 3);
  assert.equal(result.tiersApplied[0].units, 50);
  assert.equal(result.tiersApplied[1].units, 50);
  assert.equal(result.tiersApplied[2].units, 20);
});

test('water cost calculates 6 m3 correctly', () => {
  const result = calculateWaterCost(6, 15000);
  assert.equal(result.total, 90000);
  assert.equal(result.breakdown, 'Đơn giá cố định: 6 m³ × 15.000 đ');
});

test('total monthly bill calculation matches screenshot exact total of 3.470.000 đ', () => {
  const bill = calculateTotalMonthlyBill({
    roomRent: 3000000,
    electricityUsageKwh: 120,
    waterUsageM3: 6,
    waterRate: 15000,
    internetCost: 100000,
    cleaningCost: 30000,
    parkingCost: 0,
  });

  assert.equal(bill.roomRent, 3000000);
  assert.equal(bill.electricityCost, 250000);
  assert.equal(bill.waterCost, 90000);
  assert.equal(bill.serviceCost, 130000);
  assert.equal(bill.totalCost, 3470000);
  assert.equal(formatCurrencyVND(bill.totalCost), '3.470.000 đ');
});

test('validates meter reading and catches invalid end reading', () => {
  const valid = validateMeterReading(1450, 1570);
  assert.equal(valid.valid, true);
  assert.equal(valid.usage, 120);

  const invalid = validateMeterReading(1450, 1400);
  assert.equal(invalid.valid, false);
  assert.ok(invalid.errorMessage?.includes('lớn hơn hoặc bằng'));
});

test('validates price input correctly', () => {
  const valid = validatePriceInput(110000);
  assert.equal(valid.valid, true);

  const zeroPrice = validatePriceInput(0);
  assert.equal(zeroPrice.valid, false);
  assert.equal(
    zeroPrice.error,
    'Đơn giá phải lớn hơn 0. / Price must be greater than 0.'
  );

  const negativePrice = validatePriceInput(-5000);
  assert.equal(negativePrice.valid, false);
});
