import {
  blankDraftWork,
  extractDraftWork,
  hasDraftWork,
  effectiveWorks,
  workFee,
  workWellCount,
  worksTotal,
} from './works';

function draft(overrides = {}) {
  return { ...blankDraftWork(), ...overrides };
}

describe('works helpers', () => {
  test('hasDraftWork requires category and type', () => {
    expect(hasDraftWork(draft())).toBe(false);
    expect(hasDraftWork(draft({ workCat: 'con' }))).toBe(false);
    expect(hasDraftWork(draft({ workCat: 'con', workType: 'NEW' }))).toBe(true);
  });

  test('workFee: per-well rate multiplied by rows x boreholes', () => {
    const w = draft({
      workFeeRate: 100,
      workFeeUnit: 'EA',
      numbore: '2',
      wellSpecs: [{}, {}, {}],
    });
    // 3 rows x 2 boreholes x $100 = $600
    expect(workFee(w)).toBe(600);
    expect(workWellCount(w)).toBe(6);
  });

  test('workFee: site unit is a flat fee', () => {
    const w = draft({ workFeeRate: 445, workFeeUnit: 'site', wellSpecs: [{}, {}] });
    expect(workFee(w)).toBe(445);
  });

  test('workFee: tiered site unit adds the site-extra rate per well beyond siteMax', () => {
    // 6 wells (6 rows x 1 borehole): flat 445 covers the first 4, then 2 extra x 85 = 170. Total 615.
    const w = draft({
      workFeeRate: 445,
      workFeeUnit: 'site',
      workSiteMax: 4,
      workSiteExtraRate: 85,
      wellSpecs: [{}, {}, {}, {}, {}, {}],
    });
    expect(workWellCount(w)).toBe(6);
    expect(workFee(w)).toBe(615);
  });

  test('workFee: tiered site unit at or below siteMax stays at the flat fee', () => {
    const w = draft({
      workFeeRate: 445,
      workFeeUnit: 'site',
      workSiteMax: 4,
      workSiteExtraRate: 85,
      wellSpecs: [{}, {}, {}, {}],
    });
    expect(workFee(w)).toBe(445);
  });

  test('effectiveWorks merges the draft into its edit slot without double counting', () => {
    const committed = draft({ workCat: 'con', workType: 'A', workFeeRate: 100, wellSpecs: [{}] });
    const fd = {
      works: [committed],
      workEditIndex: 0,
      ...draft({ workCat: 'con', workType: 'A', workFeeRate: 250, wellSpecs: [{}] }),
    };
    const eff = effectiveWorks(fd);
    expect(eff).toHaveLength(1);
    expect(workFee(eff[0])).toBe(250);
    expect(worksTotal(fd)).toBe(250);
  });

  test('effectiveWorks appends a new draft when not editing', () => {
    const committed = draft({ workCat: 'con', workType: 'A', workFeeRate: 100, wellSpecs: [{}] });
    const fd = {
      works: [committed],
      workEditIndex: null,
      ...draft({ workCat: 'con', workType: 'B', workFeeRate: 300, wellSpecs: [{}] }),
    };
    expect(effectiveWorks(fd)).toHaveLength(2);
    expect(worksTotal(fd)).toBe(400);
  });

  test('extractDraftWork deep-copies well specs', () => {
    const fd = draft({ workCat: 'con', workType: 'A', wellSpecs: [{ owellnum: '1' }] });
    const snap = extractDraftWork(fd);
    snap.wellSpecs[0].owellnum = '999';
    expect(fd.wellSpecs[0].owellnum).toBe('1');
  });
});
