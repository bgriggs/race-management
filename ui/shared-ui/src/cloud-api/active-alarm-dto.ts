/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface ActiveAlarmDto {
    teamId: number;
    carNumber: string;
    alarmDefinitionId: string;
    name: string;
    message: string;
    displayChannelSourceColorHex: string;
    timeAfterAckToDisplaySecs: number;
    isActive: boolean;
    isAcknowledged: boolean;
    lastActivatedAt: Date | null;
    lastAcknowledgedTimestamp: Date | null;
}
