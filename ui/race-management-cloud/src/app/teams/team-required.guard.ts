import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { CanActivateFn } from '@angular/router';
import { filter, map, take } from 'rxjs/operators';
import { TeamSelectionService } from './team-selection.service';

export const teamRequiredGuard: CanActivateFn = () => {
  const teamSelection = inject(TeamSelectionService);
  return toObservable(teamSelection.selectedTeamId).pipe(
    filter((id): id is number => id !== null),
    take(1),
    map(() => true),
  );
};
