import { Coffee, PowerOff } from 'lucide-react';
import { useCoffeeStatus, useSetCoffeePower } from '../../hooks/useCoffeeStatus';
import { coffeeAllowed } from '../../lib/coffeeTimeLock';
import type { CoffeeStatus } from '../../api/types';

interface ButtonState {
  label: string;
  disabled: boolean;
  className: string;
  icon: typeof Coffee;
  nextAction: 'on' | 'off' | null;
  title?: string;
}

const BASE = 'inline-flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm font-medium transition-colors';
const STONE = 'border-stone-300 text-stone-700 hover:bg-stone-100 dark:border-stone-700 dark:text-stone-300 dark:hover:bg-stone-800';
const EMERALD = 'border-emerald-400 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:border-emerald-700 dark:bg-emerald-950 dark:text-emerald-300';
const SKY = 'border-sky-400 bg-sky-50 text-sky-700 dark:border-sky-700 dark:bg-sky-950 dark:text-sky-300';
const AMBER = 'border-amber-400 bg-amber-50 text-amber-700 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-300';
const GREY = 'border-stone-300 text-stone-400 dark:border-stone-700 dark:text-stone-500 cursor-not-allowed';

function deriveState(status: CoffeeStatus | undefined, isMutating: boolean): ButtonState {
  if (isMutating) {
    return { label: 'Schaltet…', disabled: true, className: `${BASE} ${AMBER}`, icon: Coffee, nextAction: null };
  }
  if (!coffeeAllowed()) {
    return { label: 'Gesperrt', disabled: true, className: `${BASE} ${GREY}`, icon: Coffee, nextAction: null, title: 'Coffee-Hours: 07:00–18:00' };
  }
  if (!status || !status.reachable) {
    return { label: 'Offline', disabled: true, className: `${BASE} ${GREY}`, icon: PowerOff, nextAction: null, title: status?.message ?? 'Maschine nicht erreichbar' };
  }
  if (status.powerState === 'on' && status.operationState === 'run') {
    return { label: 'Läuft', disabled: true, className: `${BASE} ${SKY}`, icon: Coffee, nextAction: null, title: 'Brühvorgang läuft' };
  }
  if (status.powerState === 'on') {
    return { label: 'Ausschalten', disabled: false, className: `${BASE} ${EMERALD}`, icon: Coffee, nextAction: 'off', title: status.label };
  }
  // off / standby / null powerState
  return { label: 'Einschalten', disabled: false, className: `${BASE} ${STONE}`, icon: Coffee, nextAction: 'on', title: status.label };
}

export function CoffeePowerButton() {
  const { data: status } = useCoffeeStatus();
  const mutation = useSetCoffeePower();

  const state = deriveState(status, mutation.isPending);

  const handleClick = () => {
    if (state.nextAction) {
      mutation.mutate(state.nextAction);
    }
  };

  const Icon = state.icon;

  return (
    <button
      type="button"
      onClick={handleClick}
      disabled={state.disabled}
      className={state.className}
      title={state.title}
      aria-label={`Kaffeemaschine: ${state.label}`}
    >
      <Icon className="h-4 w-4" />
      {state.label}
    </button>
  );
}
