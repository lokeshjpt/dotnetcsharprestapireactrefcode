import {
  isEmpty,
  hasQuotes,
  isEmail,
  isNumeric,
  isDecimal,
  validateText,
  validateEmail,
  validateZip,
  validateNumeric,
  validateDecimal,
  validateFutureDate,
  validateAfter,
  validateDate,
} from './validation';

describe('validation primitives', () => {
  test('isEmpty', () => {
    expect(isEmpty(undefined)).toBe(true);
    expect(isEmpty(null)).toBe(true);
    expect(isEmpty('   ')).toBe(true);
    expect(isEmpty('x')).toBe(false);
    expect(isEmpty(0)).toBe(false);
  });

  test('hasQuotes', () => {
    expect(hasQuotes("O'Brien")).toBe(true);
    expect(hasQuotes('say "hi"')).toBe(true);
    expect(hasQuotes('clean')).toBe(false);
  });

  test('isEmail', () => {
    expect(isEmail('a@b.com')).toBe(true);
    expect(isEmail('bad')).toBe(false);
    expect(isEmail('a@b')).toBe(false);
  });

  test('isNumeric', () => {
    expect(isNumeric('123')).toBe(true);
    expect(isNumeric('12.3')).toBe(false);
    expect(isNumeric('abc')).toBe(false);
  });

  test('isDecimal', () => {
    expect(isDecimal('12')).toBe(true);
    expect(isDecimal('12.34')).toBe(true);
    expect(isDecimal('12.')).toBe(false);
    expect(isDecimal('x')).toBe(false);
  });
});

describe('validateText', () => {
  test('required empty returns message', () => {
    expect(validateText('', { label: 'Name', required: true })).toMatch(/required/);
  });
  test('optional empty returns null', () => {
    expect(validateText('', { label: 'Name', required: false })).toBeNull();
  });
  test('quotes rejected', () => {
    expect(validateText("O'x", { label: 'Name', required: true })).toMatch(/quotes/);
  });
  test('maxLen enforced', () => {
    expect(validateText('xxxxx', { label: 'Name', required: true, maxLen: 3 })).toMatch(/3 characters/);
  });
  test('valid returns null', () => {
    expect(validateText('John', { label: 'Name', required: true, maxLen: 50 })).toBeNull();
  });
});

describe('validateEmail / validateZip / validateNumeric / validateDecimal', () => {
  test('email', () => {
    expect(validateEmail('', { label: 'E', required: true })).toMatch(/required/);
    expect(validateEmail('bad', { label: 'E', required: false })).toMatch(/valid email/);
    expect(validateEmail('a@b.com', { label: 'E', required: true })).toBeNull();
  });
  test('zip', () => {
    expect(validateZip('123', { label: 'Z', required: true })).toMatch(/5 digits/);
    expect(validateZip('94607', { label: 'Z', required: true })).toBeNull();
    expect(validateZip('', { label: 'Z', required: false })).toBeNull();
  });
  test('numeric', () => {
    expect(validateNumeric('12a', { label: 'N', required: true })).toMatch(/numeric/);
    expect(validateNumeric('123456', { label: 'N', required: true, maxLen: 5 })).toMatch(/5 digits/);
    expect(validateNumeric('123', { label: 'N', required: true, maxLen: 5 })).toBeNull();
  });
  test('decimal', () => {
    expect(validateDecimal('x', { label: 'D', required: true })).toMatch(/number/);
    expect(validateDecimal('1.5', { label: 'D', required: true })).toBeNull();
    expect(validateDecimal('', { label: 'D', required: false })).toBeNull();
  });
});

describe('date validators', () => {
  const future = new Date(Date.now() + 10 * 86400000).toISOString().slice(0, 10);
  const past = new Date(Date.now() - 10 * 86400000).toISOString().slice(0, 10);

  test('validateFutureDate', () => {
    expect(validateFutureDate('', { label: 'D', required: true })).toMatch(/required/);
    expect(validateFutureDate('not-a-date', { label: 'D', required: true })).toMatch(/valid date/);
    expect(validateFutureDate(past, { label: 'D', required: true })).toMatch(/future date/);
    expect(validateFutureDate(future, { label: 'D', required: true })).toBeNull();
  });

  test('validateAfter', () => {
    expect(validateAfter('', past, { label: 'End', otherLabel: 'start', required: true })).toMatch(/required/);
    expect(validateAfter(past, future, { label: 'End', otherLabel: 'start', required: true })).toMatch(/after start/);
    expect(validateAfter(future, past, { label: 'End', otherLabel: 'start', required: true })).toBeNull();
  });

  test('validateDate', () => {
    expect(validateDate('', { label: 'D', required: true })).toMatch(/required/);
    expect(validateDate("2020'01", { label: 'D', required: true })).toMatch(/quotes/);
    expect(validateDate('nope', { label: 'D', required: true })).toMatch(/valid date/);
    expect(validateDate('2024-01-15', { label: 'D', required: true })).toBeNull();
  });
});
