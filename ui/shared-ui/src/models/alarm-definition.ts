/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { StatementDefinition } from "./statement-definition";

/**
 * Model for an alarm definition, which includes the statements that control when the alarm is active, as well as optional display and output settings.
 */
export interface AlarmDefinition {
    /**
     * Gets or sets the unique identifier.
     */
    id: string;
    /**
     * Gets or sets the name. Maximum length is 20 characters.
     */
    name: string;
    /**
     * Statement that controls alarm activation state. The statement can define activate/deactivate comparisons.
     */
    statement: StatementDefinition;
    /**
     * Optional message to display.
     */
    messsage: string;
    /**
     * Optional color to make the source channel value on displays, like the dashboard.
     */
    displayChannelSourceColorHex: string;
    /**
     * Specifies the number of seconds to wait before displaying alarm again after it has been acknowledged.
     */
    timeAfterAckToDisplaySecs: number;
    /**
     * Optional output channel to write the alarm status to as 0 or 1. This allows for the alarm status to be used in other logic, such as to disable other alarms when this alarm is active.
     */
    alarmStatusChannelId: string | null;
}
