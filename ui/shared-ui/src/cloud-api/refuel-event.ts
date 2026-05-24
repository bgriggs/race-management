/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { RefuelConfidenceTier } from "./refuel-confidence-tier";
import { RefuelSource } from "./refuel-source";
import { EcuResetState } from "./ecu-reset-state";

export interface RefuelEvent {
    id: number;
    teamId: number;
    carNumber: string;
    raceId: number;
    detectedAt: Date;
    enteredFuelGallons: number | null;
    enteredAt: Date | null;
    confidenceTier: RefuelConfidenceTier;
    anchorFlags: number;
    source: RefuelSource;
    ecuResetState: EcuResetState;
    status: string;
}
