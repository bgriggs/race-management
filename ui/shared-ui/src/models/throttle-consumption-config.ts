/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface ThrottleConsumptionConfig {
    isEnabled: boolean;
    maxRpm: number;
    /**
     * Source channel for the throttle-position signal (default: ThrottlePosition reserved channel).
     */
    throttlePositionChannelId: string;
    /**
     * Source channel for engine RPM (default: EngineRPM reserved channel).
     */
    engineRpmChannelId: string;
}
