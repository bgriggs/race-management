/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CanChannelAssignmentConfig } from "./can-channel-assignment-config";

/**
 * Represents a CAN packet with a unique 11 or 29 bit identifier.
 */
export interface CanMessageConfig {
    isEnabled: boolean;
    /**
     * 11 or 29 bit identifier.
     */
    canId: number;
    /**
     * True if the message is an extended message 29-bit.
     */
    isExtended: boolean;
    /**
     * 1-8 bytes.
     */
    length: number;
    isBigEndian: boolean;
    isReceive: boolean;
    transmitRate: string;
    channelAssignments: CanChannelAssignmentConfig[];
}
