/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CalibrationFactorSource } from "./calibration-factor-source";

export interface CalibrationFactor {
    id: number;
    teamId: number;
    carNumber: string;
    value: number;
    source: CalibrationFactorSource;
    effectiveAt: Date;
    raceId: number | null;
}
