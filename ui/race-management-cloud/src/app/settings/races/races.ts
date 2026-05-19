import { Component, computed, effect, inject, signal } from '@angular/core';
import { Race } from '../../../../../shared-ui/src/cloud-api/race';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';

type EditingState = { mode: 'new' } | { mode: 'edit'; raceId: number } | null;

@Component({
  selector: 'app-races',
  imports: [],
  templateUrl: './races.html',
  styleUrl: './races.css',
})
export class Races {
  private readonly client = inject(ConfigurationClient);
  protected readonly teamSelection = inject(TeamSelectionService);

  protected readonly races = signal<Race[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<EditingState>(null);
  protected readonly draftName = signal('');
  protected readonly draftStart = signal('');
  protected readonly draftDuration = signal('');
  protected readonly draftNotes = signal('');
  protected readonly draftEventId = signal('');
  protected readonly draftOrgId = signal('');
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly isEditing = computed(() => this.editing() !== null);
  protected readonly editorTitle = computed(() => this.editing()?.mode === 'edit' ? 'Edit Race' : 'Add Race');
  protected readonly isDraftValid = computed(() => {
    if (this.draftName().trim().length === 0) return false;
    if (this.draftStart().trim().length === 0) return false;
    const duration = Number(this.draftDuration());
    if (!Number.isFinite(duration) || duration <= 0) return false;
    if (this.draftEventId() !== '' && !Number.isFinite(Number(this.draftEventId()))) return false;
    if (this.draftOrgId() !== '' && !Number.isFinite(Number(this.draftOrgId()))) return false;
    return true;
  });

  constructor() {
    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) {
        this.races.set([]);
        return;
      }
      void this.load(teamId);
    });
  }

  protected startAdd(): void {
    this.draftName.set('');
    this.draftStart.set('');
    this.draftDuration.set('');
    this.draftNotes.set('');
    this.draftEventId.set('');
    this.draftOrgId.set('');
    this.saveError.set(null);
    this.editing.set({ mode: 'new' });
  }

  protected startEdit(race: Race): void {
    this.draftName.set(race.name);
    this.draftStart.set(toDatetimeLocalValue(race.start));
    this.draftDuration.set(String(race.duration));
    this.draftNotes.set(race.notes);
    this.draftEventId.set(race.redMistEventId === null ? '' : String(race.redMistEventId));
    this.draftOrgId.set(race.redMistOrganizationId === null ? '' : String(race.redMistOrganizationId));
    this.saveError.set(null);
    this.editing.set({ mode: 'edit', raceId: race.id });
  }

  protected stopEdit(): void {
    if (this.saving()) return;
    this.editing.set(null);
    this.saveError.set(null);
  }

  protected onNameInput(value: string): void { this.draftName.set(value); }
  protected onStartInput(value: string): void { this.draftStart.set(value); }
  protected onDurationInput(value: string): void { this.draftDuration.set(value); }
  protected onNotesInput(value: string): void { this.draftNotes.set(value); }
  protected onEventIdInput(value: string): void { this.draftEventId.set(value); }
  protected onOrgIdInput(value: string): void { this.draftOrgId.set(value); }

  protected formatStart(value: Date | string): string {
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    return date.toLocaleString();
  }

  protected async saveDraft(): Promise<void> {
    const state = this.editing();
    const teamId = this.teamSelection.selectedTeamId();
    if (!state || teamId === null || !this.isDraftValid() || this.saving()) return;

    const race: Race = {
      id: state.mode === 'edit' ? state.raceId : 0,
      teamId,
      name: this.draftName().trim(),
      start: new Date(this.draftStart()),
      duration: Number(this.draftDuration()),
      notes: this.draftNotes(),
      redMistEventId: this.draftEventId() === '' ? null : Number(this.draftEventId()),
      redMistOrganizationId: this.draftOrgId() === '' ? null : Number(this.draftOrgId()),
    };

    this.saving.set(true);
    this.saveError.set(null);
    try {
      await this.client.saveRace(teamId, race);
      this.editing.set(null);
      await this.load(teamId);
    } catch (err) {
      console.error('Failed to save race:', err);
      this.saveError.set('Failed to save race. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  protected async deleteRace(race: Race): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId === null) return;
    if (!confirm(`Delete race "${race.name}"? This cannot be undone.`)) return;

    this.error.set(null);
    try {
      await this.client.deleteRace(teamId, race.id);
      await this.load(teamId);
    } catch (err) {
      console.error('Failed to delete race:', err);
      this.error.set('Failed to delete race.');
    }
  }

  private async load(teamId: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.races.set(await this.client.listRaces(teamId));
    } catch (err) {
      console.error('Failed to load races:', err);
      this.error.set('Failed to load races.');
      this.races.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}

function toDatetimeLocalValue(value: Date | string): string {
  if (!value) return '';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
