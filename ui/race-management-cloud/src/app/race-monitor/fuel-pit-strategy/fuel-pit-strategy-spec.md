# Fuel & Pit Strategy View — UI Specification

This document specifies the Fuel & Pit Strategy view of the Race Management UI. It is the row-4 section of the Race Monitor and the user-facing surface for the cloud Fuel Reconciler (see `design.md` § Fuel Analysis).

## Scope

- Cloud-only UI. All components live in `ui/race-management-cloud`.
- The detail panel that opens when a car is selected is referenced in this doc but specified separately.
- All view-state controls (toggles, selection, scroll position) are session-only — they reset on page reload. No persistence to backend, user preferences, or team defaults unless explicitly noted.

## Visual System

- Dark cockpit theme. Surfaces are near-black panels on the host page background.
- Typography: sans-serif for chrome and labels, monospaced for all times, numbers, and tabular values. `font-variant-numeric: tabular-nums` everywhere a number is displayed.
- Color is meaning-bearing. Two ramps carry semantics; everything else is grayscale.
  - Green (`#5DCAA5` / `#04342C`) — auto stint, surplus projection, surplus delta.
  - Purple (`#AFA9EC` / `#26215C`) — manual stint.
  - Red (`#E24B4A` / `#F09595`) — shortfall projection, shortfall delta, shortfall wedge, now-line.
  - Amber (`#854F0B`) — yellow flag stripe.
  - Dark red (`#A32D2D`) — red flag stripe.
- Time format throughout: 12-hour lowercase with single-letter suffix (`10:00a`, `1:27p`, `2:19p`). No leading zero on the hour, no leading zero on the minute below 10 only when used inside a bar (else `:00`–`:59`).

## Component Tree

- `fuel-pit-strategy-panel` — the panel as a whole. Owns:
  - Panel header (title only).
  - Legend row (item 2) including the two toggles (items 3, 4).
  - Gantt container which owns the px-per-minute scale, shared horizontal scroll, and timer.
    - `fuel-car-row` × N (item 5), each containing:
      - Car number column (item 6).
      - Gantt canvas (item 8) with all positioned children (items 9–16).
      - Right-stat block (item 17).
      - Action buttons (items 18, 19).
    - Time axis (item 20).
- `fuel-add-stint-modal` — opened by Add Stint button. Full overlay.
- `fuel-detail-view` — opened by row click or Settings button. Specified separately.

## Shared Concerns

- **Time-to-pixel scale.** A pure function `timeToPx(time, sessionStart, pxPerMinute)` is used by every positioned element on the gantt. The gantt container computes the active `pxPerMinute` as `max(fitToContainer, MIN_PX_PER_MINUTE)`. Recommended floor: `2.5 px/min`.
- **Horizontal scroll.** Single shared scroll position across every car-row gantt canvas and the time axis. Owned by the gantt container; bound into each row and the axis. Resets on reload.
- **Timer.** A single `interval(1000)` observable in the gantt container drives the now-line and the now-line time label. Other elements re-render on data updates, not on the timer.
- **Snapshot updates.** `FuelRangeSnapshot` per car is streamed via the telemetry SignalR hub. Right-stat block, projection overlay, and shortfall wedge all derive from this object.

---

# Item-by-Item Spec

## 1. Panel header

Static text region at the top of the panel.

- Title: "Fuel & Pit Strategy" with a `ti-gas-station` icon at 14px in green `#5DCAA5`.
- No event name, no session label, no clock display.
- Border-bottom: `0.5px solid #1f2730`.

## 2. Legend row

A horizontal flex row immediately below the panel header, holding swatches and the two toggles.

- Background: `#0d1116`. Border-bottom: `0.5px solid #1a2028`. Padding: `8px 14px`.
- Font: 11px sans, color `#9aa5b1`.
- Swatch entries, in order:
  - Auto stint — solid green `#5DCAA5` 18×8 rectangle.
  - Manual stint — solid purple `#AFA9EC` 18×8 rectangle.
  - Projected — surplus — green `#5DCAA5` 1.5px dashed border over faint diagonal stripe fill.
  - Projected — shortfall — red `#E24B4A` 1.5px dashed border over faint diagonal stripe fill.
- The two toggles (items 3, 4) are right-aligned in the same row with `margin-left: auto`.

