import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { UserTeam } from '../../../../shared-ui/src/cloud-api/user-team';
import { AuthService } from '../auth.service';
import { LocalManagementDataClient } from '../data/local-management-data-client';

const STORAGE_PREFIX = 'race-management-local.selected-team-id:';

@Injectable({ providedIn: 'root' })
export class TeamSelectionService {
  private readonly auth = inject(AuthService);
  private readonly client = inject(LocalManagementDataClient);

  private readonly _teams = signal<UserTeam[]>([]);
  private readonly _selectedTeamId = signal<number | null>(null);
  private readonly _loading = signal(false);
  private readonly _loaded = signal(false);

  readonly teams = computed(() => this._teams());
  readonly selectedTeamId = computed(() => this._selectedTeamId());
  readonly selectedTeam = computed(() => {
    const id = this._selectedTeamId();
    return id === null ? null : this._teams().find(t => t.id === id) ?? null;
  });
  readonly hasMultipleTeams = computed(() => this._teams().length > 1);
  readonly loading = computed(() => this._loading());

  constructor() {
    effect(() => {
      if (this.auth.isLoggedIn()) {
        void this.loadTeams();
      } else {
        this.clear();
      }
    });
  }

  selectTeam(teamId: number): void {
    if (!this._teams().some(t => t.id === teamId)) return;
    this._selectedTeamId.set(teamId);
    const key = this.storageKey();
    if (key) localStorage.setItem(key, String(teamId));
  }

  private async loadTeams(): Promise<void> {
    if (this._loading() || this._loaded()) return;
    this._loading.set(true);
    try {
      const teams = await this.client.listMyTeamsAsync();
      this._teams.set(teams);
      this._selectedTeamId.set(this.resolveInitialSelection(teams));
      this._loaded.set(true);
    } catch (err) {
      console.error('Failed to load user teams:', err);
    } finally {
      this._loading.set(false);
    }
  }

  private resolveInitialSelection(teams: UserTeam[]): number | null {
    if (teams.length === 0) return null;
    const stored = this.readStoredSelection();
    if (stored !== null && teams.some(t => t.id === stored)) return stored;
    return teams[0].id;
  }

  private clear(): void {
    this._teams.set([]);
    this._selectedTeamId.set(null);
    this._loaded.set(false);
  }

  private readStoredSelection(): number | null {
    const key = this.storageKey();
    if (!key) return null;
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    const parsed = Number(raw);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private storageKey(): string | null {
    const email = this.auth.user()?.email;
    return email ? `${STORAGE_PREFIX}${email.toLowerCase()}` : null;
  }
}
