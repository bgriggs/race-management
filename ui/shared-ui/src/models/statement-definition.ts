/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { ComparisonDefinition } from "./comparison-definition";

export interface StatementDefinition {
    id: string;
    activateComparisons: ComparisonDefinition[][];
    deactivateComparisons: ComparisonDefinition[][] | null;
}