## 3. Confidence toggle (Regular ↔ High confidence)

Switches the gantt's projection overlays, right-stat range values, and right-stat deltas between the reconciler's two output range tiers from `FuelRangeSnapshot`.

- **Regular** → `primaryRange` (best-estimate, ~90–92% confidence).
- **High confidence** → `highConfRange` (fixed 98% confidence, used for pit calls).

**Scope of effect.** View-state only; affects every car row simultaneously:
- Projection overlay (`.proj`) width, position, surplus-vs-shortfall classification, and tooltip text.
- Right-stat range value.
- Right-stat delta text and color (a row may flip surplus↔shortfall between modes).

Does **not** affect the detail panel — that view shows both ranges as separate cards regardless of toggle state.

**Default state.** Regular.
**Persistence.** Session-only. Resets on reload.
**98% threshold.** Fixed. Not configurable.

**Interaction.**
- Click the toggle knob to flip state.
- Click either label to set to that state directly.
- Keyboard: tab to focus; space/enter to flip.
- Active label brightens to `#e6edf3`, inactive stays `#9aa5b1`.
- ARIA: `role="switch"`, `aria-checked` reflects state.

**Component.**
- Lives in `ui/race-management-cloud` as `fuel-confidence-toggle`.
- Inputs: none.
- Outputs: `confidenceChange: EventEmitter<'regular' | 'high'>`.
- Parent holds state and pipes it into each row's projection rendering.

## 4. Scope toggle (Current ↔ Project to end)

Switches the gantt between two views.

- **Current** → only the projection overlay for the currently-open FuelWindow.
- **Project to end** → the full sequence of projected future stints from now to session end, as dashed outline bars.

**Scope of effect.** View-state only; affects every car row simultaneously:
- `.scope-current` element (projection overlay + shortfall wedge) is shown/hidden.
- `.scope-end` element (future projected stints + start/end lines) is shown/hidden.

Does **not** affect planned stint bars, now-line, right-stat block, or action buttons — they always render.

**Default state.** Current.
**Persistence.** Session-only. Resets on reload.

**Behavior when "Project to end":**
- The current FuelWindow's projection overlay is removed.
- Future-stint sequence starts at the now-line and continues to the end-line.
- Each future stint's duration = `tankCapacity ÷ activeConsumptionRate` where the rate is regular or high-conf per the Confidence toggle (orthogonal/independent).
- Fixed 90-second pit-stop gap between consecutive future stints.
- The sequence terminates when the next future stint would end at or after `Race.EndTime`; the last future stint is capped at `Race.EndTime`.
- If zero full future stints fit before session end, no future-stint bars render. The start-line and end-line still show.

**Interaction.**
- Click the toggle knob to flip state.
- Click either label to set to that state directly.
- Keyboard: tab to focus; space/enter to flip.
- Active label brightens to `#e6edf3`, inactive stays `#9aa5b1`.
- ARIA: `role="switch"`, `aria-checked` reflects state.

**Component.**
- Lives in `ui/race-management-cloud` as `fuel-scope-toggle`.
- Inputs: none.
- Outputs: `scopeChange: EventEmitter<'current' | 'end'>`.

## 5. Car row

One row per car on the team. The fundamental unit of the gantt.

**Composition.** Four-column CSS grid `56px minmax(0, 1fr) 110px 76px`:
1. Car number column (item 6).
2. Gantt canvas (item 8).
3. Right-stat block (item 17).
4. Action buttons (items 18, 19).

**Row ordering.** Numerical by `Car.Number`. Not user-reorderable.

**Row inclusion.** All cars in the team's `CarConfiguration` set are shown.

**Data source per row.**
- `FuelRangeSnapshot` for the car (live from telemetry hub).
- The car's planned-stint list from the Race configuration.

**No planned stints case.** When the car has zero planned stints (Race not yet configured for this car), the row renders an "Automatic" stint starting at `Race.StartTime` with length derived from the default consumption range. Behaves like any other stint for projection purposes.

**No live telemetry case.** Cars that haven't connected telemetry this session render with manual stints only. Projection uses a time-based estimator seeded from previous stints' observed range (rather than from live consumption).

