/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { RangeReadout } from "./range-readout";
import { EstimatorReadout } from "./estimator-readout";
import { ReconcilerDetails } from "./reconciler-details";
import { FuelWindowDetails } from "./fuel-window-details";

export interface FuelRangeSnapshot {
    teamId: number;
    carNumber: string;
    raceId: number;
    asOfUtc: Date;
    primary: RangeReadout;
    highConfidence: RangeReadout;
    highConfidenceThreshold: number;
    estimators: EstimatorReadout[];
    reconciler: ReconcilerDetails;
    fuelWindow: FuelWindowDetails;
}
