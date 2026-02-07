export function formatNumber(n: number): string {
  return n.toLocaleString('de-DE');
}

export function formatHour(hour: number): string {
  return `${hour.toString().padStart(2, '0')}:00`;
}

export function dayLabel(dayOfWeek: number): string {
  const labels = ['', 'Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So'];
  return labels[dayOfWeek] ?? '';
}
