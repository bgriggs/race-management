/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { MathType } from "./math-type";
import { SimpleOperationType } from "./simple-operation-type";

export interface MathDefinition {
    id: string;
    order: number;
    type: MathType;
    a: number;
    b: number;
    channel1Id: string;
    channel2Id: string;
    outputChannelId: string;
    simpleOperationType: SimpleOperationType;
}
