# ADR-0004: ECU `TripFuel` is the primary fuel ground truth; PitFill cross-checks within its noise band

**Status:** Accepted  
**Date:** 2026-05-23

## Context

The Fuel Reconciler (ADR-0003) runs three concurrent estimators. The reconciler's outlier detection and the FlowMeter's calibration-factor learning both need a designated ground-truth signal to compare against. There are two plausible candidates:

- **ECU `TripFuel`** — calculated by the engine ECU from injector pulse width × characterized injector flow rate. Precise (typically within a few percent of true) when the driver has reset it at the last refuel and the channel is currently emitting. Operationally fragile: the reset is a manual driver action and is sometimes forgotten; the channel sometimes simply stops reporting mid-stint on some hardware.
- **Manually entered pit-fill volumes (PitFill)** — physically anchored (the actual fuel that went into the tank) but bounded by ±1 gallon noise from eyeballing graduated fuel jugs and dependent on the engineer remembering to enter the value.

The intuitive choice is PitFill — it is a real-world measurement, not a sensor inference. The trade-off is that PitFill's ±1 gal jug-reading noise often exceeds ECU's intrinsic error in absolute terms.

A separate but related question: does the FlowMeter's learned **Calibration Factor** reset at session boundaries or persist across sessions and race events?

## Decision

- **ECU `TripFuel` is the primary fuel ground truth** when it is valid — i.e., when the most recent Refuel Event was followed by a `TripFuel` drop to near zero ("Reset-Confirmed") and the channel is currently reporting fresh values.
- **PitFill is the cross-check, not the authority.** PitFill confirms ECU when their values agree within combined uncertainty (PitFill's ±1 gal + ECU's intrinsic error). PitFill only displaces ECU as the ground-truth source when the two disagree beyond combined uncertainty — at which point ECU is flagged suspect.
- **The FlowMeter Calibration Factor is calibrated against ECU**, not against PitFill, for the same precision reason.
- **The Calibration Factor and the per-car learned `DefaultConsumptionGalPerMin` persist across sessions and race events.** They are properties of the car and its sensors, not of any specific session.

## Rationale

- **Precision over physical anchoring.** ECU's intrinsic error is typically 2-3%; PitFill's ±1 gal noise on a 15-gallon stint is ~7%. Calibrating the flow meter against PitFill's noise floor would propagate that noise into every flow meter reading. Calibrating against ECU produces a tighter learned factor.
- **Reset detection is workable.** The `FuelFull` channel anchors a "waiting for ECU reset" window after each Refuel Event; if `TripFuel` drops to near zero in the window, ECU is Reset-Confirmed. If `TripFuel` does not drop, ECU is marked Unreset and the ECU Estimator is unavailable for that FuelWindow. This makes "ECU as primary" robust to the well-known forgot-to-reset failure mode — it is detected and degrades gracefully.
- **PitFill's role is unchanged.** It is still the always-on baseline that survives telemetry disconnects (it depends only on manual entries and wall-clock time). The decision is about the *ground-truth* role specifically, not about which estimators are run.
- **Calibration persistence reflects sensor reality.** The flow meter's pulses-per-gallon calibration does not change because the car moved from Friday practice to Sunday race. Resetting calibration at each session boundary would force the system to re-learn from scratch every session, defeating the purpose of learning at all. Persistence is paired with an engineer-accessible "Override" / "Reset" control (ADR-0003 detail panel) so unusual situations (new flow meter, fuel-type change) can be handled explicitly.

## Consequences

- **The system depends on a reliable Refuel Event detection path** (multi-anchor: `FuelFull` channel with stint-age and InPit/GPSSpeed guards, `FuelLevel` rise at rest, or timing-system pit lap with a 20-minute floor). Without reliable Refuel Event detection, ECU reset detection fails and ECU cannot be promoted to primary — the system falls back to PitFill and FlowMeter as the only sources.
- **`CalibrationFactors` is an insert-only audit table with a `Source` enum** (`learned`, `manual_override`, `reset`) — the current factor is the most recent row per car, and the full history is preserved for retroactive analysis. Engineer overrides block automatic learning until explicitly resumed.
- **The FlowMeter Estimator emits two ranges: raw and corrected.** The raw value is the uncorrected flow-meter reading; the corrected value applies the current Calibration Factor. Until at least one FuelWindow with a Reset-Confirmed ECU has closed, no calibration data exists and the corrected sub-output is marked unavailable.
- **Engineer overrides snap the EMA seed** — pressing "Override" sets the factor to a manually entered value; pressing "Resume learning" restarts the EMA from the override value (not from a background-tracked phantom). This respects the engineer's most-recent judgment as the new prior.