**Interaction.**
- Click anywhere on the row outside the action buttons → toggles selection (item 7).
- Action buttons stop event propagation.
- Hover → row background brightens to `#151b22`.

**Row height.** Fixed by content: `padding: 14px 14px` plus the 36px gantt canvas plus the car-number stack. ~64px total, constant across team sizes.

**Component.**
- `fuel-car-row` in `ui/race-management-cloud`.
- Inputs:
  - `carNumber: string`
  - `fuelWindowOpenedAt: Date | null`
  - `plannedStints: PlannedStint[]`
  - `snapshot: FuelRangeSnapshot | null` (null when no live telemetry)
  - `selected: boolean`
  - `confidenceTier: 'regular' | 'high'`
  - `scope: 'current' | 'end'`
  - `sessionStart: Date`
  - `sessionEnd: Date`
  - `pxPerMinute: number`
  - `now: Date`
- Outputs:
  - `rowClick: EventEmitter<string>`
  - `addStintClick: EventEmitter<string>`
  - `settingsClick: EventEmitter<string>`

## 6. Car number column

Leftmost element in each car row. Fixed 56px width.

**Composition.** Two stacked text elements:
- **Car number** — 18px mono, weight 500, color `#e6edf3`. Always prefixed with `#`.
- **Sub-line** — 10px sans, color `#5f6b77`. Shows `FW open · {n}m` (active FuelWindow age in minutes since `OpenedAt`).

**Sub-line states.**
- Active FuelWindow → `FW open · {n}m`.
- No active FuelWindow → sub-line empty.
- No live telemetry → sub-line empty.

**Update frequency.** Sub-line ticks every 60 seconds.

**Interaction.** Inert. Clicks fall through to the row click handler.

**Component.** Part of `fuel-car-row` — not extracted as its own component.

## 7. Selected row state

Visually indicates which car's detail view is currently open.

**Visual treatment.**
- Left border: `2px solid #378ADD`.
- Background: `#121a25` (vs default transparent / hover `#151b22`).
- `padding-left` shifts from 14px to 12px to compensate for the border.
- Selection color is always blue, regardless of fuel state.

**Selection model.** Zero or one car selected at a time.

**Default on load.** No car selected. Detail view shows empty-state placeholder.

**Persistence.** Session-only. Resets on reload.

**Interaction.**
- Click a non-selected row → selects it and opens detail.
- Click the currently-selected row → deselects it and closes detail.
- Closing the detail view via its own control → clears selection.

**Edge cases.**
- Selected car deleted from `CarConfiguration` mid-session → selection clears, detail closes.
- Selected car has no live telemetry → selection still works; detail shows the no-telemetry variant.

**Component.**
- Parent (`fuel-pit-strategy-panel`) holds `selectedCarNumber: string | null`.
- Pipes `selected: boolean` into each row.
- `rowClick` output toggles `selectedCarNumber`.

## 8. Gantt canvas

Variable-width timeline at the center of each car row.

**Composition.** `position: relative` container; every child positioned absolutely with `left` and `width` in pixels.

**Dimensions.**
- Height: scales up if child layers stack vertically (base 36px).
- Background: `#0d1116`.
- Border: `0.5px solid #1a2028`. Border-radius: 4px.
- `overflow-x: auto` when scaled width exceeds container; otherwise no scroll.
- `overflow-y: visible` so projection bars and the now-line dot don't clip.

**Coordinate system.** Linear time mapping with a px-per-minute scale:
- Default scale fits the full session to available container width.
- Minimum floor: `MIN_PX_PER_MINUTE` (recommended `2.5`). Ensures the shortest plausible stint stays readable.
- When the floor produces a total width exceeding the container, the canvas becomes horizontally scrollable.
- Position: `left_px = (elementStartTime - Race.StartTime) / 60000 * pxPerMinute`.
- Width: `width_px = elementDurationMs / 60000 * pxPerMinute`.

**Scrolling.**
- All four (or more) car-row gantt canvases share a synchronized horizontal scroll position.
- The time axis (item 20) scrolls in sync.
- Scroll position is session-only state.

**Time range.** Always full session from `Race.StartTime` to `Race.EndTime`. No user-driven zoom or pan in v1.

