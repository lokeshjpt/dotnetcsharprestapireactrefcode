// Multi-work support for the public application (legacy app_view_form.jsp "Add Another Type of Work
// to Project"). The wizard edits ONE work at a time via the flat Step3/Step4 fields ("the draft");
// completed works are committed into formData.works. These helpers convert between the two and
// compute the running fee across every work.

import { isBoreholeCategory } from './referenceData';

export function makeWellSpec() {
  return {
    swellid: '',
    permit: '',
    dwr: '',
    owellnum: '',
    holediam: '',
    casediam: '',
    sealdepth: '',
    maxdepth: '',
    latitude: '',
    longitude: '',
  };
}

// The per-work fields carried on formData while a single work is being edited (Step3 + Step4).
export const DRAFT_WORK_FIELDS = [
  'workCat', 'workType', 'workDesc', 'workFeeRate', 'workFeeUnit', 'workSiteMax', 'workSiteExtraRate',
  'wUse', 'wUseDesc', 'drillerName', 'drillerLic', 'dmeth', 'dmethName', 'dmethOth',
  'numbore', 'holediam', 'maxdepth', 'wellSpecs',
];

// Empty per-work draft (matches the work-type/work-info defaults in useApplication's defaultState).
export function blankDraftWork() {
  return {
    workCat: '', workType: '', workDesc: '', workFeeRate: 0, workFeeUnit: 'EA', workSiteMax: 0, workSiteExtraRate: 0,
    wUse: '', wUseDesc: '', drillerName: '', drillerLic: '', dmeth: '', dmethName: '', dmethOth: '',
    numbore: '', holediam: '', maxdepth: '', wellSpecs: [makeWellSpec()],
  };
}

// Snapshot the current draft (flat fields) into a standalone work object.
export function extractDraftWork(fd) {
  const work = {};
  DRAFT_WORK_FIELDS.forEach((key) => { work[key] = fd[key]; });
  work.wellSpecs = (fd.wellSpecs || []).map((s) => ({ ...s }));
  return work;
}

// A draft only counts as a real work once a category + type have been chosen.
export function hasDraftWork(fd) {
  return !!(fd.workCat && fd.workType);
}

// The committed works plus the in-progress draft, de-duplicated by workEditIndex so a work being
// edited is previewed live in its own slot rather than double-counted.
export function effectiveWorks(fd) {
  const list = [...(fd.works || [])];
  if (hasDraftWork(fd)) {
    const draft = extractDraftWork(fd);
    const idx = fd.workEditIndex;
    if (idx != null && idx >= 0 && idx < list.length) list[idx] = draft;
    else list.push(draft);
  }
  return list;
}

// Fee for a single work. Mirrors the backend FeeCalculator / legacy BeanAppWrk.getWorkCalcAmount:
// rate × well count for every unit except "site". A "site" unit is a flat fee that covers up to
// workSiteMax wells/holes; each additional well beyond the max is charged the site-extra rate.
// Well count = rows × boreholes.
export function workFee(work) {
  const rate = Number(work.workFeeRate) || 0;
  const unit = (work.workFeeUnit || '').trim().toLowerCase();
  const wells = workWellCount(work);
  if (unit === 'site') {
    const siteMax = Number(work.workSiteMax) || 0;
    const extraRate = Number(work.workSiteExtraRate) || 0;
    const extraWells = siteMax > 0 ? Math.max(0, wells - siteMax) : 0;
    return Number((rate + extraWells * extraRate).toFixed(2));
  }
  return Number((rate * wells).toFixed(2));
}

// Number of wells/boreholes for a work (Σ drill_count, mirroring BeanAppWrk.countWells). Borehole
// (investigation / geo-probe) works are a single spec whose count is the Number of Boreholes entered
// on the work-info form; all other works are rows × boreholes across the well-spec table.
export function workWellCount(work) {
  const bores = Number(work.numbore) || 1;
  if (isBoreholeCategory(work.workCat)) return Number(work.numbore) || 1;
  const rows = (work.wellSpecs || []).length || 1;
  return rows * bores;
}

// Running total across every work on the application.
export function worksTotal(fd) {
  const total = effectiveWorks(fd).reduce((sum, w) => sum + workFee(w), 0);
  return Number(total.toFixed(2));
}
