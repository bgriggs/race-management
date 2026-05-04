/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

/**
 * Associates a Channel with a specific offset and length in a CAN message.
 */
export interface CanChannelAssignmentConfig {
    id: string;
    offset: number;
    length: number;
    mask: number;
    isSigned: boolean;
    formulaMultiplier: number;
    formulaDivider: number;
    formulaConst: number;
}
