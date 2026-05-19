/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { decode, encode } from "@msgpack/msgpack";
import { CarChannelSnapshot } from "./car-channel-snapshot";
import { ChannelValueSnapshot } from "./channel-value-snapshot";
import { ChannelChangeNotification } from "./channel-change-notification";

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

