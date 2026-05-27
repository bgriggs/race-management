import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Race } from '../../../../../shared-ui/src/cloud-api/race';
import { RaceSelectionService } from '../race-selection.service';
import { RaceStateStore } from '../race-state-store';

const PLACEHOLDER_TIME = '--:--:--';
const PLACEHOLDER_SHORT = '--';

@Component({
  selector: 'app-race-header',
  imports: [],
  templateUrl: './race-header.html',
  styleUrl: './race-header.css',
})
export class RaceHeader {
  private readonly raceSelection = inject(RaceSelectionService);
  private readonly raceState = inject(RaceStateStore);
  private readonly router = inject(Router);

  protected readonly races = this.raceSelection.races;
  protected readonly selectedRaceId = this.raceSelection.selectedRaceId;
  protected readonly selectedRace = this.raceSelection.selectedRace;
  protected readonly dropdownOpen = signal(false);

  // All header values — including "Local" — come from the RedMist feed (RaceStateStore).
  // The browser's wall clock is intentionally NOT used: every engineer in the strategy
  // booth should see exactly what the timing tower sees, and clock drift between machines
  // would otherwise cause subtle disagreements during a stop-window discussion. When
  // RedMist isn't streaming for this team — no event paired, lease lost, creds missing —
  // the store reports null and we render placeholders.
  protected readonly localTime = computed(() => this.raceState.state()?.localTimeOfDay ?? PLACEHOLDER_TIME);
  protected readonly raceTime = computed(() => this.raceState.state()?.runningRaceTime ?? PLACEHOLDER_TIME);
  protected readonly timeToGo = computed(() => this.raceState.state()?.timeToGo ?? PLACEHOLDER_TIME);
  protected readonly currentLap = computed(() => {
    const lap = this.raceState.state()?.leaderLap;
    return lap == null ? PLACEHOLDER_SHORT : String(lap);
  });
  protected readonly flagState = computed(() => this.raceState.state()?.flag ?? PLACEHOLDER_SHORT);

  protected toggleDropdown(event: Event): void {
    event.stopPropagation();
    this.dropdownOpen.update(v => !v);
  }

  protected selectRace(race: Race, event: Event): void {
    event.stopPropagation();
    this.raceSelection.selectRace(race.id);
    this.dropdownOpen.set(false);
  }

  protected onEdit(event: Event): void {
    event.stopPropagation();
    this.dropdownOpen.set(false);
    void this.router.navigate(['/settings/races']);
  }

  @HostListener('document:click')
  closeDropdown(): void {
    this.dropdownOpen.set(false);
  }

  protected raceLabel(race: Race): string {
    const date = new Date(race.start);
    if (Number.isNaN(date.getTime())) return race.name;
    return `${race.name} — ${date.toLocaleDateString()}`;
  }
}
