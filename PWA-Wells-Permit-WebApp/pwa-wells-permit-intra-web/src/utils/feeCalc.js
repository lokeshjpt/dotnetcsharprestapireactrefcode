// Client-side mirror of the backend FeeCalculator (ported from the legacy Java BeanAppWrk /
// ApplicationBean). Keeps the intra fee display and the approval "pay total" consistent with the
// amount the API actually charges: fee is multiplied by the number of wells (Σ drill_count), not by
// the number of spec rows, and "site" units are a flat fee.
const CANCELLED = 'CAN';

function isCancelled(statusCode) {
  return String(statusCode || '').toUpperCase() === CANCELLED;
}

// Σ drill_count over non-cancelled specs (legacy countWells). A missing drill_count counts as 1.
export function countWells(work) {
  return (work?.specs || [])
    .filter((s) => !isCancelled(s.statusCode))
    .reduce((sum, s) => sum + (Number(s.drillCount) || 1), 0);
}

// Fee for a single work row (legacy getWorkCalcAmount). A "site" unit is a flat fee covering up to
// workSiteMax wells/holes; each additional well beyond the max is charged the site-extra rate that
// the work DTO now carries (WORK_TYPES system/siteExtra). Non-site units are rate × well count.
export function workCalcAmount(work) {
  const rate = Number(work?.workFeeRate) || 0;
  const isSite = String(work?.workFeeUnit || '').trim().toLowerCase() === 'site';
  if (isSite) {
    const siteMax = Number(work?.workSiteMax) || 0;
    const extraRate = Number(work?.workSiteExtraRate) || 0;
    const extraWells = siteMax > 0 ? Math.max(0, countWells(work) - siteMax) : 0;
    return Number((rate + extraWells * extraRate).toFixed(2));
  }
  return Number((rate * countWells(work)).toFixed(2));
}

// Σ of every non-cancelled work's fee — the permit base fee (auth_amount).
export function baseFee(works) {
  return (works || [])
    .filter((w) => !isCancelled(w.statusCode))
    .reduce((sum, w) => sum + workCalcAmount(w), 0);
}

// Σ of EVERY work's fee, including cancelled works (legacy getAuthWorkAmt without the cancel filter).
export function allWorksFee(works) {
  return (works || []).reduce((sum, w) => sum + workCalcAmount(w), 0);
}

// Round UP to two decimals (Java BigDecimal.ROUND_UP).
export function roundUp2(value) {
  return Math.ceil(Number(value) * 100) / 100;
}

// Legacy ApplicationBean.getAuthAmt — the summary-card "Total": every work (INCLUDING cancelled)
// + service charge + fine. Diverges from the pay total only once a work has been cancelled.
export function authTotal(works, serviceCharge, fine) {
  return roundUp2(allWorksFee(works) + (Number(serviceCharge) || 0) + (Number(fine) || 0));
}

// Live recalculated total charged at approval: base works fee + service charge + fine.
export function recalculatedTotal(works, serviceCharge, fine) {
  return roundUp2(baseFee(works) + (Number(serviceCharge) || 0) + (Number(fine) || 0));
}
