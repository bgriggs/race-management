/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CanBusConfig } from "./can-bus-config";
import { ChannelDefinition } from "./channel-definition";
import { AlarmDefinition } from "./alarm-definition";
import { CounterDefinition } from "./counter-definition";
import { MathDefinition } from "./math-definition";
import { TableDefinition } from "./table-definition";
import { TimerDefinition } from "./timer-definition";
import { ConditionDefinition } from "./condition-definition";
import { LoggingDefinition } from "./logging-definition";
import { EnumDefinition } from "./enum-definition";

/**
 * Configuration settings for the service running in the car, such as on a Raspberry PI.
 */
export interface CarConfiguration {
    /**
     * ID of the instance of the configuration.
     */
    configurationId: string;
    /**
     * Version of the configuration schema. This is used to handle breaking changes to the 
     * configuration schema. If the version is different from what the service expects, it 
     * may indicate that the configuration is incompatible with the current version of the 
     * service, and the service can choose to reject the configuration and prompt the user 
     * to update their configuration to a compatible version.
     */
    configurationSchemaVersion: number;
    /**
     * Name of the configuration such as car 777.
     */
    name: string;
    /**
     * Comments about the configuration.
     */
    notes: string;
    /**
     * Last time the configuration was saved/persisted.
     */
    lastUpdated: Date;
    /**
     * Last time the configuration was updated on the car, based on the timestamp from the car. 
     * This is used to determine if the configuration has been updated on the car since it was 
     * last loaded, which can help prevent overwriting changes made on the car when saving a 
     * configuration from an external source like a phone or laptop.
     */
    lastUpdatedOnCarTimestamp: Date | null;
    /**
     * Car number this configuration runs on.
     */
    car: string;
    isCloudConnectionEnabled: boolean;
    clientId: string;
    clientSecret: string;
    canConfig: CanBusConfig;
    channelDefinitions: ChannelDefinition[];
    alarmDefinitions: AlarmDefinition[];
    counterDefinitions: CounterDefinition[];
    mathDefinitions: MathDefinition[];
    tableMappings: TableDefinition[];
    timerDefinitions: TimerDefinition[];
    userConditions: ConditionDefinition[];
    loggingDefinitions: LoggingDefinition[];
    enumDefinitions: EnumDefinition[];
}
