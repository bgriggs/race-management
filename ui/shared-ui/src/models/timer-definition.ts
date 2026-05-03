/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { StatementDefinition } from "./statement-definition";

export interface TimerDefinition {
    id: string;
    name: string;
    outputChId: string;
    statement: StatementDefinition;
    countDown: boolean;
    enableRollover: boolean;
    rolloverSeconds: number;
    enableStartSeconds: boolean;
    startSeconds: number;
    enableStopSeconds: boolean;
    stopSeconds: number;
}
