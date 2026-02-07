import type { LucideIcon } from 'lucide-react';

interface Props {
  title: string;
  value: string | number;
  icon: LucideIcon;
  subtitle?: string;
}

export function KpiCard({ title, value, icon: Icon, subtitle }: Props) {
  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-sm font-medium text-stone-500 dark:text-stone-400">
            {title}
          </p>
          <p className="mt-1 text-3xl font-bold tracking-tight">{value}</p>
          {subtitle && (
            <p className="mt-1 text-xs text-stone-400 dark:text-stone-500">
              {subtitle}
            </p>
          )}
        </div>
        <div className="rounded-lg bg-coffee-100 p-2 dark:bg-coffee-900">
          <Icon className="h-5 w-5 text-coffee-600 dark:text-coffee-300" />
        </div>
      </div>
    </div>
  );
}
