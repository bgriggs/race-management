/**
 * Time-format helpers shared across the Fuel & Pit Strategy gantt children
 * (stint bars, projection overlay tooltips, time axis, now-line label).
 * Spec §"Visual System" — 12-hour lowercase with single-letter suffix.
 */

/**
 * Formats a `Date` as `10:00a` / `1:27p` / `2:19p`. No leading zero on the
 * hour; minutes are always two digits, matching every example in the spec.
 */
export function fmtTime(date: Date): string {
  const h = date.getHours();
  const m = date.getMinutes();
  const ampm = h >= 12 ? 'p' : 'a';
  const h12 = h % 12 || 12;
  const mm = m.toString().padStart(2, '0');
  return `${h12}:${mm}${ampm}`;
}
