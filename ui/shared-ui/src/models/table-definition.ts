/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { InterpolationType } from "./interpolation-type";
import { TableMapping } from "./table-mapping";

export interface TableDefinition {
    id: string;
    name: string;
    isEnum: boolean;
    ignoreCase: boolean;
    inputChannel: string;
    outputChannel: string;
    interpolationType: InterpolationType;
    mappings: TableMapping[];
}
