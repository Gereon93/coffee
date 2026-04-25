/**
 * Returns true if the coffee machine may be operated right now.
 * Locked window: 18:00–07:00 local Berlin time.
 *
 * The coffee-api itself enforces nothing — this is UI safety against
 * accidental switches outside coffee hours.
 */
export function coffeeAllowed(now: Date = new Date()): boolean {
  const berlinHour = parseInt(
    new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit',
      hour12: false,
      timeZone: 'Europe/Berlin',
    }).format(now),
    10,
  );
  return berlinHour >= 7 && berlinHour < 18;
}
