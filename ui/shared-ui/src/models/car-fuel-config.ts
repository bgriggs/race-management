/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { ThrottleConsumptionConfig } from "./throttle-consumption-config";

export interface CarFuelConfig {
    isEnabled: boolean;
    tankCapacityGallons: number;
    defaultConsumptionGalPerMin: number;
    defaultYellowConsumptionMultiplier: number;
    defaultCode35ConsumptionMultiplier: number;
    /**
     * Source channel for tank fuel level (default: FuelLevel reserved channel). Used by the FuelReconciler's tank-level estimator.
     */
    fuelLevelChannelId: string;
    /**
     * Source channel for trip fuel (default: TripFuel reserved channel).
     */
    tripFuelChannelId: string;
    /**
     * Source channel for cumulative fuel used (default: FuelUsed reserved channel).
     */
    fuelUsedChannelId: string;
    /**
     * Source channel for the fuel-full reset signal (default: FuelFull reserved channel).
     */
    fuelFullChannelId: string;
    /**
     * Optional source channel for the pit-lane indicator (default: InPit reserved channel; null disables pit gating in the throttle proxy).
     */
    inPitChannelId: string | null;
    throttleConsumption: ThrottleConsumptionConfig;
}
