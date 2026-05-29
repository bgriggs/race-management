using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;

namespace WebApi.FuelAnalysis;

/// <summary>
/// Engineer-initiated Stint CRUD with FuelWindow / RefuelEvent cascades. Lives outside
/// <see cref="WebApi.Controllers.FuelController"/> so the cascade rules can be tested
/// independently of the controller's auth/validation surface.
/// </summary>
/// <remarks>
/// Telemetry-driven Stint changes (pit-in/pit-out, session start/end) flow through
/// <c>ChannelProcessor.FuelAnalysis</c> — NOT through this editor. This editor is for
/// the engineer-facing "Add Stint" / "Edit Stint" / "Delete Stint" UI flows only.
/// </remarks>
public static class StintEditor
{
    public sealed record CreateResult(Stint Stint, FuelWindow Window, RefuelEvent Refuel);

    public enum CreateError { CarNotFound, RaceNotFound, EndBeforeStart }
    public enum UpdateError { StintNotFound, EndBeforeStart, NoPriorWindowToMerge, NoFollowingWindowToSplitFrom }
    public enum DeleteError { StintNotFound, OwnsFirstWindowAndHasFollowers }

    public sealed record CreateOutcome(CreateResult? Success, CreateError? Error);
    public sealed record UpdateOutcome(Stint? Success, UpdateError? Error);
    public sealed record DeleteOutcome(bool Success, DeleteError? Error);

    /// <summary>
    /// Creates a Stint, plus a new pending RefuelEvent and a new FuelWindow opened at
    /// <paramref name="startAt"/>. If an open FuelWindow exists for the car/race at
    /// creation time, it is closed at <paramref name="startAt"/> with the new RefuelEvent
    /// as its <c>EndRefuelEventId</c>, and any open Stint inside it is closed at the same
    /// instant. The new Stint's <see cref="Stint.OriginType"/> is taken from the caller.
    /// </summary>
    public static async Task<CreateOutcome> CreateAsync(
        RaceManagementContext db,
        int teamId,
        string carNumber,
        int raceId,
        DateTime startAt,
        DateTime? endAt,
        StintOriginType originType,
        CancellationToken ct)
    {
        if (endAt is DateTime e && e <= startAt)
        {
            return new CreateOutcome(null, CreateError.EndBeforeStart);
        }

        var raceExists = await db.Races.AnyAsync(r => r.Id == raceId && r.TeamId == teamId, ct);
        if (!raceExists) return new CreateOutcome(null, CreateError.RaceNotFound);

        var carExists = await db.Cars.AnyAsync(c => c.TeamId == teamId && c.Number == carNumber, ct);
        if (!carExists) return new CreateOutcome(null, CreateError.CarNotFound);

        var refuel = new RefuelEvent
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            DetectedAt = startAt,
            EnteredFuelGallons = null,
            ConfidenceTier = RefuelConfidenceTier.Manual,
            Source = RefuelSource.Manual,
            EcuResetState = EcuResetState.Unknown,
            Status = "Pending",
        };
        db.RefuelEvents.Add(refuel);
        await db.SaveChangesAsync(ct);

        // Close the prior open window (if any) at startAt before creating the new one —
        // the partial unique index forbids two open FuelWindows per (team, car, race).
        var openPriorWindow = await db.FuelWindows
            .FirstOrDefaultAsync(w => w.TeamId == teamId && w.CarNumber == carNumber && w.RaceId == raceId && w.ClosedAt == null, ct);
        if (openPriorWindow is not null && openPriorWindow.OpenedAt < startAt)
        {
            openPriorWindow.ClosedAt = startAt;
            openPriorWindow.EndRefuelEventId = refuel.Id;

            // Clamp any stints in the prior window that are still open (telemetry style)
            // or that project past the new stint's StartAt (engineer-managed Auto follow-on
            // with projected EndAt). Without the projection clamp the gantt would show the
            // Auto bar overlapping the new manual stint.
            var trailing = await db.Stints
                .Where(s => s.FuelWindowId == openPriorWindow.Id
                         && (s.EndAt == null || s.EndAt > startAt))
                .ToListAsync(ct);
            foreach (var s in trailing)
            {
                s.EndAt = startAt;
            }
        }

