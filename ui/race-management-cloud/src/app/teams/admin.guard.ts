import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TeamSelectionService } from './team-selection.service';

export const adminGuard: CanActivateFn = () => {
  const teamSelection = inject(TeamSelectionService);
  const router = inject(Router);
  return teamSelection.isAdmin() ? true : router.createUrlTree(['/race-monitor']);
};
