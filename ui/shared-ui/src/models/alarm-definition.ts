/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { StatementDefinition } from "./statement-definition";

export interface AlarmDefinition {
    /**
     * Specifies the number of seconds to wait before displaying alarm again after it has been acknowledged.
     */
    timeAfterAckToDisplaySecs: number;
    id: string;
    /**
     * Statements that control alarm activation state. Each statement can define activate/deactivate comparisons.
     */
    statements: StatementDefinition[];
    /**
     * Optional message to display.
     */
    messsage: string;
    /**
     * Optional color to make the source channel value on displays, like the dashboard.
     */
    displayChannelSourceColorHex: string;
    /**
     * Optional output channel to write the alarm status to as 0 or 1. This allows for the alarm status to be used in other logic, such as to disable other alarms when this alarm is active.
     */
    alarmStatusChannelId: string | null;
}
