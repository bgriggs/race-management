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
    throttleConsumption: ThrottleConsumptionConfig;
}
