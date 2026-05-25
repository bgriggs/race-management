/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { ComparisonDefinition } from "./comparison-definition";

/**
 * Collection of logic comparisons that together form a statement. The statement is true when any of comparisons is true.
 */
export interface StatementDefinition {
    id: string;
    /**
     * Rows of comparisons that activate the statement when any comparison is true. This is a list of a list to support grouping comparisons together with AND logic.
     */
    activateComparisons: ComparisonDefinition[][];
    /**
     * Rows of comparisons that deactivate the statement when any comparison is true. This is a list of a list to support grouping comparisons together with AND logic.
     * When null, the ActivateComparisons will result in deactivation when false. When not null, the ActivateComparisons will only result in activation when true, and the 
     * DeactivateComparisons will only result in deactivation when true. 
     * This allows for more complex logic where a statement can be activated by one set of comparisons and deactivated by another set of comparisons.
     */
    deactivateComparisons: ComparisonDefinition[][] | null;
}
