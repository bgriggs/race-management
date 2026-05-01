import { Injectable } from '@angular/core';
import { CanBusConfig } from '../../../models/can-bus-config';
import { CanBusInterfaceConfig } from '../../../models/can-bus-interface-config';
import { ConditionDefinition } from '../../../models/condition-definition';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

@Injectable({ providedIn: 'root' })
export class ChannelUsageService {
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
}