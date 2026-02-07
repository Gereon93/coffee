import type { TimePeriod } from '../../lib/dateUtils';

interface Props {
  value: TimePeriod;
  onChange: (v: TimePeriod) => void;
}

const options: { value: TimePeriod; label: string }[] = [
  { value: 'week', label: 'Woche' },
  { value: 'month', label: 'Monat' },
  { value: 'year', label: 'Jahr' },
  { value: 'all', label: 'Gesamt' },
];

export function TimePeriodSelector({ value, onChange }: Props) {
  return (
    <div className="inline-flex rounded-lg bg-stone-200 p-1 dark:bg-stone-800">
      {options.map((opt) => (
        <button
          key={opt.value}
          onClick={() => onChange(opt.value)}
          className={`rounded-md px-4 py-1.5 text-sm font-medium transition-colors ${
            value === opt.value
              ? 'bg-white text-coffee-700 shadow-sm dark:bg-stone-700 dark:text-coffee-200'
              : 'text-stone-600 hover:text-stone-900 dark:text-stone-400 dark:hover:text-stone-200'
          }`}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}
