/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { decode, encode } from "@msgpack/msgpack";
import { CarConfiguration } from "./car-configuration";
import { CanMessageConfig } from "./can-message-config";
import { CanBusConfig } from "./can-bus-config";
import { CanBusInterfaceConfig } from "./can-bus-interface-config";
import { CanChannelAssignmentConfig } from "./can-channel-assignment-config";
import { ChannelDefinition } from "./channel-definition";
import { CounterDefinition } from "./counter-definition";
import { MathDefinition } from "./math-definition";
import { TableDefinition } from "./table-definition";
import { TableMapping } from "./table-mapping";
import { TimerDefinition } from "./timer-definition";
import { ConditionDefinition } from "./condition-definition";
import { StatementDefinition } from "./statement-definition";
import { ComparisonDefinition } from "./comparison-definition";
import { CarConfigurationSummary } from "./car-configuration-summary";
import { AlarmDefinition } from "./alarm-definition";
import { EnumDefinition } from "./enum-definition";
import { EnumValueDefinition } from "./enum-value-definition";
import { LoggingDefinition } from "./logging-definition";
import { CarFuelConfig } from "./car-fuel-config";
import { ThrottleConsumptionConfig } from "./throttle-consumption-config";
import { ChannelDistribution } from "./channel-distribution";
import { ChannelScope } from "./channel-scope";
import { InterpolationType } from "./interpolation-type";
import { LoggingFrequency } from "./logging-frequency";
import { LogicType } from "./logic-type";
import { MathType } from "./math-type";
import { SimpleOperationType } from "./simple-operation-type";

export function decodeMessagePack<T>(bytes: Uint8Array): T {
    return decode(bytes) as T;
}

export function encodeMessagePack<T>(value: T): Uint8Array {
    return encode(value);
}

function convertDictionary<V>(raw: unknown, convertValue: (v: unknown) => V): { [key: string]: V } {
    const out: { [key: string]: V } = {};
    if (raw == null) return out;
    if (raw instanceof Map) {
        for (const [k, v] of raw) out[String(k)] = convertValue(v);
    } else {
        for (const k of Object.keys(raw as object)) {
            out[k] = convertValue((raw as Record<string, unknown>)[k]);
        }
    }
    return out;
}

export function carConfigurationFromMessagePack(obj: Record<string, unknown>): CarConfiguration {
    return {
        configurationId: obj["ConfigurationId"] as string,
        configurationSchemaVersion: obj["ConfigurationSchemaVersion"] as number,
        name: obj["Name"] as string,
        notes: obj["Notes"] as string,
        lastUpdated: new Date(obj["LastUpdated"] as string | number | Date),
        lastUpdatedOnCarTimestamp: obj["LastUpdatedOnCarTimestamp"] != null ? new Date(obj["LastUpdatedOnCarTimestamp"] as string | number | Date) : null,
        car: obj["Car"] as string,
        isCloudConnectionEnabled: obj["IsCloudConnectionEnabled"] as boolean,
        clientId: obj["ClientId"] as string,
        clientSecret: obj["ClientSecret"] as string,
        canConfig: canBusConfigFromMessagePack(obj["CanConfig"] as Record<string, unknown>),
        channelDefinitions: (obj["ChannelDefinitions"] as unknown[]).map(v => channelDefinitionFromMessagePack(v as Record<string, unknown>)),
        alarmDefinitions: (obj["AlarmDefinitions"] as unknown[]).map(v => alarmDefinitionFromMessagePack(v as Record<string, unknown>)),
        counterDefinitions: (obj["CounterDefinitions"] as unknown[]).map(v => counterDefinitionFromMessagePack(v as Record<string, unknown>)),
        mathDefinitions: (obj["MathDefinitions"] as unknown[]).map(v => mathDefinitionFromMessagePack(v as Record<string, unknown>)),
        tableDefinitions: (obj["TableDefinitions"] as unknown[]).map(v => tableDefinitionFromMessagePack(v as Record<string, unknown>)),
        timerDefinitions: (obj["TimerDefinitions"] as unknown[]).map(v => timerDefinitionFromMessagePack(v as Record<string, unknown>)),
        userConditions: (obj["UserConditions"] as unknown[]).map(v => conditionDefinitionFromMessagePack(v as Record<string, unknown>)),
        loggingDefinitions: (obj["LoggingDefinitions"] as unknown[]).map(v => loggingDefinitionFromMessagePack(v as Record<string, unknown>)),
        enumDefinitions: (obj["EnumDefinitions"] as unknown[]).map(v => enumDefinitionFromMessagePack(v as Record<string, unknown>)),
        fuelConfig: carFuelConfigFromMessagePack(obj["FuelConfig"] as Record<string, unknown>),
    };
}

