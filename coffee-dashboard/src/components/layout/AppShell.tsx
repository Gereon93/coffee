import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { NavBar } from './NavBar';

type Theme = 'light' | 'dark';
const THEME_STORAGE_KEY = 'coffee-dashboard-theme';

function getInitialTheme(): Theme {
  if (typeof window === 'undefined') {
    return 'light';
  }

  try {
    const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);
    if (storedTheme === 'light' || storedTheme === 'dark') {
      return storedTheme;
    }
  } catch {
    // Ignore localStorage errors and continue with system preference.
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function AppShell() {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);

  useEffect(() => {
    const isDarkMode = theme === 'dark';
    document.documentElement.classList.toggle('dark', isDarkMode);
    document.documentElement.style.colorScheme = theme;

    try {
      window.localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // Ignore localStorage errors.
    }
  }, [theme]);

  const toggleTheme = () => {
    setTheme((currentTheme) => (currentTheme === 'dark' ? 'light' : 'dark'));
  };

  return (
    <div className="flex min-h-screen flex-col bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100">
      <NavBar isDarkMode={theme === 'dark'} onToggleTheme={toggleTheme} />
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6">
        <Outlet />
      </main>
      <footer className="border-t border-stone-200 bg-white py-3 text-xs text-stone-400 dark:border-stone-800 dark:bg-stone-950 dark:text-stone-600">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-2 px-4">
          <span>
            Build <code className="rounded bg-stone-100 px-1 py-0.5 font-mono dark:bg-stone-900">{__BUILD_COMMIT__}</code> &middot; {__BUILD_TIME__}
          </span>
          <a
            href="/scalar/v1"
            target="_blank"
            rel="noopener noreferrer"
            className="underline decoration-stone-300 underline-offset-2 transition-colors hover:text-stone-600 dark:decoration-stone-700 dark:hover:text-stone-400"
          >
            API Docs
          </a>
        </div>
      </footer>
    </div>
  );
}
