import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface TabDef {
  readonly path: string;
  readonly label: string;
}

@Component({
  selector: 'app-settings',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './settings.html',
  styleUrl: './settings.css',
})
export class Settings {
  protected readonly tabs: readonly TabDef[] = [
    { path: 'site-settings', label: 'Site Settings' },
    { path: 'teams', label: 'Teams' },
    { path: 'team-settings', label: 'Team Settings' },
    { path: 'cars', label: 'Cars' },
    { path: 'races', label: 'Races' },
  ];
}
