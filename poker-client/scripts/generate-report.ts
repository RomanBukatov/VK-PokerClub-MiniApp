import { writeFileSync } from 'fs';

const SPREADSHEET_ID = '1GRINVjwfqXsG0vccHfFFOaxzTbo5pcxWBGn1YOgzOn0';

const SHEETS = [
  'Общий рейтинг',
  'Рейтинг сезона',
  'ТОП-20',
  'Данные для витрины',
  'РЕГИСТРАЦИИ',
];

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

async function run() {
  const report: Record<string, unknown> = {};

  for (const sheet of SHEETS) {
    const url = `https://docs.google.com/spreadsheets/d/${SPREADSHEET_ID}/gviz/tq?tqx=out:csv&sheet=${encodeURIComponent(sheet)}`;
    const res = await fetch(url);
    const text = await res.text();
    const rows = parseCSV(text);

    report[sheet] = {
      sheetName: sheet,
      totalRows: rows.length,
      headers: rows[0] || [],
      sampleRows: rows.slice(1, 6),
    };
  }

  writeFileSync('scripts/sheets-report.json', JSON.stringify(report, null, 2), 'utf8');
  console.log('Saved report to scripts/sheets-report.json');
}

run().catch(console.error);
