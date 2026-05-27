/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { decode, encode } from "@msgpack/msgpack";
import { CarChannelSnapshot } from "./car-channel-snapshot";
import { ChannelValueSnapshot } from "./channel-value-snapshot";
import { ChannelChangeNotification } from "./channel-change-notification";
import { AlarmChangeNotification } from "./alarm-change-notification";
import { RaceStateDto } from "./race-state-dto";
import { AlarmEventType } from "./alarm-event-type";

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

export function carChannelSnapshotFromMessagePack(arr: unknown[]): CarChannelSnapshot {
    return {
        carKey: arr[0] as string,
        carNumber: arr[1] as string,
        channels: convertDictionary(arr[2], v => channelValueSnapshotFromMessagePack(v as unknown[])),
        configurationId: arr[3] != null ? arr[3] as string : null,
    };
}

export function decodeCarChannelSnapshotMessagePack(bytes: Uint8Array): CarChannelSnapshot {
    return carChannelSnapshotFromMessagePack(decode(bytes) as unknown[]);
}

export function channelValueSnapshotFromMessagePack(arr: unknown[]): ChannelValueSnapshot {
    return {
        value: arr[0] as string,
        timestamp: new Date(arr[1] as string | number | Date),
    };
}

export function decodeChannelValueSnapshotMessagePack(bytes: Uint8Array): ChannelValueSnapshot {
    return channelValueSnapshotFromMessagePack(decode(bytes) as unknown[]);
}

export function channelChangeNotificationFromMessagePack(arr: unknown[]): ChannelChangeNotification {
    return {
        sessionIndex: arr[0] as number,
        value: arr[1] as string,
        timestamp: new Date(arr[2] as string | number | Date),
    };
}

export function decodeChannelChangeNotificationMessagePack(bytes: Uint8Array): ChannelChangeNotification {
    return channelChangeNotificationFromMessagePack(decode(bytes) as unknown[]);
}

export function alarmChangeNotificationFromMessagePack(arr: unknown[]): AlarmChangeNotification {
    return {
        teamId: arr[0] as number,
        carNumber: arr[1] as string,
        alarmDefinitionId: arr[2] as string,
        eventType: arr[3] as AlarmEventType,
        isActive: arr[4] as boolean,
        isAcknowledged: arr[5] as boolean,
        timestamp: new Date(arr[6] as string | number | Date),
    };
}

export function decodeAlarmChangeNotificationMessagePack(bytes: Uint8Array): AlarmChangeNotification {
    return alarmChangeNotificationFromMessagePack(decode(bytes) as unknown[]);
}

export function raceStateDtoFromMessagePack(obj: Record<string, unknown>): RaceStateDto {
    return {
        eventId: obj["EventId"] != null ? obj["EventId"] as number : null,
        localTimeOfDay: obj["LocalTimeOfDay"] != null ? obj["LocalTimeOfDay"] as string : null,
        runningRaceTime: obj["RunningRaceTime"] != null ? obj["RunningRaceTime"] as string : null,
        timeToGo: obj["TimeToGo"] != null ? obj["TimeToGo"] as string : null,
        leaderLap: obj["LeaderLap"] != null ? obj["LeaderLap"] as number : null,
        flag: obj["Flag"] != null ? obj["Flag"] as string : null,
        lastUpdatedUtc: new Date(obj["LastUpdatedUtc"] as string | number | Date),
    };
}

export function decodeRaceStateDtoMessagePack(bytes: Uint8Array): RaceStateDto {
    return raceStateDtoFromMessagePack(decode(bytes) as Record<string, unknown>);
}

