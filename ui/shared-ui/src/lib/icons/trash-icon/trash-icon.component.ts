import { Component } from '@angular/core';

@Component({
  selector: 'rm-trash-icon',
  standalone: true,
  template: `
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M9 3h6l1 2h4v2H4V5h4l1-2zm1 6h2v8h-2V9zm4 0h2v8h-2V9zM7 9h2v8H7V9z" />
    </svg>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        width: 1rem;
        height: 1rem;
      }

      svg {
        width: 100%;
        height: 100%;
        fill: currentColor;
      }
    `
  ]
})
export class TrashIconComponent {}
