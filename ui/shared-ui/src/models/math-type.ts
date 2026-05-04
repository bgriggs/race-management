/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

/**
 * Types of supported mathematical calculations.
 */
export enum MathType {
    /**
     * Output = CH1 / (CH1 + CH2)
     */
    Bias = 0,
    /**
     * Output = (A * CH1) + B
     */
    LinearCorrector = 1,
    /**
     * Output = CH1 + CH2
     */
    SimpleOperation = 2,
    /**
     * Output = (int)(CH1 / A)
     */
    DivisionInteger = 3,
    /**
     * Output = CH1 % A
     */
    DivisionModulo = 4,
}
