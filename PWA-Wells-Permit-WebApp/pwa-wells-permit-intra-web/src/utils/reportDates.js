// Report date-preset resolver — mirrors the legacy intra BeanReportDates.setDate (param `dateR`).
// The backend endpoints take explicit start/end dates, so we resolve the preset on the client and
// send concrete dates. All dates are returned as `yyyy-MM-dd` strings (ISO date, no time) which the
// .NET `DateTime` query/JSON binder accepts.

function iso(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

// Preset options exposed in the report filter forms.
export const DATE_PRESETS = [
  { code: 'cd', label: 'Current Day' },
  { code: 'pd', label: 'Previous Day' },
  { code: 'mtd', label: 'Month to Date' },
  { code: 'pm', label: 'Previous Month' },
  { code: 'ytd', label: 'Year to Date' },
  { code: 'fytd', label: 'Fiscal Year to Date' },
  { code: 'sp', label: 'Specific Dates' },
];

// Resolve a preset code into { start, end } ISO date strings.
// For `sp` (specific), the caller supplies stDate/endDate (already yyyy-MM-dd) which pass through.
export function resolveDateRange(preset, stDate = '', endDate = '') {
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  switch (preset) {
    case 'cd':
      return { start: iso(today), end: iso(today) };
    case 'pd': {
      const prev = new Date(today);
      prev.setDate(prev.getDate() - 1);
      return { start: iso(prev), end: iso(prev) };
    }
    case 'mtd': {
      const first = new Date(today.getFullYear(), today.getMonth(), 1);
      return { start: iso(first), end: iso(today) };
    }
    case 'pm': {
      const first = new Date(today.getFullYear(), today.getMonth() - 1, 1);
      const last = new Date(today.getFullYear(), today.getMonth(), 0);
      return { start: iso(first), end: iso(last) };
    }
    case 'ytd': {
      const jan1 = new Date(today.getFullYear(), 0, 1);
      return { start: iso(jan1), end: iso(today) };
    }
    case 'fytd': {
      // Fiscal year starts Jul 1. Before July, the fiscal year began Jul 1 of the prior year.
      const fyStartYear = today.getMonth() < 6 ? today.getFullYear() - 1 : today.getFullYear();
      const jul1 = new Date(fyStartYear, 6, 1);
      return { start: iso(jul1), end: iso(today) };
    }
    case 'sp':
    default:
      return { start: stDate, end: endDate };
  }
}
