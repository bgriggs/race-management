import { Routes } from '@angular/router';
import { RaceMonitor } from './race-monitor/race-monitor';

export const routes: Routes = [
  { path: 'race-monitor', component: RaceMonitor },
  { path: '', redirectTo: 'race-monitor', pathMatch: 'full' },
];
