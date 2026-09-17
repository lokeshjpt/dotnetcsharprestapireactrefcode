// Client-side validation matching the legacy Validator rules.

import {
  isWeekend,
  isCountyHoliday,
  parseLocalDate,
  addDays,
  startOfToday,
  formatUsDate,
  VALID_START_MIN_DAYS,
  VALID_START_MAX_DAYS,
} from './countyHolidays';

const QUOTE_RE = /['"]/;
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const NUMERIC_RE = /^\d+$/;

export function isEmpty(value) {
  return value === undefined || value === null || String(value).trim() === '';
}

export function hasQuotes(value) {
  return QUOTE_RE.test(String(value || ''));
}

export function isEmail(value) {
  return EMAIL_RE.test(String(value || '').trim());
}

export function isNumeric(value) {
  return NUMERIC_RE.test(String(value || '').trim());
}

export function isDecimal(value) {
  return /^\d+(\.\d+)?$/.test(String(value || '').trim());
}

function parseDate(value) {
  if (isEmpty(value)) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

// ---- text field validators (return message or null) ----
export function validateText(value, { label, required, maxLen }) {
  if (isEmpty(value)) {
    return required ? `${label} is required.` : null;
  }
  if (hasQuotes(value)) return `${label} cannot contain quotes.`;
  if (maxLen && String(value).length > maxLen) return `${label} must be ${maxLen} characters or fewer.`;
  return null;
}

export function validateEmail(value, { label, required }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (!isEmail(value)) return `${label} must be a valid email address.`;
  return null;
}

export function validateZip(value, { label, required }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (!/^\d{5}$/.test(String(value).trim())) return `${label} must be 5 digits.`;
  return null;
}

export function validateNumeric(value, { label, required, maxLen }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (!isNumeric(value)) return `${label} must be numeric.`;
  if (maxLen && String(value).length > maxLen) return `${label} must be ${maxLen} digits or fewer.`;
  return null;
}

export function validateDecimal(value, { label, required }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (!isDecimal(value)) return `${label} must be a number.`;
  return null;
}

export function validateFutureDate(value, { label, required }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  const d = parseDate(value);
  if (!d) return `${label} must be a valid date.`;
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  if (d < today) return `${label} must be a future date.`;
  return null;
}

export function validateAfter(value, otherValue, { label, otherLabel, required }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  const d = parseDate(value);
  if (!d) return `${label} must be a valid date.`;
  const other = parseDate(otherValue);
  if (other && d < other) return `${label} must be after ${otherLabel}.`;
  return null;
}

export function validateDate(value, { label, required }) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  if (hasQuotes(value)) return `${label} cannot contain quotes.`;
  if (!parseDate(value)) return `${label} must be a valid date.`;
  return null;
}

// ---- inspection-availability project date validators (legacy app_avail_cal.jsp rules) ----

export function validateStartDate(value, { label = 'Project Start Date', required = true } = {}) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  const d = parseLocalDate(value);
  if (!d) return `${label} must be a valid date.`;
  const today = startOfToday();
  const min = addDays(today, VALID_START_MIN_DAYS);
  const max = addDays(today, VALID_START_MAX_DAYS);
  if (d < min || d > max) {
    return `${label} must be between ${formatUsDate(min)} and ${formatUsDate(max)}.`;
  }
  if (isWeekend(d) || isCountyHoliday(d)) {
    return `${label} cannot be a weekend or County holiday.`;
  }
  return null;
}

export function validateEndDate(value, startValue, { label = 'Project Completion Date', required = true } = {}) {
  if (isEmpty(value)) return required ? `${label} is required.` : null;
  const d = parseLocalDate(value);
  if (!d) return `${label} must be a valid date.`;
  const today = startOfToday();
  const min = addDays(today, VALID_START_MIN_DAYS);
  if (d < min) return `${label} must be on or after ${formatUsDate(min)}.`;
  const start = parseLocalDate(startValue);
  if (start && d < start) return `${label} must be on or after the Project Start Date.`;
  if (isWeekend(d) || isCountyHoliday(d)) {
    return `${label} cannot be a weekend or County holiday.`;
  }
  return null;
}