**Z-order (back to front).**
1. Flag stripes (opacity 0.22, `pointer-events: none`).
2. Planned stint bars (solid).
3. Projection overlay or future projected stints (depending on scope toggle).
4. Shortfall wedge.
5. Start-line and end-line (Project-to-end scope only).
6. Now-line (always on top).

**Now-line behavior.**
- Before `Race.StartTime` → hidden.
- Between start and end → renders at computed position, ticks once per second.
- After `Race.EndTime` → continues rendering off the right edge (visible only via scroll).

**`Race.EndTime` changes mid-session.** All child positions recompute immediately. No animation.

**Component.** Part of `fuel-car-row`. The gantt-container parent computes `pxPerMinute` and owns the shared scroll position.

## 9. Flag stripes

Background bands behind the planned stint bars showing `RaceFlagState` periods.

**Visual treatment.**
- Full-height absolutely positioned `<div>` per contiguous flag period (`top: 0; bottom: 0`).
- `opacity: 0.22`.
- `pointer-events: none`.
- Colors:
  - Yellow flag → `#854F0B`.
  - Red flag → `#A32D2D`.

**Data source.** Live `RaceFlagState` channel history. Streamed via telemetry SignalR hub.

**States represented.** Yellow, Red. All other states (`Green`, `White`, `Checkered`, `Black`, `Unknown`) render no stripe.

**Coverage.**
- Identical across all car rows (`RaceFlagState` is `Scope = PerTeam`).
- Stripes span from `Race.StartTime` to the most recent flag transition. The current period extends to the now-line.
- Pre-session and post-session regions are not striped.

**Same-color adjacent stripes.** Merged into a single stripe.

**Unavailable channel.** No stripes render.

**Interaction.** Inert.

**Component.** Rendered inside the gantt canvas. The flag history is held once at the gantt-container parent and passed identically to every row.

## 10. Planned stint bar

Solid colored rectangle representing one Stint.

**Visual treatment.**
- Absolutely positioned with computed `left` and `width` in pixels.
- Height: 26px fixed.
- Stays 26px even when the canvas scales taller.
- Border-radius: 3px.
- Padding: `0 8px`.
- Layout: flex row, `justify-content: space-between`. Start time left, end time right.
- Font: 10px mono, weight 500, `letter-spacing: -0.02em`.

**Color (tier).**
- **Auto** — `background: #5DCAA5`, `color: #04342C`. Automatically projected end.
- **Manual** — `background: #AFA9EC`, `color: #26215C`. Manually set end.

**Past (closed) stints.** Slightly desaturated relative to current/future stints. Implementation: `filter: saturate(0.6)` or a separate `tier-{auto|manual}-past` class with darker hex values. Trigger: `Stint.EndTime` is in the past.

**Content.** Two text spans: start time (left), end time (right). No center label, no icon.

**Data source.**
- `Stint.StartTime` → start time + `left` position.
- `Stint.EndTime` → end time + width calc. Every stint has an end time, either automatically projected or manually set.
- `Stint.OriginType` → tier color.

**Narrow-bar fallback.** When the bar is too narrow to fit both timestamps without overlap, drop both timestamps from the rendered text and surface them via a hover tooltip showing `{start} – {end}`. Threshold: combined width of formatted timestamps + 16px padding exceeds bar width.

**Interaction.**
- Normal bars → inert. Clicks bubble to row.
- Narrow bars → hover tooltip; clicks still bubble.

**Component.** `*ngFor` over the car's `Stint[]` inside the gantt canvas.

## 11. Projection overlay (surplus)

Green dashed rectangle extending past the planned end of the currently-open stint.

**Visual treatment.**
- Absolutely positioned. `top: 5px`, `height: 26px`.
- Border: `1.5px dashed #5DCAA5`.
- Background: `repeating-linear-gradient(45deg, rgba(93,202,165,0.06), rgba(93,202,165,0.06) 4px, transparent 4px, transparent 8px)`.
- Border-radius: 3px.
- Empty (no inline text).
- `cursor: help`.

**Position.**
- Starts at the planned-end of the currently-open stint.
- Ends at the projected fuel-out time.
- If planned end is in the past (driver over-running), the overlay starts at the (past) planned end and extends through the now-line to the projected end.

**Visibility rule.** Renders only when **all** of:
- Scope toggle is "Current".
- Projected end > planned end.
- Surplus delta ≥ 1 minute.

