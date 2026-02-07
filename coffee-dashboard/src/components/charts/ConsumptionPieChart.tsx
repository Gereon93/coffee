import {
  PieChart,
  Pie,
  Cell,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts';
import type { DailyAggregate } from '../../api/types';

interface Props {
  data: DailyAggregate[];
}

const COLORS = ['#d97706', '#3b82f6'];

export function ConsumptionPieChart({ data }: Props) {
  const totalCoffee = data.reduce((s, d) => s + d.coffeeCount, 0);
  const totalMilk = data.reduce((s, d) => s + d.milkCount, 0);

  const pieData = [
    { name: 'Kaffee', value: totalCoffee },
    { name: 'Milch', value: totalMilk },
  ];

  if (totalCoffee === 0 && totalMilk === 0) {
    return (
      <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
        <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
          Verteilung
        </h3>
        <p className="py-12 text-center text-sm text-stone-400">Keine Daten</p>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
        Verteilung
      </h3>
      <ResponsiveContainer width="100%" height={250}>
        <PieChart>
          <Pie
            data={pieData}
            cx="50%"
            cy="50%"
            innerRadius={60}
            outerRadius={90}
            paddingAngle={4}
            dataKey="value"
          >
            {pieData.map((_, i) => (
              <Cell key={i} fill={COLORS[i]} />
            ))}
          </Pie>
          <Tooltip
            contentStyle={{
              backgroundColor: '#1c1917',
              border: 'none',
              borderRadius: '0.5rem',
              color: '#e7e5e4',
              fontSize: '0.875rem',
            }}
          />
          <Legend
            formatter={(value) => (
              <span className="text-sm text-stone-600 dark:text-stone-400">{value}</span>
            )}
          />
        </PieChart>
      </ResponsiveContainer>
    </div>
  );
}
