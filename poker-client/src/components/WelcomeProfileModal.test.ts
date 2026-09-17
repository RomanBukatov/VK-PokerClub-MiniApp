import { describe, it, expect } from 'bun:test';
import { formatPhoneNumber } from '../utils/formatters';

describe('formatPhoneNumber', () => {
  it('returns empty string for empty input or non-digits', () => {
    expect(formatPhoneNumber('')).toBe('');
    expect(formatPhoneNumber('abc')).toBe('');
    expect(formatPhoneNumber('+')).toBe('');
    expect(formatPhoneNumber('+7')).toBe('');
  });

  it('formats partial input without premature trailing separators', () => {
    // 1 to 3 digits
    expect(formatPhoneNumber('9')).toBe('+7 (9');
    expect(formatPhoneNumber('91')).toBe('+7 (91');
    expect(formatPhoneNumber('915')).toBe('+7 (915)');

    // 4 to 6 digits (should NOT have trailing hyphen at 6 digits)
    expect(formatPhoneNumber('9156')).toBe('+7 (915) 6');
    expect(formatPhoneNumber('91566')).toBe('+7 (915) 66');
    expect(formatPhoneNumber('915666')).toBe('+7 (915) 666');

    // 7 to 8 digits (should NOT have trailing hyphen at 8 digits)
    expect(formatPhoneNumber('9156666')).toBe('+7 (915) 666-6');
    expect(formatPhoneNumber('91566666')).toBe('+7 (915) 666-66');

    // 9 to 10 digits
    expect(formatPhoneNumber('915666667')).toBe('+7 (915) 666-66-7');
    expect(formatPhoneNumber('9156666677')).toBe('+7 (915) 666-66-77');
  });

  it('handles input with leading country code 7 or 8', () => {
    expect(formatPhoneNumber('79156666677')).toBe('+7 (915) 666-66-77');
    expect(formatPhoneNumber('89156666677')).toBe('+7 (915) 666-66-77');
    expect(formatPhoneNumber('+7 (915) 666-66-77')).toBe('+7 (915) 666-66-77');
  });

  it('truncates digits beyond 10', () => {
    expect(formatPhoneNumber('91566666779999')).toBe('+7 (915) 666-66-77');
  });

  it('allows natural backspace deletion without trap', () => {
    // Simulating user deleting the last digit from 8 digits:
    const at8 = formatPhoneNumber('91566666'); // '+7 (915) 666-66'
    expect(at8).toBe('+7 (915) 666-66');
    expect(at8.endsWith('-')).toBe(false);

    // Deleting character leaves 7 digits
    const at7 = formatPhoneNumber(at8.slice(0, -1)); // '+7 (915) 666-6'
    expect(at7).toBe('+7 (915) 666-6');

    // Deleting character leaves 6 digits (no trailing hyphen)
    const at6 = formatPhoneNumber(at7.slice(0, -1)); // '+7 (915) 666'
    expect(at6).toBe('+7 (915) 666');
    expect(at6.endsWith('-')).toBe(false);
  });
});
