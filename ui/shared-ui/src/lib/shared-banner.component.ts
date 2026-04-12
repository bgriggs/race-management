import { Component, input } from '@angular/core';

@Component({
  selector: 'rm-shared-banner',
  standalone: true,
  template: `
    <section class="banner">
      <h1>{{ heading() }}</h1>
      <p>{{ subheading() }}</p>
    </section>
  `,
  styles: [
    `
      .banner {
        border: 1px solid #d7dee6;
        border-radius: 10px;
        padding: 1rem 1.25rem;
        background: #f8fbff;
      }

      h1 {
        margin: 0;
        font-size: 1.4rem;
      }

      p {
        margin: 0.4rem 0 0;
        color: #3e4c59;
      }
    `
  ]
})
export class SharedBannerComponent {
  readonly heading = input.required<string>();
  readonly subheading = input<string>('Shared UI component');
}