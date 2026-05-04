/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface CounterDefinition {
    id: string;
    name: string;
    outputChId: string;
    /**
     * Channel ID that will increment the counter when its value changes from 0 to non-zero. The counter will only increment on the rising edge of the signal, so it will not increment again until the signal goes back to 0 and then rises again.
     * Use @see {@link System.Guid.Empty} to leave this input unconfigured.
     */
    upChId: string;
    /**
     * Channel ID that will decrement the counter on its rising edge, similar to the UpChId but for decrementing. The counter will only decrement on the rising edge of the signal, so it will not decrement again until the signal goes back to 0 and then rises again.
     * Use @see {@link System.Guid.Empty} to leave this input unconfigured.
     */
    downChId: string;
    /**
     * Channel ID that will reset the counter to the StartValue on its rising edge, similar to the UpChId but for resetting. The counter will only reset on the rising edge of the signal, so it will not reset again until the signal goes back to 0 and then rises again.
     * Use @see {@link System.Guid.Empty} to leave this input unconfigured.
     */
    resetChId: string;
    /**
     * Gets or sets the maximum allowable value.
     */
    maxValue: number;
    /**
     * Gets or sets the minimum allowed value.
     */
    minValue: number;
    /**
     * When the counter value is at the specified minimum or maximum value, an increment from the maximum value will set the counter to the specified minimum value, and a decrement from the minimum value will set the counter to the specified maximum value.
     */
    rollAtLimit: boolean;
    /**
     * Gets or sets the initial value of the counter.
     */
    startValue: number;
    /**
     * Whether to save value across restarts. If false, the counter will be reset to StartValue on each restart. If true, the counter value will be persisted and restored on restart.
     */
    persistValue: boolean;
}
