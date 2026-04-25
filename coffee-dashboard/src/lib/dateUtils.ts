import {
  startOfWeek,
  endOfWeek,
  startOfMonth,
  endOfMonth,
  startOfYear,
  endOfYear,
  format,
} from 'date-fns';

export type TimePeriod = 'week' | 'month' | 'year' | 'all';

const DATE_FMT = 'yyyy-MM-dd';

export function getRange(period: TimePeriod, ref: Date = new Date()) {
  switch (period) {
    case 'week':
      return {
        from: format(startOfWeek(ref, { weekStartsOn: 1 }), DATE_FMT),
        to: format(endOfWeek(ref, { weekStartsOn: 1 }), DATE_FMT),
      };
    case 'month':
      return {
        from: format(startOfMonth(ref), DATE_FMT),
        to: format(endOfMonth(ref), DATE_FMT),
      };
    case 'year':
      return {
        from: format(startOfYear(ref), DATE_FMT),
        to: format(endOfYear(ref), DATE_FMT),
      };
    case 'all':
      return {
        from: '2020-01-01',
        to: format(endOfYear(ref), DATE_FMT),
      };
  }
}

export function formatDate(date: string) {
  return format(new Date(date), 'dd.MM.');
}

export function today() {
  return format(new Date(), DATE_FMT);
}

export function formatDisplayDate(dateKey: string): string {
  const [y, m, d] = dateKey.split('-');
  return `${d}.${m}.${y}`;
}
