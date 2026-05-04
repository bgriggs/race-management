/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { LogicType } from "./logic-type";

export interface ComparisonDefinition {
    id: string;
    channelId: string;
    logic: LogicType;
    useStaticComparison: boolean;
    staticValueComparison: string;
    /**
     * When not using static comparison, this is the channel to compare to. The value of the channel specified by ChannelId will be compared to the value of the channel specified by ChannelComparisonId.
     */
    channelComparisonId: string | null;
    /**
     * Amount of time in milliseconds the statement must be true before the statement is considered true when using the 'on for' clause.
     */
    forMs: number;
    reverseResult: boolean;
}
