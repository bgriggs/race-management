import { Component, computed, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RaceSelectionService } from '../race-selection.service';
import { RaceStateStore } from '../race-state-store';

@Component({
  selector: 'app-race-position',
  imports: [],
  templateUrl: './race-position.html',
  styleUrl: './race-position.css',
})
export class RacePosition {
  private readonly raceState = inject(RaceStateStore);
  private readonly raceSelection = inject(RaceSelectionService);
  private readonly sanitizer = inject(DomSanitizer);

  // Iframe src tracks RedMist's live event id (the one ChannelProcessor is actually
  // subscribed to, not Race.RedMistEventId — which is null for org-only pairings) and
  // the user-configured access code on the selected Race (required for private events).
  //
  // Split into two primitive-projecting computeds so the iframe only reloads on actual
  // value changes: race-state pushes arrive ~1Hz, and `selectedRace` returns a new Race
  // object reference whenever the races list reloads. If `iframeUrl` read those signals
  // directly, every push/reload would produce a new SafeResourceUrl reference and Angular
  // would rebind [src] — reloading the iframe. Reducing each upstream to a primitive
  // (number / string) lets Angular's default Object.is equality block propagation when
  // unchanged.
  private readonly eventId = computed(() => this.raceState.state()?.eventId ?? null);
  private readonly accessCode = computed(() => this.raceSelection.selectedRace()?.redMistAccessCode ?? null);

  protected readonly iframeUrl = computed<SafeResourceUrl | null>(() => {
    const id = this.eventId();
    if (id == null) return null;
    const code = this.accessCode();
    let url = `https://redmist.racing/timing/${id}?embed=1&hideHeader=1&groupClass=1&fontSize=1`;
    if (code) url += `&accessCode=${encodeURIComponent(code)}`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  });
}
