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
    channelComparisonId: string | null;
    forMs: number;
    reverseResult: boolean;
}
