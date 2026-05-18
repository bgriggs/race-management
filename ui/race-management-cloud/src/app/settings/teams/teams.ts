import { Component, computed, effect, inject, signal } from '@angular/core';
import { Team } from '../../../../../shared-ui/src/cloud-api/team';
import { TeamRequest } from '../../../../../shared-ui/src/cloud-api/team-request';
import { ConfigurationClient } from '../../clients/configuration-client';

type EditingState = { mode: 'new' } | { mode: 'edit'; teamId: number } | null;

@Component({
  selector: 'app-teams',
  imports: [],
  templateUrl: './teams.html',
  styleUrl: './teams.css',
})
export class Teams {
  private readonly client = inject(ConfigurationClient);

  protected readonly teams = signal<Team[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<EditingState>(null);
  protected readonly draftName = signal('');
  protected readonly draftClientId = signal('');
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly isDraftValid = computed(() =>
    this.draftName().trim().length > 0 && this.draftClientId().trim().length > 0,
  );
  protected readonly isEditing = computed(() => this.editing() !== null);
  protected readonly editorTitle = computed(() => this.editing()?.mode === 'edit' ? 'Edit Team' : 'Add Team');

  constructor() {
    effect(() => {
      void this.load();
    });
  }

  protected startAdd(): void {
    this.draftName.set('');
    this.draftClientId.set('');
    this.saveError.set(null);
    this.editing.set({ mode: 'new' });
  }

  protected startEdit(team: Team): void {
    this.draftName.set(team.name);
    this.draftClientId.set(team.clientId);
    this.saveError.set(null);
    this.editing.set({ mode: 'edit', teamId: team.id });
  }

  protected stopEdit(): void {
    if (this.saving()) return;
    this.editing.set(null);
    this.saveError.set(null);
  }

  protected onNameInput(value: string): void {
    this.draftName.set(value);
  }

  protected onClientIdInput(value: string): void {
    this.draftClientId.set(value);
  }

  protected async saveDraft(): Promise<void> {
    const state = this.editing();
    if (!state || !this.isDraftValid() || this.saving()) return;

    const request: TeamRequest = {
      name: this.draftName().trim(),
      clientId: this.draftClientId().trim(),
    };

    this.saving.set(true);
    this.saveError.set(null);
    try {
      if (state.mode === 'new') {
        await this.client.createTeam(request);
      } else {
        await this.client.updateTeam(state.teamId, request);
      }
      this.editing.set(null);
      await this.load();
    } catch (err) {
      console.error('Failed to save team:', err);
      this.saveError.set('Failed to save team. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  protected async deleteTeam(team: Team): Promise<void> {
    if (!confirm(`Delete team "${team.name}"? This cannot be undone.`)) return;
    this.error.set(null);
    try {
      await this.client.deleteTeam(team.id);
      await this.load();
    } catch (err) {
      console.error('Failed to delete team:', err);
      this.error.set('Failed to delete team.');
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.teams.set(await this.client.loadTeams());
    } catch (err) {
      console.error('Failed to load teams:', err);
      this.error.set('Failed to load teams.');
      this.teams.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
