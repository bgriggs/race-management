import { Component, effect, inject, signal } from '@angular/core';
import { SiteSettings as SiteSettingsModel } from '../../../../../shared-ui/src/cloud-api/site-settings';
import { ConfigurationClient } from '../../clients/configuration-client';
import { TeamSelectionService } from '../../teams/team-selection.service';

@Component({
  selector: 'app-site-settings',
  imports: [],
  templateUrl: './site-settings.html',
  styleUrl: './site-settings.css',
})
export class SiteSettings {
  private readonly client = inject(ConfigurationClient);
  protected readonly teamSelection = inject(TeamSelectionService);

  protected readonly clientId = signal('');
  protected readonly secret = signal('');
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);

  constructor() {
    effect(() => {
      const teamId = this.teamSelection.selectedTeamId();
      if (teamId === null) {
        this.clientId.set('');
        this.secret.set('');
        return;
      }
      void this.load(teamId);
    });
  }

  protected onClientIdInput(value: string): void {
    this.clientId.set(value);
    this.saved.set(false);
  }

  protected onSecretInput(value: string): void {
    this.secret.set(value);
    this.saved.set(false);
  }

  protected async save(): Promise<void> {
    const teamId = this.teamSelection.selectedTeamId();
    if (teamId === null || this.saving()) return;

    this.saving.set(true);
    this.error.set(null);
    this.saved.set(false);
    try {
      const payload: SiteSettingsModel = {
        teamId,
        redMistClientId: this.clientId(),
        redMistClientSecret: this.secret(),
      };
      const result = await this.client.saveSiteSettings(teamId, payload);
      this.clientId.set(result.redMistClientId);
      this.secret.set('');
      this.saved.set(true);
    } catch (err) {
      console.error('Failed to save site settings:', err);
      this.error.set('Failed to save settings. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  private async load(teamId: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.saved.set(false);
    try {
      const settings = await this.client.loadSiteSettings(teamId);
      this.clientId.set(settings?.redMistClientId ?? '');
      this.secret.set('');
    } catch (err) {
      console.error('Failed to load site settings:', err);
      this.error.set('Failed to load settings.');
    } finally {
      this.loading.set(false);
    }
  }
}