If projected end exceeds `Race.EndTime`, the overlay continues past the session-end position. Scroll right to see.

**Tooltip on hover.** `{projectedEndTime} · +{delta} min` — e.g. `2:19p · +11 min`.

**Data source.**
- Projected end = `now + activeRange.minutes` (regular or high-conf per Confidence toggle).
- Planned end = current stint's `Stint.EndTime`.

**Interaction.** Hover → tooltip. Click bubbles to row.

## 12. Projection overlay (shortfall)

Red dashed rectangle covering the shortfall deficit — symmetric with the surplus
overlay (item 11), anchored at the planned end of the current stint and
extending left by the shortfall delta.

**Visual treatment.**
- Absolutely positioned. `top: 5px`, `height: 26px`.
- Border: `1.5px dashed #E24B4A`.
- Background: `repeating-linear-gradient(45deg, rgba(226,75,74,0.10), rgba(226,75,74,0.10) 4px, transparent 4px, transparent 8px)`.
- Border-radius: 3px.
- Empty (no inline text).
- `cursor: help`.
- **Z-order:** sits on top of the shortfall wedge, both inside the planned-stint bar's span.

**Position.**
- Starts at the projected fuel-out time.
- Ends at the planned end of the current stint.

**Visibility rule.** Renders only when **all** of:
- Scope toggle is "Current".
- Projected end < planned end.
- Shortfall delta ≥ 1 minute.

When planned end is already in the past (over-running), the overlay still renders normally.

**Pairing.** Always co-renders with the shortfall wedge (item 13).

**Tooltip on hover.** `{projectedEndTime} · −{delta} min` — e.g. `2:03p · −10 min`.

**Data source.**
- Projected end = `now + activeRange.minutes`.
- Planned end = current stint's `Stint.EndTime`.

**Interaction.** Hover → tooltip. Click bubbles to row.

## 13. Shortfall wedge

Red-tinted band filling the gap between projected fuel-out and planned stint end.

**Visual treatment.**
- Absolutely positioned. `top: 5px`, `height: 26px`.
- Background: `rgba(226,75,74,0.18)`.
- Top border: `1px solid #E24B4A`.
- Bottom border: `1px solid #E24B4A`.
- No left/right border, no border-radius.
- `pointer-events: none`.
- Fixed opacity regardless of shortfall magnitude.

**Position.**
- Starts at the projected fuel-out time (right edge of the shortfall overlay).
- Ends at the planned end of the current stint.
- When planned end is in the past, the wedge spans from projected fuel-out to the past planned-end position (both potentially behind the now-line).

**Visibility rule.** Renders only when paired with a shortfall overlay. Same trigger conditions: scope = "Current", shortfall ≥ 1 minute, confidence-tier-active value. Never shown without the overlay.

**Z-order.** Between the planned-stint solid bar (below) and the shortfall overlay (above): planned → wedge → overlay.

**Interaction.** Inert. Clicks pass through to the row click handler.

## 14. Future projected stints (Project-to-end scope only)

Dashed outline bars showing projected future stints from now to session end.

**Visual treatment.**
- Absolutely positioned. `top: 5px`, `height: 26px`.
- Background: transparent.
- Border: `1.5px dashed #5DCAA5` (always auto-green).
- Text color: `#5DCAA5`.
- Border-radius: 3px.
- Padding: `0 8px`.
- Layout: flex row with start time left and end time right (same format as planned stint bars).
- Font: 10px mono, weight 500, `letter-spacing: -0.02em`.

**Content.** Two text spans: start time, end time. No center label.

**Position and duration of each future stint.**
- First future stint starts at the now-line.
- Each future stint duration = `tankCapacity ÷ activeConsumptionRate` (active = regular or high-conf per Confidence toggle).
- Assumes green-flag pace — flag state is not modeled into future projections.
- Fixed 90-second pit-stop gap between consecutive future stints.
- Sequence terminates when the next future stint would end at or after `Race.EndTime`; the last future stint is capped at `Race.EndTime`.
- If zero full future stints fit, nothing renders. Start-line and end-line still show.

**All future stints are auto tier.** No manual-tier future stints in v1.

**Visibility rule.** Renders only when scope toggle is "Project to end".

