export function formatDateTime(dateStr: string): string {
  try {
    const date = new Date(dateStr);
    const months = [
      'янв', 'фев', 'мар', 'апр', 'май', 'июн',
      'июл', 'авг', 'сен', 'окт', 'ноя', 'дек'
    ];
    const day = date.getDate();
    const month = months[date.getMonth()];
    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');

    return `${day} ${month}, ${hours}:${minutes}`;
  } catch {
    return dateStr;
  }
}

export function formatTime(dateStr: string): string {
  try {
    const date = new Date(dateStr);
    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');
    return `${hours}:${minutes}`;
  } catch {
    return dateStr;
  }
}

export function formatCurrency(amount: number): string {
  if (amount === 0) return 'Free Roll';
  return new Intl.NumberFormat('ru-RU', {
    style: 'currency',
    currency: 'RUB',
    maximumFractionDigits: 0,
  }).format(amount);
}

export function formatChips(amount: number): string {
  return `${amount.toLocaleString('ru-RU')} chips`;
}

export function getSeatsBadge(registered: number, maxSeats: number): {
  type: 'green' | 'amber' | 'red';
  text: string;
  remaining: number;
} {
  const remaining = Math.max(0, maxSeats - registered);

  if (remaining === 0) {
    return {
      type: 'red',
      text: 'Мест нет',
      remaining: 0,
    };
  }

  if (remaining <= 3) {
    const word = remaining === 1 ? 'место' : 'места';
    return {
      type: 'amber',
      text: `Осталось ${remaining} ${word}`,
      remaining,
    };
  }

  return {
    type: 'green',
    text: 'Много мест',
    remaining,
  };
}
