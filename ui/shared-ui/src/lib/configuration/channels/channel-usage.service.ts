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

const FEATURE_LABELS: Readonly<Record<string, string>> = {
  'fuel-analysis': 'Fuel Analysis',
  'throttle-consumption': 'Throttle Consumption',
};

function formatFeatureLabel(featureId: string): string {
  return FEATURE_LABELS[featureId]
    ?? featureId.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
}

@Injectable({ providedIn: 'root' })
export class ChannelUsageService {
  private readonly _usedChannelIds = signal<string[]>([]);
  private readonly _channelUsageMap = signal<ReadonlyMap<string, string[]>>(new Map());

  /** Channel IDs that are assigned as outputs across the active configuration. Updated via {@link updateFromConfiguration}. */
  readonly usedChannelIds: Signal<string[]> = this._usedChannelIds.asReadonly();

  /**
   * Maps each output-assigned channel ID to the list of configuration locations that write to it.
   * Updated via {@link updateFromConfiguration}.
   */
  readonly channelUsageMap: Signal<ReadonlyMap<string, string[]>> = this._channelUsageMap.asReadonly();

  /**
   * Recomputes the set of output-assigned channel IDs from the full configuration and
   * updates the {@link usedChannelIds} and {@link channelUsageMap} signals. Call this
   * whenever the active configuration is loaded or changes.
   */
  updateFromConfiguration(config: CarConfiguration | null): void {
    if (!config) {
      this._usedChannelIds.set([]);
      this._channelUsageMap.set(new Map());
      return;
    }

    const usageMap = new Map<string, string[]>();

    const addUsage = (channelId: string, label: string) => {
      if (!channelId || channelId === EMPTY_GUID) return;
      const existing = usageMap.get(channelId);
      if (existing) {
        existing.push(label);
      } else {
        usageMap.set(channelId, [label]);
      }
    };

    for (const iface of config.canConfig?.interfaces ?? []) {
      for (const msg of iface.messages) {
        if (!msg.isReceive) continue;
        const label = `CAN ${iface.interfaceName} - 0x${msg.canId.toString(16).toUpperCase()}`;
        for (const assignment of msg.channelAssignments) {
          addUsage(assignment.id, label);
        }
      }
    }

    for (const def of config.userConditions ?? []) {
      addUsage(def.outputChannelId, `Condition: ${def.name}`);
    }

    for (const def of config.mathDefinitions ?? []) {
      addUsage(def.outputChannelId, `Math: ${def.name}`);
    }

    for (const def of config.counterDefinitions ?? []) {
      addUsage(def.outputChId, `Counter: ${def.name}`);
    }

    for (const def of config.timerDefinitions ?? []) {
      addUsage(def.outputChId, `Timer: ${def.name}`);
    }

    for (const def of config.tableDefinitions ?? []) {
      addUsage(def.outputChannel, `Table: ${def.name}`);
    }

    for (const def of config.alarmDefinitions ?? []) {
      if (def.alarmStatusChannelId) {
        addUsage(def.alarmStatusChannelId, `Alarm: ${def.name}`);
      }
    }

    // Channels populated by an internal feature's code (e.g., FuelReconciler writing the
    // FuelRange* channels, ThrottleProxyConsumer writing the ThrottleProxy* channels).
    // Counts as a value source, same as a math/timer/alarm output.
    for (const def of config.channelDefinitions ?? []) {
      if (def.producedByFeature) {
        addUsage(def.id, `Output: ${formatFeatureLabel(def.producedByFeature)}`);
      }
    }

    this._usedChannelIds.set([...usageMap.keys()]);
    this._channelUsageMap.set(usageMap);
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