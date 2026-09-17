// Alameda County observed holidays, weekend detection, and small date helpers — a faithful port of
// the legacy JBoss ecomm CountyHolidays / BeanCalendar.isWeekend. Weekends and observed County
// holidays are not available for inspection, so they gate valid project start/completion dates.
// All dates are treated as LOCAL dates (constructed via new Date(y, m, d)) to avoid UTC off-by-one.

export function isWeekend(date) {
  const day = date.getDay();
  return day === 0 || day === 6;
}

function observedFixed(year, month, day) {
  // month is 1-12. Saturday -> observed Friday, Sunday -> observed Monday.
  const d = new Date(year, month - 1, day);
  const dow = d.getDay();
  if (dow === 6) d.setDate(d.getDate() - 1);
  else if (dow === 0) d.setDate(d.getDate() + 1);
  return d;
}

function observedVeterans(year) {
  const d = new Date(year, 10, 11); // Nov 11
  const dow = d.getDay();
  if (dow === 0) d.setDate(12); // Sunday -> Monday Nov 12
  else if (dow === 6) d.setDate(13); // Saturday -> Monday Nov 13 (matches legacy)
  return d;
}

function nthWeekday(year, month, weekday, occurrence) {
  // month 1-12, weekday 0-6 (0 = Sunday).
  const first = new Date(year, month - 1, 1);
  const offset = ((weekday - first.getDay()) + 7) % 7;
  return new Date(year, month - 1, 1 + offset + 7 * (occurrence - 1));
}

function lastWeekday(year, month, weekday) {
  const last = new Date(year, month, 0); // day 0 of next month = last day of this month
  const offset = ((last.getDay() - weekday) + 7) % 7;
  return new Date(year, month - 1, last.getDate() - offset);
}

function dayKey(date) {
  return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
}

function observedHolidaySet(year) {
  const thanksgivingThu = nthWeekday(year, 11, 4, 4);
  const thanksgivingFri = new Date(thanksgivingThu);
  thanksgivingFri.setDate(thanksgivingThu.getDate() + 1);

  const dates = [
    observedFixed(year, 1, 1), // New Year's Day
    nthWeekday(year, 1, 1, 3), // Martin Luther King Jr. Day (3rd Mon Jan)
    observedFixed(year, 2, 12), // Lincoln's Day
    nthWeekday(year, 2, 1, 3), // Presidents' Day (3rd Mon Feb)
    lastWeekday(year, 5, 1), // Memorial Day (last Mon May)
    observedFixed(year, 7, 4), // Independence Day
    nthWeekday(year, 9, 1, 1), // Labor Day (1st Mon Sep)
    observedVeterans(year), // Veterans Day
    thanksgivingThu, // Thanksgiving
    thanksgivingFri, // Day after Thanksgiving
    observedFixed(year, 12, 25), // Christmas
    observedFixed(year + 1, 1, 1), // next year's New Year observed on Dec 31 when Jan 1 is Saturday
  ];
  return new Set(dates.map(dayKey));
}

const holidayCache = {};

export function isCountyHoliday(date) {
  const year = date.getFullYear();
  if (!holidayCache[year]) holidayCache[year] = observedHolidaySet(year);
  return holidayCache[year].has(dayKey(date));
}

// ---- date helpers (local-date, no timezone shift) ----

export function parseLocalDate(value) {
  if (!value) return null;
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value;
  const s = String(value).trim();
  let m = s.match(/^(\d{4})-(\d{2})-(\d{2})/); // ISO yyyy-mm-dd(...)
  if (m) return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  m = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})/); // mm/dd/yyyy
  if (m) return new Date(Number(m[3]), Number(m[1]) - 1, Number(m[2]));
  const d = new Date(s);
  return Number.isNaN(d.getTime()) ? null : d;
}

export function addDays(date, n) {
  const d = new Date(date);
  d.setDate(d.getDate() + n);
  return d;
}

export function startOfToday() {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
}

export function toIsoDate(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function formatUsDate(date) {
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${m}/${d}/${date.getFullYear()}`;
}

// Legacy rule: valid project start date is 10..90 days from today.
export const VALID_START_MIN_DAYS = 10;
export const VALID_START_MAX_DAYS = 90;