export function decodeCarConfigurationMessagePack(bytes: Uint8Array): CarConfiguration {
    return carConfigurationFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function canMessageConfigFromMessagePack(obj: Record<string, unknown>): CanMessageConfig {
    return {
        isEnabled: obj["IsEnabled"] as boolean,
        canId: obj["CanId"] as number,
        isExtended: obj["IsExtended"] as boolean,
        length: obj["Length"] as number,
        isBigEndian: obj["IsBigEndian"] as boolean,
        isReceive: obj["IsReceive"] as boolean,
        transmitRate: obj["TransmitRate"] as string,
        channelAssignments: (obj["ChannelAssignments"] as unknown[]).map(v => canChannelAssignmentConfigFromMessagePack(v as Record<string, unknown>)),
    };
}

export function decodeCanMessageConfigMessagePack(bytes: Uint8Array): CanMessageConfig {
    return canMessageConfigFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function canBusConfigFromMessagePack(obj: Record<string, unknown>): CanBusConfig {
    return {
        canBusEnabled: (obj["CanBusEnabled"] as unknown[]).map(v => v as boolean),
        interfaces: (obj["Interfaces"] as unknown[]).map(v => canBusInterfaceConfigFromMessagePack(v as Record<string, unknown>)),
    };
}

export function decodeCanBusConfigMessagePack(bytes: Uint8Array): CanBusConfig {
    return canBusConfigFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function canBusInterfaceConfigFromMessagePack(obj: Record<string, unknown>): CanBusInterfaceConfig {
    return {
        interfaceName: obj["InterfaceName"] as string,
        bitRate: obj["BitRate"] as number,
        silentOnCanBus: obj["SilentOnCanBus"] as boolean,
        messages: (obj["Messages"] as unknown[]).map(v => canMessageConfigFromMessagePack(v as Record<string, unknown>)),
    };
}

export function decodeCanBusInterfaceConfigMessagePack(bytes: Uint8Array): CanBusInterfaceConfig {
    return canBusInterfaceConfigFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function canChannelAssignmentConfigFromMessagePack(obj: Record<string, unknown>): CanChannelAssignmentConfig {
    return {
        id: obj["Id"] as string,
        offset: obj["Offset"] as number,
        length: obj["Length"] as number,
        mask: obj["Mask"] as number,
        isSigned: obj["IsSigned"] as boolean,
        formulaMultiplier: obj["FormulaMultiplier"] as number,
        formulaDivider: obj["FormulaDivider"] as number,
        formulaConst: obj["FormulaConst"] as number,
    };
}

export function decodeCanChannelAssignmentConfigMessagePack(bytes: Uint8Array): CanChannelAssignmentConfig {
    return canChannelAssignmentConfigFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function channelDefinitionFromMessagePack(obj: Record<string, unknown>): ChannelDefinition {
    return {
        id: obj["Id"] as string,
        isReserved: obj["IsReserved"] as boolean,
        category: obj["Category"] as string,
        name: obj["Name"] as string,
        abbreviation: obj["Abbreviation"] as string,
        dataType: obj["DataType"] as string,
        baseUnitType: obj["BaseUnitType"] as string,
        outputUnitType: obj["OutputUnitType"] as string,
        outputDecimalPlaces: obj["OutputDecimalPlaces"] as number,
        lowRange: obj["LowRange"] as number,
        highRange: obj["HighRange"] as number,
        defaultValue: obj["DefaultValue"] as number,
        groupTag: obj["GroupTag"] as string,
        enumConversion: obj["EnumConversion"] != null ? obj["EnumConversion"] as string : null,
        timeoutMs: obj["TimeoutMs"] as number,
        distribution: obj["Distribution"] as ChannelDistribution,
        isDistributionLocked: obj["IsDistributionLocked"] as boolean,
        scope: obj["Scope"] as ChannelScope,
        managedByFeature: obj["ManagedByFeature"] != null ? obj["ManagedByFeature"] as string : null,
    };
}

export function decodeChannelDefinitionMessagePack(bytes: Uint8Array): ChannelDefinition {
    return channelDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function counterDefinitionFromMessagePack(obj: Record<string, unknown>): CounterDefinition {
    return {
        id: obj["Id"] as string,
        name: obj["Name"] as string,
        outputChId: obj["OutputChId"] as string,
        upChId: obj["UpChId"] as string,
        downChId: obj["DownChId"] as string,
        resetChId: obj["ResetChId"] as string,
        maxValue: obj["MaxValue"] as number,
        minValue: obj["MinValue"] as number,
        rollAtLimit: obj["RollAtLimit"] as boolean,
        startValue: obj["StartValue"] as number,
        persistValue: obj["PersistValue"] as boolean,
    };
}

export function decodeCounterDefinitionMessagePack(bytes: Uint8Array): CounterDefinition {
    return counterDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function mathDefinitionFromMessagePack(obj: Record<string, unknown>): MathDefinition {
    return {
        id: obj["Id"] as string,
        name: obj["Name"] as string,
        type: obj["Type"] as MathType,
        a: obj["A"] as number,
        b: obj["B"] as number,
        channel1Id: obj["Channel1Id"] as string,
        channel2Id: obj["Channel2Id"] != null ? obj["Channel2Id"] as string : null,
        outputChannelId: obj["OutputChannelId"] as string,
        simpleOperationType: obj["SimpleOperationType"] as SimpleOperationType,
    };
}

export function decodeMathDefinitionMessagePack(bytes: Uint8Array): MathDefinition {
    return mathDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function tableDefinitionFromMessagePack(obj: Record<string, unknown>): TableDefinition {
    return {
        id: obj["Id"] as string,
        name: obj["Name"] as string,
        isEnum: obj["IsEnum"] as boolean,
        ignoreCase: obj["IgnoreCase"] as boolean,
        inputChannel: obj["InputChannel"] as string,
        outputChannel: obj["OutputChannel"] as string,
        interpolationType: obj["InterpolationType"] as InterpolationType,
        mappings: (obj["Mappings"] as unknown[]).map(v => tableMappingFromMessagePack(v as Record<string, unknown>)),
    };
}

export function decodeTableDefinitionMessagePack(bytes: Uint8Array): TableDefinition {
    return tableDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function tableMappingFromMessagePack(obj: Record<string, unknown>): TableMapping {
    return {
        input: obj["Input"] as string,
        output: obj["Output"] as string,
    };
}

export function decodeTableMappingMessagePack(bytes: Uint8Array): TableMapping {
    return tableMappingFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function timerDefinitionFromMessagePack(obj: Record<string, unknown>): TimerDefinition {
    return {
        id: obj["Id"] as string,
        name: obj["Name"] as string,
        outputChId: obj["OutputChId"] as string,
        statement: statementDefinitionFromMessagePack(obj["Statement"] as Record<string, unknown>),
        countDown: obj["CountDown"] as boolean,
        enableRollover: obj["EnableRollover"] as boolean,
        rolloverSeconds: obj["RolloverSeconds"] as number,
        enableStartSeconds: obj["EnableStartSeconds"] as boolean,
        startSeconds: obj["StartSeconds"] as number,
        enableStopSeconds: obj["EnableStopSeconds"] as boolean,
        stopSeconds: obj["StopSeconds"] as number,
    };
}

export function decodeTimerDefinitionMessagePack(bytes: Uint8Array): TimerDefinition {
    return timerDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function conditionDefinitionFromMessagePack(obj: Record<string, unknown>): ConditionDefinition {
    return {
        id: obj["Id"] as string,
        name: obj["Name"] as string,
        statements: (obj["Statements"] as unknown[]).map(v => statementDefinitionFromMessagePack(v as Record<string, unknown>)),
        outputChannelId: obj["OutputChannelId"] as string,
    };
}

export function decodeConditionDefinitionMessagePack(bytes: Uint8Array): ConditionDefinition {
    return conditionDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function statementDefinitionFromMessagePack(obj: Record<string, unknown>): StatementDefinition {
    return {
        id: obj["Id"] as string,
        activateComparisons: (obj["ActivateComparisons"] as unknown[]).map(v => (v as unknown[]).map(v => comparisonDefinitionFromMessagePack(v as Record<string, unknown>))),
        deactivateComparisons: obj["DeactivateComparisons"] != null ? (obj["DeactivateComparisons"] as unknown[]).map(v => (v as unknown[]).map(v => comparisonDefinitionFromMessagePack(v as Record<string, unknown>))) : null,
    };
}

export function decodeStatementDefinitionMessagePack(bytes: Uint8Array): StatementDefinition {
    return statementDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function comparisonDefinitionFromMessagePack(obj: Record<string, unknown>): ComparisonDefinition {
    return {
        id: obj["Id"] as string,
        channelId: obj["ChannelId"] as string,
        logic: obj["Logic"] as LogicType,
        useStaticComparison: obj["UseStaticComparison"] as boolean,
        staticValueComparison: obj["StaticValueComparison"] as string,
        channelComparisonId: obj["ChannelComparisonId"] != null ? obj["ChannelComparisonId"] as string : null,
        forMs: obj["ForMs"] as number,
        reverseResult: obj["ReverseResult"] as boolean,
    };
}

export function decodeComparisonDefinitionMessagePack(bytes: Uint8Array): ComparisonDefinition {
    return comparisonDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function carConfigurationSummaryFromMessagePack(obj: Record<string, unknown>): CarConfigurationSummary {
    return {
        id: obj["Id"] as string,
        lastUpdated: new Date(obj["LastUpdated"] as string | number | Date),
        name: obj["Name"] as string,
        car: obj["Car"] as string,
        notes: obj["Notes"] as string,
        configurationSchemaVersion: obj["ConfigurationSchemaVersion"] as number,
        lastUpdatedOnCarTimestamp: obj["LastUpdatedOnCarTimestamp"] != null ? new Date(obj["LastUpdatedOnCarTimestamp"] as string | number | Date) : null,
    };
}

export function decodeCarConfigurationSummaryMessagePack(bytes: Uint8Array): CarConfigurationSummary {
    return carConfigurationSummaryFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function alarmDefinitionFromMessagePack(obj: Record<string, unknown>): AlarmDefinition {
    return {
        id: obj["Id"] as string,
        name: obj["Name"] as string,
        statement: statementDefinitionFromMessagePack(obj["Statement"] as Record<string, unknown>),
        messsage: obj["Messsage"] as string,
        displayChannelSourceColorHex: obj["DisplayChannelSourceColorHex"] as string,
        timeAfterAckToDisplaySecs: obj["TimeAfterAckToDisplaySecs"] as number,
        alarmStatusChannelId: obj["AlarmStatusChannelId"] != null ? obj["AlarmStatusChannelId"] as string : null,
    };
}

export function decodeAlarmDefinitionMessagePack(bytes: Uint8Array): AlarmDefinition {
    return alarmDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function enumDefinitionFromMessagePack(obj: Record<string, unknown>): EnumDefinition {
    return {
        id: obj["Id"] as string,
        name: obj["Name"] as string,
        values: (obj["Values"] as unknown[]).map(v => enumValueDefinitionFromMessagePack(v as Record<string, unknown>)),
    };
}

export function decodeEnumDefinitionMessagePack(bytes: Uint8Array): EnumDefinition {
    return enumDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function enumValueDefinitionFromMessagePack(obj: Record<string, unknown>): EnumValueDefinition {
    return {
        source: obj["Source"] as string,
        value: obj["Value"] as number,
    };
}

export function decodeEnumValueDefinitionMessagePack(bytes: Uint8Array): EnumValueDefinition {
    return enumValueDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function loggingDefinitionFromMessagePack(obj: Record<string, unknown>): LoggingDefinition {
    return {
        id: obj["Id"] as string,
        channelId: obj["ChannelId"] as string,
        frequency: obj["Frequency"] as LoggingFrequency,
    };
}

export function decodeLoggingDefinitionMessagePack(bytes: Uint8Array): LoggingDefinition {
    return loggingDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function carFuelConfigFromMessagePack(obj: Record<string, unknown>): CarFuelConfig {
    return {
        isEnabled: obj["IsEnabled"] as boolean,
        tankCapacityGallons: obj["TankCapacityGallons"] as number,
        defaultConsumptionGalPerMin: obj["DefaultConsumptionGalPerMin"] as number,
        defaultYellowConsumptionMultiplier: obj["DefaultYellowConsumptionMultiplier"] as number,
        defaultCode35ConsumptionMultiplier: obj["DefaultCode35ConsumptionMultiplier"] as number,
        throttleConsumption: throttleConsumptionConfigFromMessagePack(obj["ThrottleConsumption"] as Record<string, unknown>),
    };
}

export function decodeCarFuelConfigMessagePack(bytes: Uint8Array): CarFuelConfig {
    return carFuelConfigFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function throttleConsumptionConfigFromMessagePack(obj: Record<string, unknown>): ThrottleConsumptionConfig {
    return {
        isEnabled: obj["IsEnabled"] as boolean,
        maxRpm: obj["MaxRpm"] as number,
    };
}

export function decodeThrottleConsumptionConfigMessagePack(bytes: Uint8Array): ThrottleConsumptionConfig {
    return throttleConsumptionConfigFromMessagePack(decode(bytes) as Record<string, unknown>);
}

