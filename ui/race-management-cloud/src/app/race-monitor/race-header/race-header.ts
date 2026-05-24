import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Race } from '../../../../../shared-ui/src/cloud-api/race';
import { RaceSelectionService } from '../race-selection.service';

@Component({
  selector: 'app-race-header',
  imports: [],
  templateUrl: './race-header.html',
  styleUrl: './race-header.css',
})
export class RaceHeader {
  private readonly raceSelection = inject(RaceSelectionService);
  private readonly router = inject(Router);

  protected readonly races = this.raceSelection.races;
  protected readonly selectedRaceId = this.raceSelection.selectedRaceId;
  protected readonly selectedRace = this.raceSelection.selectedRace;
  protected readonly dropdownOpen = signal(false);

  protected readonly localTime = computed(() => formatClock(this.raceSelection.now()));

  protected readonly raceTime = computed(() => {
    const race = this.selectedRace();
    if (!race) return '--:--:--';
    const elapsedMs = this.raceSelection.now().getTime() - new Date(race.start).getTime();
    if (elapsedMs <= 0) return '00:00:00';
    return formatDuration(elapsedMs);
  });

  protected readonly timeToGo = computed(() => {
    const race = this.selectedRace();
    if (!race) return '--:--:--';
    const endMs = new Date(race.start).getTime() + race.duration * 60 * 60 * 1000;
    const remainingMs = endMs - this.raceSelection.now().getTime();
    if (remainingMs <= 0) return '00:00:00';
    return formatDuration(remainingMs);
  });

  protected readonly currentLap = computed(() => '--');
  protected readonly flagState = computed(() => '--');

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

function formatClock(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

function formatDuration(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}
