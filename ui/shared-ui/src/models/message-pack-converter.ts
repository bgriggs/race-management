/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { decode, encode } from "@msgpack/msgpack";
import { CarConfiguration } from "./car-configuration";
import { CanMessageConfig } from "./can-message-config";
import { CanBusConfig } from "./can-bus-config";
import { CanChannelAssignmentConfig } from "./can-channel-assignment-config";
import { ChannelDefinition } from "./channel-definition";
import { CounterDefinition } from "./counter-definition";
import { MathDefinition } from "./math-definition";
import { TableDefinition } from "./table-definition";
import { TimerDefinition } from "./timer-definition";
import { ConditionDefinition } from "./condition-definition";
import { StatementDefinition } from "./statement-definition";
import { ComparisonDefinition } from "./comparison-definition";
import { CarConfigurationSummary } from "./car-configuration-summary";
import { InterpolationType } from "./interpolation-type";
import { LogicType } from "./logic-type";
import { MathType } from "./math-type";
import { SimpleOperationType } from "./simple-operation-type";

export function decodeMessagePack<T>(bytes: Uint8Array): T {
    return decode(bytes) as T;
}

export function encodeMessagePack<T>(value: T): Uint8Array {
    return encode(value);
}

export function carConfigurationFromMessagePack(obj: Record<string, unknown>): CarConfiguration {
    return {
        configurationId: obj["ConfigurationId"] as string,
        configurationSchemaVersion: obj["ConfigurationSchemaVersion"] as number,
        name: obj["Name"] as string,
        notes: obj["Notes"] as string,
        lastUpdated: new Date(obj["LastUpdated"] as string | number | Date),
        car: obj["Car"] as string,
        clientId: obj["ClientId"] as string,
        clientSecret: obj["ClientSecret"] as string,
        canConfig: canMessageConfigFromMessagePack(obj["CanConfig"] as Record<string, unknown>),
        channelDefinitions: (obj["ChannelDefinitions"] as unknown[]).map(v => channelDefinitionFromMessagePack(v as Record<string, unknown>)),
        counterDefinitions: (obj["CounterDefinitions"] as unknown[]).map(v => counterDefinitionFromMessagePack(v as Record<string, unknown>)),
        mathDefinitions: (obj["MathDefinitions"] as unknown[]).map(v => mathDefinitionFromMessagePack(v as Record<string, unknown>)),
        tableMappings: (obj["TableMappings"] as unknown[]).map(v => tableDefinitionFromMessagePack(v as Record<string, unknown>)),
        timerDefinitions: (obj["TimerDefinitions"] as unknown[]).map(v => timerDefinitionFromMessagePack(v as Record<string, unknown>)),
        userConditions: (obj["UserConditions"] as unknown[]).map(v => conditionDefinitionFromMessagePack(v as Record<string, unknown>)),
    };
}

export function decodeCarConfigurationMessagePack(bytes: Uint8Array): CarConfiguration {
    return carConfigurationFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function canMessageConfigFromMessagePack(obj: Record<string, unknown>): CanMessageConfig {
    return {
        isEnabled: obj["IsEnabled"] as boolean,
        canId: obj["CanId"] as number,
        canBusId: obj["CanBusId"] as number,
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
        interfaceName: obj["InterfaceName"] as string,
        bitRate: obj["BitRate"] as number,
        silentOnCanBus: obj["SilentOnCanBus"] as boolean,
        messages: (obj["Messages"] as unknown[]).map(v => canMessageConfigFromMessagePack(v as Record<string, unknown>)),
    };
}

export function decodeCanBusConfigMessagePack(bytes: Uint8Array): CanBusConfig {
    return canBusConfigFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function canChannelAssignmentConfigFromMessagePack(obj: Record<string, unknown>): CanChannelAssignmentConfig {
    return {
        id: obj["Id"] as number,
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
        isStringValue: obj["IsStringValue"] as boolean,
        baseUnitType: obj["BaseUnitType"] as string,
        baseDecimalPlaces: obj["BaseDecimalPlaces"] as number,
        outputUnitType: obj["OutputUnitType"] as string,
        outputDecimalPlaces: obj["OutputDecimalPlaces"] as number,
    };
}

export function decodeChannelDefinitionMessagePack(bytes: Uint8Array): ChannelDefinition {
    return channelDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function counterDefinitionFromMessagePack(obj: Record<string, unknown>): CounterDefinition {
    return {
        id: obj["Id"] as string,
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
        order: obj["Order"] as number,
        type: obj["Type"] as MathType,
        a: obj["A"] as number,
        b: obj["B"] as number,
        channel1Id: obj["Channel1Id"] as string,
        channel2Id: obj["Channel2Id"] as string,
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
        mapping: (obj["Mapping"] as unknown[]).map(v => v as [string, string]),
        inputPoints: (obj["InputPoints"] as unknown[]).map(v => v as number),
        outputValues: (obj["OutputValues"] as unknown[]).map(v => v as number),
    };
}

export function decodeTableDefinitionMessagePack(bytes: Uint8Array): TableDefinition {
    return tableDefinitionFromMessagePack(decode(bytes) as Record<string, unknown>);
}

export function timerDefinitionFromMessagePack(obj: Record<string, unknown>): TimerDefinition {
    return {
        id: obj["Id"] as string,
        outputChId: obj["OutputChId"] as string,
        startStatementId: obj["StartStatementId"] as string,
        stopStatementId: obj["StopStatementId"] as string,
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
        notes: obj["Notes"] as string,
        configurationSchemaVersion: obj["ConfigurationSchemaVersion"] as number,
    };
}

export function decodeCarConfigurationSummaryMessagePack(bytes: Uint8Array): CarConfigurationSummary {
    return carConfigurationSummaryFromMessagePack(decode(bytes) as Record<string, unknown>);
}

