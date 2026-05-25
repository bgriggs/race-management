import { Routes } from '@angular/router';
import { RaceMonitor } from './race-monitor/race-monitor';
import { Alarms } from './settings/alarms/alarms';
import { Cars } from './settings/cars/cars';
import { Races } from './settings/races/races';
import { Settings } from './settings/settings';
import { SiteSettings } from './settings/site-settings/site-settings';
import { TeamSettings } from './settings/team-settings/team-settings';
import { Teams } from './settings/teams/teams';
import { adminGuard } from './teams/admin.guard';
import { teamRequiredGuard } from './teams/team-required.guard';

export const routes: Routes = [
  { path: 'race-monitor', component: RaceMonitor, canActivate: [teamRequiredGuard] },
  {
    path: 'settings',
    component: Settings,
    canActivate: [teamRequiredGuard, adminGuard],
    children: [
      { path: '', redirectTo: 'site-settings', pathMatch: 'full' },
      { path: 'site-settings', component: SiteSettings },
      { path: 'teams', component: Teams },
      { path: 'team-settings', component: TeamSettings },
      { path: 'cars', component: Cars },
      { path: 'races', component: Races },
      { path: 'alarms', component: Alarms },
    ],
  },
  { path: '', redirectTo: 'race-monitor', pathMatch: 'full' },
];
