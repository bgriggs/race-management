/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { AlarmEventType } from "./alarm-event-type";

export interface AlarmChangeNotification {
    teamId: number;
    carNumber: string;
    alarmDefinitionId: string;
    eventType: AlarmEventType;
    isActive: boolean;
    isAcknowledged: boolean;
    timestamp: Date;
}
