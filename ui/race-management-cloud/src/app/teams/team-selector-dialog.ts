import { Component, inject } from '@angular/core';
import { AuthService } from '../auth.service';
import { TeamSelectionService } from './team-selection.service';

@Component({
  selector: 'app-team-selector-dialog',
  templateUrl: './team-selector-dialog.html',
  styleUrl: './team-selector-dialog.css',
})
export class TeamSelectorDialog {
  protected readonly teamSelection = inject(TeamSelectionService);
  private readonly auth = inject(AuthService);

  protected select(teamId: number): void {
    this.teamSelection.selectTeam(teamId);
  }

  protected retry(): void {
    this.teamSelection.retry();
  }

  protected logout(): void {
    this.auth.logout();
  }
}
