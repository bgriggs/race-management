import { Component, input, output } from '@angular/core';

@Component({
  selector: 'lib-communications-settings',
  standalone: true,
  imports: [],
  templateUrl: './communications-settings.html',
  styleUrl: './communications-settings.css',
})
export class CommunicationsSettings {
  readonly isCloudConnectionEnabled = input(false);
  readonly isCloudConnectionEnabledChange = output<boolean>();

  onCloudConnectionToggle(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.isCloudConnectionEnabledChange.emit(target.checked);
  }
}
