import { Component } from '@angular/core';
import { AlarmsList } from './alarms-list/alarms-list';
import { CarStatusTable } from './car-status-table/car-status-table';
import { CompetitorAnalysis } from './competitor-analysis/competitor-analysis';
import { FuelPitStrategy } from './fuel-pit-strategy/fuel-pit-strategy';
import { RaceStrategyChat } from './race-strategy-chat/race-strategy-chat';

@Component({
  selector: 'app-race-monitor',
  imports: [CarStatusTable, AlarmsList, FuelPitStrategy, RaceStrategyChat, CompetitorAnalysis],
  templateUrl: './race-monitor.html',
  styleUrl: './race-monitor.css',
})
export class RaceMonitor {

}
