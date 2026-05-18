import { Component } from '@angular/core';
import { AlarmsList } from './alarms-list/alarms-list';
import { CarStatusTable } from './car-status-table/car-status-table';
import { CompetitorAnalysis } from './competitor-analysis/competitor-analysis';
import { FuelPitStrategy } from './fuel-pit-strategy/fuel-pit-strategy';
import { RaceHeader } from './race-header/race-header';
import { RacePosition } from './race-position/race-position';
import { RaceStrategyChat } from './race-strategy-chat/race-strategy-chat';

@Component({
  selector: 'app-race-monitor',
  imports: [RaceHeader, CarStatusTable, RacePosition, AlarmsList, FuelPitStrategy, RaceStrategyChat, CompetitorAnalysis],
  templateUrl: './race-monitor.html',
  styleUrl: './race-monitor.css',
})
export class RaceMonitor {

}
