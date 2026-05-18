import { Component, DestroyRef, HostListener, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Race } from '../../../../../shared-ui/src/cloud-api/race';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';

const ONE_DAY_MS = 24 * 60 * 60 * 1000;

@Component({
  selector: 'app-race-header',
  imports: [],
  templateUrl: './race-header.html',
  styleUrl: './race-header.css',
})
export class RaceHeader {
  private readonly client = inject(ConfigurationClient);
  private readonly teamSelection = inject(TeamSelectionService);
  private readonly router = inject(Router);

  protected readonly now = signal(new Date());
  protected readonly races = signal<Race[]>([]);
  protected readonly selectedRaceId = signal<number | null>(null);
  protected readonly dropdownOpen = signal(false);

  protected readonly selectedRace = computed(() => {
    const id = this.selectedRaceId();
    return id === null ? null : this.races().find(r => r.id === id) ?? null;
  });

  protected readonly localTime = computed(() => formatClock(this.now()));

  protected readonly raceTime = computed(() => {
    const race = this.selectedRace();
    if (!race) return '--:--:--';
    const elapsedMs = this.now().getTime() - new Date(race.start).getTime();
    if (elapsedMs <= 0) return '00:00:00';
    return formatDuration(elapsedMs);
  });

  protected readonly timeToGo = computed(() => {
    const race = this.selectedRace();
    if (!race) return '--:--:--';
    const endMs = new Date(race.start).getTime() + race.duration * 60 * 60 * 1000;
    const remainingMs = endMs - this.now().getTime();
    if (remainingMs <= 0) return '00:00:00';
    return formatDuration(remainingMs);
  });

  protected readonly currentLap = computed(() => '--');
  protected readonly flagState = computed(() => '--');

  constructor() {
    const destroyRef = inject(DestroyRef);
    const tick = setInterval(() => this.now.set(new Date()), 1000);
    destroyRef.onDestroy(() => clearInterval(tick));

    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) {
        this.races.set([]);
        this.selectedRaceId.set(null);
        return;
      }
      void this.loadRaces(teamId);
    });
  }

  protected toggleDropdown(event: Event): void {
    event.stopPropagation();
    this.dropdownOpen.update(v => !v);
  }

  protected selectRace(race: Race, event: Event): void {
    event.stopPropagation();
    this.selectedRaceId.set(race.id);
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

  private async loadRaces(teamId: number): Promise<void> {
    try {
      const all = await this.client.listRaces(teamId);
      const cutoff = Date.now() + ONE_DAY_MS;
      const filtered = all
        .filter(r => new Date(r.start).getTime() <= cutoff)
        .sort((a, b) => new Date(b.start).getTime() - new Date(a.start).getTime());
      this.races.set(filtered);
      if (this.selectedRaceId() === null && filtered.length > 0) {
        this.selectedRaceId.set(filtered[0].id);
      } else if (this.selectedRaceId() !== null && !filtered.some(r => r.id === this.selectedRaceId())) {
        this.selectedRaceId.set(filtered.length > 0 ? filtered[0].id : null);
      }
    } catch (err) {
      console.error('Failed to load races:', err);
      this.races.set([]);
    }
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
