/**
 * Google Apps Script Webhook: Авто-запись регистраций на турниры в лист «РЕГИСТРАЦИИ»
 *
 * Инструкция по установке:
 * 1. Откройте Google Таблицу клуба:
 *    https://docs.google.com/spreadsheets/d/1zxU_LOSjIsjrEHw7eq366BQMLWQ4x8pSDIiMdUxWPOs/edit
 * 2. В меню выберите: Расширения -> Apps Script (Extensions -> Apps Script).
 * 3. Удалите шаблонный код и вставьте содержимое этого файла.
 * 4. Нажмите синюю кнопку "Развернуть" -> "Новое развертывание" (Deploy -> New deployment).
 * 5. Нажмите шестеренку (Select type) -> "Веб-приложение" (Web app).
 * 6. Настройки:
 *    - Описание: "Регистрации VK Mini App"
 *    - Запуск от имени: "Меня" (Me)
 *    - У кого есть доступ: "Все" (Anyone) — ВАЖНО!
 * 7. Нажмите "Развернуть", разрешите доступ Google аккаунта и скопируйте URL веб-приложения:
 *    https://script.google.com/macros/s/.../exec
 * 8. Вставьте этот URL в файл .env на сервере:
 *    GOOGLE_SHEETS_REGISTRATION_WEBHOOK_URL=https://script.google.com/macros/s/.../exec
 * 9. Перезапустите контейнер API:
 *    docker compose up -d poker-api
 */

function doPost(e) {
  try {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.getSheetByName("РЕГИСТРАЦИИ");
    
    // Если листа нет — создаем его с заголовками
    if (!sheet) {
      sheet = ss.insertSheet("РЕГИСТРАЦИИ");
      sheet.appendRow([
        "Дата и время записи",
        "Турнир",
        "Игрок (ФИО / Никнейм)",
        "Номер клубной карты",
        "Номер телефона",
        "Источник"
      ]);
      // Форматируем шапку: жирный шрифт и фон
      var headerRange = sheet.getRange(1, 1, 1, 6);
      headerRange.setFontWeight("bold");
      headerRange.setBackground("#D9EAD3");
    }

    // Разбираем JSON-пейлоад от бэкенда
    var data = {};
    if (e && e.postData && e.postData.contents) {
      data = JSON.parse(e.postData.contents);
    }

    // Время регистрации (по Перми GMT+5)
    var timestamp = Utilities.formatDate(new Date(), "GMT+5", "dd.MM.yyyy HH:mm:ss");

    // Форматируем телефон с апострофом, чтобы Google Таблицы не теряли ведущий плюс и нули
    var phone = data.phoneNumber ? "'" + String(data.phoneNumber).trim() : "";
    var cardId = data.clubCardId ? "'" + String(data.clubCardId).trim() : "";

    // Добавляем строку в лист «РЕГИСТРАЦИИ»
    sheet.appendRow([
      timestamp,
      data.tournamentTitle || "Турнир",
      data.playerName || "Игрок VK",
      cardId,
      phone,
      data.source || "VK Mini App"
    ]);

    return ContentService
      .createTextOutput(JSON.stringify({ status: "success", timestamp: timestamp }))
      .setMimeType(ContentService.MimeType.JSON);

  } catch (error) {
    return ContentService
      .createTextOutput(JSON.stringify({ status: "error", message: error.toString() }))
      .setMimeType(ContentService.MimeType.JSON);
  }
}

// Проверочная функция GET для тестирования в браузере
function doGet(e) {
  return ContentService
    .createTextOutput(JSON.stringify({ status: "ok", service: "Poker Club Registration Webhook" }))
    .setMimeType(ContentService.MimeType.JSON);
}
