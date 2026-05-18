import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Nav } from './nav/nav';
import { TeamSelectionService } from './teams/team-selection.service';
import { TeamSelectorDialog } from './teams/team-selector-dialog';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Nav, TeamSelectorDialog],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly teamSelection = inject(TeamSelectionService);
}
