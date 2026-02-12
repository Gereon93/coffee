import { NavLink } from 'react-router-dom';
import { LayoutDashboard, Grid3x3, ScrollText, Moon, Sun } from 'lucide-react';

const links = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/heatmap', label: 'Heatmap', icon: Grid3x3 },
  { to: '/log', label: 'Log', icon: ScrollText },
] as const;

type NavBarProps = {
  isDarkMode: boolean;
  onToggleTheme: () => void;
};

export function NavBar({ isDarkMode, onToggleTheme }: NavBarProps) {
  return (
    <nav className="border-b border-stone-200 bg-white dark:border-stone-800 dark:bg-stone-950">
      <div className="mx-auto flex h-14 max-w-7xl items-center gap-6 px-4">
        <span className="text-lg font-bold tracking-tight text-coffee-600 dark:text-coffee-300">
          Coffee Analytics
        </span>

        <div className="flex gap-1">
          {links.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-coffee-100 text-coffee-700 dark:bg-coffee-900 dark:text-coffee-200'
                    : 'text-stone-600 hover:bg-stone-100 dark:text-stone-400 dark:hover:bg-stone-800'
                }`
              }
            >
              <Icon className="h-4 w-4" />
              {label}
            </NavLink>
          ))}
        </div>

        <button
          type="button"
          onClick={onToggleTheme}
          className="ml-auto inline-flex items-center gap-2 rounded-md border border-stone-300 px-3 py-1.5 text-sm text-stone-700 transition-colors hover:bg-stone-100 dark:border-stone-700 dark:text-stone-300 dark:hover:bg-stone-800"
          aria-label={isDarkMode ? 'Zum hellen Modus wechseln' : 'Zum dunklen Modus wechseln'}
        >
          {isDarkMode ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
          {isDarkMode ? 'Hell' : 'Dunkel'}
        </button>
      </div>
    </nav>
  );
}