        var window = new FuelWindow
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            StartRefuelEventId = refuel.Id,
            OpenedAt = startAt,
        };
        db.FuelWindows.Add(window);
        await db.SaveChangesAsync(ct);

        var stint = new Stint
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            FuelWindowId = window.Id,
            StartAt = startAt,
            EndAt = endAt,
            OriginType = originType,
        };
        db.Stints.Add(stint);
        await db.SaveChangesAsync(ct);

        // Eager Auto follow-on is only created for the FIRST stint of the race — gives
        // the engineer a "next stint will start here" placeholder right at race setup.
        // For subsequent stints we don't create the follow-on at creation; an idle gantt
        // shouldn't show speculative bars for every planned stint. Lazy creation kicks
        // in via EnsureLatestStintFollowOnIfEndedAsync (called from LoadStintsAsync) once
        // the current stint's expected EndAt actually elapses in real time.
        if (endAt is DateTime endTime && await IsOnlyStintAsync(db, teamId, carNumber, raceId, stint.Id, ct))
        {
            await EnsureFollowOnAutoStintAsync(db, teamId, carNumber, raceId, stint, window.Id, endTime, ct);
        }

        return new CreateOutcome(new CreateResult(stint, window, refuel), null);
    }

    private static async Task<bool> IsOnlyStintAsync(
        RaceManagementContext db, int teamId, string carNumber, int raceId, int stintId, CancellationToken ct)
    {
        return !await db.Stints.AnyAsync(
            s => s.TeamId == teamId && s.CarNumber == carNumber && s.RaceId == raceId && s.Id != stintId, ct);
    }

    /// <summary>
    /// Lazy companion to the eager first-stint rule in CreateAsync/UpdateAsync. When the
    /// chronologically-latest stint's EndAt has elapsed and no Auto placeholder has been
    /// created yet, open one at the latest stint's EndAt — this is what surfaces the
    /// "next stint" bar in the gantt the moment the current stint actually ends, without
    /// littering the chart with speculative bars for stints the engineer planned ahead.
    /// Designed to be called from the stints read endpoint (LoadStintsAsync) — idempotent
    /// after the first call, only emits one DB write per stint-end transition.
    /// </summary>
    public static async Task EnsureLatestStintFollowOnIfEndedAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        DateTime nowInRaceTz,
        CancellationToken ct)
    {
        var latest = await db.Stints
            .Where(s => s.TeamId == teamId && s.CarNumber == carNumber && s.RaceId == raceId)
            .OrderByDescending(s => s.StartAt)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return;

        // The latest stint is already the open Auto placeholder — nothing to add.
        if (latest.OriginType == StintOriginType.Auto && latest.EndAt is null) return;

        // Stint still active (no EndAt set, or EndAt in the future) — too early.
        if (latest.EndAt is not DateTime end || end >= nowInRaceTz) return;

        await CreateAutoFollowOnAsync(db, teamId, carNumber, raceId, end, ct);
    }

    /// <summary>
    /// Ensures there is an Auto Stint immediately following <paramref name="priorStint"/>,
    /// anchored at <paramref name="endTime"/>:
    /// <list type="bullet">
    ///   <item>If the next stint (by StartAt) is already an Auto follow-on (Auto + sitting
    ///   in a different FuelWindow than the prior, i.e. it owns its own window), sync its
    ///   StartAt and projected EndAt to track the prior stint's new EndAt.</item>
    ///   <item>If no later stint exists, create a new Auto follow-on via
    ///   <see cref="CreateAutoFollowOnAsync"/>.</item>
    ///   <item>Otherwise (next stint is Manual or a closed Auto) leave it alone — the
    ///   engineer has explicitly placed something there.</item>
    /// </list>
    /// </summary>
    private static async Task EnsureFollowOnAutoStintAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        Stint priorStint,
        int priorWindowId,
        DateTime endTime,
        CancellationToken ct)
    {
        var nextStint = await db.Stints
            .Where(s => s.TeamId == teamId
                     && s.CarNumber == carNumber
                     && s.RaceId == raceId
                     && s.Id != priorStint.Id
                     && s.StartAt >= priorStint.StartAt)
            .OrderBy(s => s.StartAt)
            .FirstOrDefaultAsync(ct);

        if (nextStint is null)
        {
            await CreateAutoFollowOnAsync(db, teamId, carNumber, raceId, endTime, ct);
            return;
        }

        // Auto follow-on that owns its own window — slide it (and any closed prior window
        // that opened at the old StartAt) to track the prior stint's new EndAt.
        if (nextStint.OriginType == StintOriginType.Auto && nextStint.FuelWindowId != priorWindowId
            && nextStint.StartAt != endTime)
        {
            var oldStart = nextStint.StartAt;
            nextStint.StartAt = endTime;
            // Reproject EndAt off the new StartAt while preserving the prior duration —
            // simpler than re-deriving from current fuel state and matches the spirit of
            // "the engineer hasn't said this Auto's range has changed, just when it begins".
            if (nextStint.EndAt is DateTime oldEnd)
            {
                nextStint.EndAt = endTime + (oldEnd - oldStart);
            }
            // Slide the new window's OpenedAt and the prior window's ClosedAt to match.
            var newWindow = await db.FuelWindows.FirstOrDefaultAsync(w => w.Id == nextStint.FuelWindowId, ct);
            if (newWindow is not null)
            {
                newWindow.OpenedAt = endTime;
            }
            var priorWindow = await db.FuelWindows.FirstOrDefaultAsync(w => w.Id == priorWindowId, ct);
            if (priorWindow is not null && priorWindow.ClosedAt == oldStart)
            {
                priorWindow.ClosedAt = endTime;
            }
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Creates an Auto follow-on Stint that assumes a full refuel happened between
    /// <paramref name="priorWindowId"/> and the new stint: closes the prior window at
    /// <paramref name="startAt"/>, opens a new FuelWindow + Pending RefuelEvent at that
    /// instant, and places an Auto Stint inside the new window with EndAt projected to
    /// <c>StartAt + (TankCapacityGallons / DefaultConsumptionGalPerMin)</c>. If the
    /// engineer later sets "no fuel on pit" on the Auto stint, the existing UpdateAsync
    /// merge cascade collapses this window back into the prior one — the right
    /// reversal path for "the pit was actually tires-only / no fuel added".
    /// </summary>
    private static async Task CreateAutoFollowOnAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        DateTime startAt,
        CancellationToken ct)
    {
        var fuelConfig = await CarFuelConfigLoader.LoadAsync(db, teamId, carNumber, ct);
        var tankCap = fuelConfig?.TankCapacityGallons ?? 0;
        var rate = fuelConfig?.DefaultConsumptionGalPerMin ?? 0;
        DateTime? projectedEndAt = tankCap > 0 && rate > 0
            ? startAt.AddMinutes(tankCap / rate)
            : null;

        var newRefuel = new RefuelEvent
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            DetectedAt = startAt,
            EnteredFuelGallons = null,
            ConfidenceTier = RefuelConfidenceTier.Manual,
            Source = RefuelSource.Manual,
            EcuResetState = EcuResetState.Unknown,
            Status = "Pending",
        };
        db.RefuelEvents.Add(newRefuel);
        await db.SaveChangesAsync(ct);

        // Close whatever window is currently open for this car+race (not just the prior
        // stint's window — it may already be closed by an earlier cascade). The partial
        // unique index forbids two open FuelWindows per (team, car, race), so persist
        // the close in its own SaveChanges before inserting the new open window —
        // otherwise EF Core may emit the INSERT before the UPDATE inside one batch and
        // trip the constraint mid-transaction.
        var openWindow = await db.FuelWindows
            .FirstOrDefaultAsync(w => w.TeamId == teamId && w.CarNumber == carNumber
                                    && w.RaceId == raceId && w.ClosedAt == null, ct);
        if (openWindow is not null)
        {
            openWindow.ClosedAt = startAt;
            openWindow.EndRefuelEventId = newRefuel.Id;
            await db.SaveChangesAsync(ct);
        }

        var newWindow = new FuelWindow
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            StartRefuelEventId = newRefuel.Id,
            OpenedAt = startAt,
        };
        db.FuelWindows.Add(newWindow);
        await db.SaveChangesAsync(ct);

        var followOn = new Stint
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            FuelWindowId = newWindow.Id,
            StartAt = startAt,
            EndAt = projectedEndAt,
            OriginType = StintOriginType.Auto,
        };
        db.Stints.Add(followOn);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Updates a Stint's <see cref="Stint.StartAt"/> / <see cref="Stint.EndAt"/> /
    /// <see cref="Stint.OriginType"/> and applies the <paramref name="noFuelOnPit"/> cascade:
    /// <list type="bullet">
    ///   <item><c>noFuelOnPit = true</c> on a Stint currently first-in-window → drops the
    ///   stint's start-RefuelEvent and its FuelWindow; merges this Stint and any later
    ///   Stints in the same window into the immediately-prior FuelWindow.</item>
    ///   <item><c>noFuelOnPit = false</c> on a Stint currently not first-in-window → creates
    ///   a new pending RefuelEvent + FuelWindow at the Stint's StartAt; reassigns this and
    ///   any later Stints in the original FuelWindow to the new FuelWindow.</item>
    ///   <item>No-op when the desired state matches the current state.</item>
    /// </list>
    /// </summary>
    public static async Task<UpdateOutcome> UpdateAsync(
        RaceManagementContext db,
        int teamId,
        int stintId,
        DateTime startAt,
        DateTime? endAt,
        StintOriginType originType,
        bool noFuelOnPit,
        CancellationToken ct)
    {
        if (endAt is DateTime e && e <= startAt)
        {
            return new UpdateOutcome(null, UpdateError.EndBeforeStart);
        }

        var stint = await db.Stints.FirstOrDefaultAsync(s => s.Id == stintId && s.TeamId == teamId, ct);
        if (stint is null) return new UpdateOutcome(null, UpdateError.StintNotFound);

        stint.StartAt = startAt;
        stint.EndAt = endAt;
        stint.OriginType = originType;

        var windowStints = await db.Stints
            .Where(s => s.FuelWindowId == stint.FuelWindowId)
            .OrderBy(s => s.StartAt)
            .ToListAsync(ct);
        var isFirstInWindow = windowStints.Count > 0 && windowStints[0].Id == stint.Id;

        if (noFuelOnPit && isFirstInWindow)
        {
            // Merge this Stint's window into the prior window.
            var currentWindow = await db.FuelWindows.FirstAsync(w => w.Id == stint.FuelWindowId, ct);
            var priorWindow = await db.FuelWindows
                .Where(w => w.TeamId == teamId && w.CarNumber == stint.CarNumber && w.RaceId == stint.RaceId && w.OpenedAt < currentWindow.OpenedAt)
                .OrderByDescending(w => w.OpenedAt)
                .FirstOrDefaultAsync(ct);
            if (priorWindow is null)
            {
                return new UpdateOutcome(null, UpdateError.NoPriorWindowToMerge);
            }

            foreach (var s in windowStints)
            {
                s.FuelWindowId = priorWindow.Id;
            }

            // The prior window had ended at currentWindow.OpenedAt because of the refuel
            // we're removing; reopen it if it was closed by this refuel, or extend its
            // close to currentWindow's close.
            priorWindow.ClosedAt = currentWindow.ClosedAt;
            priorWindow.EndRefuelEventId = currentWindow.EndRefuelEventId;
            priorWindow.ObservedConsumptionGalPerMin = null;
            priorWindow.ObservedDurationSeconds = null;
            priorWindow.ClosedBySessionEnd = currentWindow.ClosedBySessionEnd;

            var refuelToDelete = await db.RefuelEvents.FirstOrDefaultAsync(r => r.Id == currentWindow.StartRefuelEventId, ct);
            db.FuelWindows.Remove(currentWindow);
            if (refuelToDelete is not null) db.RefuelEvents.Remove(refuelToDelete);
        }
        else if (!noFuelOnPit && !isFirstInWindow)
        {
            // Split: this Stint becomes the start of a new FuelWindow; later Stints in the
            // original window follow.
            var refuel = new RefuelEvent
            {
                TeamId = teamId,
                CarNumber = stint.CarNumber,
                RaceId = stint.RaceId,
                DetectedAt = startAt,
                EnteredFuelGallons = null,
                ConfidenceTier = RefuelConfidenceTier.Manual,
                Source = RefuelSource.Manual,
                EcuResetState = EcuResetState.Unknown,
                Status = "Pending",
            };
            db.RefuelEvents.Add(refuel);
            await db.SaveChangesAsync(ct);

            var oldWindow = await db.FuelWindows.FirstAsync(w => w.Id == stint.FuelWindowId, ct);
            var newWindow = new FuelWindow
            {
                TeamId = teamId,
                CarNumber = stint.CarNumber,
                RaceId = stint.RaceId,
                StartRefuelEventId = refuel.Id,
                OpenedAt = startAt,
                ClosedAt = oldWindow.ClosedAt,
                EndRefuelEventId = oldWindow.EndRefuelEventId,
                ClosedBySessionEnd = oldWindow.ClosedBySessionEnd,
            };
            db.FuelWindows.Add(newWindow);

            // Close the old window at startAt with the new refuel as its end.
            oldWindow.ClosedAt = startAt;
            oldWindow.EndRefuelEventId = refuel.Id;
            oldWindow.ObservedConsumptionGalPerMin = null;
            oldWindow.ObservedDurationSeconds = null;
            oldWindow.ClosedBySessionEnd = false;

            await db.SaveChangesAsync(ct);

            // Move this Stint and any later Stints in the original window to the new window.
            var thisAndLater = windowStints.Where(s => s.StartAt >= stint.StartAt).ToList();
            foreach (var s in thisAndLater)
            {
                s.FuelWindowId = newWindow.Id;
            }
        }

        await db.SaveChangesAsync(ct);

        // Same first-stint rule as CreateAsync. The eager follow-on only attaches to the
        // very first stint of the race; everything else relies on the lazy creator at
        // read time (see EnsureLatestStintFollowOnIfEndedAsync).
        if (endAt is DateTime endTime && await IsOnlyStintAsync(db, teamId, stint.CarNumber, stint.RaceId, stint.Id, ct))
        {
            await EnsureFollowOnAutoStintAsync(db, teamId, stint.CarNumber, stint.RaceId, stint, stint.FuelWindowId, endTime, ct);
        }

        return new UpdateOutcome(stint, null);
    }

    /// <summary>
    /// Deletes a Stint. If the Stint was the only Stint in its FuelWindow (it owned the
    /// window outright), the FuelWindow and its start-RefuelEvent are deleted too. If the
    /// Stint was the first-in-window but had followers, those followers merge into the
    /// prior FuelWindow — and the operation is rejected when no prior window exists
    /// (would orphan the followers).
    /// </summary>
    public static async Task<DeleteOutcome> DeleteAsync(
        RaceManagementContext db,
        int teamId,
        int stintId,
        CancellationToken ct)
    {
        var stint = await db.Stints.FirstOrDefaultAsync(s => s.Id == stintId && s.TeamId == teamId, ct);
        if (stint is null) return new DeleteOutcome(false, DeleteError.StintNotFound);

        var windowStints = await db.Stints
            .Where(s => s.FuelWindowId == stint.FuelWindowId)
            .OrderBy(s => s.StartAt)
            .ToListAsync(ct);
        var isFirstInWindow = windowStints.Count > 0 && windowStints[0].Id == stint.Id;
        var hasFollowers = windowStints.Count > 1;

        if (isFirstInWindow)
        {
            var currentWindow = await db.FuelWindows.FirstAsync(w => w.Id == stint.FuelWindowId, ct);
            var priorWindow = await db.FuelWindows
                .Where(w => w.TeamId == teamId && w.CarNumber == stint.CarNumber && w.RaceId == stint.RaceId && w.OpenedAt < currentWindow.OpenedAt)
                .OrderByDescending(w => w.OpenedAt)
                .FirstOrDefaultAsync(ct);

            if (hasFollowers && priorWindow is null)
            {
                return new DeleteOutcome(false, DeleteError.OwnsFirstWindowAndHasFollowers);
            }

            // Reassign followers to prior window (if any) and drop this window+refuel.
            var followers = windowStints.Where(s => s.Id != stint.Id).ToList();
            if (priorWindow is not null)
            {
                foreach (var follower in followers)
                {
                    follower.FuelWindowId = priorWindow.Id;
                }
                priorWindow.ClosedAt = currentWindow.ClosedAt;
                priorWindow.EndRefuelEventId = currentWindow.EndRefuelEventId;
                priorWindow.ObservedConsumptionGalPerMin = null;
                priorWindow.ObservedDurationSeconds = null;
                priorWindow.ClosedBySessionEnd = currentWindow.ClosedBySessionEnd;
            }

            db.Stints.Remove(stint);
            var refuelToDelete = await db.RefuelEvents.FirstOrDefaultAsync(r => r.Id == currentWindow.StartRefuelEventId, ct);
            db.FuelWindows.Remove(currentWindow);
            if (refuelToDelete is not null) db.RefuelEvents.Remove(refuelToDelete);
        }
        else
        {
            db.Stints.Remove(stint);
        }

        await db.SaveChangesAsync(ct);
        return new DeleteOutcome(true, null);
    }
}
