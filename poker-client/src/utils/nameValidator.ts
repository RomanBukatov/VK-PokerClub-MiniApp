export const NAME_VALIDATION_ERROR = 'Пожалуйста, введите реальные Фамилию и Имя на русском языке (минимум 2 слова)';

export const validateFullName = (name: string): { isValid: boolean; error?: string } => {
  const trimmed = name.trim();
  if (!trimmed) {
    return { isValid: false, error: NAME_VALIDATION_ERROR };
  }

  // Только кириллица (русские буквы), пробелы и дефисы
  if (!/^[а-яА-ЯёЁ\s-]+$/.test(trimmed)) {
    return { isValid: false, error: NAME_VALIDATION_ERROR };
  }

  // Минимум 2 слова
  const words = trimmed.split(/\s+/).filter(Boolean);
  if (words.length < 2) {
    return { isValid: false, error: NAME_VALIDATION_ERROR };
  }

  // Каждое слово должно состоять строго из русских букв (с возможными дефисными частями, например Мамин-Сибиряк)
  const allWordsValid = words.every((w) => /^[а-яА-ЯёЁ]+(-[а-яА-ЯёЁ]+)*$/.test(w));
  if (!allWordsValid) {
    return { isValid: false, error: NAME_VALIDATION_ERROR };
  }

  // Запрет фейковых плейсхолдеров
  const lower = trimmed.toLowerCase();
  if (
    lower === 'гость клуба' ||
    lower === 'клуба гость' ||
    lower === 'гость' ||
    lower === 'игрок vk' ||
    lower === 'игрок' ||
    lower.startsWith('игрок #') ||
    /^игрок\s*\d+$/i.test(lower)
  ) {
    return { isValid: false, error: NAME_VALIDATION_ERROR };
  }

  return { isValid: true };
};
