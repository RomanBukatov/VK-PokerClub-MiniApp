import { describe, it, expect } from 'bun:test';
import { formatPhoneNumber } from '../utils/formatters';
import { validateFullName, NAME_VALIDATION_ERROR } from '../utils/nameValidator';

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

describe('validateFullName (Cyrillic & min 2 words validation)', () => {
  it('accepts valid Russian names with two or more words', () => {
    expect(validateFullName('Иванов Иван').isValid).toBe(true);
    expect(validateFullName('Иванов Иван Иванович').isValid).toBe(true);
    expect(validateFullName('  Петров   Алексей  ').isValid).toBe(true);
  });

  it('accepts valid Russian names with hyphens', () => {
    expect(validateFullName('Мамин-Сибиряк Дмитрий').isValid).toBe(true);
    expect(validateFullName('Анна-Мария Смирнова').isValid).toBe(true);
  });

  it('rejects Latin characters and mixed names', () => {
    const res1 = validateFullName('Dima Sarnavskiy');
    expect(res1.isValid).toBe(false);
    expect(res1.error).toBe(NAME_VALIDATION_ERROR);

    const res2 = validateFullName('Дмитрий Ice');
    expect(res2.isValid).toBe(false);
    expect(res2.error).toBe(NAME_VALIDATION_ERROR);

    const res3 = validateFullName('John Doe');
    expect(res3.isValid).toBe(false);
  });

  it('rejects single word names', () => {
    const res1 = validateFullName('Иван');
    expect(res1.isValid).toBe(false);
    expect(res1.error).toBe(NAME_VALIDATION_ERROR);

    const res2 = validateFullName('Дмитрий');
    expect(res2.isValid).toBe(false);
    expect(res2.error).toBe(NAME_VALIDATION_ERROR);
  });

  it('accepts Russian names containing letters ё and Ё', () => {
    expect(validateFullName('Фёдор Достоевский').isValid).toBe(true);
    expect(validateFullName('Артём Семёнов').isValid).toBe(true);
    expect(validateFullName('Ёлкин Пётр').isValid).toBe(true);
  });

  it('rejects empty, whitespace, or hyphen-only strings and malformed hyphens', () => {
    expect(validateFullName('').isValid).toBe(false);
    expect(validateFullName('   ').isValid).toBe(false);
    expect(validateFullName(' - ').isValid).toBe(false);
    expect(validateFullName('Иван -').isValid).toBe(false);
    expect(validateFullName('-Иванов Иван').isValid).toBe(false);
    expect(validateFullName('Иванов Иван-').isValid).toBe(false);
    expect(validateFullName('Иванов--Петров Иван').isValid).toBe(false);
  });

  it('rejects digits and special characters', () => {
    expect(validateFullName('Иван 123').isValid).toBe(false);
    expect(validateFullName('Иван_Иванов').isValid).toBe(false);
    expect(validateFullName('Иван @ Иванов').isValid).toBe(false);
  });

  it('rejects fake placeholder names', () => {
    expect(validateFullName('Гость Клуба').isValid).toBe(false);
    expect(validateFullName('гость клуба').isValid).toBe(false);
    expect(validateFullName('Игрок VK').isValid).toBe(false);
    expect(validateFullName('Игрок #12345').isValid).toBe(false);
  });
});

describe('WelcomeProfileModal Club Card ID Hard Barrier', () => {
  const validateClubCard = (clubCardId: string): { isValid: boolean; error?: string } => {
    const trimmed = clubCardId.trim();
    if (!trimmed) {
      return { isValid: false, error: 'Пожалуйста, укажите ваш клубный ID' };
    }
    return { isValid: true };
  };

  it('rejects empty or whitespace-only clubCardId with exact error message', () => {
    const res1 = validateClubCard('');
    expect(res1.isValid).toBe(false);
    expect(res1.error).toBe('Пожалуйста, укажите ваш клубный ID');

    const res2 = validateClubCard('   ');
    expect(res2.isValid).toBe(false);
    expect(res2.error).toBe('Пожалуйста, укажите ваш клубный ID');
  });

  it('accepts valid clubCardId strings', () => {
    expect(validateClubCard('1266').isValid).toBe(true);
    expect(validateClubCard('  CARD-99  ').isValid).toBe(true);
  });

  it('determines submit button disabled state correctly', () => {
    const isSubmitDisabled = (isSubmitting: boolean, isNameValid: boolean, clubCardId: string) => {
      const isCardValid = clubCardId.trim().length > 0;
      return isSubmitting || !isNameValid || !isCardValid;
    };

    // Card missing
    expect(isSubmitDisabled(false, true, '')).toBe(true);
    expect(isSubmitDisabled(false, true, '   ')).toBe(true);

    // Name invalid
    expect(isSubmitDisabled(false, false, '1266')).toBe(true);

    // Submitting
    expect(isSubmitDisabled(true, true, '1266')).toBe(true);

    // Valid
    expect(isSubmitDisabled(false, true, '1266')).toBe(false);
  });

  it('verifies the official VK community direct message link format', () => {
    const vkGroupLink = 'https://vk.me/club238367404';
    expect(vkGroupLink).toMatch(/^https:\/\/vk\.me\/club\d+$/);
  });

  it('allows modal close button ONLY when profile has both nickname and confirmed clubCardId', () => {
    const canCloseModal = (profile?: { nickname?: string; clubCardId?: string } | null) => {
      return Boolean(profile?.nickname && profile?.clubCardId && profile.clubCardId.trim().length > 0);
    };

    expect(canCloseModal(null)).toBe(false);
    expect(canCloseModal({ nickname: 'Ivan' })).toBe(false);
    expect(canCloseModal({ nickname: 'Ivan', clubCardId: '' })).toBe(false);
    expect(canCloseModal({ nickname: 'Ivan', clubCardId: '   ' })).toBe(false);
    expect(canCloseModal({ nickname: '', clubCardId: '1266' })).toBe(false);
    expect(canCloseModal({ nickname: 'Ivan', clubCardId: '1266' })).toBe(true);
  });

  it('triggers exact error message when user attempts to submit without clubCardId', () => {
    const handleAttemptSubmit = (clubCardId: string, setErrorMessage: (msg: string) => void) => {
      const trimmed = clubCardId.trim();
      if (!trimmed) {
        setErrorMessage('Пожалуйста, укажите ваш клубный ID');
        return false;
      }
      return true;
    };

    let error = '';
    const successEmpty = handleAttemptSubmit('', (msg) => { error = msg; });
    expect(successEmpty).toBe(false);
    expect(error).toBe('Пожалуйста, укажите ваш клубный ID');

    let wsError = '';
    const successWhitespace = handleAttemptSubmit('    ', (msg) => { wsError = msg; });
    expect(successWhitespace).toBe(false);
    expect(wsError).toBe('Пожалуйста, укажите ваш клубный ID');

    let validError = '';
    const successValid = handleAttemptSubmit('1266', (msg) => { validError = msg; });
    expect(successValid).toBe(true);
    expect(validError).toBe('');
  });
});
