import { describe, it, expect } from 'bun:test';
import { getPlayerInitials } from './PlayerAvatar';

describe('PlayerAvatar getPlayerInitials logic', () => {
  it('extracts initials from separate first and last name', () => {
    expect(getPlayerInitials('Василий', 'Лукашенко')).toBe('ВЛ');
    expect(getPlayerInitials('Роман', 'Букатов')).toBe('РБ');
    expect(getPlayerInitials('John', 'Doe')).toBe('JD');
  });

  it('extracts initials from single combined name in firstName', () => {
    expect(getPlayerInitials('Василий Лукашенко', '')).toBe('ВЛ');
    expect(getPlayerInitials('Василий Лукашенко', null)).toBe('ВЛ');
  });

  it('extracts initials from single combined name in lastName', () => {
    expect(getPlayerInitials('', 'Василий Лукашенко')).toBe('ВЛ');
  });

  it('handles single-word name by taking first two characters', () => {
    expect(getPlayerInitials('Василий', '')).toBe('ВА');
    expect(getPlayerInitials('Alex', '')).toBe('AL');
  });

  it('handles single-character name by returning single uppercase character', () => {
    expect(getPlayerInitials('В', '')).toBe('В');
  });

  it('ignores dummy VK suffix like "VK" and extracts from first name', () => {
    expect(getPlayerInitials('Василий', 'VK')).toBe('ВА');
    expect(getPlayerInitials('Иван', 'vk')).toBe('ИВ');
  });

  it('falls back to nickname if firstName and lastName are missing', () => {
    expect(getPlayerInitials(null, null, 'PokerKing')).toBe('PO');
    expect(getPlayerInitials('', '', 'AceHigh')).toBe('AC');
  });

  it('falls back to poker suit ♠️ if no names or nicknames are available', () => {
    expect(getPlayerInitials(null, null, null)).toBe('♠️');
    expect(getPlayerInitials('', '', '')).toBe('♠️');
    expect(getPlayerInitials('   ', '   ', '   ')).toBe('♠️');
  });

  it('handles irregular whitespace and lowercase names properly', () => {
    expect(getPlayerInitials('  иван  ', '  иванов  ')).toBe('ИИ');
    expect(getPlayerInitials('  Василий   Лукашенко  ', '')).toBe('ВЛ');
  });

  it('handles hyphenated names and punctuation', () => {
    expect(getPlayerInitials('Анна-Мария', '')).toBe('АН');
  });
});
