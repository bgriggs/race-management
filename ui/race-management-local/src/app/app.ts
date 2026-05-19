import { Component, HostListener, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth.service';
import { TeamSelectionService } from './teams/team-selection.service';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly teamSelection = inject(TeamSelectionService);
  protected readonly userMenuOpen = signal(false);
  protected readonly teamMenuOpen = signal(false);

  toggleUserMenu(event: Event): void {
    event.stopPropagation();
    this.teamMenuOpen.set(false);
    this.userMenuOpen.update(v => !v);
  }

  toggleTeamMenu(event: Event): void {
    event.stopPropagation();
    this.userMenuOpen.set(false);
    this.teamMenuOpen.update(v => !v);
  }

  selectTeam(teamId: number, event: Event): void {
    event.stopPropagation();
    this.teamSelection.selectTeam(teamId);
    this.teamMenuOpen.set(false);
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.userMenuOpen.set(false);
    this.teamMenuOpen.set(false);
  }
}
