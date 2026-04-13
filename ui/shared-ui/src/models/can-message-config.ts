/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CanChannelAssignmentConfig } from "./can-channel-assignment-config";

export interface CanMessageConfig {
    isEnabled: boolean;
    canId: number;
    canBusId: number;
    isExtended: boolean;
    length: number;
    isBigEndian: boolean;
    isReceive: boolean;
    transmitRate: string;
    channelAssignments: CanChannelAssignmentConfig[];
}
