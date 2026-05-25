/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { StatementDefinition } from "./statement-definition";

export interface AlarmDefinitionDto {
    id: string;
    teamId: number;
    carNumber: string | null;
    name: string;
    message: string;
    displayChannelSourceColorHex: string;
    timeAfterAckToDisplaySecs: number;
    alarmStatusChannelId: string | null;
    statement: StatementDefinition;
}
