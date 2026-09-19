/**
 * Скрипт для инспекции структуры листов таблицы "МК РЕЙТИНГ"
 * Spreadsheet ID: 1zxU_LOSjIsjrEHw7eq366BQMLWQ4x8pSDIiMdUxWPOs
 */

const SPREADSHEET_ID = process.env.GOOGLE_SHEETS_SPREADSHEET_ID || '1zxU_LOSjIsjrEHw7eq366BQMLWQ4x8pSDIiMdUxWPOs';

const TARGET_SHEETS = [
  'Общий рейтинг',
  'Осенний сезон 2026',
  'Сезон открытия',
  'ТОП-20 турниров',
  'Рейтинг сезона',
  'РЕГИСТРАЦИИ',
];

/**
 * Надежный парсер CSV с поддержкой RFC 4180 (кавычки, переводы строк внутри ячеек)
 */
function parseCSV(text: string): string[][] {
  const rows: string[][] = [];
  let currentRow: string[] = [];
  let currentCell = '';
  let insideQuotes = false;

  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    const nextChar = text[i + 1];

    if (char === '"') {
      if (insideQuotes && nextChar === '"') {
        currentCell += '"';
        i++;
      } else {
        insideQuotes = !insideQuotes;
      }
    } else if (char === ',' && !insideQuotes) {
      currentRow.push(currentCell.trim());
      currentCell = '';
    } else if ((char === '\r' || char === '\n') && !insideQuotes) {
      if (char === '\r' && nextChar === '\n') {
        i++;
      }
      currentRow.push(currentCell.trim());
      if (currentRow.some((cell) => cell.length > 0)) {
        rows.push(currentRow);
      }
      currentRow = [];
      currentCell = '';
    } else {
      currentCell += char;
    }
  }

  if (currentCell.length > 0 || currentRow.length > 0) {
    currentRow.push(currentCell.trim());
    if (currentRow.some((cell) => cell.length > 0)) {
      rows.push(currentRow);
    }
  }

  return rows;
}

function detectType(sampleValues: string[]): string {
  const nonEmpties = sampleValues.filter((v) => v !== '' && v !== null && v !== undefined);
  if (nonEmpties.length === 0) return 'string?';

  const isInt = nonEmpties.every((v) => /^-?\d+$/.test(v));
  if (isInt) return 'integer';

  const isFloat = nonEmpties.every((v) => /^-?\d+([.,]\d+)?$/.test(v));
  if (isFloat) return 'decimal';

  const isDate = nonEmpties.every((v) => /^\d{1,2}[./-]\d{1,2}[./-]\d{2,4}(\s+\d{1,2}:\d{2}(:\d{2})?)?$/.test(v));
  if (isDate) return 'datetime';

  return 'string';
}

async function inspectSheet(sheetName: string) {
  const url = `https://docs.google.com/spreadsheets/d/${SPREADSHEET_ID}/gviz/tq?tqx=out:csv&sheet=${encodeURIComponent(sheetName)}`;

  console.log(`\n======================================================================`);
  console.log(`📑 ЛИСТ: "${sheetName}"`);
  console.log(`🔗 URL: ${url}`);
  console.log(`======================================================================`);

  try {
    const res = await fetch(url, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
      },
    });

    if (!res.ok) {
      console.error(`❌ Ошибка HTTP ${res.status} ${res.statusText}`);
      const body = await res.text();
      console.error(`Ответ:`, body.slice(0, 300));
      return null;
    }

    const csvText = await res.text();
    const rows = parseCSV(csvText);

    if (rows.length === 0) {
      console.warn(`⚠️ Лист пуст или не содержит строк.`);
      return null;
    }

    const headers = rows[0];
    const dataRows = rows.slice(1);

    console.log(`✅ Успешно получено: ${dataRows.length} строк данных, ${headers.length} колонок.`);

    console.log(`\n📋 СПИСОК КОЛОНОК:`);
    headers.forEach((h, idx) => {
      const samples = dataRows.slice(0, 10).map((r) => r[idx] || '');
      const type = detectType(samples);
      console.log(`  [${idx}] "${h || `_Колонка_${idx}`}" (тип: ${type})`);
    });

    console.log(`\n🔍 ПРИМЕРЫ ДАННЫХ (Первые 5 строк):`);
    const previewRows = dataRows.slice(0, 5);
    previewRows.forEach((r, rowIdx) => {
      console.log(`\n-- Строка #${rowIdx + 1} --`);
      headers.forEach((h, colIdx) => {
        const val = r[colIdx] ?? '';
        if (val) {
          console.log(`   ${h || `Колонка_${colIdx}`}: "${val}"`);
        }
      });
    });

    return {
      sheetName,
      headers,
      rowCount: dataRows.length,
      sampleRows: previewRows,
    };
  } catch (err) {
    console.error(`❌ Исключение при запросе листа "${sheetName}":`, err);
    return null;
  }
}

async function discoverAllSheets(): Promise<{ name: string; gid: string }[]> {
  try {
    const res = await fetch(`https://docs.google.com/spreadsheets/d/${SPREADSHEET_ID}/htmlview`, {
      headers: { 'User-Agent': 'Mozilla/5.0' },
    });
    const html = await res.text();
    const sheetRegex = /<li id="sheet-button-([^"]+)"><a[^>]*>([^<]+)<\/a>/g;
    const sheets: { name: string; gid: string }[] = [];
    let match: RegExpExecArray | null;
    while ((match = sheetRegex.exec(html)) !== null) {
      sheets.push({ gid: match[1], name: match[2].trim() });
    }
    return sheets;
  } catch {
    return [];
  }
}

async function main() {
  console.log(`\n======================================================================`);
  console.log(`🚀 ИССЛЕДОВАНИЕ ТАБЛИЦЫ "МК РЕЙТИНГ" (ID: ${SPREADSHEET_ID})`);
  console.log(`======================================================================`);

  const allSheets = await discoverAllSheets();
  if (allSheets.length > 0) {
    console.log(`\n📑 ВСЕ ОБНАРУЖЕННЫЕ ЛИСТЫ В ТАБЛИЦЕ (${allSheets.length}):`);
    allSheets.forEach((s, idx) => console.log(`  ${idx + 1}. "${s.name}" (gid: ${s.gid})`));
  }

  const results: Record<string, unknown> = {};

  for (const sheet of TARGET_SHEETS) {
    const res = await inspectSheet(sheet);
    if (res) {
      results[sheet] = res;
    }
  }

  console.log(`\n\n🎯 ИТОГОВЫЙ ОТЧЕТ СФОРМИРОВАН.`);
  console.log(`Успешно исследовано листов: ${Object.keys(results).length} из ${TARGET_SHEETS.length}`);
}

main().catch(console.error);
