import { Component, input } from '@angular/core';

@Component({
  selector: 'rm-shared-banner',
  standalone: true,
  templateUrl: './shared-banner.component.html',
  styleUrl: './shared-banner.component.css'
})
export class SharedBannerComponent {
  readonly heading = input.required<string>();
  readonly subheading = input<string>('Shared UI component');
}
