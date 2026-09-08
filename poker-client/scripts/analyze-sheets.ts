import fs from 'fs';

const DEFAULT_SHEET_URL =
  'https://docs.google.com/spreadsheets/d/1zxU_LO5jIsjrEHw7eq366BQMLWQ4x8pSDIiMdUxWPOs/export?format=csv&gid=0';

export function detectDelimiter(text: string): string {
  const firstLine = text.split(/\r?\n/)[0] || '';
  let inQuotes = false;
  let commas = 0;
  let semicolons = 0;
  let tabs = 0;

  for (let i = 0; i < firstLine.length; i++) {
    const char = firstLine[i];
    if (char === '"') {
      inQuotes = !inQuotes;
    } else if (!inQuotes) {
      if (char === ',') commas++;
      else if (char === ';') semicolons++;
      else if (char === '\t') tabs++;
    }
  }

  if (semicolons > commas && semicolons > tabs) return ';';
  if (tabs > commas && tabs > semicolons) return '\t';
  return ',';
}

export function parseCsv(text: string, customDelimiter?: string): string[][] {
  const delimiter = customDelimiter || detectDelimiter(text);
  const rows: string[][] = [];
  let currentRow: string[] = [];
  let currentField = '';
  let inQuotes = false;

  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    const nextChar = text[i + 1];

    if (inQuotes) {
      if (char === '"' && nextChar === '"') {
        currentField += '"';
        i++;
      } else if (char === '"') {
        inQuotes = false;
      } else {
        currentField += char;
      }
    } else {
      if (char === '"') {
        inQuotes = true;
      } else if (char === delimiter) {
        currentRow.push(currentField.trim());
        currentField = '';
      } else if (char === '\r') {
        if (nextChar === '\n') i++;
        currentRow.push(currentField.trim());
        rows.push(currentRow);
        currentRow = [];
        currentField = '';
      } else if (char === '\n') {
        currentRow.push(currentField.trim());
        rows.push(currentRow);
        currentRow = [];
        currentField = '';
      } else {
        currentField += char;
      }
    }
  }

  if (currentField || currentRow.length > 0) {
    currentRow.push(currentField.trim());
    rows.push(currentRow);
  }

  return rows.filter((r) => r.some((cell) => cell.length > 0));
}

async function main() {
  const arg = process.argv[2];
  const target = arg || DEFAULT_SHEET_URL;

  console.log(`[Google Sheets Analyzer] Цель: ${target}`);

  let csvContent = '';

  if (fs.existsSync(target)) {
    console.log(`Чтение локального CSV-файла: ${target}`);
    csvContent = fs.readFileSync(target, 'utf-8');
  } else {
    console.log(`Отправка запроса на выгрузку CSV: ${target}...`);
    try {
      const response = await fetch(target, {
        headers: {
          'User-Agent':
            'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        },
      });

      if (!response.ok) {
        console.warn(
          `\n[Диагностика HTTP] Сервер Google вернул HTTP ${response.status} ${response.statusText} для указанного URL.`
        );
        console.warn(
          'Причина: Ссылка на таблицу закрыта настройками приватности Google Drive (доступ только владельцу) либо документ не опубликован.'
        );
        console.warn('\nКак исправить доступ в Google Таблицах:');
        console.warn('1. Способ «Опубликовать в Интернете» (Рекомендуется для автоматического импорта):');
        console.warn('   Файл -> Поделиться -> Опубликовать в Интернете -> Выбрать «Весь документ» или «Лист 1» -> Формат: CSV -> Опубликовать.');
        console.warn('2. Способ «Открыть доступ по ссылке»:');
        console.warn('   Поделиться -> Общий доступ: Все, у кого есть ссылка -> Читатель.');
        console.warn('3. Локальный запуск скрипта:');
        console.warn('   bun run scripts/analyze-sheets.ts <путь_к_скачанному_файлу.csv>\n');
      } else {
        csvContent = await response.text();
      }
    } catch (err) {
      console.error('Сетевая ошибка при загрузке таблицы:', err);
    }
  }

  if (csvContent && csvContent.trim().length > 0) {
    const delim = detectDelimiter(csvContent);
    console.log(`Определен разделитель CSV: "${delim === '\t' ? '\\t (Tab)' : delim}"`);
    const rows = parseCsv(csvContent, delim);
    const headers = rows[0] || [];

    console.log('\n================ ЗАГОЛОВКИ СТОЛБЦОВ ================');
    headers.forEach((h, idx) => {
      console.log(`[Колонка ${idx + 1}] "${h}"`);
    });

    console.log('\n================ ПЕРВЫЕ СТРОКИ ДАННЫХ ================');
    rows.slice(1, 6).forEach((row, rowIdx) => {
      console.log(`\n--- Строка ${rowIdx + 1} ---`);
      headers.forEach((h, colIdx) => {
        console.log(`  ${h || `Колонка ${colIdx + 1}`}: "${row[colIdx] ?? ''}"`);
      });
    });

    console.log('\n================ РЕЗЮМЕ СТРУКТУРЫ ================');
    console.log(`Всего колонок: ${headers.length}`);
    console.log(`Всего строк данных: ${rows.length - 1}`);
    console.log('Список полей JSON:', JSON.stringify(headers, null, 2));
  } else {
    console.log('\n================ РЕКОМЕНДУЕМАЯ МОДЕЛЬ ДАННЫХ ДЛЯ ИМПОРТА РЕЙТИНГА СТАСА ================');
    console.log('Архитектурная схема импорта для PokerClub.Api (User, RatingRecord, TournamentRegistration):');
    const recommendedModel = [
      { column: 'Место / Ранг', field: 'rank', type: 'number', required: false, description: 'Порядковый номер в сезоне' },
      { column: 'Игрок / ФИО', field: 'playerName', type: 'string', required: true, description: 'Полное имя (например: Станислав Костров, Алексей Крылов)' },
      { column: 'VK ID', field: 'vkId', type: 'string', required: true, description: 'Цифровой ID страницы ВКонтакте (например: 123456789)' },
      { column: 'Очки / Рейтинг', field: 'totalRating', type: 'number', required: true, description: 'Суммарные очки игрока за сезон' },
      { column: 'Сыграно турниров', field: 'tournamentsPlayed', type: 'number', required: false, description: 'Общее число сыгранных турниров' },
      { column: 'Победы (Топ-1)', field: 'winsCount', type: 'number', required: false, description: 'Количество выигранных турниров' },
      { column: 'Финальные столы', field: 'finalTablesCount', type: 'number', required: false, description: 'Попадания в топ-9' },
    ];
    console.table(recommendedModel);
  }
}

main();
