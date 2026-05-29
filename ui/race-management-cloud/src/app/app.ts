import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Nav } from './nav/nav';
import { TeamSelectionService } from './teams/team-selection.service';
import { TeamSelectorDialog } from './teams/team-selector-dialog';
import { VersionFooterComponent } from '../../../shared-ui/src/lib/version-footer/version-footer.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Nav, TeamSelectorDialog, VersionFooterComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly teamSelection = inject(TeamSelectionService);
}
