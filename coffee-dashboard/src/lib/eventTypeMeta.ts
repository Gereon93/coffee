import type { EventType } from '../api/types';

export interface EventTypeMeta {
  type: EventType;
  emoji: string;
  label: string;
}

export const EVENT_TYPE_META: EventTypeMeta[] = [
  { type: 'birthday', emoji: '🎂', label: 'Geburtstag' },
  { type: 'visitors', emoji: '👥', label: 'Besuch' },
  { type: 'party',    emoji: '🎉', label: 'Feier' },
  { type: 'sick',     emoji: '🏥', label: 'Krank' },
  { type: 'vacation', emoji: '✈️', label: 'Urlaub' },
  { type: 'other',    emoji: '📌', label: 'Sonstiges' },
];

export function emojiForEventType(t: EventType): string {
  return EVENT_TYPE_META.find((p) => p.type === t)?.emoji ?? '📌';
}
