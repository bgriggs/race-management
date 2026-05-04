/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { StatementDefinition } from "./statement-definition";

/**
 * User defined set of logic statement from channels that output to another channel as 0 or 1.
 */
export interface ConditionDefinition {
    id: string;
    name: string;
    statements: StatementDefinition[];
    /**
     * Gets or sets the identifier of the output channel associated with this instance. This will be 0 or 1.
     * Use @see {@link System.Guid.Empty} when no output channel is needed.
     */
    outputChannelId: string;
}
