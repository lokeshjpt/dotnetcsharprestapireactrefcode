import { countWells, workCalcAmount, baseFee, recalculatedTotal } from './feeCalc';

function spec(drillCount, statusCode = 'PEND') {
  return { drillCount, statusCode };
}

describe('intra feeCalc', () => {
  test('countWells sums drill_count over non-cancelled specs (missing counts as 1)', () => {
    const work = { specs: [spec(2), spec(null), spec(3, 'CAN')] };
    expect(countWells(work)).toBe(3); // 2 + 1, cancelled 3 excluded
  });

  test('per-well work: rate x total wells', () => {
    const work = { workFeeRate: 660, workFeeUnit: 'well', specs: [spec(2)] };
    expect(workCalcAmount(work)).toBe(1320);
  });

  test('tiered site work: flat rate plus site-extra per well beyond siteMax', () => {
    // 6 wells: flat 445 covers 4, then 2 extra x 85 = 170. Total 615.
    const work = {
      workFeeRate: 445,
      workFeeUnit: 'site',
      workSiteMax: 4,
      workSiteExtraRate: 85,
      specs: [spec(6)],
    };
    expect(workCalcAmount(work)).toBe(615);
  });

  test('tiered site work at or below siteMax stays flat', () => {
    const work = {
      workFeeRate: 445,
      workFeeUnit: 'site',
      workSiteMax: 4,
      workSiteExtraRate: 85,
      specs: [spec(4)],
    };
    expect(workCalcAmount(work)).toBe(445);
  });

  test('site work without an extra rate never tiers', () => {
    const work = { workFeeRate: 445, workFeeUnit: 'site', workSiteMax: 4, specs: [spec(10)] };
    expect(workCalcAmount(work)).toBe(445);
  });

  test('recalculatedTotal sums base works fee plus service charge and fine, rounded up', () => {
    const works = [
      { workFeeRate: 660, workFeeUnit: 'well', specs: [spec(1)] },
      { workFeeRate: 445, workFeeUnit: 'site', workSiteMax: 4, workSiteExtraRate: 85, specs: [spec(5)] },
    ];
    // 660 + (445 + 1*85=530) = 1190 base, + 12 service + 0 fine = 1202
    expect(baseFee(works)).toBe(1190);
    expect(recalculatedTotal(works, 12, 0)).toBe(1202);
  });
});