**Narrow-bar fallback.** Same as planned stint bars — tooltip with start–end times when timestamps don't fit.

**Interaction.** Inert apart from narrow-bar tooltip. Clicks bubble to row.

**Data source.**
- Active consumption rate from `FuelRangeSnapshot` (regular or high-conf).
- `CarFuelConfig.TankCapacityGallons`.
- 90-second gap constant.
- `Race.EndTime`.

## 15. Now-line

Vertical red line marking current race time.

**Visual treatment.**
- 1px wide, `background: #E24B4A`.
- Extends slightly above and below the canvas (`top: -2px; bottom: -2px`).
- Dot head at the top only: 7px circle in the same red (`top: -3px; left: -3px` relative to the line).
- Z-index 3 (always on top).
- Color stays red even when overlapping a red-tinted shortfall wedge.

**Position.** `left_px = (now - Race.StartTime) / 60000 * pxPerMinute`.

**Update frequency.** Once per second.

**Visibility rule.**
- Before `Race.StartTime` → hidden.
- Between start and end → renders.
- After `Race.EndTime` → continues to render off the right edge.

**Synchronization.** Identical across all rows. Scrolls with the gantt's shared horizontal scroll. No offscreen indicator when scrolled out of view.

**Interaction.** Inert.

## 16. Start-line and end-line (Project-to-end scope only)

Vertical light-gray reference lines marking `Race.StartTime` and `Race.EndTime`.

**Visual treatment.**
- 1px wide, `background: #cdd5de`, `opacity: 0.4`.
- Extends slightly above and below the canvas (`top: -2px; bottom: -2px`).
- Label above the line: 9px mono, color `#cdd5de`, opacity 0.6, positioned `top: -12px; left: -9px` relative to the line.
  - Start line label: `start`.
  - End line label: `end`.
- Z-index 2 (below now-line, above future-stint bars).

**Position.**
- Start line: `left_px = 0` (at `Race.StartTime`).
- End line: `left_px = (Race.EndTime - Race.StartTime) / 60000 * pxPerMinute`.

**Visibility rule.** Both render only when scope toggle is "Project to end". Always render in that scope, including the zero-future-stints case.

**`Race.EndTime` changes mid-session.** Lines snap to new positions immediately, no animation.

**Collision.** When now-line and end-line are very close (session about to end), accept the visual overlap.

**Interaction.** Inert.

## 17. Right-stat block

Two-line summary on the right of each car row giving "how is this car doing vs plan?"

**Composition.** Fixed 110px width. Right-aligned. Two stacked lines:
- **Range value (top)** — large mono. The numeric portion at 14px weight 500; the ` min` suffix at 12px in `#7b8794`.
- **Delta line (bottom)** — small mono. Text: `+N min vs plan`, `−N min vs plan`, or `on plan`.

**Color coding — exact match, no tolerance.**
- Delta > 0 → green `#5DCAA5` for both range value and delta line.
- Delta < 0 → red `#F09595` for both range value and delta line.
- Delta == 0 → range value default white `#e6edf3`; delta line muted gray `#7b8794`, text reads `on plan`.

**Data source.**
- Range value → `FuelRangeSnapshot.primaryRange.minutes` (regular) or `highConfRange.minutes` (high-conf).
- Delta → projected end time − planned end time of the currently-open stint, rounded to whole minutes.

**Scope toggle.** Right-stat does not change with the scope toggle. Always reflects the current stint.

**No-currently-open-stint case.** Range value: `— min`. Delta: `—`.

**No-telemetry case.** Time-based projection from previous stints' observed range. Format unchanged.

**Update frequency.** Re-renders on `FuelRangeSnapshot` updates from the telemetry hub.

**Interaction.** Inert. Clicks bubble to row.

## 18. Add stint button

Quick-action button to manually add a stint.

**Visual treatment.**
- Icon-only: `<i class="ti ti-plus">` at 20px, color `#ffffff`.
- Transparent background, no border.
- Padding: 6px (~32px square hit target).
- Border-radius: 6px (visible on hover).
- Hover: background `#3a434d`. Icon stays white.
- Focus-visible: 2px blue outline `#378ADD`, 1px offset.
- Tooltip: "Add stint" (11px mono, dark popover above).

