// Hardcoded fallbacks used when the reference API is unavailable.

export const STATE_CODES = [
  'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA', 'HI', 'ID', 'IL',
  'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD', 'MA', 'MI', 'MN', 'MS', 'MO', 'MT',
  'NE', 'NV', 'NH', 'NJ', 'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI',
  'SC', 'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY',
].map((code) => ({ code, label: code }));

export const WORK_CATEGORIES = [
  { code: 'con', label: 'Construction' },
  { code: 'inv', label: 'Investigation / Monitoring' },
  { code: 'invprb', label: 'Investigation - Probe / Direct Push' },
  { code: 'des', label: 'Destruction' },
];

// keyed by work category code
export const WORK_TYPES = {
  con: [
    { code: 'con-dom', label: 'Domestic Water Well', feeRate: 379, feeUnit: 'EA', siteMax: 0 },
    { code: 'con-irr', label: 'Irrigation / Agricultural Well', feeRate: 379, feeUnit: 'EA', siteMax: 0 },
    { code: 'con-mon', label: 'Monitoring Well', feeRate: 195, feeUnit: 'EA', siteMax: 0 },
  ],
  inv: [
    { code: 'inv-mon', label: 'Environmental Investigation Well', feeRate: 195, feeUnit: 'EA', siteMax: 0 },
    { code: 'inv-cpt', label: 'Cone Penetration Test', feeRate: 195, feeUnit: 'EA', siteMax: 0 },
  ],
  invprb: [
    { code: 'invprb-dp', label: 'Direct Push / Probe Borings', feeRate: 150, feeUnit: 'EA', siteMax: 0 },
  ],
  des: [
    { code: 'des-well', label: 'Well Destruction', feeRate: 195, feeUnit: 'EA', siteMax: 0 },
  ],
};

export const DRILL_METHODS = [
  { code: 'MUD', label: 'Mud Rotary' },
  { code: 'AIR', label: 'Air Rotary' },
  { code: 'CABLE', label: 'Cable Tool' },
  { code: 'AUGER', label: 'Hollow Stem Auger' },
  { code: 'SONIC', label: 'Sonic' },
  { code: 'DIRECT', label: 'Direct Push' },
  { code: 'OTH', label: 'Other' },
];

export const WELL_USE_TYPES = [
  { code: 'DOM', label: 'Domestic' },
  { code: 'IRR', label: 'Irrigation' },
  { code: 'IND', label: 'Industrial' },
  { code: 'MON', label: 'Monitoring' },
  { code: 'MUN', label: 'Municipal' },
  { code: 'OTH', label: 'Other' },
];

export function formatDollar(value) {
  const num = Number(value) || 0;
  return num.toLocaleString('en-US', { style: 'currency', currency: 'USD' });
}

// The "Other" drilling method requires a free-text description. The reference API returns the code
// as "other" (lowercase) while legacy/fallbacks use "OTH", so match either the code or the label.
export function isOtherDrillMethod(code, name) {
  const c = (code || '').trim().toLowerCase();
  const n = (name || '').trim().toLowerCase();
  return c === 'oth' || c === 'other' || n === 'other';
}

// Borehole (investigation / geo-probe) categories collect a single set of Borehole Specifications
// (number of boreholes, hole diameter, max depth) rather than a per-well specifications table.
// Covers the "inv" and "invprb" category codes — mirrors the backend
// PermitDocumentService.BoreholeCategories = { "inv", "invprb" } and the legacy app_work_info.jsp
// branch (workCat == "inv" || workCat == "invprb").
export function isBoreholeCategory(cat) {
  return (cat || '').trim().toLowerCase().startsWith('inv');
}