import { Injectable, Signal, signal } from '@angular/core';
import { CarConfiguration } from '../../../models/car-configuration';
import { CanBusConfig } from '../../../models/can-bus-config';
import { CanBusInterfaceConfig } from '../../../models/can-bus-interface-config';
import { ConditionDefinition } from '../../../models/condition-definition';
import { MathDefinition } from '../../../models/math-definition';
import { CounterDefinition } from '../../../models/counter-definition';
import { TimerDefinition } from '../../../models/timer-definition';
import { TableDefinition } from '../../../models/table-definition';
import { AlarmDefinition } from '../../../models/alarm-definition';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

@Injectable({ providedIn: 'root' })
export class ChannelUsageService {
  private readonly _usedChannelIds = signal<string[]>([]);

  /** Channel IDs that are assigned as outputs across the active configuration. Updated via {@link updateFromConfiguration}. */
  readonly usedChannelIds: Signal<string[]> = this._usedChannelIds.asReadonly();

  /**
   * Recomputes the set of output-assigned channel IDs from the full configuration and
   * updates the {@link usedChannelIds} signal. Call this whenever the active configuration
   * is loaded or changes.
   */
  updateFromConfiguration(config: CarConfiguration | null): void {
    if (!config) {
      this._usedChannelIds.set([]);
      return;
    }

    const ids = new Set<string>();

    for (const id of this.getUsedChannelIdsFromCanInterfaces(config.canConfig?.interfaces ?? [])) {
      ids.add(id);
    }
    for (const id of this.getUsedChannelIdsFromUserConditions(config.userConditions)) {
      ids.add(id);
    }
    for (const id of this.getUsedChannelIdsFromMathDefinitions(config.mathDefinitions)) {
      ids.add(id);
    }
    for (const id of this.getUsedChannelIdsFromCounterDefinitions(config.counterDefinitions)) {
      ids.add(id);
    }
    for (const id of this.getUsedChannelIdsFromTimerDefinitions(config.timerDefinitions)) {
      ids.add(id);
    }
    for (const id of this.getUsedChannelIdsFromTableDefinitions(config.tableDefinitions)) {
      ids.add(id);
    }
    for (const id of this.getUsedChannelIdsFromAlarmDefinitions(config.alarmDefinitions)) {
      ids.add(id);
    }

    this._usedChannelIds.set([...ids]);
  }

  getUsedChannelIdsFromCanConfig(
    canConfig: CanBusConfig | null | undefined,
    userConditions: readonly ConditionDefinition[] | null | undefined = []
  ): string[] {
    const usedChannelIds = new Set<string>();

    for (const channelId of this.getUsedChannelIdsFromCanInterfaces(canConfig?.interfaces ?? [])) {
      usedChannelIds.add(channelId);
    }

    for (const channelId of this.getUsedChannelIdsFromUserConditions(userConditions)) {
      usedChannelIds.add(channelId);
    }

    return [...usedChannelIds];
  }

  getUsedChannelIdsFromCanInterfaces(interfaces: readonly CanBusInterfaceConfig[]): string[] {
    const usedChannelIds = new Set<string>();

    for (const canInterface of interfaces) {
      for (const message of canInterface.messages) {
        // A receive mapping writes into the channel, so it is a channel data source.
        if (!message.isReceive) {
          continue;
        }

        for (const assignment of message.channelAssignments) {
          if (assignment.id) {
            usedChannelIds.add(assignment.id);
          }
        }
      }
    }

    return [...usedChannelIds];
  }

  getUsedChannelIdsFromUserConditions(
    userConditions: readonly ConditionDefinition[] | null | undefined
  ): string[] {
    if (!userConditions?.length) {
      return [];
    }

    const usedChannelIds = new Set<string>();

    for (const condition of userConditions) {
      if (condition.outputChannelId && condition.outputChannelId !== EMPTY_GUID) {
        usedChannelIds.add(condition.outputChannelId);
      }
    }

    return [...usedChannelIds];
  }

  getUsedChannelIdsFromMathDefinitions(
    mathDefinitions: readonly MathDefinition[] | null | undefined
  ): string[] {
    if (!mathDefinitions?.length) {
      return [];
    }

    const usedChannelIds = new Set<string>();

    for (const def of mathDefinitions) {
      if (def.outputChannelId && def.outputChannelId !== EMPTY_GUID) {
        usedChannelIds.add(def.outputChannelId);
      }
    }

    return [...usedChannelIds];
  }

  getUsedChannelIdsFromCounterDefinitions(
    counterDefinitions: readonly CounterDefinition[] | null | undefined
  ): string[] {
    if (!counterDefinitions?.length) {
      return [];
    }

    const usedChannelIds = new Set<string>();

    for (const def of counterDefinitions) {
      if (def.outputChId && def.outputChId !== EMPTY_GUID) {
        usedChannelIds.add(def.outputChId);
      }
    }

    return [...usedChannelIds];
  }

  getUsedChannelIdsFromTimerDefinitions(
    timerDefinitions: readonly TimerDefinition[] | null | undefined
  ): string[] {
    if (!timerDefinitions?.length) {
      return [];
    }

    const usedChannelIds = new Set<string>();

    for (const def of timerDefinitions) {
      if (def.outputChId && def.outputChId !== EMPTY_GUID) {
        usedChannelIds.add(def.outputChId);
      }
    }

    return [...usedChannelIds];
  }

  getUsedChannelIdsFromTableDefinitions(
    tableDefinitions: readonly TableDefinition[] | null | undefined
  ): string[] {
    if (!tableDefinitions?.length) {
      return [];
    }

    const usedChannelIds = new Set<string>();

    for (const def of tableDefinitions) {
      if (def.outputChannel && def.outputChannel !== EMPTY_GUID) {
        usedChannelIds.add(def.outputChannel);
      }
    }

    return [...usedChannelIds];
  }

  getUsedChannelIdsFromAlarmDefinitions(
    alarmDefinitions: readonly AlarmDefinition[] | null | undefined
  ): string[] {
    if (!alarmDefinitions?.length) {
      return [];
    }

    const usedChannelIds = new Set<string>();

    for (const def of alarmDefinitions) {
      if (def.alarmStatusChannelId && def.alarmStatusChannelId !== EMPTY_GUID) {
        usedChannelIds.add(def.alarmStatusChannelId);
      }
    }

    return [...usedChannelIds];
  }
}