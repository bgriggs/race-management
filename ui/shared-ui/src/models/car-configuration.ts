/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CanBusConfig } from "./can-bus-config";
import { ChannelDefinition } from "./channel-definition";
import { CounterDefinition } from "./counter-definition";
import { MathDefinition } from "./math-definition";
import { TableDefinition } from "./table-definition";
import { TimerDefinition } from "./timer-definition";
import { ConditionDefinition } from "./condition-definition";

export interface CarConfiguration {
    configurationId: string;
    configurationSchemaVersion: number;
    name: string;
    notes: string;
    lastUpdated: Date;
    lastUpdatedOnCarTimestamp: Date | null;
    car: string;
    isCloudConnectionEnabled: boolean;
    clientId: string;
    clientSecret: string;
    canConfig: CanBusConfig;
    channelDefinitions: ChannelDefinition[];
    counterDefinitions: CounterDefinition[];
    mathDefinitions: MathDefinition[];
    tableMappings: TableDefinition[];
    timerDefinitions: TimerDefinition[];
    userConditions: ConditionDefinition[];
}