**Position.** Right side of the row, first of the two action buttons. Inside the 76px action-button column.

**Action on click.** Opens a full overlay modal.

**Modal fields.**
- **Start time** — required, defaulted to the end time of the car's most recent stint.
- **End time** — required.
- **Tier (auto / manual)** — required, defaulted to manual.
- **Fuel added (gallons)** — optional. When provided, a corresponding manual Refuel Event is created and linked (publishes via the telemetry stream per ADR-0005). When omitted, the stint is standalone.

**Persistence.** Writes via a WebApi endpoint under `/v1/fuel/` (FuelController).

**Disabled states.** None. Always active.

**Event propagation.** Clicks do not bubble — `event.stopPropagation()` on the button container.

**Keyboard.** Tab-focusable; space/enter to activate.

**Accessibility.**
- `aria-label="Add stint for Car {number}"`.
- Icon `aria-hidden="true"`.

**Component.**
- Button is part of `fuel-car-row`.
- Outputs: `addStintClick: EventEmitter<string>` (car number).
- The modal `fuel-add-stint-modal` lives in `ui/race-management-cloud`.

## 19. Settings button

Opens the fuel analysis detail view for this car.

**Visual treatment.**
- Icon-only: `<i class="ti ti-settings">` at 20px, color `#ffffff`.
- Otherwise identical styling to the Add Stint button.
- Tooltip: "Settings".

**Position.** Right side of the row, second of the two action buttons (right of Add Stint).

**Action on click.** Opens the fuel analysis detail view for this car. Same surface that opens when clicking the car row, but does not toggle selection (always opens, never closes).

**Disabled states.** None.

**Event propagation.** Clicks do not bubble — `event.stopPropagation()`.

**Keyboard.** Tab-focusable; space/enter to activate.

**Accessibility.**
- `aria-label="Open fuel analysis details for Car {number}"`.
- Icon `aria-hidden="true"`.

**Component.**
- Part of `fuel-car-row`.
- Outputs: `settingsClick: EventEmitter<string>` (car number).
- Parent routes to the same detail-view component used for row-click selection.

## 20. Time axis

Horizontal label strip below the gantt rows.

**Visual treatment.**
- Flex row with `justify-content: space-between` for static hour ticks.
- Padding: `8px 14px 0 70px` (the 70px left aligns the first tick under the start of the gantt canvases).
- Font: 10px mono, color `#5f6b77`, `font-variant-numeric: tabular-nums`.
- Border-top: `0.5px solid #1a2028`. Margin-top: 4px.
- One `<span>` per hour tick. No vertical tick mark lines.

**Tick placement.** One label per hour boundary across the session timeline.
- Format matches in-bar time format: lowercase `a`/`p` suffix.
- First tick at the next whole hour ≥ `Race.StartTime`; last tick at the latest whole hour ≤ `Race.EndTime`.
- Hour-only granularity (no adaptive density). Designed for events >4 hours.

**Now-line label.** A moving time label tracks the now-line position.
- Same font/size as hour ticks (10px mono, tabular nums).
- Color: red `#E24B4A` to match the now-line.
- Aligned to the now-line's x-coordinate in the axis row.
- Updates once per second.
- Follows the same visibility as the now-line.

**Synchronization with gantt scroll.** Shares horizontal scroll position with the car-row gantt canvases.

**Update.** Hour ticks re-render on `Race.StartTime` / `Race.EndTime` changes (immediate, no animation). Now-line label updates per second.

**Interaction.** Inert.

**Component.** Rendered once at the gantt-container level, not per row.

---

# WebApi Surface Touched by This View

For reference; these endpoints already exist under `FuelController` per `design.md`:

- `GET /v1/fuel/load-fuel-snapshot?teamId={...}&carNumber={...}` — fetched per car on detail open; the telemetry hub also pushes updates.
- Plus the rest of the fuel CRUD set referenced by the Add Stint modal and detail-view actions (manual refuel entry, dismissal of pending events, calibration controls, per-race RefuelEvent / FuelWindow / Stint history).

# Out of Scope for This Document

- The fuel detail view itself (opens on row click or Settings button click).
- The fuel-add-stint modal's full layout — only its fields are listed.
- The Race configuration screen where planned stints are initially created.
- The Race Monitor's other rows.
